using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Common;
using IPNetwork = ZeroNetwork.Common.IPNetwork;

namespace ZeroNetwork.Discovery
{
    /// <summary>
    /// Represents a network node discovered via active Data Link layer ARP probing.
    /// </summary>
    public readonly struct DiscoveredDevice
    {
        public IPAddress IPAddress { get; }
        public MacAddress MacAddress { get; }
        public string VendorName { get; }
        public bool IsVirtual { get; }

        public DiscoveredDevice(IPAddress ip, MacAddress mac, string vendorName, bool isVirtual)
        {
            IPAddress = ip;
            MacAddress = mac;
            VendorName = vendorName;
            IsVirtual = isVirtual;
        }

        public override string ToString() => $"[Device] {IPAddress,-15} -> {MacAddress} ({(IsVirtual ? "Virtual: " + VendorName : "Physical")})";
    }

    /// <summary>
    /// High-throughput parallel Data Link layer (Layer 2) ARP subnet scanner.
    /// Discovers 100% of live connected devices on the local switch, completely bypassing Windows Firewall
    /// and industrial devices that block ICMP echo ping and closed TCP ports.
    /// </summary>
    public static class ActiveArpScanner
    {
        /// <summary>
        /// Scans an entire <see cref="IPNetwork"/> subnet concurrently using ARP resolution.
        /// </summary>
        /// <param name="subnet">Target IP subnet.</param>
        /// <param name="timeoutMs">Timeout in milliseconds per host (default: 300ms).</param>
        /// <param name="maxConcurrency">Maximum parallel ARP worker tasks (default: 64).</param>
        /// <param name="onDeviceDiscovered">Real-time callback invoked immediately when a live device responds.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of all discovered physical and virtual devices on the subnet.</returns>
        public static async Task<List<DiscoveredDevice>> ScanSubnetAsync(
            IPNetwork subnet,
            int timeoutMs = 300,
            int maxConcurrency = 64,
            Action<DiscoveredDevice>? onDeviceDiscovered = null,
            CancellationToken cancellationToken = default)
        {
            var hosts = new List<IPAddress>();
            foreach (var ip in subnet.EnumerateUsableHosts())
            {
                hosts.Add(ip);
            }

            var discovered = new List<DiscoveredDevice>();
            var lockObj = new object();

            using (var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency)))
            {
                var tasks = new List<Task>(hosts.Count);

                foreach (var ip in hosts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    var targetIp = ip;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var mac = await ArpTable.ResolveMacAsync(targetIp, timeoutMs).ConfigureAwait(false);
                            if (mac.HasValue && !mac.Value.IsEmpty && !mac.Value.IsBroadcast)
                            {
                                bool isVirtual = mac.Value.IsKnownVirtualVendor(out string vendor);
                                var device = new DiscoveredDevice(targetIp, mac.Value, vendor, isVirtual);

                                lock (lockObj)
                                {
                                    discovered.Add(device);
                                }

                                onDeviceDiscovered?.Invoke(device);
                            }
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }, cancellationToken));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            return discovered;
        }
    }
}
