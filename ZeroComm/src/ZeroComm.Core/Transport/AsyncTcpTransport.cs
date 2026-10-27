using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroComm.Core.Transport
{
    /// <summary>
    /// High-throughput asynchronous TCP socket transport optimized for industrial fieldbus communications.
    /// Features low-latency Nagle disabling (TCP_NODELAY), background zero-copy packet pump, and auto-reconnect.
    /// </summary>
    public class AsyncTcpTransport : ITransport
    {
        private readonly string _host;
        private readonly int _port;
        private readonly int _connectTimeoutMs;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly object _stateLock = new object();

        private TcpClient? _client;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;
        private Task? _receiveTask;
        private bool _isDisposed;
        private bool _isExplicitDisconnect;

        /// <summary>
        /// Gets whether the underlying TCP connection is currently open and healthy.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                lock (_stateLock)
                {
                    return _client != null && _client.Connected && _stream != null;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to automatically attempt reconnection when the socket drops unexpectedly.
        /// </summary>
        public bool AutoReconnect { get; set; } = false;

        /// <summary>
        /// Gets or sets the delay between reconnection attempts in milliseconds (default: 3000ms).
        /// </summary>
        public int ReconnectIntervalMs { get; set; } = 3000;

        /// <summary>
        /// Event raised when the socket is connected or reconnected.
        /// </summary>
        public event Action? OnConnected;

        /// <summary>
        /// Event raised when new stream data is received from the TCP socket.
        /// </summary>
        public event Action<byte[], int, int>? DataReceived;

        /// <summary>
        /// Event raised when an unhandled transport error occurs.
        /// </summary>
        public event Action<Exception>? OnError;

        /// <summary>
        /// Event raised when the socket is disconnected.
        /// </summary>
        public event Action? OnDisconnected;

        private IProtocolSession? _attachedSession;

        /// <summary>
        /// Gets the attached application-layer protocol session, if any.
        /// </summary>
        public IProtocolSession? AttachedSession => _attachedSession;

        /// <summary>
        /// Attaches an application-layer protocol session for automatic handshake recovery on reconnect.
        /// </summary>
        public void AttachSession(IProtocolSession session) => _attachedSession = session;

        /// <summary>
        /// Detaches the currently attached protocol session.
        /// </summary>
        public void DetachSession() => _attachedSession = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncTcpTransport"/> class.
        /// </summary>
        /// <param name="host">Remote hostname or IP address.</param>
        /// <param name="port">Remote TCP port.</param>
        /// <param name="connectTimeoutMs">Connect timeout in milliseconds (default: 5000ms).</param>
        public AsyncTcpTransport(string host, int port, int connectTimeoutMs = 5000)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentNullException(nameof(host));
            if (port <= 0 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");

            _host = host;
            _port = port;
            _connectTimeoutMs = connectTimeoutMs;
        }

        /// <summary>
        /// Connects asynchronously to the remote host.
        /// </summary>
        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            await DisconnectInternalAsync(false).ConfigureAwait(false);

            _isExplicitDisconnect = false;
            var newClient = new TcpClient();
            newClient.NoDelay = true; // Disable Nagle algorithm for low-latency industrial messaging

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(_connectTimeoutMs);

            try
            {
#if NET8_0_OR_GREATER
                await newClient.ConnectAsync(_host, _port, linkedCts.Token).ConfigureAwait(false);
#else
                var connectTask = newClient.ConnectAsync(_host, _port);
                var delayTask = Task.Delay(_connectTimeoutMs, linkedCts.Token);
                var completed = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
                if (completed != connectTask)
                {
                    newClient.Close();
                    throw new TimeoutException($"Connection to {_host}:{_port} timed out after {_connectTimeoutMs}ms.");
                }
                await connectTask.ConfigureAwait(false); // Propagate any connection exception
#endif
                lock (_stateLock)
                {
                    _client = newClient;
                    _stream = newClient.GetStream();
                    _cts = new CancellationTokenSource();
                }

                _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

                OnConnected?.Invoke();
                if (_attachedSession != null)
                {
                    await _attachedSession.OnSessionConnectedAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                newClient.Dispose();
                OnError?.Invoke(ex);
                throw;
            }
        }

        /// <summary>
        /// Disconnects gracefully from the remote host.
        /// </summary>
        public async Task DisconnectAsync()
        {
            _isExplicitDisconnect = true;
            await DisconnectInternalAsync(true).ConfigureAwait(false);
        }

        private async Task DisconnectInternalAsync(bool notify)
        {
            CancellationTokenSource? ctsToCancel;
            TcpClient? clientToClose;
            NetworkStream? streamToClose;
            Task? rxTask;

            lock (_stateLock)
            {
                ctsToCancel = _cts;
                _cts = null;
                clientToClose = _client;
                _client = null;
                streamToClose = _stream;
                _stream = null;
                rxTask = _receiveTask;
                _receiveTask = null;
            }

            if (ctsToCancel != null)
            {
                try { ctsToCancel.Cancel(); } catch { }
                ctsToCancel.Dispose();
            }

            if (streamToClose != null)
            {
                try { streamToClose.Dispose(); } catch { }
            }

            if (clientToClose != null)
            {
                try { clientToClose.Close(); } catch { }
            }

            if (rxTask != null)
            {
                try { await rxTask.ConfigureAwait(false); } catch { }
            }

            if (_attachedSession != null)
            {
                try { await _attachedSession.OnSessionDisconnectedAsync().ConfigureAwait(false); } catch { }
            }

            if (notify)
            {
                OnDisconnected?.Invoke();
            }
        }

        /// <summary>
        /// Sends raw bytes asynchronously over the TCP socket.
        /// </summary>
        public async Task SendAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count));

            NetworkStream? stream;
            lock (_stateLock)
            {
                stream = _stream;
            }

            if (stream == null || !IsConnected)
                throw new InvalidOperationException("Transport is not connected.");

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await stream.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Sends a read-only memory buffer asynchronously over the TCP socket.
        /// </summary>
        public async Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (buffer.Length == 0) return;

            NetworkStream? stream;
            lock (_stateLock)
            {
                stream = _stream;
            }

            if (stream == null || !IsConnected)
                throw new InvalidOperationException("Transport is not connected.");

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
#if NET8_0_OR_GREATER
                await stream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
#else
                if (System.Runtime.InteropServices.MemoryMarshal.TryGetArray(buffer, out var segment) && segment.Array != null)
                {
                    await stream.WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    byte[] arr = buffer.ToArray();
                    await stream.WriteAsync(arr, 0, arr.Length, cancellationToken).ConfigureAwait(false);
                }
#endif
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    NetworkStream? stream;
                    lock (_stateLock)
                    {
                        stream = _stream;
                    }

                    if (stream == null)
                        break;

                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        // Socket closed gracefully by remote peer
                        break;
                    }

                    DataReceived?.Invoke(buffer, 0, bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    OnError?.Invoke(ex);
                }
            }
            finally
            {
                if (!_isExplicitDisconnect)
                {
                    _ = Task.Run(async () =>
                    {
                        await DisconnectInternalAsync(true).ConfigureAwait(false);
                        if (AutoReconnect && !_isDisposed && !_isExplicitDisconnect)
                        {
                            await HandleAutoReconnectAsync().ConfigureAwait(false);
                        }
                    });
                }
            }
        }

        private async Task HandleAutoReconnectAsync()
        {
            while (AutoReconnect && !_isDisposed && !_isExplicitDisconnect && !IsConnected)
            {
                try
                {
                    await Task.Delay(ReconnectIntervalMs).ConfigureAwait(false);
                    if (_isExplicitDisconnect || _isDisposed)
                        break;

                    await ConnectAsync().ConfigureAwait(false);
                    if (IsConnected)
                        break;
                }
                catch
                {
                    // Reconnection failed, retry next loop
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AsyncTcpTransport));
        }

        /// <summary>
        /// Disposes socket resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _isExplicitDisconnect = true;
            try
            {
                DisconnectInternalAsync(false).GetAwaiter().GetResult();
            }
            catch { }
            _sendLock.Dispose();
        }
    }
}
