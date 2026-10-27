using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Transport;

namespace ZeroComm.Core.Siemens
{
    /// <summary>
    /// High-performance asynchronous Siemens S7 PLC communication client (ISO-on-TCP / S7comm).
    /// Supports S7-300, S7-400, S7-1200, and S7-1500 PLCs.
    /// Operates over any <see cref="ITransport"/> (e.g. TcpTransport).
    /// </summary>
    public class S7TcpClient : IDisposable, IProtocolSession, IIndustrialPlcClient
    {
        private readonly ITransport _transport;
        private readonly CircularRingBuffer _ringBuffer = new CircularRingBuffer(65536);
        private readonly object _ringLock = new object();
        private readonly Channels.HalfDuplexChannel<byte[]> _channel = new Channels.HalfDuplexChannel<byte[]>();
        private ushort _sequenceNumber = 1;
        private int _configuredRack = 0;
        private int _configuredSlot = 1;
        private bool _handshakeCompleted;
        private bool _isDisposed;

        /// <summary>
        /// Gets the underlying transport instance.
        /// </summary>
        public ITransport Transport => _transport;

        /// <summary>
        /// Gets the negotiated maximum S7 PDU length in bytes.
        /// </summary>
        public ushort NegotiatedPduLength { get; private set; } = S7Frame.DefaultPduLength;

        /// <summary>
        /// Gets or sets the default timeout in milliseconds (default: 3000ms).
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// Initializes a new instance of the <see cref="S7TcpClient"/> class.
        /// </summary>
        public S7TcpClient(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.DataReceived += OnDataReceived;
            _transport.OnDisconnected += OnDisconnected;

            if (_transport is AsyncTcpTransport tcp)
            {
                tcp.AttachSession(this);
            }
        }

        private void OnDataReceived(byte[] buffer, int offset, int count)
        {
            lock (_ringLock)
            {
                _ringBuffer.Write(buffer, offset, count);

                while (StreamingFrameParser.TryExtractS7Frame(_ringBuffer, out byte[] frame))
                {
                    _channel.TrySetResponse(frame);
                }
            }
        }

        private void OnDisconnected()
        {
            _channel.FaultPending(new IOException("Transport disconnected while awaiting Siemens S7 response."));
        }

        #region Connection & Handshake

        /// <summary>
        /// Performs complete ISO-on-TCP COTP handshake and S7 PDU negotiation.
        /// For S7-1200/1500: rack = 0, slot = 1. For S7-300: rack = 0, slot = 2.
        /// </summary>
        public async Task ConnectHandshakeAsync(int rack = 0, int slot = 1, CancellationToken ct = default)
        {
            _configuredRack = rack;
            _configuredSlot = slot;

            // 1. Step 1: Send COTP Connection Request (CR)
            byte[] crPacket = S7Frame.BuildConnectionRequest(rack, slot);
            byte[] ccResponse = await SendAndReceiveAsync(crPacket, DefaultTimeoutMs, ct).ConfigureAwait(false);

            if (!S7Frame.ValidateConnectionConfirm(ccResponse))
            {
                throw new S7Exception("COTP Connection Request was rejected by Siemens PLC.");
            }

            // 2. Step 2: Negotiate S7 Communication Setup PDU
            ushort seq = GetNextSequenceNumber();
            byte[] setupPacket = S7Frame.BuildSetupCommunication(seq, S7Frame.DefaultPduLength);
            byte[] setupResponse = await SendAndReceiveAsync(setupPacket, DefaultTimeoutMs, ct).ConfigureAwait(false);

            NegotiatedPduLength = S7Frame.ParseSetupCommunicationResponse(setupResponse);
            _handshakeCompleted = true;
        }

        async Task IProtocolSession.OnSessionConnectedAsync(CancellationToken cancellationToken)
        {
            if (_handshakeCompleted)
            {
                await ConnectHandshakeAsync(_configuredRack, _configuredSlot, cancellationToken).ConfigureAwait(false);
            }
        }

        Task IProtocolSession.OnSessionDisconnectedAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Read Operations

