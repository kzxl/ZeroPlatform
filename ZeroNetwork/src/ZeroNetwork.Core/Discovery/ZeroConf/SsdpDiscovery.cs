using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Discovery.ZeroConf
{
    /// <summary>
    /// Represents a discovered SSDP / UPnP device or service.
    /// </summary>
    public class SsdpDevice
    {
        public string Location { get; set; } = string.Empty;
        public string Usn { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public string St { get; set; } = string.Empty;
        public IPAddress? RemoteAddress { get; set; }
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public override string ToString() => $"[SSDP] {Usn} ({Location}) from {RemoteAddress}";
    }

    /// <summary>
    /// Simple Service Discovery Protocol (SSDP) client for zero-configuration discovery of
    /// UPnP devices, industrial smart cameras, networked printers, and edge services.
    /// </summary>
    public static class SsdpDiscovery
    {
        public const string SsdpMulticastAddress = "239.255.255.250";
        public const int SsdpPort = 1900;

        /// <summary>
        /// Discovers SSDP / UPnP devices on the local network within the specified timeout.
        /// </summary>
        /// <param name="searchTarget">Target service type or "ssdp:all" (default: "ssdp:all").</param>
        /// <param name="timeoutMs">Timeout in milliseconds to collect responses (default: 2000ms).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of discovered devices.</returns>
        public static async Task<List<SsdpDevice>> DiscoverAsync(
            string searchTarget = "ssdp:all",
            int timeoutMs = 2000,
            CancellationToken cancellationToken = default)
        {
            var devices = new List<SsdpDevice>();
            var seenUsns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string query =
                "M-SEARCH * HTTP/1.1\r\n" +
                $"HOST: {SsdpMulticastAddress}:{SsdpPort}\r\n" +
                "MAN: \"ssdp:discover\"\r\n" +
                "MX: 1\r\n" +
                $"ST: {searchTarget}\r\n" +
                "\r\n";

            byte[] queryBytes = Encoding.ASCII.GetBytes(query);

            using (var client = new UdpClient())
            {
                client.EnableBroadcast = true;
                client.Client.ReceiveTimeout = timeoutMs;

                var multicastEndpoint = new IPEndPoint(IPAddress.Parse(SsdpMulticastAddress), SsdpPort);
                await client.SendAsync(queryBytes, queryBytes.Length, multicastEndpoint).ConfigureAwait(false);

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(timeoutMs);

                    while (!cts.IsCancellationRequested)
                    {
                        try
                        {
                            var receiveTask = client.ReceiveAsync();
                            var timeoutTask = Task.Delay(timeoutMs, cts.Token);

                            var completed = await Task.WhenAny(receiveTask, timeoutTask).ConfigureAwait(false);
                            if (completed != receiveTask)
                            {
                                break;
                            }

                            var result = await receiveTask.ConfigureAwait(false);
                            string responseText = Encoding.UTF8.GetString(result.Buffer);

                            var device = ParseResponse(responseText, result.RemoteEndPoint.Address);
                            if (device != null && !string.IsNullOrEmpty(device.Usn))
                            {
                                if (seenUsns.Add(device.Usn))
                                {
                                    devices.Add(device);
                                }
                            }
                        }
                        catch
                        {
                            break;
                        }
                    }
                }
            }

            return devices;
        }

        private static SsdpDevice? ParseResponse(string responseText, IPAddress remoteAddress)
        {
            if (string.IsNullOrWhiteSpace(responseText)) return null;

            var device = new SsdpDevice { RemoteAddress = remoteAddress };
            string[] lines = responseText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    string key = line.Substring(0, colonIdx).Trim();
                    string val = line.Substring(colonIdx + 1).Trim();
                    device.Headers[key] = val;

                    if (key.Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
                        device.Location = val;
                    else if (key.Equals("USN", StringComparison.OrdinalIgnoreCase))
                        device.Usn = val;
                    else if (key.Equals("SERVER", StringComparison.OrdinalIgnoreCase))
                        device.Server = val;
                    else if (key.Equals("ST", StringComparison.OrdinalIgnoreCase))
                        device.St = val;
                }
            }

            return device;
        }
    }
}
