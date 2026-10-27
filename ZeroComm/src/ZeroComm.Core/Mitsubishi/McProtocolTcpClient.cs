using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Transport;

namespace ZeroComm.Core.Mitsubishi
{
    /// <summary>
    /// High-performance asynchronous Mitsubishi MELSEC MC Protocol 3E Binary client.
    /// Manages sequential frame dispatch, zero-copy packet extraction, and device read/write operations.
    /// Supports Q/L/iQ-R and FX5U series PLCs.
    /// </summary>
    public class McProtocolTcpClient : IDisposable
    {
        private readonly ITransport _transport;
        private readonly CircularRingBuffer _ringBuffer = new CircularRingBuffer(65536);
        private readonly object _ringLock = new object();
        private readonly Channels.HalfDuplexChannel<byte[]> _channel = new Channels.HalfDuplexChannel<byte[]>();
        private bool _isDisposed;

        /// <summary>
        /// Gets the underlying transport instance.
        /// </summary>
        public ITransport Transport => _transport;

        /// <summary>
        /// Gets or sets the default timeout in milliseconds (default: 3000ms).
        /// </summary>
        public int DefaultTimeoutMs { get; set; } = 3000;

        /// <summary>
        /// Initializes a new instance of the <see cref="McProtocolTcpClient"/> class.
        /// </summary>
        public McProtocolTcpClient(ITransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.DataReceived += OnDataReceived;
            _transport.OnDisconnected += OnDisconnected;
        }

        private void OnDataReceived(byte[] buffer, int offset, int count)
        {
            lock (_ringLock)
            {
                _ringBuffer.Write(buffer, offset, count);

                while (StreamingFrameParser.TryExtractMcProtocolFrame(_ringBuffer, out byte[] frame))
                {
                    _channel.TrySetResponse(frame);
                }
            }
        }

        private void OnDisconnected()
        {
            _channel.FaultPending(new IOException("Transport disconnected while awaiting MELSEC response."));
        }

        private Task<byte[]> SendAndReceiveAsync(byte[] request, int timeoutMs, CancellationToken ct)
        {
            ThrowIfDisposed();
            return _channel.ExecuteRequestAsync(
                token => _transport.SendAsync(request, 0, request.Length, token),
                timeoutMs,
                ct);
        }

        /// <summary>
        /// Reads word devices (e.g. D100..D110) asynchronously.
        /// </summary>
        public async Task<ushort[]> ReadWordsAsync(
            McDeviceCode deviceCode,
            int headDeviceNumber,
            ushort points,
            int? timeoutMs = null,
            CancellationToken ct = default)
        {
            byte[] req = McProtocolFrame.BuildBatchReadWordsRequest(deviceCode, headDeviceNumber, points);
            byte[] resp = await SendAndReceiveAsync(req, timeoutMs ?? DefaultTimeoutMs, ct).ConfigureAwait(false);

            if (!McProtocolFrame.ParseResponse(resp, 0, resp.Length, out ushort endCode, out byte[] data))
            {
                throw new McProtocolException(endCode);
            }

            return McProtocolFrame.ToWords(data);
        }

        /// <summary>
        /// Writes word devices (e.g. D100..D104) asynchronously.
        /// </summary>
        public async Task WriteWordsAsync(
            McDeviceCode deviceCode,
            int headDeviceNumber,
            ushort[] values,
            int? timeoutMs = null,
            CancellationToken ct = default)
        {
            byte[] req = McProtocolFrame.BuildBatchWriteWordsRequest(deviceCode, headDeviceNumber, values);
            byte[] resp = await SendAndReceiveAsync(req, timeoutMs ?? DefaultTimeoutMs, ct).ConfigureAwait(false);

            if (!McProtocolFrame.ParseResponse(resp, 0, resp.Length, out ushort endCode, out _))
            {
                throw new McProtocolException(endCode);
            }
        }

        /// <summary>
        /// Reads bit devices (e.g. M100..M115) asynchronously.
        /// </summary>
        public async Task<bool[]> ReadBitsAsync(
            McDeviceCode deviceCode,
            int headDeviceNumber,
            ushort points,
            int? timeoutMs = null,
            CancellationToken ct = default)
        {
            byte[] req = McProtocolFrame.BuildBatchReadBitsRequest(deviceCode, headDeviceNumber, points);
            byte[] resp = await SendAndReceiveAsync(req, timeoutMs ?? DefaultTimeoutMs, ct).ConfigureAwait(false);

            if (!McProtocolFrame.ParseResponse(resp, 0, resp.Length, out ushort endCode, out byte[] data))
            {
                throw new McProtocolException(endCode);
            }

            bool[] bits = new bool[points];
            for (int i = 0; i < points; i++)
            {
                int byteIndex = i / 2;
                if (byteIndex < data.Length)
                {
                    int nibble = (i % 2 == 0) ? (data[byteIndex] >> 4) : (data[byteIndex] & 0x0F);
                    bits[i] = (nibble != 0);
                }
            }
            return bits;
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(McProtocolTcpClient));
        }

        /// <summary>
        /// Disposes client resources and unhooks events.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _transport.DataReceived -= OnDataReceived;
            _transport.OnDisconnected -= OnDisconnected;
            _channel.Dispose();
        }
    }
}