        /// <summary>
        /// Reads a block of raw bytes from a specified S7 memory area (e.g. DB, Merkers, Inputs, Outputs).
        /// </summary>
        public async Task<byte[]> ReadBytesAsync(
            S7Area area,
            ushort dbNumber,
            int startByte,
            ushort count,
            CancellationToken ct = default)
        {
            var variable = new S7VariableAddress(area, dbNumber, startByte, count, isBit: false);
            ushort seq = GetNextSequenceNumber();
            byte[] request = S7Frame.BuildReadRequest(seq, new[] { variable });

            byte[] response = await SendAndReceiveAsync(request, DefaultTimeoutMs, ct).ConfigureAwait(false);
            var results = S7Frame.ParseReadResponse(response, expectedItems: 1);
            return results[0];
        }

        /// <summary>
        /// Reads a single bit from a specified S7 memory area.
        /// </summary>
        public async Task<bool> ReadBitAsync(
            S7Area area,
            ushort dbNumber,
            int startByte,
            int bitIndex,
            CancellationToken ct = default)
        {
            if (bitIndex < 0 || bitIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be between 0 and 7.");

            var variable = new S7VariableAddress(area, dbNumber, startByte, 1, isBit: true, bitIndex: bitIndex);
            ushort seq = GetNextSequenceNumber();
            byte[] request = S7Frame.BuildReadRequest(seq, new[] { variable });

            byte[] response = await SendAndReceiveAsync(request, DefaultTimeoutMs, ct).ConfigureAwait(false);
            var results = S7Frame.ParseReadResponse(response, expectedItems: 1);

            return results[0].Length > 0 && results[0][0] != 0;
        }

        /// <summary>
        /// Reads multiple disparate variables in a single multi-variable PDU request.
        /// </summary>
        public async Task<List<byte[]>> ReadMultiVariablesAsync(
            IReadOnlyList<S7VariableAddress> variables,
            CancellationToken ct = default)
        {
            ushort seq = GetNextSequenceNumber();
            byte[] request = S7Frame.BuildReadRequest(seq, variables);

            byte[] response = await SendAndReceiveAsync(request, DefaultTimeoutMs, ct).ConfigureAwait(false);
            return S7Frame.ParseReadResponse(response, expectedItems: variables.Count);
        }

        #endregion

        #region Write Operations

        /// <summary>
        /// Writes raw bytes to a specified S7 memory area.
        /// </summary>
        public async Task WriteBytesAsync(
            S7Area area,
            ushort dbNumber,
            int startByte,
            byte[] data,
            CancellationToken ct = default)
        {
            var variable = new S7VariableAddress(area, dbNumber, startByte, (ushort)data.Length, isBit: false);
            ushort seq = GetNextSequenceNumber();
            byte[] request = S7Frame.BuildWriteRequest(seq, variable, data);

            byte[] response = await SendAndReceiveAsync(request, DefaultTimeoutMs, ct).ConfigureAwait(false);
            S7Frame.ParseWriteResponse(response);
        }

        /// <summary>
        /// Writes a single bit to a specified S7 memory area.
        /// </summary>
        public async Task WriteBitAsync(
            S7Area area,
            ushort dbNumber,
            int startByte,
            int bitIndex,
            bool value,
            CancellationToken ct = default)
        {
            if (bitIndex < 0 || bitIndex > 7)
                throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be between 0 and 7.");

            var variable = new S7VariableAddress(area, dbNumber, startByte, 1, isBit: true, bitIndex: bitIndex);
            byte[] data = new byte[] { (byte)(value ? 1 : 0) };

            ushort seq = GetNextSequenceNumber();
            byte[] request = S7Frame.BuildWriteRequest(seq, variable, data);

            byte[] response = await SendAndReceiveAsync(request, DefaultTimeoutMs, ct).ConfigureAwait(false);
            S7Frame.ParseWriteResponse(response);
        }

        #endregion

        #region Typed Helpers (Big-Endian S7 Data Formats)

        public async Task<short> ReadInt16Async(S7Area area, ushort dbNumber, int startByte, CancellationToken ct = default)
        {
            byte[] bytes = await ReadBytesAsync(area, dbNumber, startByte, 2, ct).ConfigureAwait(false);
            return (short)((bytes[0] << 8) | bytes[1]);
        }

        public async Task WriteInt16Async(S7Area area, ushort dbNumber, int startByte, short value, CancellationToken ct = default)
        {
            byte[] bytes = new byte[] { (byte)(value >> 8), (byte)(value & 0xFF) };
            await WriteBytesAsync(area, dbNumber, startByte, bytes, ct).ConfigureAwait(false);
        }

        public async Task<int> ReadInt32Async(S7Area area, ushort dbNumber, int startByte, CancellationToken ct = default)
        {
            byte[] bytes = await ReadBytesAsync(area, dbNumber, startByte, 4, ct).ConfigureAwait(false);
            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        public async Task WriteInt32Async(S7Area area, ushort dbNumber, int startByte, int value, CancellationToken ct = default)
        {
            byte[] bytes = new byte[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            };
            await WriteBytesAsync(area, dbNumber, startByte, bytes, ct).ConfigureAwait(false);
        }

        public async Task<float> ReadFloatAsync(S7Area area, ushort dbNumber, int startByte, CancellationToken ct = default)
        {
            byte[] bytes = await ReadBytesAsync(area, dbNumber, startByte, 4, ct).ConfigureAwait(false);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return BitConverter.ToSingle(bytes, 0);
        }

        public async Task WriteFloatAsync(S7Area area, ushort dbNumber, int startByte, float value, CancellationToken ct = default)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            await WriteBytesAsync(area, dbNumber, startByte, bytes, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads standard Siemens S7 String format (Byte 0: MaxLength, Byte 1: ActualLength, Bytes 2..N: ASCII payload).
        /// </summary>
        public async Task<string> ReadStringAsync(S7Area area, ushort dbNumber, int startByte, ushort maxLength, CancellationToken ct = default)
        {
            ushort totalToRead = (ushort)(maxLength + 2);
            byte[] bytes = await ReadBytesAsync(area, dbNumber, startByte, totalToRead, ct).ConfigureAwait(false);

            if (bytes.Length < 2) return string.Empty;
            int actualLength = bytes[1];
            if (actualLength > bytes.Length - 2)
            {
                actualLength = bytes.Length - 2;
            }

            return Encoding.ASCII.GetString(bytes, 2, actualLength);
        }

        #endregion

        #region Internal Dispatch Loop

        private Task<byte[]> SendAndReceiveAsync(byte[] request, int timeoutMs, CancellationToken ct)
        {
            ThrowIfDisposed();
            return _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(request, 0, request.Length, token),
                timeoutMs,
                ct);
        }

        private ushort GetNextSequenceNumber()
        {
            return unchecked(++_sequenceNumber);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(S7TcpClient));
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_transport is AsyncTcpTransport tcp)
            {
                tcp.DetachSession();
            }

            _transport.DataReceived -= OnDataReceived;
            _transport.OnDisconnected -= OnDisconnected;
            _channel.Dispose();
        }

