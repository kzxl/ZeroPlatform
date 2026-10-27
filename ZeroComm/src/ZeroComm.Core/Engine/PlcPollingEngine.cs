using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Abstractions;

namespace ZeroComm.Core.Engine
{
    /// <summary>
    /// High-performance industrial SCADA polling engine for heterogeneous PLCs.
    /// Orchestrates contiguous block read requests, zero-allocation deadband filtering,
    /// and ultra-low latency telemetry dispatch compatible with ZeroUI off-heap pipelines.
    /// </summary>
    public class PlcPollingEngine : IDisposable
    {
        private readonly IIndustrialPlcClient _client;
        private readonly int _maxContiguousBytes;
        private readonly int _maxAllowedGapBytes;

        private readonly ConcurrentDictionary<int, PlcBatchReadRequest> _registeredTags = new ConcurrentDictionary<int, PlcBatchReadRequest>();
        private readonly ConcurrentDictionary<int, DeadbandFilterSlot> _deadbandSlots = new ConcurrentDictionary<int, DeadbandFilterSlot>();

        private IReadOnlyList<CoalescedMemoryBlock> _coalescedBlocks = Array.Empty<CoalescedMemoryBlock>();
        private readonly object _blockLock = new object();

        private CancellationTokenSource? _cts;
        private Task? _pollTask;
        private bool _isDisposed;

        /// <summary>
        /// Gets the underlying PLC client instance.
        /// </summary>
        public IIndustrialPlcClient Client => _client;

        /// <summary>
        /// Gets whether the polling engine is actively running.
        /// </summary>
        public bool IsRunning => _pollTask != null && !_pollTask.IsCompleted;

        /// <summary>
        /// Gets or sets the target polling cycle interval in milliseconds (default: 100ms).
        /// </summary>
        public int PollingIntervalMs { get; set; } = 100;

        /// <summary>
        /// Gets the current coalesced memory blocks formed from registered tags.
        /// </summary>
        public IReadOnlyList<CoalescedMemoryBlock> CoalescedBlocks
        {
            get
            {
                lock (_blockLock)
                {
                    return _coalescedBlocks;
                }
            }
        }

        /// <summary>
        /// Event raised when one or more tag values change beyond deadband or trigger heartbeat.
        /// </summary>
        public event Action<IReadOnlyList<PlcTagValue>>? OnTagValuesChanged;

        /// <summary>
        /// High-throughput zero-allocation callback invoked directly for each sampled telemetry point.
        /// Ideal for high-speed streaming into ZeroOffHeapTelemetrySeries or charting buffers without boxing.
        /// </summary>
        public Action<int, double, long>? OnTelemetrySampled { get; set; }

        /// <summary>
        /// Event raised whenever an unexpected error occurs during a polling cycle.
        /// </summary>
        public event Action<Exception>? OnPollingError;

        /// <summary>
        /// Initializes a new instance of the <see cref="PlcPollingEngine"/> class.
        /// </summary>
        public PlcPollingEngine(
            IIndustrialPlcClient client,
            int maxContiguousBytes = 500,
            int maxAllowedGapBytes = 10)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _maxContiguousBytes = Math.Min(maxContiguousBytes, client.Capabilities?.MaxContiguousReadBytes ?? 500);
            _maxAllowedGapBytes = maxAllowedGapBytes;
        }

        #region Tag Registration

        /// <summary>
        /// Registers a tag for periodic batch polling with optional deadband and heartbeat filters.
        /// </summary>
        public void RegisterTag(
            int tagId,
            string tagAddress,
            TagDataType dataType = TagDataType.UInt16,
            int byteLength = 0,
            double deadbandAbsolute = 0.0,
            int heartbeatIntervalMs = 0)
        {
            var res = PlcAddressParser.Parse(tagAddress, _client.Vendor);
            if (!res.IsValid)
            {
                throw new ArgumentException($"Invalid tag address '{tagAddress}' for {_client.Vendor}: {res.ErrorMessage}", nameof(tagAddress));
            }

            int len = byteLength > 0 ? byteLength : GetDefaultByteLength(res.DataType);
            var req = new PlcBatchReadRequest(tagId, in res, len);

            _registeredTags[tagId] = req;
            _deadbandSlots[tagId] = new DeadbandFilterSlot(tagId, deadbandAbsolute, heartbeatIntervalMs);

            RebuildCoalescedBlocks();
        }

        /// <summary>
        /// Unregisters a tag from batch polling.
        /// </summary>
        public bool UnregisterTag(int tagId)
        {
            bool removed = _registeredTags.TryRemove(tagId, out _);
            _deadbandSlots.TryRemove(tagId, out _);

            if (removed)
            {
                RebuildCoalescedBlocks();
            }

            return removed;
        }

        /// <summary>
        /// Clears all registered tags.
        /// </summary>
        public void ClearTags()
        {
            _registeredTags.Clear();
            _deadbandSlots.Clear();
            RebuildCoalescedBlocks();
        }

        private void RebuildCoalescedBlocks()
        {
            lock (_blockLock)
            {
                var reqList = new List<PlcBatchReadRequest>(_registeredTags.Values);
                _coalescedBlocks = BatchCoalescingEngine.CoalesceRequests(reqList, _maxContiguousBytes, _maxAllowedGapBytes);
            }
        }

