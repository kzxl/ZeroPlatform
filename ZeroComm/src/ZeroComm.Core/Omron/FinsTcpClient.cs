using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Abstractions;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Channels;
using ZeroComm.Core.Transport;

namespace ZeroComm.Core.Omron
{
    /// <summary>
    /// High-performance asynchronous Omron FINS TCP client for CP, CJ, CS, and NX series PLCs.
    /// Manages 16-byte FINS TCP encapsulation, client/server node address allocation handshake,
    /// and unified tag-based memory area access with zero runtime third-party dependencies.
    /// </summary>
    public class FinsTcpClient : IDisposable, IProtocolSession, IIndustrialPlcClient
    {
        private readonly ITransport _transport;
        private readonly CircularRingBuffer _ringBuffer = new CircularRingBuffer(65536);
        private readonly object _ringLock = new object();
        private readonly HalfDuplexChannel<byte[]> _channel = new HalfDuplexChannel<byte[]>();

        private byte _serviceId = 1;
        private byte _configuredClientNode;
        private bool _handshakeCompleted;
        private bool _isDisposed;

        /// <summary>
        /// Gets the underlying transport instance.
        /// </summary>
        public ITransport Transport => _transport;

        /// <summary>
        /// Gets the assigned client node address (DA1 for PLC, SA1 for client).
        /// </summary>
        public byte ClientNode { get; private set; } = 0xEF;

        /// <summary>
        /// Gets the target PLC server node address (DA1 for requests).
        /// </summary>
        public byte ServerNode { get; private set; } = 0x01;

        /// <summary>
        /// Gets or sets the default timeout in milliseconds (default: 3000ms).
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// Initializes a new instance of the <see cref="FinsTcpClient"/> class.
        /// </summary>
        public FinsTcpClient(ITransport transport)
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

