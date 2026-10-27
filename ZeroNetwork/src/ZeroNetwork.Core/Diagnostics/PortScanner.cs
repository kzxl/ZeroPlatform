using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ZeroNetwork.Common;
using IPNetwork = ZeroNetwork.Common.IPNetwork;

namespace ZeroNetwork.Diagnostics
{
    /// <summary>
    /// Represents the outcome of an individual host port probe.
    /// </summary>
    public readonly struct PortScanResult
    {
        public string Host { get; }
        public int Port { get; }
        public bool IsOpen { get; }
        public long LatencyMs { get; }

        public PortScanResult(string host, int port, bool isOpen, long latencyMs)
        {
            Host = host;
            Port = port;
            IsOpen = isOpen;
            LatencyMs = latencyMs;
        }

        public override string ToString() => $"[{Host}:{Port}] Open: {IsOpen} ({LatencyMs}ms)";
    }

    /// <summary>
    /// High-throughput asynchronous subnet and port range scanner with bounded concurrency and real-time streaming.
    /// </summary>
    public static class PortScanner
    {
        /// <summary>
        /// Scans a range of TCP ports on a single target host with optional real-time streaming callback.
        /// </summary>
        public static async Task<List<PortScanResult>> ScanPortRangeAsync(
            string host,
            int startPort,
            int endPort,
            int timeoutMs = 500,
            int maxConcurrency = 64,
            bool onlyOpenPorts = false,
            Action<PortScanResult>? onResult = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentNullException(nameof(host));
            if (startPort <= 0 || endPort > 65535 || startPort > endPort)
                throw new ArgumentOutOfRangeException("Invalid port range.");

            int count = endPort - startPort + 1;
            var ports = new int[count];
            for (int i = 0; i < count; i++)
            {
                ports[i] = startPort + i;
            }

            return await ScanPortsAsync(host, ports, timeoutMs, maxConcurrency, onlyOpenPorts, onResult, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Scans an array of TCP ports on a single target host with optional real-time streaming callback.
        /// </summary>
        public static async Task<List<PortScanResult>> ScanPortsAsync(
            string host,
            int[] ports,
            int timeoutMs = 500,
            int maxConcurrency = 64,
            bool onlyOpenPorts = false,
            Action<PortScanResult>? onResult = null,
            CancellationToken cancellationToken = default)
        {
            if (ports == null || ports.Length == 0) return new List<PortScanResult>();

            var results = new List<PortScanResult>(ports.Length);
            using (var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency)))
            {
                var tasks = new List<Task>();
                var lockObj = new object();

                foreach (int port in ports)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    int targetPort = port;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var sw = Stopwatch.StartNew();
                            bool isOpen = await NetworkProbe.IsPortOpenAsync(host, targetPort, timeoutMs, cancellationToken).ConfigureAwait(false);
                            sw.Stop();

                            if (!onlyOpenPorts || isOpen)
                            {
                                var res = new PortScanResult(host, targetPort, isOpen, sw.ElapsedMilliseconds);
                                lock (lockObj)
                                {
                                    results.Add(res);
                                }
                                onResult?.Invoke(res);
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

            return results;
        }

        /// <summary>
        /// Scans a specific TCP port across all usable hosts in an <see cref="IPNetwork"/> subnet with optional real-time streaming callback.
        /// </summary>
        public static async Task<List<PortScanResult>> ScanSubnetAsync(
            IPNetwork subnet,
            int port,
            int timeoutMs = 500,
            int maxConcurrency = 64,
            bool onlyOpenPorts = true,
            Action<PortScanResult>? onResult = null,
            CancellationToken cancellationToken = default)
        {
            var hosts = new List<string>();
            foreach (var ip in subnet.EnumerateUsableHosts())
            {
                hosts.Add(ip.ToString());
            }

            var results = new List<PortScanResult>();
            using (var semaphore = new SemaphoreSlim(Math.Max(1, maxConcurrency)))
            {
                var tasks = new List<Task>();
                var lockObj = new object();

                foreach (string host in hosts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

                    string targetHost = host;
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            var sw = Stopwatch.StartNew();
                            bool isOpen = await NetworkProbe.IsPortOpenAsync(targetHost, port, timeoutMs, cancellationToken).ConfigureAwait(false);
                            sw.Stop();

                            if (!onlyOpenPorts || isOpen)
                            {
                                var res = new PortScanResult(targetHost, port, isOpen, sw.ElapsedMilliseconds);
                                lock (lockObj)
                                {
                                    results.Add(res);
                                }
                                onResult?.Invoke(res);
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

            return results;
        }
    }
}
