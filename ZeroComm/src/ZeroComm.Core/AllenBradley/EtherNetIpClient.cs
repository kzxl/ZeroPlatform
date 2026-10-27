using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Channels;
using ZeroComm.Core.Transport;

namespace ZeroComm.Core.AllenBradley
{
    /// <summary>
    /// High-performance asynchronous Allen-Bradley EtherNet/IP and CIP Tag Client for ControlLogix, CompactLogix, and Micro800 PLCs.
    /// Supports ANSI Extended Symbol tag resolution, struct/array paths, Unconnected Send routing, and IIndustrialPlcClient.
    /// Pure C# implementation with zero external dependencies.
    /// </summary>
    public class EtherNetIpClient : IDisposable, IProtocolSession, IIndustrialPlcClient
    {
        private readonly ITransport _transport;
        private readonly CircularRingBuffer _ringBuffer = new CircularRingBuffer(65536);
        private readonly object _ringLock = new object();
        private readonly HalfDuplexChannel<byte[]> _channel = new HalfDuplexChannel<byte[]>();

        private bool _handshakeCompleted;
        private bool _isDisposed;

        /// <summary>
        /// Gets the underlying transport instance.
        /// </summary>
        public ITransport Transport => _transport;

        /// <summary>
        /// Gets the active EtherNet/IP Session Handle assigned by the controller.
        /// </summary>
        public uint SessionHandle { get; private set; }

        /// <summary>
        /// Gets or sets the target controller slot in chassis (default: 0 for CompactLogix or Slot 0 ControlLogix).
        /// </summary>
        public byte Slot { get; set; } = 0;

        /// <summary>
        /// Gets or sets whether to enforce Connection Manager Unconnected Send backplane routing.
        /// Automatically true if Slot > 0.
        /// </summary>
        public bool UseRouting { get; set; } = false;

        /// <summary>
        /// Gets or sets the default timeout in milliseconds (default: 3000ms).
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// Initializes a new instance of the <see cref="EtherNetIpClient"/> class.
        /// </summary>
        public EtherNetIpClient(ITransport transport)
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

