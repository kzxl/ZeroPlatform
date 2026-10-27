using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Http;

namespace ZeroNetwork.RealTime
{
    /// <summary>
    /// High-performance, lightweight ASP.NET Core SignalR JSON Hub Client (Protocol v1).
    /// Built purely on top of <see cref="ZeroWebSocketClient"/> with ZERO external dependencies,
    /// avoiding assembly conflicts and DLL bloat in .NET Framework 4.6.2 and modern runtimes.
    /// </summary>
    public class ZeroSignalRClient : IDisposable
    {
        private readonly ZeroSignalROptions _options;
        private readonly ZeroWebSocketClient _wsClient;
        private readonly ConcurrentDictionary<string, List<Delegate>> _handlers = new ConcurrentDictionary<string, List<Delegate>>(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, InvocationEntry> _pendingInvocations = new ConcurrentDictionary<string, InvocationEntry>();

        private TaskCompletionSource<bool>? _handshakeTcs;
        private Timer? _pingTimer;
        private long _invocationCounter;
        private bool _isHandshakeCompleted;
        private bool _isDisposed;
        private readonly StringBuilder _textBuffer = new StringBuilder();
        private readonly object _bufferLock = new object();

        /// <summary>
        /// Event fired when the SignalR connection and handshake are successfully completed.
        /// </summary>
        public event Action? Connected;

        /// <summary>
        /// Event fired when the client is disconnected from the hub.
        /// </summary>
        public event Action<Exception?>? Disconnected;

        /// <summary>
        /// Event fired when the client begins an automatic reconnection attempt.
        /// </summary>
        public event Action<int, TimeSpan>? Reconnecting;

        /// <summary>
        /// Event fired when the client successfully reconnects and re-establishes the hub handshake.
        /// </summary>
        public event Action? Reconnected;

        /// <summary>
        /// Event fired when an error occurs during protocol handling or invocation.
        /// </summary>
        public event Action<Exception>? Error;

        /// <summary>
        /// Gets whether the client is connected to the hub and the handshake is active.
        /// </summary>
        public bool IsConnected => _wsClient.IsConnected && _isHandshakeCompleted;

        /// <summary>
        /// Gets the configuration options for this client.
        /// </summary>
        public ZeroSignalROptions Options => _options;

        /// <summary>
        /// Gets or sets the JSON serializer used for serializing and deserializing invocation arguments.
        /// </summary>
        public IZeroJsonSerializer Serializer
        {
            get => _wsClient.Serializer;
            set => _wsClient.Serializer = value;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroSignalRClient"/> with the specified options.
        /// </summary>
        public ZeroSignalRClient(ZeroSignalROptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (_options.HubUri == null) throw new ArgumentException("HubUri must be configured in ZeroSignalROptions.", nameof(options));

            Uri normalizedWsUri = NormalizeHubUri(_options.HubUri);

            var wsOptions = new ZeroWebSocketOptions
            {
                ServerUri = normalizedWsUri,
                AutoReconnect = _options.AutoReconnect,
                InitialReconnectDelay = _options.InitialReconnectDelay,
                MaxReconnectDelay = _options.MaxReconnectDelay
            };

            foreach (var header in _options.Headers)
            {
                wsOptions.Headers[header.Key] = header.Value;
            }

            _wsClient = new ZeroWebSocketClient(wsOptions);
            _wsClient.Connected += OnWebSocketConnected;
            _wsClient.Disconnected += OnWebSocketDisconnected;
            _wsClient.Reconnecting += (attempt, delay) => Reconnecting?.Invoke(attempt, delay);
            _wsClient.Reconnected += OnWebSocketReconnected;
            _wsClient.MessageReceived += OnRawTextMessageReceived;
            _wsClient.Error += ex => Error?.Invoke(ex);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroSignalRClient"/> for a hub URL.
        /// </summary>
        public ZeroSignalRClient(Uri hubUri) : this(new ZeroSignalROptions { HubUri = hubUri })
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroSignalRClient"/> for a hub URL string.
        /// </summary>
        public ZeroSignalRClient(string hubUrl) : this(new Uri(hubUrl ?? throw new ArgumentNullException(nameof(hubUrl))))
        {
        }

        private static Uri NormalizeHubUri(Uri uri)
        {
            string scheme = uri.Scheme.ToLowerInvariant();
            if (scheme == "http")
            {
                var builder = new UriBuilder(uri) { Scheme = "ws" };
                return builder.Uri;
            }
            if (scheme == "https")
            {
                var builder = new UriBuilder(uri) { Scheme = "wss" };
                return builder.Uri;
            }
            return uri;
        }

        /// <summary>
        /// Connects to the SignalR hub and performs the protocol handshake.
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            _handshakeTcs = new TaskCompletionSource<bool>();
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linkedCts.CancelAfter(_options.HandshakeTimeout);
                using (linkedCts.Token.Register(() => _handshakeTcs.TrySetCanceled()))
                {
                    await _wsClient.ConnectAsync(linkedCts.Token).ConfigureAwait(false);
                    await _handshakeTcs.Task.ConfigureAwait(false);
                }
            }

            StartPingTimer();
            Connected?.Invoke();
        }

        /// <summary>
        /// Stops the SignalR connection gracefully.
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopPingTimer();
            _isHandshakeCompleted = false;
            FailAllPendingInvocations(new OperationCanceledException("Connection closed by client."));
            await _wsClient.CloseAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private void OnWebSocketConnected()
        {
            _ = SendHandshakeRequestAsync();
        }

        private void OnWebSocketReconnected()
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    _isHandshakeCompleted = false;
                    _handshakeTcs = new TaskCompletionSource<bool>();

                    await SendHandshakeRequestAsync().ConfigureAwait(false);

                    using (var cts = new CancellationTokenSource(_options.HandshakeTimeout))
                    using (cts.Token.Register(() => _handshakeTcs.TrySetCanceled()))
                    {
                        await _handshakeTcs.Task.ConfigureAwait(false);
                    }

                    StartPingTimer();
                    Reconnected?.Invoke();
                }
                catch (Exception ex)
                {
                    Error?.Invoke(ex);
                }
            });
        }

        private void OnWebSocketDisconnected(Exception? ex)
        {
            StopPingTimer();
            _isHandshakeCompleted = false;
            FailAllPendingInvocations(ex ?? new IOException("SignalR connection was closed."));
            Disconnected?.Invoke(ex);
        }

        private async Task SendHandshakeRequestAsync()
        {
            try
            {
                await _wsClient.SendTextAsync(SignalRProtocol.HandshakeRequest).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _handshakeTcs?.TrySetException(ex);
                Error?.Invoke(ex);
            }
        }

        private void OnRawTextMessageReceived(string text)
        {
            List<string> messages = new List<string>();

            lock (_bufferLock)
            {
                _textBuffer.Append(text);
                string current = _textBuffer.ToString();

                int sepIdx;
                while ((sepIdx = current.IndexOf(SignalRProtocol.RecordSeparator)) >= 0)
                {
                    string msg = current.Substring(0, sepIdx);
                    messages.Add(msg);
                    current = current.Substring(sepIdx + 1);
                }

                _textBuffer.Clear();
                if (current.Length > 0)
                {
                    _textBuffer.Append(current);
                }
            }

            foreach (var message in messages)
            {
                ProcessSingleMessage(message);
            }
        }

        private void ProcessSingleMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            // 1. Check for Handshake Response if not yet completed
            if (!_isHandshakeCompleted)
            {
                if (message.Contains("\"error\""))
                {
                    var handshakeError = new InvalidOperationException($"SignalR Handshake error: {message}");
                    _handshakeTcs?.TrySetException(handshakeError);
                    Error?.Invoke(handshakeError);
                    return;
                }

                _isHandshakeCompleted = true;
                _handshakeTcs?.TrySetResult(true);
                return;
            }

            // 2. Parse standard SignalR Hub message
            try
            {
                var hubMsg = SignalRProtocol.ParseHubMessage(message);
                if (hubMsg == null) return;

                switch (hubMsg.Type)
                {
                    case 1: // Invocation from Server
                        DispatchServerInvocation(hubMsg);
                        break;

                    case 3: // Completion of Client Invocation
                        HandleInvocationCompletion(hubMsg);
                        break;

                    case 6: // Ping Keep-Alive
                        // Responding to ping or accepting ping
                        break;

                    case 7: // Close
                        _isHandshakeCompleted = false;
                        _ = StopAsync();
                        break;
                }
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
        }

        private void DispatchServerInvocation(SignalRHubMessage hubMsg)
        {
            if (string.IsNullOrEmpty(hubMsg.Target)) return;

            if (_handlers.TryGetValue(hubMsg.Target!, out var handlers))
            {
                lock (handlers)
                {
                    foreach (var handler in handlers)
                    {
                        try
                        {
                            InvokeHandler(handler, hubMsg.RawArguments);
                        }
                        catch (Exception ex)
                        {
                            Error?.Invoke(ex);
                        }
                    }
                }
            }
        }

        private void InvokeHandler(Delegate handler, List<string> rawArgs)
        {
            var method = handler.Method;
            var parameters = method.GetParameters();

            if (parameters.Length == 0)
            {
                handler.DynamicInvoke();
                return;
            }

            object?[] invokeArgs = new object?[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                string raw = (i < rawArgs.Count) ? rawArgs[i] : "null";
                invokeArgs[i] = ConvertRawArgument(raw, parameters[i].ParameterType);
            }

            handler.DynamicInvoke(invokeArgs);
        }

        private object? ConvertRawArgument(string raw, Type targetType)
        {
            if (raw == "null" || string.IsNullOrWhiteSpace(raw))
            {
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            if (targetType == typeof(string))
            {
                raw = raw.Trim();
                if (raw.StartsWith("\"") && raw.EndsWith("\"") && raw.Length >= 2)
                {
                    return ParseQuotedJsonString(raw);
                }
                return raw;
            }

            if (targetType == typeof(int) || targetType == typeof(int?))
            {
                return int.Parse(raw.Trim(), CultureInfo.InvariantCulture);
            }
            if (targetType == typeof(long) || targetType == typeof(long?))
            {
                return long.Parse(raw.Trim(), CultureInfo.InvariantCulture);
            }
            if (targetType == typeof(bool) || targetType == typeof(bool?))
            {
                return bool.Parse(raw.Trim());
            }
            if (targetType == typeof(double) || targetType == typeof(double?))
            {
                return double.Parse(raw.Trim(), CultureInfo.InvariantCulture);
            }
            if (targetType == typeof(decimal) || targetType == typeof(decimal?))
            {
                return decimal.Parse(raw.Trim(), CultureInfo.InvariantCulture);
            }

            // Complex object deserialization via IZeroJsonSerializer
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(raw)))
            {
                var task = Serializer.DeserializeAsync<object>(ms);
                // Synchronous bridge for event dispatch
                return task.GetAwaiter().GetResult();
            }
        }

        private string ParseQuotedJsonString(string raw)
        {
            if (raw.Length < 2) return "";
            var sb = new StringBuilder(raw.Length);
            for (int i = 1; i < raw.Length - 1; i++)
            {
                char c = raw[i];
                if (c == '\\' && i + 1 < raw.Length - 1)
                {
                    char next = raw[++i];
                    switch (next)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(next); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private void HandleInvocationCompletion(SignalRHubMessage hubMsg)
        {
            if (string.IsNullOrEmpty(hubMsg.InvocationId)) return;

            if (_pendingInvocations.TryRemove(hubMsg.InvocationId!, out var entry))
            {
                if (!string.IsNullOrEmpty(hubMsg.Error))
                {
                    entry.SetException(new InvalidOperationException($"Hub method returned error: {hubMsg.Error}"));
                }
                else
                {
                    entry.SetResult(hubMsg.RawResult, this);
                }
            }
        }

        /// <summary>
        /// Registers a handler for a parameterless hub method invoked by the server.
        /// </summary>
        public void On(string target, Action handler)
        {
            RegisterHandler(target, handler);
        }

        /// <summary>
        /// Registers a handler for a 1-parameter hub method invoked by the server.
        /// </summary>
        public void On<T>(string target, Action<T> handler)
        {
            RegisterHandler(target, handler);
        }

        /// <summary>
        /// Registers a handler for a 2-parameter hub method invoked by the server.
        /// </summary>
        public void On<T1, T2>(string target, Action<T1, T2> handler)
        {
            RegisterHandler(target, handler);
        }

        /// <summary>
        /// Registers a handler for a 3-parameter hub method invoked by the server.
        /// </summary>
        public void On<T1, T2, T3>(string target, Action<T1, T2, T3> handler)
        {
            RegisterHandler(target, handler);
        }

        private void RegisterHandler(string target, Delegate handler)
        {
            if (string.IsNullOrWhiteSpace(target)) throw new ArgumentNullException(nameof(target));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var list = _handlers.GetOrAdd(target, _ => new List<Delegate>());
            lock (list)
            {
                list.Add(handler);
            }
        }

        /// <summary>
        /// Invokes a hub method without expecting a return value (fire-and-forget).
        /// </summary>
        public async Task SendAsync(string target, params object?[]? args)
        {
            ThrowIfDisposed();
            if (!IsConnected) throw new InvalidOperationException("SignalR client is not connected.");

            string json = await SignalRProtocol.BuildInvocationMessageAsync(null, target, args, Serializer, CancellationToken.None).ConfigureAwait(false);
            await _wsClient.SendTextAsync(SignalRProtocol.FormatMessage(json)).ConfigureAwait(false);
        }

        /// <summary>
        /// Invokes a hub method and asynchronously awaits the returned result.
        /// </summary>
        public async Task<TResult> InvokeAsync<TResult>(string target, params object?[]? args)
        {
            return await InvokeAsync<TResult>(target, CancellationToken.None, args).ConfigureAwait(false);
        }

        /// <summary>
        /// Invokes a hub method with cancellation token and asynchronously awaits the returned result.
        /// </summary>
        public async Task<TResult> InvokeAsync<TResult>(string target, CancellationToken cancellationToken, params object?[]? args)
        {
            ThrowIfDisposed();
            if (!IsConnected) throw new InvalidOperationException("SignalR client is not connected.");

            string invocationId = Interlocked.Increment(ref _invocationCounter).ToString(CultureInfo.InvariantCulture);
            var tcs = new TaskCompletionSource<TResult>();
            var entry = new InvocationEntry<TResult>(tcs);

            _pendingInvocations[invocationId] = entry;

            using (var timeoutCts = new CancellationTokenSource(_options.InvocationTimeout))
            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
            using (linkedCts.Token.Register(() =>
            {
                if (_pendingInvocations.TryRemove(invocationId, out _))
                {
                    tcs.TrySetCanceled();
                }
            }))
            {
                string json = await SignalRProtocol.BuildInvocationMessageAsync(invocationId, target, args, Serializer, linkedCts.Token).ConfigureAwait(false);
                await _wsClient.SendTextAsync(SignalRProtocol.FormatMessage(json), linkedCts.Token).ConfigureAwait(false);

                return await tcs.Task.ConfigureAwait(false);
            }
        }

        private void StartPingTimer()
        {
            StopPingTimer();
            if (_options.PingInterval > TimeSpan.Zero)
            {
                _pingTimer = new Timer(async _ =>
                {
                    if (IsConnected)
                    {
                        try
                        {
                            await _wsClient.SendTextAsync(SignalRProtocol.PingMessage).ConfigureAwait(false);
                        }
                        catch { /* ignore background ping failure */ }
                    }
                }, null, _options.PingInterval, _options.PingInterval);
            }
        }

        private void StopPingTimer()
        {
            _pingTimer?.Dispose();
            _pingTimer = null;
        }

        private void FailAllPendingInvocations(Exception ex)
        {
            foreach (var kvp in _pendingInvocations)
            {
                if (_pendingInvocations.TryRemove(kvp.Key, out var entry))
                {
                    entry.SetException(ex);
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// Releases all managed resources used by <see cref="ZeroSignalRClient"/>.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            StopPingTimer();
            FailAllPendingInvocations(new ObjectDisposedException(GetType().FullName));
            _wsClient.Dispose();
        }

        private abstract class InvocationEntry
        {
            public abstract void SetResult(string? rawResult, ZeroSignalRClient client);
            public abstract void SetException(Exception ex);
        }

        private class InvocationEntry<T> : InvocationEntry
        {
            private readonly TaskCompletionSource<T> _tcs;

            public InvocationEntry(TaskCompletionSource<T> tcs)
            {
                _tcs = tcs;
            }

            public override void SetResult(string? rawResult, ZeroSignalRClient client)
            {
                try
                {
                    if (rawResult == null || rawResult == "null")
                    {
                        _tcs.TrySetResult(default!);
                        return;
                    }

                    object? converted = client.ConvertRawArgument(rawResult, typeof(T));
                    _tcs.TrySetResult((T)converted!);
                }
                catch (Exception ex)
                {
                    _tcs.TrySetException(ex);
                }
            }

            public override void SetException(Exception ex)
            {
                _tcs.TrySetException(ex);
            }
        }
    }
}
