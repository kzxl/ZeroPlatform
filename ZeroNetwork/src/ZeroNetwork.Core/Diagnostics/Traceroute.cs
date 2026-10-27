using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Diagnostics
{
    /// <summary>
    /// Represents a single routing hop in a network traceroute.
    /// </summary>
    public readonly struct TracerouteHop
    {
        public int Hop { get; }
        public string Address { get; }
        public long RoundtripTimeMs { get; }
        public IPStatus Status { get; }

        public TracerouteHop(int hop, string address, long roundtripTimeMs, IPStatus status)
        {
            Hop = hop;
            Address = address;
            RoundtripTimeMs = roundtripTimeMs;
            Status = status;
        }

        public override string ToString() => $"Hop {Hop,2}: {Address,-15} ({RoundtripTimeMs}ms) [{Status}]";
    }

    /// <summary>
    /// High-performance asynchronous Traceroute engine using incrementing ICMP TTL.
    /// Identifies routing hops, inter-switch latency, and path topology bottlenecks.
    /// </summary>
    public static class Traceroute
    {
        /// <summary>
        /// Traces the routing path to a target host or IP address.
        /// </summary>
        /// <param name="targetHost">Destination hostname or IP address.</param>
        /// <param name="maxHops">Maximum number of intermediate hops (default: 30).</param>
        /// <param name="timeoutMs">Timeout in milliseconds per hop (default: 1000ms).</param>
        /// <param name="onHopDiscovered">Optional callback invoked in real-time as each hop is determined.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered hops along the network route.</returns>
        public static async Task<List<TracerouteHop>> TraceRouteAsync(
            string targetHost,
            int maxHops = 30,
            int timeoutMs = 1000,
            Action<TracerouteHop>? onHopDiscovered = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetHost))
                throw new ArgumentNullException(nameof(targetHost));

            var hops = new List<TracerouteHop>();
            maxHops = Math.Max(1, Math.Min(128, maxHops));

            for (int ttl = 1; ttl <= maxHops; ttl++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var probe = await NetworkProbe.PingAsync(
                    targetHost,
                    timeoutMs: timeoutMs,
                    ttl: ttl,
                    dontFragment: true).ConfigureAwait(false);

                var hop = new TracerouteHop(
                    ttl,
                    string.IsNullOrEmpty(probe.Address) ? "*" : probe.Address,
                    probe.RoundtripTimeMs >= 0 ? probe.RoundtripTimeMs : -1,
                    probe.Status);

                hops.Add(hop);
                onHopDiscovered?.Invoke(hop);

                // Reached destination target
                if (probe.Status == IPStatus.Success)
                {
                    break;
                }
            }

            return hops;
        }
    }
}
