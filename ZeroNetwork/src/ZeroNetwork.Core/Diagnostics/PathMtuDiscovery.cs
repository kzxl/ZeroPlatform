using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Diagnostics
{
    /// <summary>
    /// Path Maximum Transmission Unit (PMTU) discovery tool.
    /// Determines the maximum unfragmented packet size supported across an industrial network path,
    /// crucial for validating Jumbo Frame (MTU 9000) settings for GigE Vision cameras and high-throughput telemetry.
    /// </summary>
    public static class PathMtuDiscovery
    {
        private const int IpHdrSize = 20;
        private const int IcmpHdrSize = 8;
        private const int HeadersOverhead = IpHdrSize + IcmpHdrSize; // 28 bytes

        /// <summary>
        /// Discovers the maximum allowable MTU (in bytes) between the local machine and the destination host
        /// using binary search with Don't Fragment (DF) ICMP packets.
        /// </summary>
        /// <param name="targetHost">Destination hostname or IP address.</param>
        /// <param name="minMtu">Minimum MTU threshold to test (default: 576 bytes).</param>
        /// <param name="maxMtu">Maximum MTU threshold to test (default: 9000 bytes for Jumbo Frames).</param>
        /// <param name="timeoutMs">Timeout per probe in milliseconds (default: 800ms).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The highest discovered unfragmented MTU size in bytes, or -1 if unreachable.</returns>
        public static async Task<int> DiscoverMtuAsync(
            string targetHost,
            int minMtu = 576,
            int maxMtu = 9000,
            int timeoutMs = 800,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetHost))
                throw new ArgumentNullException(nameof(targetHost));

            minMtu = Math.Max(HeadersOverhead + 1, minMtu);
            maxMtu = Math.Max(minMtu, maxMtu);

            // First verify baseline connectivity
            var baseline = await NetworkProbe.PingAsync(targetHost, timeoutMs, dontFragment: false).ConfigureAwait(false);
            if (!baseline.Success)
            {
                return -1;
            }

            int low = minMtu;
            int high = maxMtu;
            int bestMtu = -1;

            while (low <= high)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int midMtu = low + ((high - low) / 2);
                int payloadSize = midMtu - HeadersOverhead;

                byte[] buffer = new byte[payloadSize];

                var probe = await NetworkProbe.PingAsync(
                    targetHost,
                    timeoutMs: timeoutMs,
                    ttl: 64,
                    dontFragment: true,
                    buffer: buffer).ConfigureAwait(false);

                if (probe.Success)
                {
                    bestMtu = midMtu;
                    // Try higher MTU
                    low = midMtu + 1;
                }
                else
                {
                    // Too large or fragmented, search lower
                    high = midMtu - 1;
                }
            }

            return bestMtu;
        }
    }
}
