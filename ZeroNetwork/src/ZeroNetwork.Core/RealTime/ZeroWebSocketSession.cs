using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Represents an active server-side WebSocket session adhering to RFC 6455.
    /// Handles frame decoding, unmasking, and thread-safe streaming frame transmission.
    /// </summary>
    public sealed class ZeroWebSocketSession : IDisposable
    {
        private const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _stream;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly byte[] _maskBuffer = new byte[4];
        private int _disposed;
        private bool _isClosed;

        /// <summary>
        /// Gets the unique session identifier.
        /// </summary>
        public string SessionId { get; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Gets the remote client endpoint.
        /// </summary>
        public EndPoint? RemoteEndPoint => _tcpClient.Client.RemoteEndPoint;

        /// <summary>
        /// Gets whether the WebSocket session is active and open.
        /// </summary>
        public bool IsOpen => Volatile.Read(ref _disposed) == 0 && !_isClosed && _tcpClient.Connected;

        /// <summary>
        /// Event fired when a UTF-8 text message is received from the client.
        /// </summary>
        public event Action<ZeroWebSocketSession, string>? TextReceived;

        /// <summary>
        /// Event fired when a binary payload is received from the client.
        /// </summary>
        public event Action<ZeroWebSocketSession, byte[]>? BinaryReceived;

        /// <summary>
        /// Event fired when the session is closed or disconnected.
        /// </summary>
        public event Action<ZeroWebSocketSession, Exception?>? Closed;

        internal ZeroWebSocketSession(TcpClient tcpClient)
        {
            _tcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            _stream = tcpClient.GetStream();
        }

        internal async Task<bool> PerformServerHandshakeAsync(CancellationToken cancellationToken)
        {
            try
            {
                var reader = new StreamReader(_stream, Encoding.UTF8, false, 2048, leaveOpen: true);
                string? requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(requestLine)) return false;

                string? secKey = null;
                string? headerLine;
                while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                    int colonIdx = headerLine.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        string name = headerLine.Substring(0, colonIdx).Trim();
                        string value = headerLine.Substring(colonIdx + 1).Trim();
                        if (name.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
                        {
                            secKey = value;
                        }
                    }
                }

                if (string.IsNullOrEmpty(secKey))
                    return false;

                // Compute Sec-WebSocket-Accept with zero-allocation ZeroSha1
                string acceptKey = ZeroSha1.ComputeWebSocketAccept(secKey!);

                string response = "HTTP/1.1 101 Switching Protocols\r\n" +
                                  "Upgrade: websocket\r\n" +
                                  "Connection: Upgrade\r\n" +
                                  $"Sec-WebSocket-Accept: {acceptKey}\r\n\r\n";

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                await _stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal async Task RunReceiveLoopAsync()
        {
            var token = _cts.Token;
            Exception? terminationException = null;

            try
            {
                byte[] headerBuffer = new byte[14]; // Max RFC 6455 frame header size (2 + 8 len + 4 mask)
                while (!token.IsCancellationRequested && IsOpen)
                {
                    // Read first 2 bytes (FIN/Opcode and Mask/Length)
                    if (!await ReadExactAsync(_stream, headerBuffer, 0, 2, token).ConfigureAwait(false))
                        break;

                    byte b0 = headerBuffer[0];
                    byte b1 = headerBuffer[1];

                    bool fin = (b0 & 0x80) != 0;
                    int opcode = b0 & 0x0F;
                    bool masked = (b1 & 0x80) != 0;
                    long payloadLength = b1 & 0x7F;

                    if (payloadLength == 126)
                    {
                        if (!await ReadExactAsync(_stream, headerBuffer, 2, 2, token).ConfigureAwait(false))
                            break;
                        payloadLength = (headerBuffer[2] << 8) | headerBuffer[3];
                    }
                    else if (payloadLength == 127)
                    {
                        if (!await ReadExactAsync(_stream, headerBuffer, 2, 8, token).ConfigureAwait(false))
                            break;
                        payloadLength = 0;
                        for (int i = 0; i < 8; i++)
                        {
                            payloadLength = (payloadLength << 8) | headerBuffer[2 + i];
                        }
                    }

                    // Client frames MUST be masked
                    if (masked)
                    {
                        if (!await ReadExactAsync(_stream, _maskBuffer, 0, 4, token).ConfigureAwait(false))
                            break;
                    }

                    if (payloadLength > 64 * 1024 * 1024) // 64MB protection threshold
                        throw new InvalidDataException("WebSocket frame payload exceeds 64MB limit.");

                    byte[] payload = new byte[payloadLength];
                    if (payloadLength > 0)
                    {
                        if (!await ReadExactAsync(_stream, payload, 0, (int)payloadLength, token).ConfigureAwait(false))
                            break;

                        if (masked)
                        {
                            ZeroFastMask.ApplyMask(payload, _maskBuffer);
                        }
                    }

                    // Process Opcode
                    switch (opcode)
                    {
                        case 0x1: // Text frame
                            string text = Encoding.UTF8.GetString(payload);
                            TextReceived?.Invoke(this, text);
                            break;

                        case 0x2: // Binary frame
                            BinaryReceived?.Invoke(this, payload);
                            break;

                        case 0x8: // Close frame
                            _isClosed = true;
                            await SendFrameInternalAsync(0x8, payload, CancellationToken.None).ConfigureAwait(false);
                            return;

                        case 0x9: // Ping frame -> respond Pong
                            await SendFrameInternalAsync(0xA, payload, token).ConfigureAwait(false);
                            break;

                        case 0xA: // Pong frame
                            break;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                    terminationException = ex;
            }
            finally
            {
                _isClosed = true;
                Closed?.Invoke(this, terminationException);
                Dispose();
            }
        }

        /// <summary>
        /// Sends a text message to the client.
        /// </summary>
        public Task SendTextAsync(string message, CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            byte[] payload = Encoding.UTF8.GetBytes(message);
            return SendFrameAsync(0x1, payload, cancellationToken);
        }

        /// <summary>
        /// Sends a binary message to the client.
        /// </summary>
        public Task SendBinaryAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            return SendFrameAsync(0x2, payload.ToArray(), cancellationToken);
        }

        /// <summary>
        /// Gracefully closes the WebSocket session.
        /// </summary>
        public async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (_isClosed) return;
            _isClosed = true;
            try
            {
                await SendFrameAsync(0x8, Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                Dispose();
            }
        }

        private async Task SendFrameAsync(int opcode, byte[] payload, CancellationToken cancellationToken)
        {
            if (!IsOpen && opcode != 0x8)
                throw new InvalidOperationException("WebSocket session is not open.");

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await SendFrameInternalAsync(opcode, payload, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task SendFrameInternalAsync(int opcode, byte[] payload, CancellationToken cancellationToken)
        {
            int payloadLength = payload.Length;
            int headerSize = 2;
            if (payloadLength > 125 && payloadLength <= 65535)
                headerSize += 2;
            else if (payloadLength > 65535)
                headerSize += 8;

            byte[] frame = new byte[headerSize + payloadLength];
            frame[0] = (byte)(0x80 | (opcode & 0x0F)); // FIN = 1

            if (payloadLength <= 125)
            {
                frame[1] = (byte)payloadLength; // Mask = 0 (Server to Client must NOT mask)
            }
            else if (payloadLength <= 65535)
            {
                frame[1] = 126;
                frame[2] = (byte)(payloadLength >> 8);
                frame[3] = (byte)(payloadLength & 0xFF);
            }
            else
            {
                frame[1] = 127;
                long len = payloadLength;
                for (int i = 7; i >= 0; i--)
                {
                    frame[2 + i] = (byte)(len & 0xFF);
                    len >>= 8;
                }
            }

            if (payloadLength > 0)
            {
                Buffer.BlockCopy(payload, 0, frame, headerSize, payloadLength);
            }

            await _stream.WriteAsync(frame, 0, frame.Length, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<bool> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int readTotal = 0;
            while (readTotal < count)
            {
                int read = await stream.ReadAsync(buffer, offset + readTotal, count - readTotal, cancellationToken).ConfigureAwait(false);
                if (read <= 0) return false;
                readTotal += read;
            }
            return true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _isClosed = true;
                _cts.Cancel();
                _stream.Dispose();
                _tcpClient.Close();
                _sendLock.Dispose();
                _cts.Dispose();
            }
        }
    }
}
