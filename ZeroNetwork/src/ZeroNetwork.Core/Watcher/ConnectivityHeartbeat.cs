using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Diagnostics;
using ZeroNetwork.Discovery;

namespace ZeroNetwork.Watcher
{
    /// <summary>
    /// Event arguments for active connectivity state transitions.
    /// </summary>
    public class ConnectivityStatusEventArgs : EventArgs
    {
        public bool IsConnected { get; }
        public string ActiveTarget { get; }
        public long LatencyMs { get; }
        public DateTime Timestamp { get; }

        public ConnectivityStatusEventArgs(bool isConnected, string activeTarget, long latencyMs)
        {
            IsConnected = isConnected;
            ActiveTarget = activeTarget;
            LatencyMs = latencyMs;
            Timestamp = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Actively probes target network hosts (e.g. Default Gateway, local edge server, or DNS)
    /// to determine real-world network and WAN reachability beyond simple link-carrier status.
    /// </summary>
    public class ConnectivityHeartbeat : IDisposable
    {
        private readonly List<string> _targets = new List<string>();
        private readonly int _intervalMs;
        private readonly int _timeoutMs;
        private Timer? _heartbeatTimer;
        private readonly object _lock = new object();
        private bool _disposed;
        private bool _lastState;
        private bool _hasInitialized;

        public event EventHandler<ConnectivityStatusEventArgs>? ConnectivityChanged;

        public bool IsConnected => _lastState;

        public ConnectivityHeartbeat(IEnumerable<string>? targets = null, int intervalMs = 5000, int timeoutMs = 1000)
        {
            _intervalMs = Math.Max(1000, intervalMs);
            _timeoutMs = Math.Max(200, timeoutMs);

            if (targets != null)
            {
                foreach (var t in targets)
                {
                    if (!string.IsNullOrWhiteSpace(t)) _targets.Add(t.Trim());
                }
            }

            // Default fallback targets if none provided: Default Gateway, Cloudflare, Google DNS
            if (_targets.Count == 0)
            {
                string gw = NetworkInfo.GetDefaultGateway();
                if (!string.IsNullOrEmpty(gw)) _targets.Add(gw);
                _targets.Add("1.1.1.1");
                _targets.Add("8.8.8.8");
            }
        }

        /// <summary>
        /// Starts the background periodic connectivity polling.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed || _heartbeatTimer != null) return;
                _heartbeatTimer = new Timer(async _ => await PollAsync().ConfigureAwait(false), null, 0, _intervalMs);
            }
        }

        /// <summary>
        /// Stops the background polling.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                _heartbeatTimer?.Dispose();
                _heartbeatTimer = null;
            }
        }

        private async Task PollAsync()
        {
            bool connected = false;
            string responsiveTarget = string.Empty;
            long bestLatency = -1;

            string[] currentTargets;
            lock (_lock)
            {
                currentTargets = _targets.ToArray();
            }

            for (int i = 0; i < currentTargets.Length; i++)
            {
                string target = currentTargets[i];
                var result = await NetworkProbe.PingAsync(target, _timeoutMs).ConfigureAwait(false);
                if (result.Success)
                {
                    connected = true;
                    responsiveTarget = target;
                    bestLatency = result.RoundtripTimeMs;
                    break;
                }
            }

            bool fireEvent = false;
            lock (_lock)
            {
                if (_disposed) return;

                if (!_hasInitialized || _lastState != connected)
                {
                    _lastState = connected;
                    _hasInitialized = true;
                    fireEvent = true;
                }
            }

            if (fireEvent)
            {
                ConnectivityChanged?.Invoke(this, new ConnectivityStatusEventArgs(connected, responsiveTarget, bestLatency));
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                Stop();
            }
        }
    }
}