        #endregion

        #region IIndustrialPlcClient Implementation

        public string ClientId { get; set; } = "SiemensS7";
        public PlcVendor Vendor => PlcVendor.SiemensS7;

        public PlcConnectionState State =>
            _transport.IsConnected ? (_handshakeCompleted ? PlcConnectionState.Connected : PlcConnectionState.Handshaking) : PlcConnectionState.Disconnected;

        public PlcDriverCapabilities Capabilities { get; } = new PlcDriverCapabilities
        {
            Vendor = PlcVendor.SiemensS7,
            MaxPduBytes = 240,
            MaxBatchReadItems = 19,
            MaxContiguousReadBytes = 460,
            SupportsBitAddressing = true,
            SupportsRandomBatchRead = true,
            SupportsTagNames = false
        };

        public event Action<IIndustrialPlcClient, PlcConnectionState>? StateChanged;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await ConnectHandshakeAsync(_configuredRack, _configuredSlot, cancellationToken).ConfigureAwait(false);
            StateChanged?.Invoke(this, State);
        }

        public async Task DisconnectAsync()
        {
            await _transport.DisconnectAsync().ConfigureAwait(false);
            StateChanged?.Invoke(this, State);
        }

        public async Task<T> ReadAsync<T>(string tagAddress, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.SiemensS7);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Siemens address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            var area = (S7Area)res.AreaCode;

            if (typeof(T) == typeof(bool))
            {
                bool bitVal = await ReadBitAsync(area, res.DbNumber, res.Offset, res.BitOffset, cancellationToken).ConfigureAwait(false);
                return (T)(object)bitVal;
            }

