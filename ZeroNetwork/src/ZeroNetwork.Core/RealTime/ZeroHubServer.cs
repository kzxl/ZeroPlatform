using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Http;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// Self-hosted ASP.NET Core SignalR Protocol v1 Hub Server for ZeroPlatform.
    /// Pure C# BCL socket-based WebSocket server with zero external dependencies.
    /// Supports RPC invocations, caller completion results, group management, and automatic keep-alive ping.
    /// </summary>
    /// <typeparam name="THub">The type of the Hub handling business methods.</typeparam>
    public sealed class ZeroHubServer<THub> : IDisposable where THub : ZeroHub, new()
    {
        private readonly ZeroWebSocketServer _wsServer;
        private readonly IZeroJsonSerializer _serializer;
        private readonly ConcurrentDictionary<string, ZeroWebSocketSession> _sessions = new ConcurrentDictionary<string, ZeroWebSocketSession>();
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _groups = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, StringBuilder> _sessionBuffers = new ConcurrentDictionary<string, StringBuilder>();
        private readonly Dictionary<string, MethodInfo> _hubMethods = new Dictionary<string, MethodInfo>(StringComparer.OrdinalIgnoreCase);

        private Timer? _pingTimer;
        private int _disposed;

        /// <summary>
        /// Gets the underlying WebSocket server.
        /// </summary>
        public ZeroWebSocketServer WebSocketServer => _wsServer;

        /// <summary>
        /// Gets the port the hub server is listening on.
        /// </summary>
        public int Port => _wsServer.Port;

        /// <summary>
        /// Gets whether the hub server is currently running.
        /// </summary>
        public bool IsRunning => _wsServer.IsRunning;

        /// <summary>
        /// Gets the count of connected clients.
        /// </summary>
        public int ConnectedCount => _sessions.Count;

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroHubServer{THub}"/>.
        /// </summary>
        /// <param name="port">TCP port to listen on (e.g. 8080).</param>
        /// <param name="host">Host interface (default "0.0.0.0").</param>
        /// <param name="serializer">Optional JSON serializer.</param>
        public ZeroHubServer(int port = 8080, string host = "0.0.0.0", IZeroJsonSerializer? serializer = null)
        {
            _wsServer = new ZeroWebSocketServer(port, host);
#if NET8_0_OR_GREATER
            _serializer = serializer ?? new SystemTextJsonStreamSerializer();
#else
            _serializer = serializer ?? new FallbackStreamSerializer();
#endif

            // Discover public instance methods declared on THub (exclude object and ZeroHub base methods)
            var methods = typeof(THub).GetMethods(BindingFlags.Public | BindingFlags.Instance);
            foreach (var m in methods)
            {
                if (m.DeclaringType != typeof(object) && m.DeclaringType != typeof(ZeroHub))
                {
                    _hubMethods[m.Name] = m;
                }
            }

            _wsServer.SessionConnected += OnSessionConnected;
            _wsServer.SessionDisconnected += OnSessionDisconnected;
        }

        /// <summary>
        /// Starts the Hub server and keep-alive ping loop.
        /// </summary>
        public void Start()
        {
            ThrowIfDisposed();
            _wsServer.Start();
            _pingTimer = new Timer(SendHeartbeatPing, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        }

        /// <summary>
        /// Stops the Hub server and cleans up active sessions.
        /// </summary>
        public void Stop()
        {
            _pingTimer?.Dispose();
            _pingTimer = null;
            _wsServer.Stop();
            _sessions.Clear();
            _groups.Clear();
            _sessionBuffers.Clear();
        }

        private void OnSessionConnected(ZeroWebSocketSession session)
        {
            _sessions[session.SessionId] = session;
            var buffer = _sessionBuffers.GetOrAdd(session.SessionId, _ => new StringBuilder());

            bool handshakeDone = false;
            HubCallerContext? context = null;

            session.TextReceived += async (s, text) =>
            {
                List<string> completedMessages = new List<string>();
                lock (buffer)
                {
                    buffer.Append(text);
                    string current = buffer.ToString();
                    int sepIdx;
                    while ((sepIdx = current.IndexOf(SignalRProtocol.RecordSeparator)) >= 0)
                    {
                        string msg = current.Substring(0, sepIdx);
                        completedMessages.Add(msg);
                        current = current.Substring(sepIdx + 1);
                    }
                    buffer.Clear();
                    buffer.Append(current);
                }

                for (int i = 0; i < completedMessages.Count; i++)
                {
                    string rawMsg = completedMessages[i];
                    if (string.IsNullOrWhiteSpace(rawMsg)) continue;

                    if (!handshakeDone)
                    {
                        // SignalR Protocol Handshake: {"protocol":"json","version":1}
                        if (rawMsg.Contains("\"protocol\""))
                        {
                            handshakeDone = true;
                            context = new HubCallerContext(s.SessionId, s.RemoteEndPoint);

                            // Send handshake response: {}\u001e
                            await s.SendTextAsync("{}" + SignalRProtocol.RecordSeparator).ConfigureAwait(false);

                            // Trigger OnConnectedAsync
                            _ = InvokeLifecycleAsync(context, s, h => h.OnConnectedAsync());
                        }
                        continue;
                    }

                    // Process SignalR Messages
                    var hubMsg = SignalRProtocol.ParseHubMessage(rawMsg);
                    if (hubMsg == null) continue;

                    if (hubMsg.Type == 6) // Ping
                    {
                        // Client sent ping -> ack
                        continue;
                    }

                    if (hubMsg.Type == 1 && !string.IsNullOrEmpty(hubMsg.Target)) // Invocation
                    {
                        if (context != null)
                        {
                            _ = HandleInvocationAsync(context, s, hubMsg);
                        }
                    }
                }
            };
        }

        private void OnSessionDisconnected(ZeroWebSocketSession session, Exception? ex)
        {
            _sessions.TryRemove(session.SessionId, out _);
            _sessionBuffers.TryRemove(session.SessionId, out _);

            // Remove from all groups
            foreach (var g in _groups.Values)
            {
                g.TryRemove(session.SessionId, out _);
            }

            var context = new HubCallerContext(session.SessionId, session.RemoteEndPoint);
            _ = InvokeLifecycleAsync(context, session, h => h.OnDisconnectedAsync(ex));
        }

        private async Task InvokeLifecycleAsync(HubCallerContext context, ZeroWebSocketSession session, Func<THub, Task> action)
        {
            using (var hub = new THub())
            {
                var groupManager = new HubGroupManager(_groups);
                var clients = new HubCallerClients(this, session.SessionId);
                hub.Context = context;
                hub.Clients = clients;
                hub.Groups = groupManager;

                try
                {
                    await action(hub).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private async Task HandleInvocationAsync(HubCallerContext context, ZeroWebSocketSession session, SignalRHubMessage hubMsg)
        {
            string methodName = hubMsg.Target!;
            string? invocationId = hubMsg.InvocationId;

            if (!_hubMethods.TryGetValue(methodName, out var method))
            {
                if (!string.IsNullOrEmpty(invocationId))
                {
                    string errJson = $"{{\"type\":3,\"invocationId\":\"{invocationId}\",\"error\":\"Method '{methodName}' not found on Hub.\"}}{SignalRProtocol.RecordSeparator}";
                    await session.SendTextAsync(errJson).ConfigureAwait(false);
                }
                return;
            }

            using (var hub = new THub())
            {
                var groupManager = new HubGroupManager(_groups);
                var clients = new HubCallerClients(this, session.SessionId);
                hub.Context = context;
                hub.Clients = clients;
                hub.Groups = groupManager;

                try
                {
                    // Convert arguments
                    var parameters = method.GetParameters();
                    object?[] invokeArgs = new object?[parameters.Length];

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var pType = parameters[i].ParameterType;
                        if (i < hubMsg.RawArguments.Count)
                        {
                            string rawArg = hubMsg.RawArguments[i];
                            invokeArgs[i] = ConvertArgument(rawArg, pType);
                        }
                        else
                        {
                            invokeArgs[i] = pType.IsValueType ? Activator.CreateInstance(pType) : null;
                        }
                    }

                    object? result = method.Invoke(hub, invokeArgs);

                    // Await if asynchronous
                    if (result is Task task)
                    {
                        await task.ConfigureAwait(false);
                        var prop = task.GetType().GetProperty("Result");
                        result = prop != null ? prop.GetValue(task) : null;
                    }

                    // Return completion message if requested
                    if (!string.IsNullOrEmpty(invocationId))
                    {
                        string resultJson = await SignalRProtocol.SerializeArgumentAsync(result, _serializer, CancellationToken.None).ConfigureAwait(false);
                        string completionJson = $"{{\"type\":3,\"invocationId\":\"{invocationId}\",\"result\":{resultJson}}}{SignalRProtocol.RecordSeparator}";
                        await session.SendTextAsync(completionJson).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrEmpty(invocationId))
                    {
                        string cleanErr = SignalRProtocol.EscapeJsonString(ex.InnerException?.Message ?? ex.Message);
                        string errJson = $"{{\"type\":3,\"invocationId\":\"{invocationId}\",\"error\":\"{cleanErr}\"}}{SignalRProtocol.RecordSeparator}";
                        await session.SendTextAsync(errJson).ConfigureAwait(false);
                    }
                }
            }
        }

        private object? ConvertArgument(string rawArg, Type targetType)
        {
            if (string.IsNullOrWhiteSpace(rawArg) || rawArg == "null")
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;

            if (targetType == typeof(string))
            {
                if (rawArg.StartsWith("\"") && rawArg.EndsWith("\"") && rawArg.Length >= 2)
                {
                    return rawArg.Substring(1, rawArg.Length - 2)
                        .Replace("\\\"", "\"")
                        .Replace("\\\\", "\\")
                        .Replace("\\n", "\n")
                        .Replace("\\r", "\r");
                }
                return rawArg;
            }

            if (targetType == typeof(int)) return int.Parse(rawArg, CultureInfo.InvariantCulture);
            if (targetType == typeof(long)) return long.Parse(rawArg, CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(rawArg, CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(rawArg, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool)) return bool.Parse(rawArg);
            if (targetType == typeof(byte)) return byte.Parse(rawArg, CultureInfo.InvariantCulture);
            if (targetType == typeof(short)) return short.Parse(rawArg, CultureInfo.InvariantCulture);

            // Fallback: deserialize via stream
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawArg)))
            {
                var method = typeof(IZeroJsonSerializer).GetMethod("DeserializeAsync")?.MakeGenericMethod(targetType);
                if (method != null)
                {
                    var task = (Task)method.Invoke(_serializer, new object[] { ms, CancellationToken.None })!;
                    task.GetAwaiter().GetResult();
                    var prop = task.GetType().GetProperty("Result");
                    return prop?.GetValue(task);
                }
            }

            return null;
        }

        private void SendHeartbeatPing(object? state)
        {
            string ping = SignalRProtocol.PingMessage;
            foreach (var s in _sessions.Values)
            {
                if (s.IsOpen)
                {
                    _ = s.SendTextAsync(ping);
                }
            }
        }

        internal async Task SendInvocationToSessionAsync(string sessionId, string method, object?[]? args, CancellationToken cancellationToken)
        {
            if (_sessions.TryGetValue(sessionId, out var session) && session.IsOpen)
            {
                string json = await SignalRProtocol.BuildInvocationMessageAsync(null, method, args, _serializer, cancellationToken).ConfigureAwait(false);
                await session.SendTextAsync(SignalRProtocol.FormatMessage(json), cancellationToken).ConfigureAwait(false);
            }
        }

        internal async Task SendInvocationToAllAsync(string? exceptSessionId, string method, object?[]? args, CancellationToken cancellationToken)
        {
            string json = await SignalRProtocol.BuildInvocationMessageAsync(null, method, args, _serializer, cancellationToken).ConfigureAwait(false);
            string formatted = SignalRProtocol.FormatMessage(json);

            var tasks = new List<Task>(_sessions.Count);
            foreach (var kvp in _sessions)
            {
                if (kvp.Key != exceptSessionId && kvp.Value.IsOpen)
                {
                    tasks.Add(kvp.Value.SendTextAsync(formatted, cancellationToken));
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        internal async Task SendInvocationToGroupAsync(string groupName, string? exceptSessionId, string method, object?[]? args, CancellationToken cancellationToken)
        {
            if (!_groups.TryGetValue(groupName, out var members))
                return;

            string json = await SignalRProtocol.BuildInvocationMessageAsync(null, method, args, _serializer, cancellationToken).ConfigureAwait(false);
            string formatted = SignalRProtocol.FormatMessage(json);

            var tasks = new List<Task>();
            foreach (var connectionId in members.Keys)
            {
                if (connectionId != exceptSessionId && _sessions.TryGetValue(connectionId, out var s) && s.IsOpen)
                {
                    tasks.Add(s.SendTextAsync(formatted, cancellationToken));
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        #region Internal Proxies and Managers
        private sealed class HubGroupManager : IGroupManager
        {
            private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> _groups;

            public HubGroupManager(ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> groups)
            {
                _groups = groups;
            }

            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            {
                var dict = _groups.GetOrAdd(groupName, _ => new ConcurrentDictionary<string, byte>());
                dict[connectionId] = 1;
                return Task.CompletedTask;
            }

            public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            {
                if (_groups.TryGetValue(groupName, out var dict))
                {
                    dict.TryRemove(connectionId, out _);
                }
                return Task.CompletedTask;
            }
        }

        private sealed class HubCallerClients : IHubCallerClients
        {
            private readonly ZeroHubServer<THub> _server;
            private readonly string _callerSessionId;

            public IClientProxy All => new ClientProxy(cToken => (m, a) => _server.SendInvocationToAllAsync(null, m, a, cToken));
            public IClientProxy Caller => new ClientProxy(cToken => (m, a) => _server.SendInvocationToSessionAsync(_callerSessionId, m, a, cToken));
            public IClientProxy Others => new ClientProxy(cToken => (m, a) => _server.SendInvocationToAllAsync(_callerSessionId, m, a, cToken));

            public HubCallerClients(ZeroHubServer<THub> server, string callerSessionId)
            {
                _server = server;
                _callerSessionId = callerSessionId;
            }

            public IClientProxy Client(string connectionId)
                => new ClientProxy(cToken => (m, a) => _server.SendInvocationToSessionAsync(connectionId, m, a, cToken));

            public IClientProxy Group(string groupName)
                => new ClientProxy(cToken => (m, a) => _server.SendInvocationToGroupAsync(groupName, null, m, a, cToken));
        }

        private sealed class ClientProxy : IClientProxy
        {
            private readonly Func<CancellationToken, Func<string, object?[]?, Task>> _senderFactory;

            public ClientProxy(Func<CancellationToken, Func<string, object?[]?, Task>> senderFactory)
            {
                _senderFactory = senderFactory;
            }

            public Task SendCoreAsync(string method, object?[]? args, CancellationToken cancellationToken = default)
            {
                var sender = _senderFactory(cancellationToken);
                return sender(method, args);
            }
        }
        #endregion

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ZeroHubServer<THub>));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                Stop();
                _wsServer.Dispose();
            }
        }
    }
}