                while (StreamingFrameParser.TryExtractFinsTcpFrame(_ringBuffer, out byte[] frame))
                {
                    _channel.TrySetResponse(frame);
                }
            }
        }

        private void OnDisconnected()
        {
            _channel.FaultPending(new IOException("Transport disconnected while awaiting Omron FINS TCP response."));
        }

        #region Handshake & Protocol Session

        /// <summary>
        /// Executes FINS/TCP Client/Server Node Address Allocation Handshake.
        /// </summary>
        /// <param name="clientNode">Requested client node (0 = auto-assignment by PLC).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task ConnectHandshakeAsync(byte clientNode = 0, CancellationToken cancellationToken = default)
        {
            _configuredClientNode = clientNode;
            byte[] request = FinsFrame.BuildTcpNodeAllocationRequest(clientNode);

            byte[] response = await _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(request, 0, request.Length, token),
                DefaultTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (!FinsFrame.ParseTcpNodeAllocationResponse(response, out byte assignedClient, out byte assignedServer))
            {
                throw new FinsException("FINS/TCP Node Address Handshake was rejected by the PLC.");
            }

            ClientNode = assignedClient;
            ServerNode = assignedServer;
            _handshakeCompleted = true;
        }

        async Task IProtocolSession.OnSessionConnectedAsync(CancellationToken cancellationToken)
        {
            if (_handshakeCompleted)
            {
                await ConnectHandshakeAsync(_configuredClientNode, cancellationToken).ConfigureAwait(false);
            }
        }

        Task IProtocolSession.OnSessionDisconnectedAsync()
        {
            return Task.CompletedTask;
        }

        #endregion

        #region Core Read / Write Operations

        /// <summary>
        /// Reads word registers from the specified Omron memory area.
        /// </summary>
        public async Task<ushort[]> ReadWordsAsync(
            FinsMemoryArea area,
            ushort address,
            ushort wordCount,
            CancellationToken cancellationToken = default)
        {
            byte sid = GetNextServiceId();
            byte[] innerFins = FinsFrame.BuildMemoryAreaReadRequest(
                area, address, wordCount, bitOffset: 0, destNode: ServerNode, srcNode: ClientNode, serviceId: sid);

            byte[] dataBytes = await SendFinsFrameAsync(innerFins, cancellationToken).ConfigureAwait(false);
            return FinsFrame.ToWords(dataBytes);
        }

        /// <summary>
        /// Writes word values to the specified Omron memory area.
        /// </summary>
        public async Task WriteWordsAsync(
            FinsMemoryArea area,
            ushort address,
            ushort[] values,
            CancellationToken cancellationToken = default)
        {
            byte sid = GetNextServiceId();
            byte[] innerFins = FinsFrame.BuildMemoryAreaWriteRequest(
                area, address, values, destNode: ServerNode, srcNode: ClientNode, serviceId: sid);

            await SendFinsFrameAsync(innerFins, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Reads a single bit from the specified Omron memory area.
        /// </summary>
        public async Task<bool> ReadBitAsync(
            FinsMemoryArea area,
            ushort address,
            byte bitOffset,
            CancellationToken cancellationToken = default)
        {
            byte sid = GetNextServiceId();
            byte[] innerFins = FinsFrame.BuildMemoryAreaReadRequest(
                area, address, numberOfItems: 1, bitOffset: bitOffset, destNode: ServerNode, srcNode: ClientNode, serviceId: sid);

            byte[] dataBytes = await SendFinsFrameAsync(innerFins, cancellationToken).ConfigureAwait(false);
            return dataBytes.Length > 0 && dataBytes[0] != 0;
        }

        /// <summary>
        /// Writes a single bit to the specified Omron memory area.
        /// </summary>
        public async Task WriteBitAsync(
            FinsMemoryArea area,
            ushort address,
            byte bitOffset,
            bool value,
            CancellationToken cancellationToken = default)
        {
            byte sid = GetNextServiceId();
            byte[] innerFins = FinsFrame.BuildMemoryAreaWriteBitRequest(
                area, address, bitOffset, value, destNode: ServerNode, srcNode: ClientNode, serviceId: sid);

            await SendFinsFrameAsync(innerFins, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Internal Dispatch

        private async Task<byte[]> SendFinsFrameAsync(byte[] innerFins, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            byte[] tcpPacket = FinsFrame.WrapInTcpHeader(innerFins);
            byte[] responsePacket = await _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(tcpPacket, 0, tcpPacket.Length, token),
                DefaultTimeoutMs,
                cancellationToken).ConfigureAwait(false);

            if (!FinsFrame.UnwrapTcpHeader(responsePacket, out byte[] responsePayload))
            {
                throw new FinsException("Invalid FINS/TCP response header received.");
            }

            if (!FinsFrame.ParseResponse(responsePayload, 0, responsePayload.Length, out _, out ushort endCode, out byte[] data))
            {
                throw new FinsException(endCode);
            }

            return data;
        }

        private byte GetNextServiceId()
        {
            return unchecked(++_serviceId);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(FinsTcpClient));
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

        public string ClientId { get; set; } = "OmronFins";
        public PlcVendor Vendor => PlcVendor.OmronFins;

        public PlcConnectionState State =>
            _transport.IsConnected ? (_handshakeCompleted ? PlcConnectionState.Connected : PlcConnectionState.Handshaking) : PlcConnectionState.Disconnected;

        public PlcDriverCapabilities Capabilities { get; } = new PlcDriverCapabilities
        {
            Vendor = PlcVendor.OmronFins,
            MaxPduBytes = 2000,
            MaxBatchReadItems = 180,
            MaxContiguousReadBytes = 1990,
            SupportsBitAddressing = true,
            SupportsRandomBatchRead = true,
            SupportsTagNames = false
        };

        public event Action<IIndustrialPlcClient, PlcConnectionState>? StateChanged;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            await _transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            await ConnectHandshakeAsync(_configuredClientNode, cancellationToken).ConfigureAwait(false);
            StateChanged?.Invoke(this, State);
        }

        public async Task DisconnectAsync()
        {
            await _transport.DisconnectAsync().ConfigureAwait(false);
            StateChanged?.Invoke(this, State);
        }

        public async Task<T> ReadAsync<T>(string tagAddress, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.OmronFins);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Omron FINS address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            var area = (FinsMemoryArea)res.AreaCode;

            if (typeof(T) == typeof(bool))
            {
                bool bitVal = await ReadBitAsync(area, (ushort)res.Offset, res.BitOffset, cancellationToken).ConfigureAwait(false);
                return (T)(object)bitVal;
            }

            if (typeof(T) == typeof(ushort))
            {
                ushort[] words = await ReadWordsAsync(area, (ushort)res.Offset, 1, cancellationToken).ConfigureAwait(false);
                return (T)(object)words[0];
            }

            if (typeof(T) == typeof(short))
            {
                ushort[] words = await ReadWordsAsync(area, (ushort)res.Offset, 1, cancellationToken).ConfigureAwait(false);
                return (T)(object)(short)words[0];
            }

            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint) || typeof(T) == typeof(float))
            {
                ushort[] words = await ReadWordsAsync(area, (ushort)res.Offset, 2, cancellationToken).ConfigureAwait(false);
                byte[] bytes = new byte[4];
                bytes[0] = (byte)(words[0] >> 8);
                bytes[1] = (byte)(words[0] & 0xFF);
                bytes[2] = (byte)(words[1] >> 8);
                bytes[3] = (byte)(words[1] & 0xFF);

                if (typeof(T) == typeof(int))
                {
                    int val = (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                    return (T)(object)val;
                }
                if (typeof(T) == typeof(uint))
                {
                    uint val = ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
                    return (T)(object)val;
                }

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                float fVal = BitConverter.ToSingle(bytes, 0);
                return (T)(object)fVal;
            }

            throw new NotSupportedException($"Type {typeof(T).Name} is not supported for Omron tag reading.");
        }

        public async Task<bool> WriteAsync<T>(string tagAddress, T value, CancellationToken cancellationToken = default) where T : unmanaged
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.OmronFins);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Omron FINS address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            var area = (FinsMemoryArea)res.AreaCode;

            if (typeof(T) == typeof(bool))
            {
                await WriteBitAsync(area, (ushort)res.Offset, res.BitOffset, (bool)(object)value, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(ushort))
            {
                await WriteWordsAsync(area, (ushort)res.Offset, new ushort[] { (ushort)(object)value }, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(short))
            {
                await WriteWordsAsync(area, (ushort)res.Offset, new ushort[] { (ushort)(short)(object)value }, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(int) || typeof(T) == typeof(uint))
            {
                uint uval = typeof(T) == typeof(int) ? (uint)(int)(object)value : (uint)(object)value;
                ushort[] words = new ushort[]
                {
                    (ushort)(uval >> 16),
                    (ushort)(uval & 0xFFFF)
                };
                await WriteWordsAsync(area, (ushort)res.Offset, words, cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (typeof(T) == typeof(float))
            {
                float fval = (float)(object)value;
                byte[] bytes = BitConverter.GetBytes(fval);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }
                ushort[] words = new ushort[]
                {
                    (ushort)((bytes[0] << 8) | bytes[1]),
                    (ushort)((bytes[2] << 8) | bytes[3])
                };
                await WriteWordsAsync(area, (ushort)res.Offset, words, cancellationToken).ConfigureAwait(false);
                return true;
            }

            throw new NotSupportedException($"Type {typeof(T).Name} is not supported for Omron tag writing.");
        }

        public async Task<string> ReadStringAsync(string tagAddress, int length, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.OmronFins);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Omron FINS address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            ushort wordsToRead = (ushort)((length + 1) / 2);
            ushort[] words = await ReadWordsAsync((FinsMemoryArea)res.AreaCode, (ushort)res.Offset, wordsToRead, cancellationToken).ConfigureAwait(false);

            byte[] bytes = new byte[words.Length * 2];
            for (int i = 0; i < words.Length; i++)
            {
                bytes[i * 2] = (byte)(words[i] >> 8);
                bytes[i * 2 + 1] = (byte)(words[i] & 0xFF);
            }

            encoding ??= Encoding.ASCII;
            int actualLength = Math.Min(length, bytes.Length);
            return encoding.GetString(bytes, 0, actualLength).TrimEnd('\0');
        }

        public async Task<bool> WriteStringAsync(string tagAddress, string value, Encoding? encoding = null, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.OmronFins);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Omron FINS address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            encoding ??= Encoding.ASCII;
            byte[] textBytes = encoding.GetBytes(value ?? string.Empty);
            int wordCount = (textBytes.Length + 1) / 2;
            ushort[] words = new ushort[wordCount];

            for (int i = 0; i < wordCount; i++)
            {
                byte b0 = (i * 2 < textBytes.Length) ? textBytes[i * 2] : (byte)0;
                byte b1 = (i * 2 + 1 < textBytes.Length) ? textBytes[i * 2 + 1] : (byte)0;
                words[i] = (ushort)((b0 << 8) | b1);
            }

            await WriteWordsAsync((FinsMemoryArea)res.AreaCode, (ushort)res.Offset, words, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<byte[]> ReadRawBytesAsync(string tagAddress, int length, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.OmronFins);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Omron FINS address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            ushort wordsToRead = (ushort)((length + 1) / 2);
            ushort[] words = await ReadWordsAsync((FinsMemoryArea)res.AreaCode, (ushort)res.Offset, wordsToRead, cancellationToken).ConfigureAwait(false);

            byte[] result = new byte[length];
            for (int i = 0; i < words.Length && i * 2 < length; i++)
            {
                result[i * 2] = (byte)(words[i] >> 8);
                if (i * 2 + 1 < length)
                {
                    result[i * 2 + 1] = (byte)(words[i] & 0xFF);
                }
            }

            return result;
        }

        public async Task<bool> WriteRawBytesAsync(string tagAddress, byte[] data, CancellationToken cancellationToken = default)
        {
            var res = PlcAddressParser.Parse(tagAddress, PlcVendor.OmronFins);
            if (!res.IsValid)
                throw new ArgumentException($"Invalid Omron FINS address '{tagAddress}': {res.ErrorMessage}", nameof(tagAddress));

            int wordCount = (data.Length + 1) / 2;
            ushort[] words = new ushort[wordCount];

            for (int i = 0; i < wordCount; i++)
            {
                byte b0 = (i * 2 < data.Length) ? data[i * 2] : (byte)0;
                byte b1 = (i * 2 + 1 < data.Length) ? data[i * 2 + 1] : (byte)0;
                words[i] = (ushort)((b0 << 8) | b1);
            }

            await WriteWordsAsync((FinsMemoryArea)res.AreaCode, (ushort)res.Offset, words, cancellationToken).ConfigureAwait(false);
            return true;
        }

        #endregion
    }
}
