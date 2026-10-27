using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using ZeroStorage.Core.Indexing;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Core.Persistence
{
    /// <summary>
    /// High-throughput append-only disk storage using MemoryMappedFiles for zero-copy streaming persistence.
    /// Provides random-access block queries by metric and time range.
    /// </summary>
    public sealed class MemoryMappedTimeSeriesLog : IDisposable
    {
        private const int Magic = 0x5A545331; // "ZTS1"
        private const int Version = 1;
        private const int HeaderSize = 32;

        private readonly string _filePath;
        private readonly FileStream _fileStream;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _accessor;
        private readonly object _syncRoot = new object();
        private bool _disposed;

        private int _blockCount;
        private long _writeOffset;
        private SparseTimeIndex? _sparseIndex;

        public int BlockCount => _blockCount;
        public long CurrentSize => _writeOffset;
        public string FilePath => _filePath;
        public SparseTimeIndex? SparseIndex
        {
            get => _sparseIndex;
            set => _sparseIndex = value;
        }

        public MemoryMappedTimeSeriesLog(string filePath, long initialCapacity = 16 * 1024 * 1024, bool enableSparseIndex = false)
        {
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

            bool fileExists = File.Exists(filePath);
            _fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

            if (!fileExists || _fileStream.Length < HeaderSize)
            {
                if (_fileStream.Length < initialCapacity)
                {
                    _fileStream.SetLength(initialCapacity);
                }

                _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true);
                _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                // Write Header
                _accessor.Write(0, Magic);
                _accessor.Write(4, Version);
                _accessor.Write(8, 0); // BlockCount = 0
                _accessor.Write(12, (long)HeaderSize); // WriteOffset = HeaderSize
                _accessor.Flush();

                _blockCount = 0;
                _writeOffset = HeaderSize;

                if (enableSparseIndex)
                {
                    _sparseIndex = new SparseTimeIndex();
                }
            }
            else
            {
                _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true);
                _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                int magic = _accessor.ReadInt32(0);
                if (magic != Magic)
                    throw new InvalidDataException("Invalid time-series log file magic signature.");

                _blockCount = _accessor.ReadInt32(8);
                _writeOffset = _accessor.ReadInt64(12);

                string companionIdx = filePath + ".tidx";
                if (File.Exists(companionIdx))
                {
                    try
                    {
                        _sparseIndex = SparseTimeIndex.Load(companionIdx);
                    }
                    catch
                    {
                        if (enableSparseIndex) _sparseIndex = SparseTimeIndex.BuildFromLog(this);
                    }
                }
                else if (enableSparseIndex)
                {
                    _sparseIndex = SparseTimeIndex.BuildFromLog(this);
                }
            }
        }

        public void AppendBlock(TimeSeriesBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));

            lock (_syncRoot)
            {
                int recordHeaderSize = 4 + 8 + 8 + 4 + 8 + 8 + 8 + 4; // 48 bytes
                int totalRecordSize = recordHeaderSize + block.CompressedData.Length;

                // Ensure capacity
                if (_writeOffset + totalRecordSize > _fileStream.Length)
                {
                    long newCap = Math.Max(_fileStream.Length * 2, _writeOffset + totalRecordSize + 1024 * 1024);
                    _accessor.Dispose();
                    _mmf.Dispose();

                    _fileStream.SetLength(newCap);
                    _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true);
                    _accessor = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
                }

                long offset = _writeOffset;
                _accessor.Write(offset + 0, block.MetricId);
                _accessor.Write(offset + 4, block.StartTimeMs);
                _accessor.Write(offset + 12, block.EndTimeMs);
                _accessor.Write(offset + 20, block.Count);
                _accessor.Write(offset + 24, block.Min);
                _accessor.Write(offset + 32, block.Max);
                _accessor.Write(offset + 40, block.Sum);
                _accessor.Write(offset + 48, block.CompressedData.Length);
                _accessor.WriteArray(offset + 52, block.CompressedData, 0, block.CompressedData.Length);

                _writeOffset += totalRecordSize;
                _blockCount++;

                if (_sparseIndex != null)
                {
                    _sparseIndex.AddEntry(new SparseIndexEntry(
                        block.MetricId,
                        block.StartTimeMs,
                        block.EndTimeMs,
                        offset,
                        totalRecordSize,
                        block.Count,
                        block.Min,
                        block.Max,
                        block.Sum));
                }

                // Update Header
                _accessor.Write(8, _blockCount);
                _accessor.Write(12, _writeOffset);
                _accessor.Flush();
            }
        }

        public List<TimeSeriesBlock> ReadAllBlocks()
        {
            lock (_syncRoot)
            {
                var list = new List<TimeSeriesBlock>(_blockCount);
                long offset = HeaderSize;

                for (int i = 0; i < _blockCount; i++)
                {
                    int metricId = _accessor.ReadInt32(offset + 0);
                    long start = _accessor.ReadInt64(offset + 4);
                    long end = _accessor.ReadInt64(offset + 12);
                    int count = _accessor.ReadInt32(offset + 20);
                    double min = _accessor.ReadDouble(offset + 24);
                    double max = _accessor.ReadDouble(offset + 32);
                    double sum = _accessor.ReadDouble(offset + 40);
                    int dataLen = _accessor.ReadInt32(offset + 48);

                    byte[] data = new byte[dataLen];
                    _accessor.ReadArray(offset + 52, data, 0, dataLen);

                    list.Add(new TimeSeriesBlock(metricId, start, end, count, min, max, sum, data));

                    offset += 52 + dataLen;
                }

                return list;
            }
        }

        public List<BlockOffsetInfo> ReadAllBlocksWithOffsets()
        {
            lock (_syncRoot)
            {
                var list = new List<BlockOffsetInfo>(_blockCount);
                long offset = HeaderSize;

                for (int i = 0; i < _blockCount; i++)
                {
                    int metricId = _accessor.ReadInt32(offset + 0);
                    long start = _accessor.ReadInt64(offset + 4);
                    long end = _accessor.ReadInt64(offset + 12);
                    int count = _accessor.ReadInt32(offset + 20);
                    double min = _accessor.ReadDouble(offset + 24);
                    double max = _accessor.ReadDouble(offset + 32);
                    double sum = _accessor.ReadDouble(offset + 40);
                    int dataLen = _accessor.ReadInt32(offset + 48);

                    byte[] data = new byte[dataLen];
                    _accessor.ReadArray(offset + 52, data, 0, dataLen);

                    int totalLen = 52 + dataLen;
                    var block = new TimeSeriesBlock(metricId, start, end, count, min, max, sum, data);
                    list.Add(new BlockOffsetInfo(block, offset, totalLen));

                    offset += totalLen;
                }

                return list;
            }
        }

        public List<TimeSeriesBlock> Query(int metricId, long fromTimeMs, long toTimeMs)
        {
            lock (_syncRoot)
            {
                if (_sparseIndex != null)
                {
                    var entries = _sparseIndex.QueryOverlappingEntries(metricId, fromTimeMs, toTimeMs);
                    var results = new List<TimeSeriesBlock>(entries.Count);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];
                        long blockOffset = entry.FileOffset;
                        int dataLen = _accessor.ReadInt32(blockOffset + 48);
                        byte[] data = new byte[dataLen];
                        _accessor.ReadArray(blockOffset + 52, data, 0, dataLen);
                        results.Add(new TimeSeriesBlock(entry.MetricId, entry.StartTimeMs, entry.EndTimeMs, entry.Count, entry.Min, entry.Max, entry.Sum, data));
                    }
                    return results;
                }

                var matching = new List<TimeSeriesBlock>();
                long offset = HeaderSize;

                for (int i = 0; i < _blockCount; i++)
                {
                    int mId = _accessor.ReadInt32(offset + 0);
                    long start = _accessor.ReadInt64(offset + 4);
                    long end = _accessor.ReadInt64(offset + 12);
                    int count = _accessor.ReadInt32(offset + 20);
                    double min = _accessor.ReadDouble(offset + 24);
                    double max = _accessor.ReadDouble(offset + 32);
                    double sum = _accessor.ReadDouble(offset + 40);
                    int dataLen = _accessor.ReadInt32(offset + 48);

                    if (mId == metricId && end >= fromTimeMs && start <= toTimeMs)
                    {
                        byte[] data = new byte[dataLen];
                        _accessor.ReadArray(offset + 52, data, 0, dataLen);
                        matching.Add(new TimeSeriesBlock(mId, start, end, count, min, max, sum, data));
                    }

                    offset += 52 + dataLen;
                }

                return matching;
            }
        }

        /// <summary>
        /// Computes aggregate statistics (Min, Max, Sum, Count, Mean) over a time window [fromTimeMs, toTimeMs].
        /// Employs O(1) header-only aggregate push-down: fully enclosed blocks read precalculated stats
        /// directly from headers or sparse index without decompressing payload data.
        /// </summary>
        public BlockAggregateSummary Aggregate(int metricId, long fromTimeMs, long toTimeMs)
        {
            lock (_syncRoot)
            {
                int totalCount = 0;
                double min = double.MaxValue;
                double max = double.MinValue;
                double sum = 0.0;

                if (_sparseIndex != null)
                {
                    var entries = _sparseIndex.QueryOverlappingEntries(metricId, fromTimeMs, toTimeMs);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var entry = entries[i];

                        // Case 1: Fully enclosed block -> O(1) direct header stat accumulation
                        if (entry.StartTimeMs >= fromTimeMs && entry.EndTimeMs <= toTimeMs)
                        {
                            if (entry.Count > 0)
                            {
                                totalCount += entry.Count;
                                if (entry.Min < min) min = entry.Min;
                                if (entry.Max > max) max = entry.Max;
                                sum += entry.Sum;
                            }
                        }
                        else
                        {
                            // Case 2: Boundary block straddling fromTimeMs or toTimeMs -> decompress & filter
                            long blockOffset = entry.FileOffset;
                            int dataLen = _accessor.ReadInt32(blockOffset + 48);
                            byte[] data = new byte[dataLen];
                            _accessor.ReadArray(blockOffset + 52, data, 0, dataLen);

                            var block = new TimeSeriesBlock(entry.MetricId, entry.StartTimeMs, entry.EndTimeMs, entry.Count, entry.Min, entry.Max, entry.Sum, data);
                            var points = block.Decompress();

                            for (int p = 0; p < points.Count; p++)
                            {
                                var pt = points[p];
                                if (pt.TimestampMs >= fromTimeMs && pt.TimestampMs <= toTimeMs)
                                {
                                    totalCount++;
                                    if (pt.Value < min) min = pt.Value;
                                    if (pt.Value > max) max = pt.Value;
                                    sum += pt.Value;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // Linear header scan without index
                    long offset = HeaderSize;
                    for (int i = 0; i < _blockCount; i++)
                    {
                        int mId = _accessor.ReadInt32(offset + 0);
                        long start = _accessor.ReadInt64(offset + 4);
                        long end = _accessor.ReadInt64(offset + 12);
                        int count = _accessor.ReadInt32(offset + 20);
                        double bMin = _accessor.ReadDouble(offset + 24);
                        double bMax = _accessor.ReadDouble(offset + 32);
                        double bSum = _accessor.ReadDouble(offset + 40);
                        int dataLen = _accessor.ReadInt32(offset + 48);

                        if (mId == metricId && end >= fromTimeMs && start <= toTimeMs)
                        {
                            if (start >= fromTimeMs && end <= toTimeMs)
                            {
                                // Fully enclosed block -> read header only, skip payload
                                if (count > 0)
                                {
                                    totalCount += count;
                                    if (bMin < min) min = bMin;
                                    if (bMax > max) max = bMax;
                                    sum += bSum;
                                }
                            }
                            else
                            {
                                // Boundary block
                                byte[] data = new byte[dataLen];
                                _accessor.ReadArray(offset + 52, data, 0, dataLen);

                                var block = new TimeSeriesBlock(mId, start, end, count, bMin, bMax, bSum, data);
                                var points = block.Decompress();

                                for (int p = 0; p < points.Count; p++)
                                {
                                    var pt = points[p];
                                    if (pt.TimestampMs >= fromTimeMs && pt.TimestampMs <= toTimeMs)
                                    {
                                        totalCount++;
                                        if (pt.Value < min) min = pt.Value;
                                        if (pt.Value > max) max = pt.Value;
                                        sum += pt.Value;
                                    }
                                }
                            }
                        }

                        offset += 52 + dataLen;
                    }
                }

                if (totalCount == 0)
                {
                    return BlockAggregateSummary.Empty(metricId, fromTimeMs, toTimeMs);
                }

                return new BlockAggregateSummary(metricId, totalCount, min, max, sum, fromTimeMs, toTimeMs);
            }
        }


        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _accessor?.Dispose();
            _mmf?.Dispose();
            _fileStream?.Dispose();
        }
    }

    /// <summary>
    /// Encapsulates a TimeSeriesBlock along with its physical file offset and byte length in a log file.
    /// </summary>
    public sealed class BlockOffsetInfo
    {
        public TimeSeriesBlock Block { get; }
        public long Offset { get; }
        public int Length { get; }

        public BlockOffsetInfo(TimeSeriesBlock block, long offset, int length)
        {
            Block = block ?? throw new ArgumentNullException(nameof(block));
            Offset = offset;
            Length = length;
        }
    }
}