                while (StreamingFrameParser.TryExtractEtherNetIpFrame(_ringBuffer, out byte[] frame))
                {
                    _channel.TrySetResponse(frame);
                }
            }
        }

        private void OnDisconnected()
        {
            _channel.FaultPending(new IOException("Transport disconnected while awaiting EtherNet/IP response."));
        }

        #region Handshake & Protocol Session

        /// <summary>
        /// Registers a session with the EtherNet/IP target controller.
        /// </summary>
        public async Task ConnectHandshakeAsync(CancellationToken cancellationToken = default)
        {
            byte[] request = CipFrame.BuildRegisterSession();

            byte[] response = await _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(request, 0, request.Length, token),
                DefaultTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (!CipFrame.ParseRegisterSessionResponse(response, out uint sessionHandle, out uint status))
            {
                throw new CipException(status);
            }

            SessionHandle = sessionHandle;
            _handshakeCompleted = true;
        }

        /// <summary>
        /// Closes the active session on the controller.
        /// </summary>
        public async Task UnregisterSessionAsync(CancellationToken cancellationToken = default)
        {
            if (SessionHandle == 0) return;

            byte[] request = CipFrame.BuildUnregisterSession(SessionHandle);
            try
            {
                await _transport.SendAsync(request, 0, request.Length, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Unregister failure can be safely ignored during disconnect
            }
            finally
            {
                SessionHandle = 0;
                _handshakeCompleted = false;
            }
        }

        async Task IProtocolSession.OnSessionConnectedAsync(CancellationToken cancellationToken)
        {
            lock (_ringLock)
            {
                _ringBuffer.Clear();
            }

            if (_handshakeCompleted)
            {
                await ConnectHandshakeAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        Task IProtocolSession.OnSessionDisconnectedAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Raw Tag Read / Write Operations

        /// <summary>
        /// Reads raw CIP data bytes for the specified tag name.
        /// </summary>
        public async Task<(CipDataType DataType, byte[] Data)> ReadTagRawAsync(
            string tagName,
            ushort elementsCount = 1,
            CancellationToken cancellationToken = default)
        {
            EnsureSessionActive();

            byte[] request = CipFrame.BuildReadTagRequest(SessionHandle, tagName, elementsCount, Slot, UseRouting);

            byte[] response = await _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(request, 0, request.Length, token),
                DefaultTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            ReadOnlyMemory<byte> payload = CipFrame.ParseCipResponse(response, CipService.ReadTag, out CipDataType detectedType);
            return (detectedType, payload.ToArray());
        }

        /// <summary>
        /// Writes raw bytes to the specified tag name with specified CIP data type.
        /// </summary>
        public async Task WriteTagRawAsync(
            string tagName,
            CipDataType dataType,
            ReadOnlyMemory<byte> data,
            ushort elementsCount = 1,
            CancellationToken cancellationToken = default)
        {
            EnsureSessionActive();

            byte[] request = CipFrame.BuildWriteTagRequest(SessionHandle, tagName, dataType, data.Span, elementsCount, Slot, UseRouting);

            byte[] response = await _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(request, 0, request.Length, token),
                DefaultTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            CipFrame.ParseCipResponse(response, CipService.WriteTag, out _);
        }

        #endregion

        #region Typed High-Level Operations

        public async Task<bool> ReadBoolAsync(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 1) throw new InvalidDataException($"Expected at least 1 byte for BOOL tag '{tagName}'.");
            return data[0] != 0;
        }

        public async Task WriteBoolAsync(string tagName, bool value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[] { (byte)(value ? 0xFF : 0x00) };
            await WriteTagRawAsync(tagName, CipDataType.Bool, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        public async Task<short> ReadInt16Async(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 2) throw new InvalidDataException($"Expected at least 2 bytes for INT tag '{tagName}'.");
            return BinaryPrimitives.ReadInt16LittleEndian(data);
        }

        public async Task WriteInt16Async(string tagName, short value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(raw, value);
            await WriteTagRawAsync(tagName, CipDataType.Int16, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        public async Task<ushort> ReadUInt16Async(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 2) throw new InvalidDataException($"Expected at least 2 bytes for UINT tag '{tagName}'.");
            return BinaryPrimitives.ReadUInt16LittleEndian(data);
        }

        public async Task WriteUInt16Async(string tagName, ushort value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(raw, value);
            await WriteTagRawAsync(tagName, CipDataType.UInt16, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        public async Task<int> ReadInt32Async(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 4) throw new InvalidDataException($"Expected at least 4 bytes for DINT tag '{tagName}'.");
            return BinaryPrimitives.ReadInt32LittleEndian(data);
        }

        public async Task WriteInt32Async(string tagName, int value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(raw, value);
            await WriteTagRawAsync(tagName, CipDataType.Int32, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        public async Task<uint> ReadUInt32Async(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 4) throw new InvalidDataException($"Expected at least 4 bytes for UDINT tag '{tagName}'.");
            return BinaryPrimitives.ReadUInt32LittleEndian(data);
        }

        public async Task WriteUInt32Async(string tagName, uint value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(raw, value);
            await WriteTagRawAsync(tagName, CipDataType.UInt32, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        public async Task<long> ReadInt64Async(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 8) throw new InvalidDataException($"Expected at least 8 bytes for LINT tag '{tagName}'.");
            return BinaryPrimitives.ReadInt64LittleEndian(data);
        }

        public async Task WriteInt64Async(string tagName, long value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[8];
            BinaryPrimitives.WriteInt64LittleEndian(raw, value);
            await WriteTagRawAsync(tagName, CipDataType.Int64, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        public async Task<float> ReadFloatAsync(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 4) throw new InvalidDataException($"Expected at least 4 bytes for REAL tag '{tagName}'.");
#if NET8_0_OR_GREATER
            return BinaryPrimitives.ReadSingleLittleEndian(data);
#else
            int intVal = BinaryPrimitives.ReadInt32LittleEndian(data);
            return BitConverter.ToSingle(BitConverter.GetBytes(intVal), 0);
#endif
        }

        public async Task WriteFloatAsync(string tagName, float value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[4];
#if NET8_0_OR_GREATER
            BinaryPrimitives.WriteSingleLittleEndian(raw, value);
#else
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, raw, 0, 4);
#endif
            await WriteTagRawAsync(tagName, CipDataType.Single, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        public async Task<double> ReadDoubleAsync(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 8) throw new InvalidDataException($"Expected at least 8 bytes for LREAL tag '{tagName}'.");
#if NET8_0_OR_GREATER
            return BinaryPrimitives.ReadDoubleLittleEndian(data);
#else
            long longVal = BinaryPrimitives.ReadInt64LittleEndian(data);
            return BitConverter.ToDouble(BitConverter.GetBytes(longVal), 0);
#endif
        }

        public async Task WriteDoubleAsync(string tagName, double value, CancellationToken cancellationToken = default)
        {
            byte[] raw = new byte[8];
#if NET8_0_OR_GREATER
            BinaryPrimitives.WriteDoubleLittleEndian(raw, value);
#else
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
            Buffer.BlockCopy(bytes, 0, raw, 0, 8);
#endif
            await WriteTagRawAsync(tagName, CipDataType.Double, raw, 1, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a standard Allen-Bradley STRING structure (4-byte DINT length prefix followed by ASCII characters).
        /// </summary>
        public async Task<string> ReadStringAsync(string tagName, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagName, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 4) return string.Empty;

            int strLen = BinaryPrimitives.ReadInt32LittleEndian(data);
            if (strLen <= 0) return string.Empty;

            int actualChars = Math.Min(strLen, data.Length - 4);
            return Encoding.ASCII.GetString(data, 4, actualChars);
        }

        #endregion

        #region IIndustrialPlcClient Implementation

        public string ClientId { get; set; } = "AllenBradleyCip";

        public PlcVendor Vendor => PlcVendor.AllenBradleyCip;

        public PlcConnectionState State =>
            _transport.IsConnected ? (_handshakeCompleted ? PlcConnectionState.Connected : PlcConnectionState.Handshaking) : PlcConnectionState.Disconnected;

        /// <summary>
        /// Gets whether the client is currently connected and authenticated with the PLC.
        /// </summary>
        public bool IsConnected => State == PlcConnectionState.Connected;

        public PlcDriverCapabilities Capabilities => PlcDriverCapabilities.AllenBradleyCip;

        public event Action<IIndustrialPlcClient, PlcConnectionState>? StateChanged;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await ConnectHandshakeAsync(cancellationToken).ConfigureAwait(false);
            StateChanged?.Invoke(this, State);
        }

        public async Task DisconnectAsync()
        {
            await UnregisterSessionAsync().ConfigureAwait(false);
            await _transport.DisconnectAsync().ConfigureAwait(false);
            StateChanged?.Invoke(this, State);
        }

        public async Task<T> ReadAsync<T>(string tagAddress, CancellationToken cancellationToken = default) where T : unmanaged
        {
            Type t = typeof(T);

            if (t == typeof(bool))
            {
                bool b = await ReadBoolAsync(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)b;
            }
            if (t == typeof(short))
            {
                short v = await ReadInt16Async(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)v;
            }
            if (t == typeof(ushort))
            {
                ushort v = await ReadUInt16Async(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)v;
            }
            if (t == typeof(int))
            {
                int v = await ReadInt32Async(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)v;
            }
            if (t == typeof(uint))
            {
                uint v = await ReadUInt32Async(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)v;
            }
            if (t == typeof(long))
            {
                long v = await ReadInt64Async(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)v;
            }
            if (t == typeof(float))
            {
                float v = await ReadFloatAsync(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)v;
            }
            if (t == typeof(double))
            {
                double v = await ReadDoubleAsync(tagAddress, cancellationToken).ConfigureAwait(false);
                return (T)(object)v;
            }
            if (t == typeof(byte))
            {
                var (_, data) = await ReadTagRawAsync(tagAddress, 1, cancellationToken).ConfigureAwait(false);
                return (T)(object)(data.Length > 0 ? data[0] : (byte)0);
            }
            if (t == typeof(sbyte))
            {
                var (_, data) = await ReadTagRawAsync(tagAddress, 1, cancellationToken).ConfigureAwait(false);
                return (T)(object)(data.Length > 0 ? (sbyte)data[0] : (sbyte)0);
            }

            throw new NotSupportedException($"Type '{typeof(T).FullName}' is not supported for single-tag CIP read.");
        }

        public async Task<bool> WriteAsync<T>(string tagAddress, T value, CancellationToken cancellationToken = default) where T : unmanaged
        {
            if (value is bool b)
            {
                await WriteBoolAsync(tagAddress, b, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is short s)
            {
                await WriteInt16Async(tagAddress, s, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is ushort us)
            {
                await WriteUInt16Async(tagAddress, us, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is int i)
            {
                await WriteInt32Async(tagAddress, i, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is uint ui)
            {
                await WriteUInt32Async(tagAddress, ui, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is long l)
            {
                await WriteInt64Async(tagAddress, l, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is float f)
            {
                await WriteFloatAsync(tagAddress, f, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is double d)
            {
                await WriteDoubleAsync(tagAddress, d, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is byte bt)
            {
                await WriteTagRawAsync(tagAddress, CipDataType.Byte, new byte[] { bt }, 1, cancellationToken).ConfigureAwait(false);
                return true;
            }
            if (value is sbyte sbt)
            {
                await WriteTagRawAsync(tagAddress, CipDataType.SByte, new byte[] { (byte)sbt }, 1, cancellationToken).ConfigureAwait(false);
                return true;
            }

            throw new NotSupportedException($"Type '{typeof(T).FullName}' is not supported for single-tag CIP write.");
        }

        public async Task<string> ReadStringAsync(string tagAddress, int length, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            var (_, data) = await ReadTagRawAsync(tagAddress, 1, cancellationToken).ConfigureAwait(false);
            if (data.Length < 4) return string.Empty;

            int strLen = BinaryPrimitives.ReadInt32LittleEndian(data);
            if (strLen <= 0) return string.Empty;

            encoding ??= Encoding.ASCII;
            int actualChars = Math.Min(Math.Min(strLen, length), data.Length - 4);
            return encoding.GetString(data, 4, actualChars);
        }

        public async Task<bool> WriteStringAsync(string tagAddress, string value, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            value ??= string.Empty;
            encoding ??= Encoding.ASCII;
            byte[] textBytes = encoding.GetBytes(value);
            int strLen = textBytes.Length;

            byte[] payload = new byte[4 + Math.Max(strLen, 82)];
            BinaryPrimitives.WriteInt32LittleEndian(payload, strLen);
            Buffer.BlockCopy(textBytes, 0, payload, 4, Math.Min(strLen, 82));

            await WriteTagRawAsync(tagAddress, CipDataType.Struct, payload, 1, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<byte[]> ReadRawBytesAsync(string tagAddress, int length, CancellationToken cancellationToken = default)
        {
            ushort elementsCount = (ushort)Math.Max(1, length);
            var (_, data) = await ReadTagRawAsync(tagAddress, elementsCount, cancellationToken).ConfigureAwait(false);
            if (data.Length > length)
            {
                byte[] trimmed = new byte[length];
                Buffer.BlockCopy(data, 0, trimmed, 0, length);
                return trimmed;
            }
            return data;
        }

        public async Task<bool> WriteRawBytesAsync(string tagAddress, byte[] data, CancellationToken cancellationToken = default)
        {
            ushort elementsCount = (ushort)Math.Max(1, data.Length);
            await WriteTagRawAsync(tagAddress, CipDataType.Byte, data, elementsCount, cancellationToken).ConfigureAwait(false);
            return true;
        }

        private void EnsureSessionActive()
        {
            if (!_handshakeCompleted || SessionHandle == 0)
            {
                throw new InvalidOperationException("EtherNet/IP session is not registered. Call ConnectHandshakeAsync first.");
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _transport.DataReceived -= OnDataReceived;
            _transport.OnDisconnected -= OnDisconnected;
            _channel.Dispose();
        }

        #endregion
    }
}
