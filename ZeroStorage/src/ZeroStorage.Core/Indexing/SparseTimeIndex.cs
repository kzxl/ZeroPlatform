using System;
using System.Collections.Generic;
using System.IO;
using ZeroPrimitives.Cryptography;
using ZeroStorage.Core.Persistence;

namespace ZeroStorage.Core.Indexing
{
    /// <summary>
    /// Metadata record in a sparse time-series index mapping a compressed block to its byte range and statistics.
    /// </summary>
    public sealed class SparseIndexEntry
    {
        public int MetricId { get; }
        public long StartTimeMs { get; }
        public long EndTimeMs { get; }
        public long FileOffset { get; }
        public int Length { get; }
        public int Count { get; }
        public double Min { get; }
        public double Max { get; }
        public double Sum { get; }

        public SparseIndexEntry(
            int metricId,
            long startTimeMs,
            long endTimeMs,
            long fileOffset,
            int length,
            int count,
            double min,
            double max,
            double sum)
        {
            MetricId = metricId;
            StartTimeMs = startTimeMs;
            EndTimeMs = endTimeMs;
            FileOffset = fileOffset;
            Length = length;
            Count = count;
            Min = min;
            Max = max;
            Sum = sum;
        }
    }

    /// <summary>
    /// High-performance time-partitioned sparse index enabling O(log N) binary search queries over time-series blocks.
    /// Eliminates linear full-file disk scans.
    /// </summary>
    public sealed class SparseTimeIndex
    {
        private const uint IndexMagic = 0x58444954; // "TIDX"
        private const ushort IndexVersion = 1;

        private readonly object _syncRoot = new object();
        private readonly Dictionary<int, List<SparseIndexEntry>> _entriesByMetric = new Dictionary<int, List<SparseIndexEntry>>();
        private int _totalEntries;

        public int TotalEntries
        {
            get
            {
                lock (_syncRoot) return _totalEntries;
            }
        }

