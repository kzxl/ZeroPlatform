using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Diagnostics;

namespace ZeroNetwork.Tests
{
    public class DiagnosticsTests
    {
        [Fact]
        public async Task IsPortOpenAsync_ClosedPort_ReturnsFalse()
        {
            // Port 59999 is typically unused and closed
            bool isOpen = await NetworkProbe.IsPortOpenAsync("127.0.0.1", 59999, timeoutMs: 200);
            Assert.False(isOpen);
        }

        [Fact]
        public async Task IsPortOpenAsync_OpenPort_ReturnsTrue()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            try
            {
                bool isOpen = await NetworkProbe.IsPortOpenAsync("127.0.0.1", port, timeoutMs: 1000);
                Assert.True(isOpen);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task IsPortOpenAsync_Cancelled_ReturnsFalseQuickly()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                bool isOpen = await NetworkProbe.IsPortOpenAsync("127.0.0.1", 59999, timeoutMs: 2000, cts.Token);
                Assert.False(isOpen);
            }
        }

        [Fact]
        public async Task PingAsync_Loopback_ReturnsSuccess()
        {
            var result = await NetworkProbe.PingAsync("127.0.0.1", timeoutMs: 1000, ttl: 64);
            Assert.True(result.Success);
            Assert.True(result.RoundtripTimeMs >= 0);
        }

        [Fact]
        public async Task PortScanner_ScanPortsAsync_DetectsOpenPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int openPort = ((IPEndPoint)listener.LocalEndpoint).Port;

            try
            {
                int[] testPorts = new[] { openPort, 59998, 59999 };
                var results = await PortScanner.ScanPortsAsync("127.0.0.1", testPorts, timeoutMs: 300, onlyOpenPorts: true);

                Assert.Single(results);
                Assert.Equal(openPort, results[0].Port);
                Assert.True(results[0].IsOpen);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task PortScanner_ScanPortsAsync_WithStreamingCallback_Invoked()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int openPort = ((IPEndPoint)listener.LocalEndpoint).Port;

            try
            {
                int[] testPorts = new[] { openPort, 59997 };
                var streamedResults = new List<PortScanResult>();

                await PortScanner.ScanPortsAsync(
                    "127.0.0.1",
                    testPorts,
                    timeoutMs: 300,
                    onlyOpenPorts: false,
                    onResult: res =>
                    {
                        lock (streamedResults) streamedResults.Add(res);
                    });

                Assert.Equal(2, streamedResults.Count);
            }
            finally
            {
                listener.Stop();
            }
        }

        [Fact]
        public async Task PathMtuDiscovery_Loopback_Succeeds()
        {
            int discoveredMtu = await PathMtuDiscovery.DiscoverMtuAsync("127.0.0.1", minMtu: 576, maxMtu: 1500, timeoutMs: 500);
            Assert.True(discoveredMtu >= 576, $"Expected MTU >= 576, got {discoveredMtu}");
        }

        [Fact]
        public void NetworkThroughputMeter_Lifecycle_Succeeds()
        {
            using (var meter = new NetworkThroughputMeter(sampleIntervalMs: 500))
            {
                meter.Start();
                Assert.True(meter.IsRunning);
                meter.Stop();
                Assert.False(meter.IsRunning);
            }
        }

        [Fact]
        public async Task DnsProbe_ResolveIPv4_IPString_ReturnsParsedIP()
        {
            // When already an IP address, DnsProbe returns immediately without network query
            var ip = await DnsProbe.ResolveIPv4Async("127.0.0.1");
            Assert.NotNull(ip);
            Assert.Equal(IPAddress.Loopback, ip);
        }
    }
}
