using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using ZeroNetwork.Discovery;

namespace ZeroNetwork.Diagnostics
{
    /// <summary>
    /// Telemetry data sample representing instantaneous network bandwidth, throughput, and link saturation.
    /// </summary>
    public class NetworkThroughputSample
    {
        public string AdapterName { get; set; } = string.Empty;
        public double RxMbps { get; set; }
        public double TxMbps { get; set; }
        public long BytesReceivedDelta { get; set; }
        public long BytesSentDelta { get; set; }
        public long LinkSpeedBps { get; set; }
        public double SaturationPercentage { get; set; }
        public long IncomingPacketsDiscardedDelta { get; set; }
        public long OutgoingPacketsDiscardedDelta { get; set; }
        public DateTime Timestamp { get; set; }

        public override string ToString() =>
            $"[{AdapterName}] Rx: {RxMbps:F2} Mbps | Tx: {TxMbps:F2} Mbps | Saturation: {SaturationPercentage:F1}%";
    }

    /// <summary>
    /// Monitors real-time network interface bandwidth consumption, packet transmission rates,
    /// and detects network saturation warnings (essential for industrial GigE Vision cameras and high-frequency telemetry).
    /// </summary>
    public class NetworkThroughputMeter : IDisposable
    {
        private readonly string? _targetAdapterName;
        private readonly int _sampleIntervalMs;
        private readonly double _saturationWarningThreshold;
        private Timer? _sampleTimer;
        private readonly object _lock = new object();
        private bool _disposed;

        private long _lastBytesReceived;
        private long _lastBytesSent;
        private long _lastInDiscarded;
        private long _lastOutDiscarded;
        private long _lastTimestampTicks;
        private bool _hasBaseline;

        public event EventHandler<NetworkThroughputSample>? Sampled;
        public event EventHandler<NetworkThroughputSample>? SaturationWarning;

        public bool IsRunning => _sampleTimer != null;

        public NetworkThroughputMeter(
            string? adapterName = null,
            int sampleIntervalMs = 1000,
            double saturationWarningThreshold = 85.0)
        {
            _targetAdapterName = adapterName;
            _sampleIntervalMs = Math.Max(200, sampleIntervalMs);
            _saturationWarningThreshold = Math.Max(1.0, Math.Min(100.0, saturationWarningThreshold));
        }

        /// <summary>
        /// Starts collecting real-time throughput metrics.
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_disposed || _sampleTimer != null) return;
                _hasBaseline = false;
                _sampleTimer = new Timer(_ => CollectSample(), null, 0, _sampleIntervalMs);
            }
        }

        /// <summary>
        /// Stops collecting throughput metrics.
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                _sampleTimer?.Dispose();
                _sampleTimer = null;
            }
        }

        private void CollectSample()
        {
            try
            {
                NetworkInterface? nic = ResolveTargetNic();
                if (nic == null) return;

                var stats = nic.GetIPv4Statistics();
                long currentBytesRecv = stats.BytesReceived;
                long currentBytesSent = stats.BytesSent;
                long currentInDiscard = stats.IncomingPacketsDiscarded;
                long currentOutDiscard = stats.OutgoingPacketsDiscarded;
                long currentTicks = Stopwatch.GetTimestamp();

                if (!_hasBaseline)
                {
                    _lastBytesReceived = currentBytesRecv;
                    _lastBytesSent = currentBytesSent;
                    _lastInDiscarded = currentInDiscard;
                    _lastOutDiscarded = currentOutDiscard;
                    _lastTimestampTicks = currentTicks;
                    _hasBaseline = true;
                    return;
                }

                double elapsedSec = (double)(currentTicks - _lastTimestampTicks) / Stopwatch.Frequency;
                if (elapsedSec <= 0.0001) return;

                long deltaRecv = Math.Max(0, currentBytesRecv - _lastBytesReceived);
                long deltaSent = Math.Max(0, currentBytesSent - _lastBytesSent);
                long deltaInDiscard = Math.Max(0, currentInDiscard - _lastInDiscarded);
                long deltaOutDiscard = Math.Max(0, currentOutDiscard - _lastOutDiscarded);

                _lastBytesReceived = currentBytesRecv;
                _lastBytesSent = currentBytesSent;
                _lastInDiscarded = currentInDiscard;
                _lastOutDiscarded = currentOutDiscard;
                _lastTimestampTicks = currentTicks;

                double rxBps = (deltaRecv * 8.0) / elapsedSec;
                double txBps = (deltaSent * 8.0) / elapsedSec;
                double rxMbps = rxBps / 1_000_000.0;
                double txMbps = txBps / 1_000_000.0;

                long linkSpeed = nic.Speed > 0 ? nic.Speed : 1_000_000_000; // Default 1 Gbps if undefined
                double maxBps = Math.Max(rxBps, txBps);
                double saturationPct = (maxBps / linkSpeed) * 100.0;

                var sample = new NetworkThroughputSample
                {
                    AdapterName = nic.Name,
                    RxMbps = rxMbps,
                    TxMbps = txMbps,
                    BytesReceivedDelta = deltaRecv,
                    BytesSentDelta = deltaSent,
                    LinkSpeedBps = linkSpeed,
                    SaturationPercentage = saturationPct,
                    IncomingPacketsDiscardedDelta = deltaInDiscard,
                    OutgoingPacketsDiscardedDelta = deltaOutDiscard,
                    Timestamp = DateTime.UtcNow
                };

                Sampled?.Invoke(this, sample);

                if (saturationPct >= _saturationWarningThreshold)
                {
                    SaturationWarning?.Invoke(this, sample);
                }
            }
            catch
            {
                // Suppress inspection errors
            }
        }

        private NetworkInterface? ResolveTargetNic()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            if (!string.IsNullOrWhiteSpace(_targetAdapterName))
            {
                foreach (var n in nics)
                {
                    if (n.Name.Equals(_targetAdapterName, StringComparison.OrdinalIgnoreCase) ||
                        n.Description.Equals(_targetAdapterName, StringComparison.OrdinalIgnoreCase))
                    {
                        return n;
                    }
                }
            }

            // Fallback: choose primary operational physical NIC
            foreach (var n in nics)
            {
                if (n.OperationalStatus == OperationalStatus.Up && !VirtualAdapterFilter.IsVirtual(n))
                {
                    return n;
                }
            }

            return nics.Length > 0 ? nics[0] : null;
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
