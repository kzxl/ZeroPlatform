using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Diagnostics
{
    /// <summary>
    /// Result structure for ping diagnostics.
    /// </summary>
    public readonly struct PingProbeResult
    {
        public bool Success { get; }
        public long RoundtripTimeMs { get; }
        public IPStatus Status { get; }
        public string Address { get; }

        public PingProbeResult(bool success, long roundtripTimeMs, IPStatus status, string address)
        {
            Success = success;
            RoundtripTimeMs = roundtripTimeMs;
            Status = status;
            Address = address;
        }

        public static PingProbeResult Failed(IPStatus status = IPStatus.Unknown, string address = "")
            => new PingProbeResult(false, -1, status, address);
    }

    /// <summary>
    /// High-performance, hardened network connectivity diagnostics and probing tools.
    /// Eliminates socket handle leaks and guarantees non-blocking execution across UI and background threads.
    /// </summary>
    public static class NetworkProbe
    {
        private static readonly byte[] DefaultPingBuffer = new byte[32];

        /// <summary>
        /// Asynchronously tests whether a specific TCP host and port is accepting connections within the specified timeout.
        /// Aborts socket immediately on timeout or cancellation, preventing background thread-pool socket leaks.
        /// </summary>
        /// <param name="host">Target IP or hostname.</param>
        /// <param name="port">Target TCP port.</param>
        /// <param name="timeoutMs">Timeout in milliseconds (default: 1000ms).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>True if connection succeeded; otherwise false.</returns>
        public static async Task<bool> IsPortOpenAsync(
            string host, 
            int port, 
            int timeoutMs = 1000, 
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0 || port > 65535)
                return false;

            Socket? socket = null;
            CancellationTokenSource? linkedCts = null;
            try
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    Blocking = false,
                    NoDelay = true
                };

                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(Math.Max(10, timeoutMs));

                // Register socket close on cancellation to abort any pending connect
                using (linkedCts.Token.Register(() =>
                {
                    try { socket?.Close(); } catch { }
                }))
                {
                    var connectTask = Task.Factory.FromAsync(
                        socket.BeginConnect,
                        socket.EndConnect,
                        host,
                        port,
                        null);

                    await connectTask.ConfigureAwait(false);
                    return socket.Connected;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                linkedCts?.Dispose();
                if (socket != null)
                {
                    try { socket.Close(); } catch { }
                    socket.Dispose();
                }
            }
        }

        /// <summary>
        /// Asynchronously sends an ICMP echo ping to the specified host with optional TTL and fragmentation options.
        /// </summary>
        /// <param name="host">Target IP or hostname.</param>
        /// <param name="timeoutMs">Timeout in milliseconds (default: 1000ms).</param>
        /// <param name="ttl">Time to live hop count (default: 64).</param>
        /// <param name="dontFragment">Don't fragment flag for Path MTU tests.</param>
        /// <param name="buffer">Custom buffer payload or null for default 32-byte payload.</param>
        /// <returns>PingProbeResult containing roundtrip time and status.</returns>
        public static async Task<PingProbeResult> PingAsync(
            string host,
            int timeoutMs = 1000,
            int ttl = 64,
            bool dontFragment = false,
            byte[]? buffer = null)
        {
            if (string.IsNullOrWhiteSpace(host))
                return PingProbeResult.Failed(address: host);

            try
            {
                using (var ping = new Ping())
                {
                    var options = new PingOptions(Math.Max(1, Math.Min(255, ttl)), dontFragment);
                    byte[] sendBuffer = buffer ?? DefaultPingBuffer;

                    var reply = await ping.SendPingAsync(host, timeoutMs, sendBuffer, options).ConfigureAwait(false);
                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        return new PingProbeResult(
                            true,
                            reply.RoundtripTime,
                            reply.Status,
                            reply.Address?.ToString() ?? host);
                    }

                    return PingProbeResult.Failed(reply?.Status ?? IPStatus.Unknown, reply?.Address?.ToString() ?? host);
                }
            }
            catch
            {
                return PingProbeResult.Failed(address: host);
            }
        }
    }
}