        public void AddEntry(SparseIndexEntry entry)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));

            lock (_syncRoot)
            {
                if (!_entriesByMetric.TryGetValue(entry.MetricId, out var list))
                {
                    list = new List<SparseIndexEntry>();
                    _entriesByMetric[entry.MetricId] = list;
                }

                // Maintain sorted order by StartTimeMs
                if (list.Count == 0 || list[list.Count - 1].StartTimeMs <= entry.StartTimeMs)
                {
                    list.Add(entry);
                }
                else
                {
                    int idx = BinarySearchInsertPosition(list, entry.StartTimeMs);
                    list.Insert(idx, entry);
                }

                _totalEntries++;
            }
        }

        /// <summary>
        /// Finds all block entries overlapping the requested [fromTimeMs, toTimeMs] window via O(log N) binary search.
        /// </summary>
        public List<SparseIndexEntry> QueryOverlappingEntries(int metricId, long fromTimeMs, long toTimeMs)
        {
            lock (_syncRoot)
            {
                var results = new List<SparseIndexEntry>();
                if (!_entriesByMetric.TryGetValue(metricId, out var list) || list.Count == 0)
                {
                    return results;
                }

                // Binary search for first block where EndTimeMs >= fromTimeMs
                int low = 0;
                int high = list.Count - 1;
                int startIdx = list.Count;

                while (low <= high)
                {
                    int mid = (low + high) >> 1;
                    if (list[mid].EndTimeMs >= fromTimeMs)
                    {
                        startIdx = mid;
                        high = mid - 1; // Look for earlier candidate
                    }
                    else
                    {
                        low = mid + 1;
                    }
                }

                // Scan forward from candidate while StartTimeMs <= toTimeMs
                for (int i = startIdx; i < list.Count; i++)
                {
                    var entry = list[i];
                    if (entry.StartTimeMs > toTimeMs) break;

                    if (entry.EndTimeMs >= fromTimeMs && entry.StartTimeMs <= toTimeMs)
                    {
                        results.Add(entry);
                    }
                }

                return results;
            }
        }

        public void Save(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));

            lock (_syncRoot)
            {
                using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write(IndexMagic);
                    writer.Write(IndexVersion);
                    writer.Write(_totalEntries);

                    foreach (var kvp in _entriesByMetric)
                    {
                        var list = kvp.Value;
                        for (int i = 0; i < list.Count; i++)
                        {
                            var e = list[i];
                            writer.Write(e.MetricId);
                            writer.Write(e.StartTimeMs);
                            writer.Write(e.EndTimeMs);
                            writer.Write(e.FileOffset);
                            writer.Write(e.Length);
                            writer.Write(e.Count);
                            writer.Write(e.Min);
                            writer.Write(e.Max);
                            writer.Write(e.Sum);
                        }
                    }

                    writer.Flush();
                    byte[] data = ms.ToArray();
                    uint crc = FastCrc.Crc32C(data);

                    fs.Write(data, 0, data.Length);
                    using (var fsWriter = new BinaryWriter(fs))
                    {
                        fsWriter.Write(crc);
                        fsWriter.Flush();
                    }
                }
            }
        }

        public static SparseTimeIndex Load(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException("Index file not found.", filePath);

            byte[] allBytes = File.ReadAllBytes(filePath);
            if (allBytes.Length < 14) throw new InvalidDataException("Index file is truncated.");

            int payloadLen = allBytes.Length - 4;
            uint expectedCrc = BitConverter.ToUInt32(allBytes, payloadLen);
            uint actualCrc = FastCrc.Crc32C(allBytes.AsSpan(0, payloadLen));

            if (expectedCrc != actualCrc)
                throw new InvalidDataException("SparseTimeIndex CRC32C verification failed. File corrupted.");

            var index = new SparseTimeIndex();
            using (var ms = new MemoryStream(allBytes, 0, payloadLen))
            using (var reader = new BinaryReader(ms))
            {
                uint magic = reader.ReadUInt32();
                if (magic != IndexMagic) throw new InvalidDataException("Invalid index file magic signature.");

                ushort version = reader.ReadUInt16();
                if (version != IndexVersion) throw new InvalidDataException($"Unsupported index version: {version}.");

                int totalCount = reader.ReadInt32();
                for (int i = 0; i < totalCount; i++)
                {
                    int metricId = reader.ReadInt32();
                    long start = reader.ReadInt64();
                    long end = reader.ReadInt64();
                    long offset = reader.ReadInt64();
                    int length = reader.ReadInt32();
                    int count = reader.ReadInt32();
                    double min = reader.ReadDouble();
                    double max = reader.ReadDouble();
                    double sum = reader.ReadDouble();

                    index.AddEntry(new SparseIndexEntry(metricId, start, end, offset, length, count, min, max, sum));
                }
            }

            return index;
        }

        public static SparseTimeIndex BuildFromLog(MemoryMappedTimeSeriesLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));

            var index = new SparseTimeIndex();
            var allBlocks = log.ReadAllBlocksWithOffsets();
            for (int i = 0; i < allBlocks.Count; i++)
            {
                var item = allBlocks[i];
                index.AddEntry(new SparseIndexEntry(
                    item.Block.MetricId,
                    item.Block.StartTimeMs,
                    item.Block.EndTimeMs,
                    item.Offset,
                    item.Length,
                    item.Block.Count,
                    item.Block.Min,
                    item.Block.Max,
                    item.Block.Sum));
            }
            return index;
        }

        private static int BinarySearchInsertPosition(List<SparseIndexEntry> list, long startTimeMs)
        {
            int low = 0;
            int high = list.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) >> 1;
                if (list[mid].StartTimeMs < startTimeMs) low = mid + 1;
                else if (list[mid].StartTimeMs > startTimeMs) high = mid - 1;
                else return mid;
            }
            return low;
        }
    }
}