        private static int GetDefaultByteLength(TagDataType dataType)
        {
            return dataType switch
            {
                TagDataType.Bool or TagDataType.Byte => 1,
                TagDataType.Int16 or TagDataType.UInt16 => 2,
                TagDataType.Int32 or TagDataType.UInt32 or TagDataType.Float => 4,
                TagDataType.Int64 or TagDataType.UInt64 or TagDataType.Double => 8,
                _ => 2
            };
        }

        #endregion

        #region Polling Execution

        /// <summary>
        /// Starts background polling at the specified interval.
        /// </summary>
        public void Start(int? intervalMs = null)
        {
            if (IsRunning) return;

            if (intervalMs.HasValue && intervalMs.Value > 0)
            {
                PollingIntervalMs = intervalMs.Value;
            }

            _cts = new CancellationTokenSource();
            _pollTask = Task.Run(() => PollLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Stops background polling gracefully.
        /// </summary>
        public async Task StopAsync()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                if (_pollTask != null)
                {
                    try
                    {
                        await _pollTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Normal cancellation
                    }
                }
                _cts.Dispose();
                _cts = null;
                _pollTask = null;
            }
        }

        /// <summary>
        /// Executes a single polling cycle over all coalesced blocks.
        /// </summary>
        public async Task PollOnceAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<CoalescedMemoryBlock> blocks;
            lock (_blockLock)
            {
                blocks = _coalescedBlocks;
            }

            if (blocks.Count == 0) return;

            long timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var changedValues = new List<PlcTagValue>();

            // Preallocated buffer for unpack
            PlcTagValue[] unpackBuffer = new PlcTagValue[64];

            foreach (var block in blocks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    byte[] rawData;
                    if (!string.IsNullOrEmpty(block.SymbolicTag))
                    {
                        rawData = await _client.ReadRawBytesAsync(block.SymbolicTag!, block.ByteLength, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        string blockAddress = FormatBlockAddress(block);
                        rawData = await _client.ReadRawBytesAsync(blockAddress, block.ByteLength, cancellationToken).ConfigureAwait(false);
                    }

                    if (unpackBuffer.Length < block.Mappings.Count)
                    {
                        unpackBuffer = new PlcTagValue[block.Mappings.Count];
                    }

                    int unpackedCount = block.Unpack(rawData, unpackBuffer, timestampMs);

                    for (int i = 0; i < unpackedCount; i++)
                    {
                        var sample = unpackBuffer[i];
                        if (_deadbandSlots.TryGetValue(sample.TagId, out var slot))
                        {
                            if (slot.Evaluate(in sample, out var reportedValue))
                            {
                                _deadbandSlots[sample.TagId] = slot;
                                changedValues.Add(reportedValue);

                                // Direct invocation of high-speed delegate
                                OnTelemetrySampled?.Invoke(reportedValue.TagId, reportedValue.NumericValue, reportedValue.TimestampUtcMs);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnPollingError?.Invoke(ex);
                }
            }

            if (changedValues.Count > 0)
            {
                OnTagValuesChanged?.Invoke(changedValues);
            }
        }

        private async Task PollLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await PollOnceAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    OnPollingError?.Invoke(ex);
                }

                try
                {
                    await Task.Delay(PollingIntervalMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private static string FormatBlockAddress(CoalescedMemoryBlock block)
        {
            switch (block.Vendor)
            {
                case PlcVendor.SiemensS7:
                    if (block.DbNumber > 0 || block.AreaCode == 0x84)
                        return $"DB{block.DbNumber}.DBB{block.StartOffset}";
                    if (block.AreaCode == 0x81) return $"IB{block.StartOffset}";
                    if (block.AreaCode == 0x82) return $"QB{block.StartOffset}";
                    if (block.AreaCode == 0x83) return $"MB{block.StartOffset}";
                    return $"DB1.DBB{block.StartOffset}";

                case PlcVendor.MitsubishiMelsec:
                    return block.AreaCode switch
                    {
                        0xA8 => $"D{block.StartOffset}",
                        0xB4 => $"W{block.StartOffset:X}",
                        0x9C => $"X{block.StartOffset:X}",
                        0x9D => $"Y{block.StartOffset:X}",
                        0x90 => $"M{block.StartOffset}",
                        0xA0 => $"B{block.StartOffset:X}",
                        0xD2 => $"ZR{block.StartOffset}",
                        _ => $"D{block.StartOffset}"
                    };

                case PlcVendor.OmronFins:
                    return block.AreaCode switch
                    {
                        0x82 or 0x02 => $"D{block.StartOffset}",
                        0xB0 or 0x30 => $"CIO{block.StartOffset}",
                        0xB1 or 0x31 => $"W{block.StartOffset}",
                        0xB2 or 0x32 => $"H{block.StartOffset}",
                        0xB3 or 0x33 => $"A{block.StartOffset}",
                        _ => $"D{block.StartOffset}"
                    };

                case PlcVendor.ModbusTcp or PlcVendor.ModbusRtu:
                    return block.AreaCode switch
                    {
                        1 => $"COIL{block.StartOffset}",
                        2 => $"DI{block.StartOffset}",
                        4 => $"IR{block.StartOffset}",
                        _ => $"HR{block.StartOffset}"
                    };

                default:
                    return block.SymbolicTag ?? $"D{block.StartOffset}";
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _cts?.Cancel();
            _cts?.Dispose();
            _registeredTags.Clear();
            _deadbandSlots.Clear();
        }

        #endregion
    }
}
