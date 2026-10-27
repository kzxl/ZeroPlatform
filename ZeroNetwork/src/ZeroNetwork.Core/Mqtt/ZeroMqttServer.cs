using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.PubSub;

namespace ZeroNetwork.Mqtt
{
    /// <summary>
    /// Ultra-high-throughput, sovereign embedded MQTT v3.1.1 Broker for Industrial Automation &amp; Edge IoT.
    /// Pure C# BCL socket listener, zero external dependencies, executes without Windows Administrator privileges.
    /// Features sub-microsecond wildcard topic routing (+, #) via TopicTrie and seamless
    /// bi-directional bridging to <see cref="IZeroBus"/>.
    /// </summary>
    public class ZeroMqttServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly int _configuredPort;
        private int _boundPort;
        private readonly string _host;
        private readonly ConcurrentDictionary<string, ZeroMqttSession> _sessions = new ConcurrentDictionary<string, ZeroMqttSession>();
        private TopicTrie<string> _topicRouter = new TopicTrie<string>();
        private readonly object _lock = new object();

        private CancellationTokenSource? _serverCts;
        private bool _disposed;
        private long _messagesRouted;
        private IZeroBus? _bus;
        private IZeroSubscription? _busSubscription;

        /// <summary>
        /// Gets the listening port (defaults to standard MQTT 1883 or dynamic ephemeral port if configured with 0).
        /// </summary>
        public int Port => _boundPort > 0 ? _boundPort : _configuredPort;

        /// <summary>
        /// Gets whether the MQTT broker is currently running and accepting connections.
        /// </summary>
        public bool IsRunning => _serverCts != null && !_serverCts.IsCancellationRequested;

        /// <summary>
        /// Gets the number of currently connected MQTT client sessions.
        /// </summary>
        public int ConnectedClientsCount => _sessions.Count;

        /// <summary>
        /// Gets the total number of MQTT messages routed by the broker.
        /// </summary>
        public long MessagesRouted => Interlocked.Read(ref _messagesRouted);

        /// <summary>
        /// Event fired when a client successfully completes the MQTT handshake.
        /// </summary>
        public event Action<ZeroMqttSession>? ClientConnected;

        /// <summary>
        /// Event fired when a client disconnects.
        /// </summary>
        public event Action<ZeroMqttSession>? ClientDisconnected;

        /// <summary>
        /// Event fired when an MQTT message is published by any client.
        /// </summary>
        public event Action<string, byte[], MqttQoS>? MessagePublished;

        /// <summary>
        /// Initializes a new instance of <see cref="ZeroMqttServer"/> bound to the specified port and host.
        /// Pass port 0 to allow the operating system to dynamically assign a free ephemeral port.
        /// </summary>
        /// <param name="port">TCP port to bind (defaults to standard 1883, or 0 for ephemeral).</param>
        /// <param name="host">Bind host address (defaults to "0.0.0.0" for all interfaces).</param>
        /// <param name="bus">Optional <see cref="IZeroBus"/> instance to bridge published messages with.</param>
        public ZeroMqttServer(int port = 1883, string host = "0.0.0.0", IZeroBus? bus = null)
        {
            _configuredPort = port;
            _host = host;
            _bus = bus;

            IPAddress bindAddress = host == "0.0.0.0" ? IPAddress.Any : IPAddress.Parse(host);
            _listener = new TcpListener(bindAddress, port);

            if (_bus != null)
            {
                BridgeToBus(_bus);
            }
        }

