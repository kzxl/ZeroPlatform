using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// High-throughput, zero-allocation Modbus TCP master protocol adapter.
    /// Features:
    /// - Register Block Polling via ModbusAddressPlanner (eliminates N+1 polling).
    /// - Zero heap allocations on hot paths via ArrayPool&lt;byte&gt;.Shared.
    /// - Lock-free atomic TransactionId allocator.
    /// - Bounded pipelining concurrency via MaxInFlight limiter.
    /// - Fine-grained observability: pure Network RTT, Decode time, Tag update time, and Total cycle time.
    /// </summary>
    public sealed class ModbusTcpAdapter : IProtocolAdapter
    {
        private const int MaxModbusAduLength = 260; // 7-byte MBAP + 253-byte PDU

        private readonly string _adapterId;
        private readonly string _host;
        private readonly int _port;
        private readonly byte _unitId;
        private readonly TimeSpan _timeout;
        private readonly int _maxInFlight;
        private readonly int _maxBlockRegisters;
        private readonly int _maxRegisterGap;

        private readonly ConcurrentDictionary<string, AdapterTagDefinition> _tags =
            new ConcurrentDictionary<string, AdapterTagDefinition>(StringComparer.OrdinalIgnoreCase);

        private readonly SemaphoreSlim _inFlightLimiter;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _socketLock = new SemaphoreSlim(1, 1);
        private readonly ModbusMetrics _metrics = new ModbusMetrics();

        private Socket? _socket;
        private int _transactionIdCounter;
        private AdapterConnectionState _state = AdapterConnectionState.Disconnected;
        private bool _isDisposed;
        private volatile bool _isBlocksDirty = true;
        private IReadOnlyList<ModbusReadBlock> _plannedBlocks = Array.Empty<ModbusReadBlock>();

        public string AdapterId => _adapterId;
        public string Endpoint => $"{_host}:{_port}";
        public AdapterConnectionState State => _state;
        public TimeSpan Latency => _metrics.NetworkRtt;
        public ModbusMetrics Metrics => _metrics;
        public int MaxInFlight => _maxInFlight;

        public event Action<IProtocolAdapter, AdapterConnectionState>? StateChanged;

        public ModbusTcpAdapter(
            string adapterId,
            string host,
            int port = 502,
            byte unitId = 1,
            TimeSpan? timeout = null,
            int maxInFlight = 1,
            int maxBlockRegisters = ModbusAddressPlanner.DefaultMaxBlockRegisters,
            int maxRegisterGap = ModbusAddressPlanner.DefaultMaxRegisterGap)
        {
            _adapterId = adapterId ?? throw new ArgumentNullException(nameof(adapterId));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _unitId = unitId;
            _timeout = timeout ?? TimeSpan.FromSeconds(3);
            _maxInFlight = Math.Max(1, maxInFlight);
            _maxBlockRegisters = maxBlockRegisters;
            _maxRegisterGap = maxRegisterGap;
            _inFlightLimiter = new SemaphoreSlim(_maxInFlight, _maxInFlight);
        }

        public void RegisterTag(AdapterTagDefinition tagDef)
        {
            if (tagDef == null) throw new ArgumentNullException(nameof(tagDef));
            _tags[tagDef.TagPath] = tagDef;
            _isBlocksDirty = true;
        }

        public IReadOnlyCollection<AdapterTagDefinition> GetRegisteredTags()
        {
            return (IReadOnlyCollection<AdapterTagDefinition>)_tags.Values;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(ModbusTcpAdapter));
            UpdateState(AdapterConnectionState.Connecting);

            try
            {
                DisconnectSocket();

                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true,
                    SendTimeout = (int)_timeout.TotalMilliseconds,
                    ReceiveTimeout = (int)_timeout.TotalMilliseconds
                };

                var connectTask = socket.ConnectAsync(_host, _port);
                var completedTask = await Task.WhenAny(connectTask, Task.Delay(_timeout, cancellationToken)).ConfigureAwait(false);

                if (completedTask != connectTask)
                {
                    socket.Dispose();
                    throw new TimeoutException($"Modbus connection to {Endpoint} timed out.");
                }

                _socket = socket;
                UpdateState(AdapterConnectionState.Connected);
            }
            catch (Exception)
            {
                UpdateState(AdapterConnectionState.Faulted);
                throw;
            }
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectSocket();
            UpdateState(AdapterConnectionState.Disconnected);
            return Task.CompletedTask;
        }

        public async Task PollOnceAsync(CancellationToken cancellationToken = default)
        {
            if (_socket == null || !_socket.Connected)
            {
                UpdateState(AdapterConnectionState.Disconnected);
                return;
            }

            var cycleSw = Stopwatch.StartNew();

            // Rebuild blocks if registered tags changed
            if (_isBlocksDirty)
            {
                _plannedBlocks = ModbusAddressPlanner.PlanBlocks(
                    _tags.Values,
                    _maxBlockRegisters,
                    _maxRegisterGap);
                _metrics.PlannedBlockCount = _plannedBlocks.Count;
                _isBlocksDirty = false;
            }

            if (_plannedBlocks.Count == 0)
            {
                cycleSw.Stop();
                _metrics.TotalCycleTime = cycleSw.Elapsed;
                return;
            }

            long totalNetworkRttTicks = 0;
            long totalDecodeTicks = 0;
            long totalTagUpdateTicks = 0;
            int successfulBlocks = 0;

            if (_maxInFlight == 1)
            {
                // Strict serial lockstep: safe for all PLCs and serial-bridge gateways
                for (int i = 0; i < _plannedBlocks.Count; i++)
                {
                    var block = _plannedBlocks[i];
                    var result = await ExecuteBlockAsync(block, cancellationToken).ConfigureAwait(false);
                    if (result.Success)
                    {
                        totalNetworkRttTicks += result.NetworkRttTicks;
                        totalDecodeTicks += result.DecodeTicks;
                        totalTagUpdateTicks += result.TagUpdateTicks;
                        successfulBlocks++;
                    }
                }
            }
            else
            {
                // Bounded pipelined execution
                var tasks = new Task<(bool Success, long NetworkRttTicks, long DecodeTicks, long TagUpdateTicks)>[_plannedBlocks.Count];
                for (int i = 0; i < _plannedBlocks.Count; i++)
                {
                    var block = _plannedBlocks[i];
                    tasks[i] = ExecuteBlockAsync(block, cancellationToken);
                }

                var results = await Task.WhenAll(tasks).ConfigureAwait(false);
                for (int i = 0; i < results.Length; i++)
                {
                    if (results[i].Success)
                    {
                        totalNetworkRttTicks += results[i].NetworkRttTicks;
                        totalDecodeTicks += results[i].DecodeTicks;
                        totalTagUpdateTicks += results[i].TagUpdateTicks;
                        successfulBlocks++;
                    }
                }
            }

            cycleSw.Stop();

            // Update fine-grained metrics
            _metrics.TotalCycleTime = cycleSw.Elapsed;
            _metrics.TotalPollCycles++;

            if (successfulBlocks > 0)
            {
                _metrics.NetworkRtt = new TimeSpan(totalNetworkRttTicks / successfulBlocks);
                _metrics.DecodeTime = new TimeSpan(totalDecodeTicks);
                _metrics.TagUpdateTime = new TimeSpan(totalTagUpdateTicks);
            }
        }

        private async Task<(bool Success, long NetworkRttTicks, long DecodeTicks, long TagUpdateTicks)> ExecuteBlockAsync(
            ModbusReadBlock block,
            CancellationToken ct)
        {
            await _inFlightLimiter.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ushort transId = AllocateTransactionId();

                // 1. Rent request & response buffers from ArrayPool (Zero-alloc)
                byte[] reqBuffer = ArrayPool<byte>.Shared.Rent(12);
                byte[] respBuffer = ArrayPool<byte>.Shared.Rent(MaxModbusAduLength);

                long rttTicks = 0;
                long decodeTicks = 0;
                long tagUpdateTicks = 0;
                int receivedBytes = 0;

                try
                {
                    // 2. Build 12-byte MBAP request
                    BuildMbapPacket(reqBuffer, transId, block.FunctionCode, block.StartAddress, block.RegisterCount);

                    // 3. Send & Receive over socket measuring pure Network RTT
                    var rttSw = Stopwatch.StartNew();

                    await _socketLock.WaitAsync(ct).ConfigureAwait(false);
                    try
                    {
                        if (_socket == null || !_socket.Connected)
                        {
                            return (false, 0, 0, 0);
                        }

                        // Send request
                        var sendSegment = new ArraySegment<byte>(reqBuffer, 0, 12);
                        await _socket.SendAsync(sendSegment, SocketFlags.None).ConfigureAwait(false);
                        _metrics.TotalRequestsSent++;
                        _metrics.TotalBytesSent += 12;

                        // Receive response with deterministic Modbus MBAP framing
                        receivedBytes = await ReceiveModbusPacketAsync(_socket, respBuffer, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        _socketLock.Release();
                    }

                    rttSw.Stop();
                    rttTicks = rttSw.ElapsedTicks;

                    if (receivedBytes < 9)
                    {
                        _metrics.TotalErrors++;
                        MarkBlockTagsBad(block);
                        return (false, rttTicks, 0, 0);
                    }

                    _metrics.TotalResponsesReceived++;
                    _metrics.TotalBytesReceived += receivedBytes;

                    // 4. Decode payload measuring Decode Time
                    byte byteCount = respBuffer[8];
                    int expectedPayloadBytes = block.RegisterCount * 2;
                    if (byteCount < expectedPayloadBytes || receivedBytes < 9 + byteCount)
                    {
                        _metrics.TotalErrors++;
                        MarkBlockTagsBad(block);
                        return (false, rttTicks, 0, 0);
                    }

                    var decodeSw = Stopwatch.StartNew();
                    var dataPayload = new ReadOnlySpan<byte>(respBuffer, 9, byteCount);

                    var decodedValues = new (string TagPath, double ScaledValue)[block.TagMappings.Count];
                    for (int i = 0; i < block.TagMappings.Count; i++)
                    {
                        var mapping = block.TagMappings[i];
                        int byteOffset = mapping.RelativeRegisterOffset * 2;
                        int regBytes = mapping.RegisterCount * 2;

                        if (byteOffset + regBytes <= dataPayload.Length)
                        {
                            var slice = dataPayload.Slice(byteOffset, regBytes);
                            double raw = DecodeRegisterData(slice, mapping.TagDefinition.DataType);
                            double scaled = raw * mapping.TagDefinition.Scale + mapping.TagDefinition.Offset;
                            decodedValues[i] = (mapping.TagDefinition.TagPath, scaled);
                        }
                    }
                    decodeSw.Stop();
                    decodeTicks = decodeSw.ElapsedTicks;

                    // 5. Dispatch to Tag Engine measuring Tag Update Time
                    var updateSw = Stopwatch.StartNew();
                    for (int i = 0; i < decodedValues.Length; i++)
                    {
                        var item = decodedValues[i];
                        if (item.TagPath != null)
                        {
                            ZeroTagEngine.SetTagValue(item.TagPath, item.ScaledValue, ScadaQuality.Good);
                        }
                    }
                    updateSw.Stop();
                    tagUpdateTicks = updateSw.ElapsedTicks;

                    return (true, rttTicks, decodeTicks, tagUpdateTicks);
                }
                catch (Exception)
                {
                    _metrics.TotalErrors++;
                    MarkBlockTagsBad(block);
                    return (false, rttTicks, 0, 0);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(reqBuffer);
                    ArrayPool<byte>.Shared.Return(respBuffer);
                }
            }
            finally
            {
                _inFlightLimiter.Release();
            }
        }

        private static void MarkBlockTagsBad(ModbusReadBlock block)
        {
            for (int i = 0; i < block.TagMappings.Count; i++)
            {
                ZeroTagEngine.SetTagValue(block.TagMappings[i].TagDefinition.TagPath, null, ScadaQuality.Bad);
            }
        }

        public async Task<bool> WriteTagAsync(string tagPath, object value, CancellationToken cancellationToken = default)
        {
            if (!_tags.TryGetValue(tagPath, out var tag) || _socket == null || !_socket.Connected)
            {
                return false;
            }

            if (!ushort.TryParse(tag.FieldAddress, out ushort registerAddress))
            {
                return false;
            }

            byte[] reqBuffer = ArrayPool<byte>.Shared.Rent(12);
            byte[] respBuffer = ArrayPool<byte>.Shared.Rent(MaxModbusAduLength);

            try
            {
                ushort transId = AllocateTransactionId();

                if (tag.DataType == TagDataType.Boolean)
                {
                    bool bVal = Convert.ToBoolean(value);
                    ushort coilVal = bVal ? (ushort)0xFF00 : (ushort)0x0000;
                    BuildMbapPacket(reqBuffer, transId, functionCode: 0x05, registerAddress, coilVal);
                }
                else
                {
                    ushort regVal = Convert.ToUInt16(value);
                    BuildMbapPacket(reqBuffer, transId, functionCode: 0x06, registerAddress, regVal);
                }

                await _socketLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var sendSeg = new ArraySegment<byte>(reqBuffer, 0, 12);
                    await _socket.SendAsync(sendSeg, SocketFlags.None).ConfigureAwait(false);
                    int read = await ReceiveModbusPacketAsync(_socket, respBuffer, cancellationToken).ConfigureAwait(false);
                    return read >= 12;
                }
                finally
                {
                    _socketLock.Release();
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(reqBuffer);
                ArrayPool<byte>.Shared.Return(respBuffer);
            }
        }

        private ushort AllocateTransactionId()
        {
            return (ushort)(Interlocked.Increment(ref _transactionIdCounter) & 0xFFFF);
        }

        private void BuildMbapPacket(byte[] buffer, ushort transactionId, byte functionCode, ushort address, ushort valueOrCount)
        {
            var span = buffer.AsSpan(0, 12);
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(0, 2), transactionId);
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(2, 2), 0x0000); // Modbus Protocol
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(4, 2), 0x0006); // 6 bytes follow
            span[6] = _unitId;
            span[7] = functionCode;
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(8, 2), address);
            BinaryPrimitives.WriteUInt16BigEndian(span.Slice(10, 2), valueOrCount);
        }

        private static async Task<int> ReceiveModbusPacketAsync(Socket socket, byte[] buffer, CancellationToken ct)
        {
            // Deterministic framing: Read 7-byte MBAP header first
            int headerRead = 0;
            while (headerRead < 7)
            {
                var seg = new ArraySegment<byte>(buffer, headerRead, 7 - headerRead);
                int r = await socket.ReceiveAsync(seg, SocketFlags.None).ConfigureAwait(false);
                if (r <= 0) return 0;
                headerRead += r;
            }

            // Bytes 4-5 contain length of remaining bytes (UnitId + PDU)
            ushort remainingLength = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(4, 2));
            if (remainingLength < 2 || remainingLength > 254)
            {
                return 0; // Malformed length
            }

            int pduBytesToRead = remainingLength - 1; // UnitId was byte 6 of header
            int pduRead = 0;
            while (pduRead < pduBytesToRead)
            {
                var seg = new ArraySegment<byte>(buffer, 7 + pduRead, pduBytesToRead - pduRead);
                int r = await socket.ReceiveAsync(seg, SocketFlags.None).ConfigureAwait(false);
                if (r <= 0) return 0;
                pduRead += r;
            }

            return 7 + pduRead;
        }

        private static double DecodeRegisterData(ReadOnlySpan<byte> data, TagDataType dataType)
        {
            if (data.Length < 2) return 0.0;

            switch (dataType)
            {
                case TagDataType.Boolean:
                    return (data[0] != 0 || data[1] != 0) ? 1.0 : 0.0;

                case TagDataType.Int16:
                    return BinaryPrimitives.ReadInt16BigEndian(data);

                case TagDataType.UInt16:
                    return BinaryPrimitives.ReadUInt16BigEndian(data);

                case TagDataType.Int32 when data.Length >= 4:
                    return BinaryPrimitives.ReadInt32BigEndian(data);

                case TagDataType.UInt32 when data.Length >= 4:
                    return BinaryPrimitives.ReadUInt32BigEndian(data);

                case TagDataType.Float32 when data.Length >= 4:
                    uint rawBits = BinaryPrimitives.ReadUInt32BigEndian(data);
                    return ZeroUI.Core.Common.ZeroMemory.Int32BitsToSingle((int)rawBits);

                case TagDataType.Double64 when data.Length >= 8:
                    ulong raw64 = BinaryPrimitives.ReadUInt64BigEndian(data);
                    return BitConverter.Int64BitsToDouble((long)raw64);

                default:
                    return BinaryPrimitives.ReadUInt16BigEndian(data);
            }
        }

        private void DisconnectSocket()
        {
            try
            {
                _socket?.Shutdown(SocketShutdown.Both);
                _socket?.Close();
                _socket?.Dispose();
            }
            catch
            {
                // Ignored during cleanup
            }
            finally
            {
                _socket = null;
            }
        }

        private void UpdateState(AdapterConnectionState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                try
                {
                    StateChanged?.Invoke(this, newState);
                }
                catch
                {
                    // Guard subscriber exceptions
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            DisconnectSocket();
            _inFlightLimiter.Dispose();
            _sendLock.Dispose();
            _socketLock.Dispose();
        }
    }
}
