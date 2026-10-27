using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Http;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// High-performance, resilient RFC 6455 WebSocket client built on BCL <see cref="ClientWebSocket"/>.
    /// Supports thread-safe sending, automatic frame reassembly, configurable keep-alive,
    /// and auto-reconnection with exponential backoff and jitter.
    /// </summary>
    public class ZeroWebSocketClient : IDisposable
    {
        private readonly ZeroWebSocketOptions _options;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        private ClientWebSocket? _webSocket;
        private CancellationTokenSource? _connectionCts;
        private Task? _receiveTask;
        private bool _explicitlyClosed;
        private bool _isDisposed;
        private int _reconnectAttempts;

        /// <summary>
        /// Event fired when the client successfully establishes a WebSocket connection.
        /// </summary>
        public event Action? Connected;

        /// <summary>
        /// Event fired when the client is disconnected.
        /// </summary>
        public event Action<Exception?>? Disconnected;

        /// <summary>
        /// Event fired when a text message is received and reassembled.
        /// </summary>
        public event Action<string>? MessageReceived;

        /// <summary>
        /// Event fired when a binary message is received and reassembled.
        /// </summary>
        public event Action<byte[]>? BinaryReceived;

        /// <summary>
        /// Event fired when an automatic reconnection attempt begins.
        /// Passes the current attempt count and delay before connecting.
        /// </summary>
        public event Action<int, TimeSpan>? Reconnecting;

        /// <summary>
        /// Event fired when an automatic reconnection successfully restores the connection.
        /// </summary>
        public event Action? Reconnected;

        /// <summary>
        /// Event fired when an internal socket or protocol error occurs.
        /// </summary>
        public event Action<Exception>? Error;

        /// <summary>
        /// Gets the current state of the underlying WebSocket.
        /// </summary>
        public WebSocketState State => _webSocket?.State ?? WebSocketState.None;

        /// <summary>
        /// Gets whether the WebSocket is currently connected and open for transmission.
        /// </summary>
        public bool IsConnected => _webSocket != null && _webSocket.State == WebSocketState.Open;

        /// <summary>
        /// Gets the configuration options for this client.
        /// </summary>
        public ZeroWebSocketOptions Options => _options;

        /// <summary>
        /// Gets or sets the JSON serializer used for typed messaging.
        /// Defaults to <see cref="ZeroApiClient.DefaultSerializer"/>.
        /// </summary>
        public IZeroJsonSerializer Serializer { get; set; } = ZeroApiClient.DefaultSerializer;

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroWebSocketClient"/> with specified options.
        /// </summary>
        public ZeroWebSocketClient(ZeroWebSocketOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroWebSocketClient"/> pointing to a server URI.
        /// </summary>
        public ZeroWebSocketClient(Uri serverUri) : this(new ZeroWebSocketOptions { ServerUri = serverUri })
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroWebSocketClient"/> pointing to a server URL string.
        /// </summary>
        public ZeroWebSocketClient(string serverUrl) : this(new Uri(serverUrl ?? throw new ArgumentNullException(nameof(serverUrl))))
        {
        }

        /// <summary>
        /// Connects to the configured WebSocket server.
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (_options.ServerUri == null)
            {
                throw new InvalidOperationException("ServerUri must be specified in ZeroWebSocketOptions.");
            }

            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsConnected) return;

                _explicitlyClosed = false;
                _reconnectAttempts = 0;
                await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);

                Connected?.Invoke();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task ConnectCoreAsync(CancellationToken cancellationToken)
        {
            CleanupSocket();

            _connectionCts = new CancellationTokenSource();
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _connectionCts.Token))
            {
                linkedCts.CancelAfter(_options.ConnectTimeout);

                var ws = new ClientWebSocket();
                ws.Options.KeepAliveInterval = _options.KeepAliveInterval;

                foreach (var header in _options.Headers)
                {
                    ws.Options.SetRequestHeader(header.Key, header.Value);
                }

                _options.ConfigureClientWebSocket?.Invoke(ws.Options);

                await ws.ConnectAsync(_options.ServerUri!, linkedCts.Token).ConfigureAwait(false);
                _webSocket = ws;
            }

            _receiveTask = Task.Run(ReceiveLoopAsync);
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[_options.BufferSize];
            var messageBuffer = new MemoryStream();
            var cts = _connectionCts;
            var token = cts?.Token ?? CancellationToken.None;

            Exception? disconnectException = null;

            try
            {
                while (!token.IsCancellationRequested && _webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult result;
                    try
                    {
                        var segment = new ArraySegment<byte>(buffer);
                        result = await _webSocket.ReceiveAsync(segment, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        disconnectException = ex;
                        Error?.Invoke(ex);
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        try
                        {
                            if (_webSocket.State == WebSocketState.CloseReceived)
                            {
                                await _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledge Close", CancellationToken.None).ConfigureAwait(false);
                            }
                        }
                        catch { /* ignore close handshake errors */ }

                        break;
                    }

                    messageBuffer.Write(buffer, 0, result.Count);

                    if (result.EndOfMessage)
                    {
                        byte[] messageBytes = messageBuffer.ToArray();
                        messageBuffer.SetLength(0);

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            string text = Encoding.UTF8.GetString(messageBytes);
                            try
                            {
                                MessageReceived?.Invoke(text);
                            }
                            catch (Exception ex)
                            {
                                Error?.Invoke(ex);
                            }
                        }
                        else if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            try
                            {
                                BinaryReceived?.Invoke(messageBytes);
                            }
                            catch (Exception ex)
                            {
                                Error?.Invoke(ex);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                disconnectException = ex;
                Error?.Invoke(ex);
            }
            finally
            {
                messageBuffer.Dispose();

                Disconnected?.Invoke(disconnectException);

                if (!_explicitlyClosed && !_isDisposed && _options.AutoReconnect)
                {
                    _ = Task.Run(ReconnectLoopAsync);
                }
            }
        }

        private async Task ReconnectLoopAsync()
        {
            var random = new Random();

            while (!_explicitlyClosed && !_isDisposed && !IsConnected)
            {
                _reconnectAttempts++;

                // Calculate exponential backoff delay with random jitter (0-20%)
                double backoffSeconds = _options.InitialReconnectDelay.TotalSeconds * Math.Pow(_options.ReconnectMultiplier, Math.Min(_reconnectAttempts - 1, 6));
                double maxSeconds = _options.MaxReconnectDelay.TotalSeconds;
                double delaySeconds = Math.Min(backoffSeconds, maxSeconds);
                double jitter = delaySeconds * (random.NextDouble() * 0.2);
                TimeSpan delay = TimeSpan.FromSeconds(delaySeconds + jitter);

                try
                {
                    Reconnecting?.Invoke(_reconnectAttempts, delay);
                    await Task.Delay(delay).ConfigureAwait(false);

                    if (_explicitlyClosed || _isDisposed) break;

                    await _connectionLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (_explicitlyClosed || _isDisposed || IsConnected) break;

                        await ConnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
                        _reconnectAttempts = 0;
                        Reconnected?.Invoke();
                        return;
                    }
                    finally
                    {
                        _connectionLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    Error?.Invoke(ex);
                }
            }
        }

        /// <summary>
        /// Sends a text message to the connected WebSocket server.
        /// </summary>
        public async Task SendTextAsync(string message, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (message == null) throw new ArgumentNullException(nameof(message));

            byte[] bytes = Encoding.UTF8.GetBytes(message);
            await SendRawAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a binary message to the connected WebSocket server.
        /// </summary>
        public async Task SendBinaryAsync(byte[] data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (data == null) throw new ArgumentNullException(nameof(data));

            await SendRawAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a typed object as a JSON payload to the connected WebSocket server.
        /// </summary>
        public async Task SendJsonAsync<T>(T payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            using (var ms = new MemoryStream())
            {
                await Serializer.SerializeAsync(ms, payload, cancellationToken).ConfigureAwait(false);
                byte[] bytes = ms.ToArray();
                await SendRawAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task SendRawAsync(ArraySegment<byte> segment, WebSocketMessageType messageType, CancellationToken cancellationToken)
        {
            if (!IsConnected || _webSocket == null)
            {
                throw new InvalidOperationException("WebSocket is not connected. Call ConnectAsync first.");
            }

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!IsConnected || _webSocket == null)
                {
                    throw new InvalidOperationException("WebSocket is not connected.");
                }

                await _webSocket.SendAsync(segment, messageType, true, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Closes the WebSocket connection gracefully.
        /// </summary>
        public async Task CloseAsync(WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure, string statusDescription = "Closed by client", CancellationToken cancellationToken = default)
        {
            _explicitlyClosed = true;

            await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_webSocket != null && (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived))
                {
                    try
                    {
                        using (var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                        {
                            await _webSocket.CloseAsync(closeStatus, statusDescription, linkedCts.Token).ConfigureAwait(false);
                        }
                    }
                    catch { /* ignore errors during close handshake */ }
                }

                CleanupSocket();
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private void CleanupSocket()
        {
            try
            {
                _connectionCts?.Cancel();
                _connectionCts?.Dispose();
                _connectionCts = null;
            }
            catch { }

            try
            {
                _webSocket?.Dispose();
                _webSocket = null;
            }
            catch { }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// Releases all managed resources used by <see cref="ZeroWebSocketClient"/>.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _explicitlyClosed = true;

            CleanupSocket();
            _sendLock.Dispose();
            _connectionLock.Dispose();
        }
    }
}
