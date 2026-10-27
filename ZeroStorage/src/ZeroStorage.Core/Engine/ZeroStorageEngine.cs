using System;
using System.Collections.Generic;
using System.IO;
using ZeroStorage.Core.Analytics;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.Indexing;
using ZeroStorage.Core.Persistence;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Core.Engine
{
    /// <summary>
    /// Configuration options for the unified ZeroStorageEngine.
    /// </summary>
    public sealed class ZeroStorageEngineOptions
    {
        public string DataDirectory { get; set; }
        public int MemTableThresholdPoints { get; set; } = 10000;
        public long MaxSegmentSizeBytes { get; set; } = 32 * 1024 * 1024; // 32MB
        public WalSyncMode WalSyncMode { get; set; } = WalSyncMode.Deferred;
        public WalChecksumMode WalChecksumMode { get; set; } = WalChecksumMode.Crc32C_Hardware;
        public bool EnableSparseIndex { get; set; } = true;

        /// <summary>
        /// Gets or sets whether out-of-order timestamp arrivals are allowed and automatically sorted.
        /// </summary>
        public bool AllowOutOfOrder { get; set; } = true;

        /// <summary>
        /// Maximum allowed time delay in milliseconds for out-of-order packets (default: 24 hours).
        /// </summary>
        public long OutOfOrderGracePeriodMs { get; set; } = 24 * 3600 * 1000L;

        /// <summary>
        /// Gets or sets whether duplicate timestamps for the same metric are deduplicated (keeping latest value).
        /// </summary>
        public bool DeduplicateTimestamps { get; set; } = true;

        public ZeroStorageEngineOptions(string dataDirectory)
        {
            DataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }
    }

    /// <summary>
    /// Unified LSM-Tree hybrid storage engine for ZeroStorage.
    /// Orchestrates high-speed in-memory MemTable, durable Write-Ahead Logging (WAL),
    /// immutable memory-mapped segment files (.zts), sparse time indexing (.tidx),
    /// and multi-dimensional tag indexing (TSI).
    /// </summary>
    public sealed class ZeroStorageEngine : IDisposable
    {
        private const byte RecordTypePoint = 1;

        private readonly ZeroStorageEngineOptions _options;
        private readonly object _syncRoot = new object();
        private readonly TagInvertedIndex _tagIndex = new TagInvertedIndex();
        private readonly Dictionary<int, List<TimeSeriesPoint>> _memTable = new Dictionary<int, List<TimeSeriesPoint>>();
        private readonly List<MemoryMappedTimeSeriesLog> _segments = new List<MemoryMappedTimeSeriesLog>();

        private WriteAheadLog _wal;
        private int _totalMemTablePoints;
        private int _segmentSequence;
        private bool _disposed;

        public TagInvertedIndex TagIndex => _tagIndex;
        public int TotalMemTablePoints => _totalMemTablePoints;
        public int ActiveSegmentCount => _segments.Count;

        public ZeroStorageEngine(ZeroStorageEngineOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            string baseDir = _options.DataDirectory;
            string walDir = Path.Combine(baseDir, "wal");
            string segDir = Path.Combine(baseDir, "segments");

            Directory.CreateDirectory(walDir);
            Directory.CreateDirectory(segDir);

            // Open existing segments
            var segFiles = Directory.GetFiles(segDir, "*.zts");
            Array.Sort(segFiles);

            foreach (var segFile in segFiles)
            {
                var log = new MemoryMappedTimeSeriesLog(segFile, _options.MaxSegmentSizeBytes, _options.EnableSparseIndex);
                _segments.Add(log);
            }

            _segmentSequence = _segments.Count;

            // Initialize WAL and recover uncommitted MemTable records if any
            string walPath = Path.Combine(walDir, "active.wal");
            _wal = new WriteAheadLog(walPath, autoFlush: false, checksumMode: _options.WalChecksumMode);

            RecoverMemTableFromWal();
        }

        #region Ingestion

        public void WritePoint(int metricId, long timestampMs, double value)
        {
            lock (_syncRoot)
            {
                // 1. Append to durable WAL
                Span<byte> payload = stackalloc byte[4 + 8 + 8];
                BitConverter.GetBytes(metricId).CopyTo(payload.Slice(0, 4));
                BitConverter.GetBytes(timestampMs).CopyTo(payload.Slice(4, 8));
                BitConverter.GetBytes(value).CopyTo(payload.Slice(12, 8));

                _wal.Append(RecordTypePoint, payload, _options.WalSyncMode);

                // 2. Add to active MemTable
                if (!_memTable.TryGetValue(metricId, out var list))
                {
                    list = new List<TimeSeriesPoint>();
                    _memTable[metricId] = list;
                }

                list.Add(new TimeSeriesPoint(timestampMs, value));
                _totalMemTablePoints++;

                // 3. Flush to immutable segment if threshold reached
                if (_totalMemTablePoints >= _options.MemTableThresholdPoints)
                {
                    FlushMemTableInternal();
                }
            }
        }

        public int WritePoint(string measurement, IReadOnlyDictionary<string, string> tags, long timestampMs, double value)
        {
            int seriesId = _tagIndex.GetOrRegisterSeries(measurement, tags);
            WritePoint(seriesId, timestampMs, value);
            return seriesId;
        }

        public void Flush()
        {
            lock (_syncRoot)
            {
                FlushMemTableInternal();
            }
        }

        private void FlushMemTableInternal()
        {
            if (_totalMemTablePoints == 0) return;

            string segDir = Path.Combine(_options.DataDirectory, "segments");
            string segPath = Path.Combine(segDir, $"segment_{++_segmentSequence:D6}.zts");

            var log = new MemoryMappedTimeSeriesLog(segPath, _options.MaxSegmentSizeBytes, _options.EnableSparseIndex);

            foreach (var kvp in _memTable)
            {
                int metricId = kvp.Key;
                var points = kvp.Value;
                if (points.Count == 0) continue;

                // Sort chronologically before compression
                points.Sort((a, b) => a.TimestampMs.CompareTo(b.TimestampMs));

                var cleanPoints = points;
                if (_options.DeduplicateTimestamps && points.Count > 1)
                {
                    cleanPoints = Deduplicate(points);
                }

                var block = TimeSeriesBlock.FromPoints(metricId, cleanPoints);
                log.AppendBlock(block);
            }

            // Save companion index if enabled
            if (_options.EnableSparseIndex && log.SparseIndex != null)
            {
                string idxPath = segPath + ".tidx";
                log.SparseIndex.Save(idxPath);
            }

            _segments.Add(log);

            // Reset MemTable
            _memTable.Clear();
            _totalMemTablePoints = 0;

            // Truncate active WAL
            _wal.Truncate();
        }

        private void RecoverMemTableFromWal()
        {
            var records = _wal.ReadAllRecords();
            if (records.Count == 0) return;

            for (int i = 0; i < records.Count; i++)
            {
                var r = records[i];
                if (r.RecordType == RecordTypePoint && r.Payload.Length >= 20)
                {
                    int metricId = BitConverter.ToInt32(r.Payload, 0);
                    long ts = BitConverter.ToInt64(r.Payload, 4);
                    double val = BitConverter.ToDouble(r.Payload, 12);

                    if (!_memTable.TryGetValue(metricId, out var list))
                    {
                        list = new List<TimeSeriesPoint>();
                        _memTable[metricId] = list;
                    }

                    list.Add(new TimeSeriesPoint(ts, val));
                    _totalMemTablePoints++;
                }
            }
        }

        #endregion

        #region Querying

        /// <summary>
        /// Queries time-series points across both the active MemTable and on-disk indexed segments.
        /// Returns all matching points chronologically sorted.
        /// </summary>
        public List<TimeSeriesPoint> Query(int metricId, long fromTimeMs, long toTimeMs)
        {
            lock (_syncRoot)
            {
                var allPoints = new List<TimeSeriesPoint>();

                // 1. Query disk segments via SparseTimeIndex
                for (int s = 0; s < _segments.Count; s++)
                {
                    var blocks = _segments[s].Query(metricId, fromTimeMs, toTimeMs);
                    for (int b = 0; b < blocks.Count; b++)
                    {
                        var pts = blocks[b].Decompress();
                        for (int p = 0; p < pts.Count; p++)
                        {
                            if (pts[p].TimestampMs >= fromTimeMs && pts[p].TimestampMs <= toTimeMs)
                            {
                                allPoints.Add(pts[p]);
                            }
                        }
                    }
                }

                // 2. Query in-memory MemTable
                if (_memTable.TryGetValue(metricId, out var memList))
                {
                    for (int i = 0; i < memList.Count; i++)
                    {
                        var pt = memList[i];
                        if (pt.TimestampMs >= fromTimeMs && pt.TimestampMs <= toTimeMs)
                        {
                            allPoints.Add(pt);
                        }
                    }
                }

                // 3. Sort chronologically
                allPoints.Sort((a, b) => a.TimestampMs.CompareTo(b.TimestampMs));

                // 4. Deduplicate if enabled
                if (_options.DeduplicateTimestamps && allPoints.Count > 1)
                {
                    return Deduplicate(allPoints);
                }

                return allPoints;
            }
        }

        /// <summary>
        /// Queries aggregated time-series buckets (Min, Max, Avg, Count) across a specified time range.
        /// </summary>
        public List<RollupBucket> QueryRollup(int metricId, long fromTimeMs, long toTimeMs, RollupInterval interval)
        {
            var rawPoints = Query(metricId, fromTimeMs, toTimeMs);
            return TimeSeriesRollup.ComputeRollup(rawPoints, interval);
        }

        /// <summary>
        /// Queries aggregated time-series buckets by measurement and tag filters.
        /// </summary>
        public Dictionary<int, List<RollupBucket>> QuerySeriesRollup(
            string? measurement,
            long fromTimeMs,
            long toTimeMs,
            RollupInterval interval,
            params KeyValuePair<string, string>[] tagFilters)
        {
            var seriesDict = QuerySeries(measurement, fromTimeMs, toTimeMs, tagFilters);
            var result = new Dictionary<int, List<RollupBucket>>();

            foreach (var kvp in seriesDict)
            {
                result[kvp.Key] = TimeSeriesRollup.ComputeRollup(kvp.Value, interval);
            }

            return result;
        }

        /// <summary>
        /// Queries time-series points by measurement name and tag filters.
        /// </summary>
        public Dictionary<int, List<TimeSeriesPoint>> QuerySeries(
            string? measurement,
            long fromTimeMs,
            long toTimeMs,
            params KeyValuePair<string, string>[] tagFilters)
        {
            lock (_syncRoot)
            {
                int[] seriesIds = _tagIndex.FindSeries(measurement, tagFilters);
                var result = new Dictionary<int, List<TimeSeriesPoint>>(seriesIds.Length);

                for (int i = 0; i < seriesIds.Length; i++)
                {
                    int sId = seriesIds[i];
                    result[sId] = Query(sId, fromTimeMs, toTimeMs);
                }

                return result;
            }
        }

        /// <summary>
        /// Computes aggregate statistics (Min, Max, Sum, Count, Mean) over a time window across both disk segments
        /// and active MemTable points using header-only aggregate push-down.
        /// </summary>
        public BlockAggregateSummary Aggregate(int metricId, long fromTimeMs, long toTimeMs)
        {
            lock (_syncRoot)
            {
                int totalCount = 0;
                double min = double.MaxValue;
                double max = double.MinValue;
                double sum = 0.0;

                // 1. Push-down aggregates to immutable disk segments
                for (int s = 0; s < _segments.Count; s++)
                {
                    var segAgg = _segments[s].Aggregate(metricId, fromTimeMs, toTimeMs);
                    if (segAgg.Count > 0)
                    {
                        totalCount += segAgg.Count;
                        if (segAgg.Min < min) min = segAgg.Min;
                        if (segAgg.Max > max) max = segAgg.Max;
                        sum += segAgg.Sum;
                    }
                }

                // 2. Aggregate active MemTable points
                if (_memTable.TryGetValue(metricId, out var memList))
                {
                    for (int i = 0; i < memList.Count; i++)
                    {
                        var pt = memList[i];
                        if (pt.TimestampMs >= fromTimeMs && pt.TimestampMs <= toTimeMs)
                        {
                            totalCount++;
                            if (pt.Value < min) min = pt.Value;
                            if (pt.Value > max) max = pt.Value;
                            sum += pt.Value;
                        }
                    }
                }

                if (totalCount == 0)
                {
                    return BlockAggregateSummary.Empty(metricId, fromTimeMs, toTimeMs);
                }

                return new BlockAggregateSummary(metricId, totalCount, min, max, sum, fromTimeMs, toTimeMs);
            }
        }

        /// <summary>
        /// Computes aggregate statistics for all series matching measurement name and tag filters.
        /// </summary>
        public Dictionary<int, BlockAggregateSummary> QuerySeriesAggregate(
            string? measurement,
            long fromTimeMs,
            long toTimeMs,
            params KeyValuePair<string, string>[] tagFilters)
        {
            lock (_syncRoot)
            {
                int[] seriesIds = _tagIndex.FindSeries(measurement, tagFilters);
                var result = new Dictionary<int, BlockAggregateSummary>(seriesIds.Length);

                for (int i = 0; i < seriesIds.Length; i++)
                {
                    int sId = seriesIds[i];
                    result[sId] = Aggregate(sId, fromTimeMs, toTimeMs);
                }

                return result;
            }
        }

        /// <summary>
        /// Queries time-series points and applies storage-level LTTB (Largest-Triangle-Three-Buckets) decimation
        /// to return at most targetThreshold representative points for high-frequency chart rendering.
        /// </summary>
        public List<TimeSeriesPoint> QueryLttb(int metricId, long fromTimeMs, long toTimeMs, int targetThreshold)
        {
            var rawPoints = Query(metricId, fromTimeMs, toTimeMs);
            return LttbDecimator.Decimate(rawPoints, targetThreshold);
        }

        /// <summary>
        /// Queries multiple time-series by measurement and tag filters, applying storage-level LTTB downsampling per series.
        /// </summary>
        public Dictionary<int, List<TimeSeriesPoint>> QuerySeriesLttb(
            string? measurement,
            long fromTimeMs,
            long toTimeMs,
            int targetThreshold,
            params KeyValuePair<string, string>[] tagFilters)
        {
            var rawSeries = QuerySeries(measurement, fromTimeMs, toTimeMs, tagFilters);
            var result = new Dictionary<int, List<TimeSeriesPoint>>(rawSeries.Count);

            foreach (var kvp in rawSeries)
            {
                result[kvp.Key] = LttbDecimator.Decimate(kvp.Value, targetThreshold);
            }

            return result;
        }

        #endregion

        #region Compaction and Lifecycle

        /// <summary>
        /// Compacts all existing segments into a single consolidated, dense segment file.
        /// </summary>
        public int Compact()
        {
            lock (_syncRoot)
            {
                if (_segments.Count <= 1) return 0;

                // Ensure active memory is flushed first
                FlushMemTableInternal();

                string segDir = Path.Combine(_options.DataDirectory, "segments");
                string compactedPath = Path.Combine(segDir, $"segment_compacted_{DateTime.UtcNow.Ticks}.zts");

                // Collect all blocks from all segments
                var allBlocks = new List<TimeSeriesBlock>();
                for (int s = 0; s < _segments.Count; s++)
                {
                    allBlocks.AddRange(_segments[s].ReadAllBlocks());
                }

                // Group by metric
                var groups = new Dictionary<int, List<TimeSeriesBlock>>();
                for (int i = 0; i < allBlocks.Count; i++)
                {
                    var b = allBlocks[i];
                    if (!groups.TryGetValue(b.MetricId, out var list))
                    {
                        list = new List<TimeSeriesBlock>();
                        groups[b.MetricId] = list;
                    }
                    list.Add(b);
                }

                // Write compacted segment
                var compactedLog = new MemoryMappedTimeSeriesLog(compactedPath, _options.MaxSegmentSizeBytes, _options.EnableSparseIndex);
                foreach (var kvp in groups)
                {
                    var compactedBlock = TimeSeriesCompactor.Compact(kvp.Key, kvp.Value);
                    compactedLog.AppendBlock(compactedBlock);
                }

                if (_options.EnableSparseIndex && compactedLog.SparseIndex != null)
                {
                    compactedLog.SparseIndex.Save(compactedPath + ".tidx");
                }

                // Dispose and delete old segment files
                for (int s = 0; s < _segments.Count; s++)
                {
                    string oldPath = _segments[s].FilePath;
                    _segments[s].Dispose();

                    try { File.Delete(oldPath); } catch { }
                    try { File.Delete(oldPath + ".tidx"); } catch { }
                }

                _segments.Clear();
                _segments.Add(compactedLog);

                return groups.Count;
            }
        }

        private static List<TimeSeriesPoint> Deduplicate(List<TimeSeriesPoint> points)
        {
            var result = new List<TimeSeriesPoint>(points.Count);
            result.Add(points[0]);

            for (int i = 1; i < points.Count; i++)
            {
                var prev = result[result.Count - 1];
                var cur = points[i];
                if (cur.TimestampMs == prev.TimestampMs)
                {
                    // Overwrite with latest ingested value for duplicate timestamp
                    result[result.Count - 1] = cur;
                }
                else
                {
                    result.Add(cur);
                }
            }
            return result;
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            lock (_syncRoot)
            {
                try
                {
                    FlushMemTableInternal();
                }
                catch { }

                _wal?.Dispose();

                for (int s = 0; s < _segments.Count; s++)
                {
                    _segments[s]?.Dispose();
                }
                _segments.Clear();
            }
        }
    }
}
