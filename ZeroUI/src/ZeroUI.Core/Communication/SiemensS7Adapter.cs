using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// High-performance Siemens S7Comm protocol adapter over ISO-on-TCP (RFC 1006).
    /// Connects to Siemens S7-1200, S7-1500, S7-300, and S7-400 PLCs on port 102,
    /// reading and writing Data Blocks (DB) directly into ZeroTagEngine.
    /// </summary>
    public sealed class SiemensS7Adapter : IProtocolAdapter
    {
        private readonly string _adapterId;
        private readonly string _host;
        private readonly int _port;
        private readonly byte _rack;
        private readonly byte _slot;
        private readonly TimeSpan _timeout;

        private readonly ConcurrentDictionary<string, AdapterTagDefinition> _tags =
            new ConcurrentDictionary<string, AdapterTagDefinition>(StringComparer.OrdinalIgnoreCase);

        private IReadOnlyList<S7ReadBlock>? _compiledBlocks;
        private readonly object _plannerLock = new object();

        private Socket? _socket;
        private AdapterConnectionState _state = AdapterConnectionState.Disconnected;
        private TimeSpan _latency = TimeSpan.Zero;
        private bool _isDisposed;

        public string AdapterId => _adapterId;
        public string Endpoint => $"{_host}:{_port} (Rack {_rack}, Slot {_slot})";
        public AdapterConnectionState State => _state;
        public TimeSpan Latency => _latency;
        public IReadOnlyList<S7ReadBlock> CompiledBlocks => GetOrCompileBlocks();

        public event Action<IProtocolAdapter, AdapterConnectionState>? StateChanged;

        public SiemensS7Adapter(
            string adapterId,
            string host,
            int port = 102,
            byte rack = 0,
            byte slot = 1,
            TimeSpan? timeout = null)
        {
            _adapterId = adapterId ?? throw new ArgumentNullException(nameof(adapterId));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _rack = rack;
            _slot = slot;
            _timeout = timeout ?? TimeSpan.FromSeconds(3);
        }

        public void RegisterTag(AdapterTagDefinition tagDef)
        {
            if (tagDef == null) throw new ArgumentNullException(nameof(tagDef));
            _tags[tagDef.TagPath] = tagDef;
            lock (_plannerLock)
            {
                _compiledBlocks = null;
            }
        }

        public IReadOnlyCollection<AdapterTagDefinition> GetRegisteredTags()
        {
            return (IReadOnlyCollection<AdapterTagDefinition>)_tags.Values;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(SiemensS7Adapter));
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
                    throw new TimeoutException($"Siemens S7 connection to {_host}:{_port} timed out.");
                }

                _socket = socket;

                // Step 1: ISO-on-TCP COTP Connection Request (CR)
                await SendCotpConnectRequestAsync(cancellationToken).ConfigureAwait(false);

                // Step 2: S7 Setup Communication PDU
                await SendS7SetupCommunicationAsync(cancellationToken).ConfigureAwait(false);

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

            var blocks = GetOrCompileBlocks();
            if (blocks.Count == 0) return;

            var sw = Stopwatch.StartNew();

            foreach (var block in blocks)
            {
                try
                {
                    await PollBlockAsync(block, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    for (int i = 0; i < block.TagMappings.Count; i++)
                    {
                        ZeroTagEngine.SetTagValue(block.TagMappings[i].TagDefinition.TagPath, null, ScadaQuality.Bad);
                    }
                }
            }

            sw.Stop();
            _latency = sw.Elapsed;
        }

        private IReadOnlyList<S7ReadBlock> GetOrCompileBlocks()
        {
            var blocks = _compiledBlocks;
            if (blocks != null) return blocks;

            lock (_plannerLock)
            {
                if (_compiledBlocks != null) return _compiledBlocks;
                _compiledBlocks = S7AddressPlanner.PlanReadBlocks(_tags.Values);
                return _compiledBlocks;
            }
        }

        public async Task<bool> WriteTagAsync(string tagPath, object value, CancellationToken cancellationToken = default)
        {
            if (!_tags.TryGetValue(tagPath, out var tag) || _socket == null || !_socket.Connected)
            {
                return false;
            }

            if (!TryParseS7Address(tag.FieldAddress, out int dbNumber, out int byteOffset, out int bitOffset))
            {
                return false;
            }

            // Construct S7 Write PDU
            byte[] valueBytes = EncodeValueToBytes(value, tag.DataType);
            var writePacket = BuildS7WriteRequest((ushort)dbNumber, byteOffset, bitOffset, tag.DataType, valueBytes);

            var response = await SendReceiveAsync(writePacket, cancellationToken).ConfigureAwait(false);
            if (response != null && response.Length >= 22)
            {
                // Return code 0xFF indicates success
                return response[response.Length - 1] == 0xFF;
            }

            return false;
        }

        private async Task PollBlockAsync(S7ReadBlock block, CancellationToken ct)
        {
            var readRequest = BuildS7ReadRequest(block.DbNumber, block.StartByteOffset, block.ByteCount);
            var response = await SendReceiveAsync(readRequest, ct).ConfigureAwait(false);

            if (response != null && response.Length >= 25)
            {
                // S7 Read Var Data starts at index 25
                int dataIndex = 25;
                int payloadLen = response.Length - dataIndex;

                for (int i = 0; i < block.TagMappings.Count; i++)
                {
                    var mapping = block.TagMappings[i];
                    var tag = mapping.TagDefinition;

                    if (mapping.RelativeByteOffset + mapping.ByteLength <= payloadLen)
                    {
                        var dataSlice = new ReadOnlySpan<byte>(response, dataIndex + mapping.RelativeByteOffset, mapping.ByteLength);
                        double parsedVal = DecodeS7Data(dataSlice, tag.DataType, mapping.BitOffset);
                        double scaledVal = (parsedVal * tag.Scale) + tag.Offset;

                        ZeroTagEngine.SetTagValue(tag.TagPath, scaledVal, ScadaQuality.Good);
                    }
                    else
                    {
                        ZeroTagEngine.SetTagValue(tag.TagPath, null, ScadaQuality.Bad);
                    }
                }
            }
            else
            {
                for (int i = 0; i < block.TagMappings.Count; i++)
                {
                    ZeroTagEngine.SetTagValue(block.TagMappings[i].TagDefinition.TagPath, null, ScadaQuality.Bad);
                }
            }
        }

        private static double DecodeS7Data(ReadOnlySpan<byte> data, TagDataType dataType, int bitOffset)
        {
            switch (dataType)
            {
                case TagDataType.Boolean:
                    return ((data[0] >> bitOffset) & 0x01) == 1 ? 1.0 : 0.0;

                case TagDataType.Int16 when data.Length >= 2:
                    return BinaryPrimitives.ReadInt16BigEndian(data);

                case TagDataType.UInt16 when data.Length >= 2:
                    return BinaryPrimitives.ReadUInt16BigEndian(data);

                case TagDataType.Int32 when data.Length >= 4:
                    return BinaryPrimitives.ReadInt32BigEndian(data);

                case TagDataType.UInt32 when data.Length >= 4:
                    return BinaryPrimitives.ReadUInt32BigEndian(data);

                case TagDataType.Float32 when data.Length >= 4:
                    uint rawBits = BinaryPrimitives.ReadUInt32BigEndian(data);
                    return ZeroUI.Core.Common.ZeroMemory.Int32BitsToSingle((int)rawBits);

                default:
                    return data[0];
            }
        }

        private static byte[] EncodeValueToBytes(object value, TagDataType dataType)
        {
            switch (dataType)
            {
                case TagDataType.Boolean:
                    return new[] { Convert.ToBoolean(value) ? (byte)1 : (byte)0 };

                case TagDataType.Int16:
                    var int16Bytes = new byte[2];
                    BinaryPrimitives.WriteInt16BigEndian(int16Bytes, Convert.ToInt16(value));
                    return int16Bytes;

                case TagDataType.Float32:
                    var floatBytes = new byte[4];
                    float fVal = Convert.ToSingle(value);
                    uint rawBits = (uint)ZeroUI.Core.Common.ZeroMemory.SingleToInt32Bits(fVal);
                    BinaryPrimitives.WriteUInt32BigEndian(floatBytes, rawBits);
                    return floatBytes;

                default:
                    return new[] { Convert.ToByte(value) };
            }
        }

        private async Task SendCotpConnectRequestAsync(CancellationToken ct)
        {
            // TPKT (4 bytes) + COTP Connection Request (18 bytes)
            byte srcTsap1 = 0x01;
            byte srcTsap2 = 0x00;
            byte dstTsap1 = 0x01;
            byte dstTsap2 = (byte)((_rack * 0x20) + _slot);

            byte[] crPacket = new byte[]
            {
                0x03, 0x00, 0x00, 0x16, // TPKT: Version 3, Length 22
                0x11,                   // COTP Length 17
                0xE0,                   // PDU Type: CR
                0x00, 0x00,             // DST Ref
                0x00, 0x01,             // SRC Ref
                0x00,                   // Class / Option
                0xC1, 0x02, srcTsap1, srcTsap2, // Calling TSAP
                0xC2, 0x02, dstTsap1, dstTsap2, // Called TSAP
                0xC0, 0x01, 0x0A        // TPDU Size: 1024
            };

            var response = await SendReceiveAsync(crPacket, ct).ConfigureAwait(false);
            if (response == null || response.Length < 7 || response[5] != 0xD0)
            {
                throw new InvalidOperationException("Failed to establish S7 COTP connection.");
            }
        }

        private async Task SendS7SetupCommunicationAsync(CancellationToken ct)
        {
            // S7 Setup Communication PDU
            byte[] setupPacket = new byte[]
            {
                0x03, 0x00, 0x00, 0x19, // TPKT: Length 25
                0x02, 0xF0, 0x80,       // COTP: DT Data
                0x32, 0x01,             // S7 Header: Protocol ID 0x32, Type 1 (Job)
                0x00, 0x00,             // Reserved
                0x00, 0x00,             // Sequence / PDU Ref
                0x00, 0x08,             // Param Length (8)
                0x00, 0x00,             // Data Length (0)
                0xF0,                   // Function: Setup Communication
                0x00,                   // Reserved
                0x00, 0x01,             // Max AmQ Calling (1)
                0x00, 0x01,             // Max AmQ Called (1)
                0x01, 0xE0              // Max PDU Length (480 bytes)
            };

            var response = await SendReceiveAsync(setupPacket, ct).ConfigureAwait(false);
            if (response == null || response.Length < 19 || response[7] != 0x32)
            {
                throw new InvalidOperationException("Failed to negotiate S7 communication setup.");
            }
        }

        private static byte[] BuildS7ReadRequest(ushort dbNumber, int byteOffset, int byteCount)
        {
            // S7 Read Var Request
            int bitAddress = byteOffset * 8;
            byte[] packet = new byte[31];

            // TPKT
            packet[0] = 0x03;
            packet[1] = 0x00;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), 31);

            // COTP DT Data
            packet[4] = 0x02;
            packet[5] = 0xF0;
            packet[6] = 0x80;

            // S7 Header
            packet[7] = 0x32; // Protocol ID
            packet[8] = 0x01; // Job
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(9, 2), 0x0000);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(11, 2), 0x0001); // PDU Ref
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(13, 2), 0x000E); // Param length (14)
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(15, 2), 0x0000); // Data length (0)

            // Function Read Var
            packet[17] = 0x04; // Read
            packet[18] = 0x01; // Item count (1)

            // Item 1
            packet[19] = 0x12; // Variable Spec
            packet[20] = 0x0A; // Length of remaining item bytes
            packet[21] = 0x10; // Syntax ID: S7ANY
            packet[22] = 0x02; // Transport size: Byte
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(23, 2), (ushort)byteCount);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(25, 2), dbNumber);
            packet[27] = 0x84; // Area: DB
            packet[28] = (byte)((bitAddress >> 16) & 0xFF);
            packet[29] = (byte)((bitAddress >> 8) & 0xFF);
            packet[30] = (byte)(bitAddress & 0xFF);

            return packet;
        }

        private static byte[] BuildS7WriteRequest(ushort dbNumber, int byteOffset, int bitOffset, TagDataType dataType, byte[] data)
        {
            int bitAddress = (byteOffset * 8) + bitOffset;
            int totalLen = 35 + data.Length;
            byte[] packet = new byte[totalLen];

            // TPKT
            packet[0] = 0x03;
            packet[1] = 0x00;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(2, 2), (ushort)totalLen);

            // COTP
            packet[4] = 0x02;
            packet[5] = 0xF0;
            packet[6] = 0x80;

            // S7 Header
            packet[7] = 0x32;
            packet[8] = 0x01;
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(9, 2), 0x0000);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(11, 2), 0x0002); // PDU Ref
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(13, 2), 0x000E); // Param length (14)
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(15, 2), (ushort)(4 + data.Length)); // Data length

            // Function Write Var
            packet[17] = 0x05; // Write
            packet[18] = 0x01; // Item count (1)

            // Item 1
            packet[19] = 0x12;
            packet[20] = 0x0A;
            packet[21] = 0x10;
            packet[22] = dataType == TagDataType.Boolean ? (byte)0x01 : (byte)0x02; // Bit or Byte
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(23, 2), (ushort)data.Length);
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(25, 2), dbNumber);
            packet[27] = 0x84; // DB Area
            packet[28] = (byte)((bitAddress >> 16) & 0xFF);
            packet[29] = (byte)((bitAddress >> 8) & 0xFF);
            packet[30] = (byte)(bitAddress & 0xFF);

            // Data section
            packet[31] = 0x00; // Return code
            packet[32] = dataType == TagDataType.Boolean ? (byte)0x03 : (byte)0x04; // Transport size
            BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(33, 2), (ushort)(data.Length * 8));
            Array.Copy(data, 0, packet, 35, data.Length);

            return packet;
        }

        private static bool TryParseS7Address(string address, out int dbNumber, out int byteOffset, out int bitOffset)
        {
            return S7AddressPlanner.TryParseAddress(address, out dbNumber, out byteOffset, out bitOffset);
        }

        private async Task<byte[]?> SendReceiveAsync(byte[] request, CancellationToken ct)
        {
            if (_socket == null || !_socket.Connected) return null;

            try
            {
                var sendSegment = new ArraySegment<byte>(request);
                await _socket.SendAsync(sendSegment, SocketFlags.None).ConfigureAwait(false);

                var buffer = new byte[512];
                var recvSegment = new ArraySegment<byte>(buffer);
                int received = await _socket.ReceiveAsync(recvSegment, SocketFlags.None).ConfigureAwait(false);

                if (received <= 0) return null;

                var result = new byte[received];
                Array.Copy(buffer, result, received);
                return result;
            }
            catch
            {
                UpdateState(AdapterConnectionState.Faulted);
                return null;
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
                // Clean exit
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
        }
    }
}