            if (typeof(T) == typeof(byte))
            {
                byte[] bytes = await ReadBytesAsync(area, res.DbNumber, res.Offset, 1, cancellationToken).ConfigureAwait(false);
                return (T)(object)bytes[0];
            }

            if (typeof(T) == typeof(short))
            {
                short val = await ReadInt16Async(area, res.DbNumber, res.Offset, cancellationToken).ConfigureAwait(false);
                return (T)(object)val;
            }

            if (typeof(T) == typeof(ushort))
            {
                short val = await ReadInt16Async(area, res.DbNumber, res.Offset, cancellationToken).ConfigureAwait(false);
                return (T)(object)(ushort)val;
            }

            if (typeof(T) == typeof(int))
            {
                int val = await ReadInt32Async(area, res.DbNumber, res.Offset, cancellationToken).ConfigureAwait(false);
                return (T)(object)val;
            }

            if (typeof(T) == typeof(uint))
            {
                int val = await ReadInt32Async(area, res.DbNumber, res.Offset, cancellationToken).ConfigureAwait(false);
                return (T)(object)(uint)val;
            }

            if (typeof(T) == typeof(float))
            {
                float val = await ReadFloatAsync(area, res.DbNumber, res.Offset, cancellationToken).ConfigureAwait(false);
                return (T)(object)val;
            }

            if (typeof(T) == typeof(double))
            {
                byte[] bytes = await ReadBytesAsync(area, res.DbNumber, res.Offset, 8, cancellationToken).ConfigureAwait(false);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                double val = BitConverter.ToDouble(bytes, 0);
                return (T)(object)val;
            }

            throw new NotSupportedException($"Type {typeof(T).Name} is not supported for single-tag reading.");
        }

        public async Task<bool> WriteAsync<T>(string tagAddress, T value, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.SiemensS7);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Siemens address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            var area = (S7Area)res.AreaCode;

            if (typeof(T) == typeof(bool))
            {
                await WriteBitAsync(area, res.DbNumber, res.Offset, res.BitOffset, (bool)(object)value, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(byte))
            {
                await WriteBytesAsync(area, res.DbNumber, res.Offset, new byte[] { (byte)(object)value }, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(short))
            {
                await WriteInt16Async(area, res.DbNumber, res.Offset, (short)(object)value, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(ushort))
            {
                await WriteInt16Async(area, res.DbNumber, res.Offset, (short)(ushort)(object)value, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(int))
            {
                await WriteInt32Async(area, res.DbNumber, res.Offset, (int)(object)value, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(uint))
            {
                await WriteInt32Async(area, res.DbNumber, res.Offset, (int)(uint)(object)value, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(float))
            {
                await WriteFloatAsync(area, res.DbNumber, res.Offset, (float)(object)value, cancellationToken).ConfigureAwait(false);
                return true;
            }

            throw new NotSupportedException($"Type {typeof(T).Name} is not supported for single-tag writing.");
        }

        public Task<string> ReadStringAsync(string tagAddress, int length, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.SiemensS7);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Siemens address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            return ReadStringAsync((S7Area)res.AreaCode, res.DbNumber, res.Offset, (ushort)length, cancellationToken);
        }

        public async Task<bool> WriteStringAsync(string tagAddress, string value, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.SiemensS7);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Siemens address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            encoding ??= Encoding.ASCII;
            byte[] textBytes = encoding.GetBytes(value ?? string.Empty);
            byte[] s7StringBytes = new byte[textBytes.Length + 2];
            s7StringBytes[0] = (byte)textBytes.Length; // Max length
            s7StringBytes[1] = (byte)textBytes.Length; // Actual length
            Array.Copy(textBytes, 0, s7StringBytes, 2, textBytes.Length);

            await WriteBytesAsync((S7Area)res.AreaCode, res.DbNumber, res.Offset, s7StringBytes, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public Task<byte[]> ReadRawBytesAsync(string tagAddress, int length, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.SiemensS7);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Siemens address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            return ReadBytesAsync((S7Area)res.AreaCode, res.DbNumber, res.Offset, (ushort)length, cancellationToken);
        }

        public async Task<bool> WriteRawBytesAsync(string tagAddress, byte[] data, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.SiemensS7);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Siemens address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            await WriteBytesAsync((S7Area)res.AreaCode, res.DbNumber, res.Offset, data, cancellationToken).ConfigureAwait(false);
            return true;
        }

        #endregion
    }
}
