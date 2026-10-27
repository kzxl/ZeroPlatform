using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.Diagnostics
{
    /// <summary>
    /// Fast, non-blocking UDP DNS client for forward (A) and reverse (PTR in-addr.arpa) record lookups.
    /// Operates with strict, sub-second configurable timeouts (e.g. 200–500ms) to prevent UI/thread freezes
    /// caused by the 15-second default blocking timeout of the operating system DNS resolver.
    /// </summary>
    public static class DnsProbe
    {
        public const int DefaultDnsPort = 53;
        public const string DefaultPrimaryDns = "1.1.1.1";

        /// <summary>
        /// Asynchronously resolves an IPv4 address for the specified hostname via standard UDP DNS query.
        /// </summary>
        public static async Task<IPAddress?> ResolveIPv4Async(
            string hostName,
            string dnsServer = DefaultPrimaryDns,
            int timeoutMs = 500,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostName)) return null;

            if (IPAddress.TryParse(hostName, out var alreadyIp))
                return alreadyIp;

            try
            {
                byte[] query = BuildDnsQuery(hostName, qType: 1); // QTYPE 1 = A
                byte[]? response = await QueryDnsServerAsync(dnsServer, query, timeoutMs, cancellationToken).ConfigureAwait(false);
                if (response == null || response.Length < 12) return null;

                return ParseARecordResponse(response);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Asynchronously performs a reverse DNS lookup (PTR in-addr.arpa) to resolve a hostname from an IPv4 address.
        /// </summary>
        public static async Task<string?> ResolveReversePtrAsync(
            IPAddress ip,
            string dnsServer = DefaultPrimaryDns,
            int timeoutMs = 500,
            CancellationToken cancellationToken = default)
        {
            if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork)
                return null;

            byte[] bytes = ip.GetAddressBytes();
            string ptrDomain = $"{bytes[3]}.{bytes[2]}.{bytes[1]}.{bytes[0]}.in-addr.arpa";

            try
            {
                byte[] query = BuildDnsQuery(ptrDomain, qType: 12); // QTYPE 12 = PTR
                byte[]? response = await QueryDnsServerAsync(dnsServer, query, timeoutMs, cancellationToken).ConfigureAwait(false);
                if (response == null || response.Length < 12) return null;

                return ParsePtrRecordResponse(response);
            }
            catch
            {
                return null;
            }
        }

        private static async Task<byte[]?> QueryDnsServerAsync(
            string dnsServer,
            byte[] queryPacket,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            if (!IPAddress.TryParse(dnsServer, out var dnsIp))
            {
                dnsIp = IPAddress.Parse(DefaultPrimaryDns);
            }

            using (var udp = new UdpClient())
            {
                var targetEp = new IPEndPoint(dnsIp, DefaultDnsPort);
                await udp.SendAsync(queryPacket, queryPacket.Length, targetEp).ConfigureAwait(false);

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(Math.Max(50, timeoutMs));
                    var receiveTask = udp.ReceiveAsync();
                    var timeoutTask = Task.Delay(timeoutMs, cts.Token);

                    var completed = await Task.WhenAny(receiveTask, timeoutTask).ConfigureAwait(false);
                    if (completed == receiveTask)
                    {
                        var res = await receiveTask.ConfigureAwait(false);
                        return res.Buffer;
                    }
                }
            }

            return null;
        }

        private static byte[] BuildDnsQuery(string domain, ushort qType)
        {
            var buffer = new List<byte>(64);
            ushort id = (ushort)new Random().Next(1, 65535);

            // Header (12 bytes)
            buffer.Add((byte)(id >> 8));
            buffer.Add((byte)(id & 0xFF));
            buffer.Add(0x01); // Flags: standard query, recursion desired (0x0100)
            buffer.Add(0x00);
            buffer.Add(0x00); // QDCOUNT: 1 question
            buffer.Add(0x01);
            buffer.Add(0x00); // ANCOUNT: 0
            buffer.Add(0x00);
            buffer.Add(0x00); // NSCOUNT: 0
            buffer.Add(0x00);
            buffer.Add(0x00); // ARCOUNT: 0
            buffer.Add(0x00);

            // Question: Domain labels
            string[] labels = domain.Trim('.').Split('.');
            foreach (var label in labels)
            {
                byte[] labelBytes = Encoding.ASCII.GetBytes(label);
                buffer.Add((byte)labelBytes.Length);
                buffer.AddRange(labelBytes);
            }
            buffer.Add(0x00); // Zero length label ends domain

            // QTYPE
            buffer.Add((byte)(qType >> 8));
            buffer.Add((byte)(qType & 0xFF));

            // QCLASS (IN = 1)
            buffer.Add(0x00);
            buffer.Add(0x01);

            return buffer.ToArray();
        }

        private static IPAddress? ParseARecordResponse(byte[] response)
        {
            int offset = 12; // Skip 12-byte header
            // Skip Question QNAME
            while (offset < response.Length && response[offset] != 0)
            {
                if ((response[offset] & 0xC0) == 0xC0) // Pointer compression
                {
                    offset += 2;
                    break;
                }
                offset += response[offset] + 1;
            }
            if (offset < response.Length && response[offset] == 0) offset++;

            offset += 4; // Skip QTYPE and QCLASS

            // Parse Answer RRs
            while (offset + 10 <= response.Length)
            {
                // Skip Name
                if ((response[offset] & 0xC0) == 0xC0) offset += 2;
                else
                {
                    while (offset < response.Length && response[offset] != 0) offset += response[offset] + 1;
                    if (offset < response.Length) offset++;
                }

                if (offset + 10 > response.Length) break;

                ushort type = (ushort)((response[offset] << 8) | response[offset + 1]);
                offset += 8; // Skip TYPE (2), CLASS (2), TTL (4)

                ushort rdLength = (ushort)((response[offset] << 8) | response[offset + 1]);
                offset += 2;

                if (type == 1 && rdLength == 4 && offset + 4 <= response.Length) // Type A
                {
                    byte[] ipBytes = new byte[4];
                    Buffer.BlockCopy(response, offset, ipBytes, 0, 4);
                    return new IPAddress(ipBytes);
                }

                offset += rdLength;
            }

            return null;
        }

        private static string? ParsePtrRecordResponse(byte[] response)
        {
            int offset = 12; // Skip 12-byte header
            // Skip Question
            while (offset < response.Length && response[offset] != 0)
            {
                if ((response[offset] & 0xC0) == 0xC0) { offset += 2; break; }
                offset += response[offset] + 1;
            }
            if (offset < response.Length && response[offset] == 0) offset++;
            offset += 4; // Skip QTYPE and QCLASS

            // Parse Answer RRs
            while (offset + 10 <= response.Length)
            {
                if ((response[offset] & 0xC0) == 0xC0) offset += 2;
                else
                {
                    while (offset < response.Length && response[offset] != 0) offset += response[offset] + 1;
                    if (offset < response.Length) offset++;
                }

                if (offset + 10 > response.Length) break;
                ushort type = (ushort)((response[offset] << 8) | response[offset + 1]);
                offset += 8; // Skip TYPE, CLASS, TTL
                ushort rdLength = (ushort)((response[offset] << 8) | response[offset + 1]);
                offset += 2;

                if (type == 12) // Type PTR
                {
                    return ReadDomainLabels(response, offset);
                }

                offset += rdLength;
            }

            return null;
        }

        private static string ReadDomainLabels(byte[] buffer, int offset)
        {
            var sb = new StringBuilder();
            int current = offset;
            int maxHops = 10;

            while (current < buffer.Length && buffer[current] != 0 && maxHops-- > 0)
            {
                if ((buffer[current] & 0xC0) == 0xC0)
                {
                    int pointer = ((buffer[current] & 0x3F) << 8) | buffer[current + 1];
                    current = pointer;
                    continue;
                }

                int len = buffer[current++];
                if (current + len > buffer.Length) break;

                if (sb.Length > 0) sb.Append('.');
                sb.Append(Encoding.ASCII.GetString(buffer, current, len));
                current += len;
            }

            return sb.ToString();
        }
    }
}