        /// <summary>
        /// Bridges messages bidirectionally between this MQTT broker and an <see cref="IZeroBus"/> instance.
        /// </summary>
        /// <param name="bus">The target bus to bridge with.</param>
        /// <param name="topicPattern">Topic pattern to subscribe to on the bus (defaults to all topics "#").</param>
        /// <returns>A subscription token to dispose when unbridging.</returns>
        public IDisposable BridgeToBus(IZeroBus bus, string topicPattern = "#")
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            _bus = bus;
            _busSubscription?.Dispose();
            _busSubscription = bus.SubscribeTopic(topicPattern, async (topic, payload) =>
            {
                await PublishAsync(topic, payload.ToArray(), MqttQoS.AtMostOnce).ConfigureAwait(false);
            });
            return _busSubscription;
        }

        /// <summary>
        /// Starts the MQTT broker listening loop asynchronously.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed || IsRunning) return;

                _listener.Start();
                _boundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
                _serverCts = new CancellationTokenSource();
                var token = _serverCts.Token;

                _ = Task.Run(() => AcceptClientsLoopAsync(token), token);
            }
        }

        /// <summary>
        /// Stops the MQTT broker and terminates all client sessions.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!IsRunning) return;

                _serverCts?.Cancel();
                try { _listener.Stop(); } catch { }
                _boundPort = 0;

                foreach (var session in _sessions.Values)
                {
                    try { session.Dispose(); } catch { }
                }
                _sessions.Clear();
                _topicRouter = new TopicTrie<string>();

                _serverCts?.Dispose();
                _serverCts = null;
            }
        }

        /// <summary>
        /// Publishes an MQTT message to all matching subscribed clients from the broker server.
        /// </summary>
        public async Task PublishAsync(string topic, byte[] payload, MqttQoS qos = MqttQoS.AtMostOnce)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            if (payload == null) payload = Array.Empty<byte>();

            Interlocked.Increment(ref _messagesRouted);
            MessagePublished?.Invoke(topic, payload, qos);

            var matches = new List<string>();
            lock (_lock)
            {
                _topicRouter.GetMatches(topic.AsSpan(), matches);
            }

            for (int i = 0; i < matches.Count; i++)
            {
                if (_sessions.TryGetValue(matches[i], out var session) && session.IsOpen)
                {
                    try
                    {
                        await session.SendPublishAsync(topic, payload, qos).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore individual client transmission failure
                    }
                }
            }
        }

        private async Task AcceptClientsLoopAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient tcpClient = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    tcpClient.NoDelay = true;

                    var session = new ZeroMqttSession(tcpClient);

                    session.Connected += OnSessionConnected;
                    session.MessageReceived += OnSessionMessageReceived;
                    session.Subscribed += OnSessionSubscribed;
                    session.Unsubscribed += OnSessionUnsubscribed;
                    session.Disconnected += OnSessionDisconnected;

                    _ = Task.Run(() => session.RunReceiveLoopAsync(), cancellationToken);
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
                    // Backoff briefly on accept error
                    try { await Task.Delay(50, cancellationToken).ConfigureAwait(false); } catch { break; }
                }
            }
        }

        private void OnSessionConnected(ZeroMqttSession session)
        {
            if (!string.IsNullOrEmpty(session.ClientId))
            {
                _sessions[session.ClientId] = session;
                ClientConnected?.Invoke(session);
            }
        }

        private void OnSessionMessageReceived(ZeroMqttSession session, string topic, byte[] data, MqttQoS qos, ushort packetId)
        {
            _ = Task.Run(async () =>
            {
                await PublishAsync(topic, data, qos).ConfigureAwait(false);

                if (_bus != null)
                {
                    try
                    {
                        await _bus.PublishTopicAsync(topic, data).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Ignore bus publish failure
                    }
                }
            });
        }

        private void OnSessionSubscribed(ZeroMqttSession session, ushort packetId, (string TopicFilter, MqttQoS RequestedQos)[] subscriptions)
        {
            lock (_lock)
            {
                for (int i = 0; i < subscriptions.Length; i++)
                {
                    _topicRouter.Add(subscriptions[i].TopicFilter, session.ClientId);
                }
            }
        }

        private void OnSessionUnsubscribed(ZeroMqttSession session, ushort packetId, string[] topicFilters)
        {
            lock (_lock)
            {
                for (int i = 0; i < topicFilters.Length; i++)
                {
                    _topicRouter.Remove(topicFilters[i], session.ClientId);
                }
            }
        }

        private void OnSessionDisconnected(ZeroMqttSession session, Exception? exception)
        {
            if (!string.IsNullOrEmpty(session.ClientId))
            {
                _sessions.TryRemove(session.ClientId, out _);
                lock (_lock)
                {
                    foreach (var filter in session.SubscribedTopics)
                    {
                        _topicRouter.Remove(filter, session.ClientId);
                    }
                }
            }
            ClientDisconnected?.Invoke(session);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                _busSubscription?.Dispose();
                Stop();
            }
        }
    }
}
