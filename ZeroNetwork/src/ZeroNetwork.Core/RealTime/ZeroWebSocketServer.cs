using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Lightweight, sovereign embedded RFC 6455 WebSocket Server for ZeroPlatform.
    /// Pure C# BCL socket listener, zero external dependencies, runs without Windows Administrator privileges.
    /// </summary>
    public sealed class ZeroWebSocketServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly int _port;
        private readonly string _host;
        private readonly ConcurrentDictionary<string, ZeroWebSocketSession> _sessions = new ConcurrentDictionary<string, ZeroWebSocketSession>();

        private CancellationTokenSource? _serverCts;
        private readonly object _lock = new object();
        private int _disposed;

        /// <summary>
        /// Gets the port on which the server is listening.
        /// </summary>
        public int Port => _port;

        /// <summary>
        /// Gets whether the server is currently running and accepting connections.
        /// </summary>
        public bool IsRunning => _serverCts != null && !_serverCts.IsCancellationRequested;

        /// <summary>
        /// Gets the current number of active connected WebSocket sessions.
        /// </summary>
        public int ActiveSessionCount => _sessions.Count;

        /// <summary>
        /// Event fired when a new WebSocket client completes the handshake and connects.
        /// </summary>
        public event Action<ZeroWebSocketSession>? SessionConnected;

        /// <summary>
        /// Event fired when an existing WebSocket client disconnects.
        /// </summary>
        public event Action<ZeroWebSocketSession, Exception?>? SessionDisconnected;

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroWebSocketServer"/>.
        /// </summary>
        /// <param name="port">TCP port to bind (e.g. 8080). Use 0 to bind dynamically to any available OS port.</param>
        /// <param name="host">Host interface (default "0.0.0.0" for all interfaces, or "127.0.0.1").</param>
        public ZeroWebSocketServer(int port = 8080, string host = "0.0.0.0")
        {
            _host = host;
            IPAddress bindAddress = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host);
            _listener = new TcpListener(bindAddress, port);
            _port = port;
        }

        /// <summary>
        /// Starts the WebSocket server asynchronously.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                ThrowIfDisposed();
                if (IsRunning) return;

                _listener.Start();
                _serverCts = new CancellationTokenSource();
                var token = _serverCts.Token;

                Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                            _ = Task.Run(() => HandleIncomingClientAsync(client, token), token);
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }
                        catch (SocketException)
                        {
                            break;
                        }
                        catch
                        {
                            if (token.IsCancellationRequested) break;
                            try { await Task.Delay(50, token).ConfigureAwait(false); } catch { break; }
                        }
                    }
                }, token);
            }
        }

        private async Task HandleIncomingClientAsync(TcpClient client, CancellationToken token)
        {
            var session = new ZeroWebSocketSession(client);
            bool handshakeOk = await session.PerformServerHandshakeAsync(token).ConfigureAwait(false);
            if (!handshakeOk)
            {
                session.Dispose();
                return;
            }

            _sessions[session.SessionId] = session;
            session.Closed += (s, ex) =>
            {
                _sessions.TryRemove(s.SessionId, out _);
                SessionDisconnected?.Invoke(s, ex);
            };

            SessionConnected?.Invoke(session);

            await session.RunReceiveLoopAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Broadcasts a text message to all active WebSocket sessions.
        /// </summary>
        public async Task BroadcastTextAsync(string message, CancellationToken cancellationToken = default)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));

            var tasks = new List<Task>(_sessions.Count);
            foreach (var kvp in _sessions)
            {
                if (kvp.Value.IsOpen)
                {
                    tasks.Add(kvp.Value.SendTextAsync(message, cancellationToken));
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Broadcasts a binary payload to all active WebSocket sessions.
        /// </summary>
        public async Task BroadcastBinaryAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            var tasks = new List<Task>(_sessions.Count);
            foreach (var kvp in _sessions)
            {
                if (kvp.Value.IsOpen)
                {
                    tasks.Add(kvp.Value.SendBinaryAsync(payload, cancellationToken));
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to retrieve an active session by its ID.
        /// </summary>
        public bool TryGetSession(string sessionId, out ZeroWebSocketSession? session)
        {
            return _sessions.TryGetValue(sessionId, out session);
        }

        /// <summary>
        /// Gets all currently active sessions.
        /// </summary>
        public IReadOnlyCollection<ZeroWebSocketSession> GetActiveSessions()
        {
            return (IReadOnlyCollection<ZeroWebSocketSession>)_sessions.Values;
        }

        /// <summary>
        /// Stops accepting new connections and closes all active sessions.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (_serverCts != null)
                {
                    _serverCts.Cancel();
                    try { _listener.Stop(); } catch { }
                    _serverCts = null;
                }

                foreach (var session in _sessions.Values)
                {
                    try { session.Dispose(); } catch { }
                }
                _sessions.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ZeroWebSocketServer));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Stop();
            }
        }
    }
}
