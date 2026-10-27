using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroComm.Core.P2P
{
    /// <summary>
    /// Lightweight RFC 5389 compliant STUN client for NAT traversal discovery.
    /// Provides zero-dependency UDP endpoint resolution across NAT firewalls.
    /// </summary>
    public static class StunClient
    {
        public static readonly string[] DefaultStunServers = new[]
        {
            "stun.l.google.com",
            "stun1.l.google.com",
            "stun2.l.google.com"
        };

        public const int DefaultStunPort = 19302;
        public const int DefaultTimeoutMs = 3000;
        public const int DefaultMaxRetries = 2;

        // STUN message protocol constants (RFC 5389)
        public const ushort BindingRequest = 0x0001;
        public const ushort BindingResponse = 0x0101;
        public const ushort MappedAddressAttr = 0x0001;
        public const ushort XorMappedAddressAttr = 0x0020;
        public const uint MagicCookie = 0x2112A442;

        /// <summary>
        /// Synchronously discovers the public endpoint (IP:Port) via STUN.
        /// </summary>
        public static IPEndPoint? GetPublicEndPoint(int localPort = 0, int timeoutMs = DefaultTimeoutMs)
        {
            foreach (var server in DefaultStunServers)
            {
                for (int retry = 0; retry < DefaultMaxRetries; retry++)
                {
                    try
                    {
                        var result = SendBindingRequest(server, DefaultStunPort, localPort, timeoutMs);
                        if (result != null) return result;
                    }
                    catch
                    {
                        // Fall through to next retry / server
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Synchronously discovers the public endpoint reusing an existing UdpClient socket.
        /// </summary>
        public static IPEndPoint? GetPublicEndPoint(UdpClient udpClient, string server = "stun.l.google.com", int port = DefaultStunPort, int timeoutMs = DefaultTimeoutMs)
        {
            for (int retry = 0; retry < DefaultMaxRetries; retry++)
            {
                try
                {
                    var result = SendBindingRequest(udpClient, server, port, timeoutMs);
                    if (result != null) return result;
                }
                catch
                {
                    // Fall through to next retry
                }
            }
            return null;
        }

        /// <summary>
        /// Asynchronously discovers the public endpoint (IP:Port) via STUN.
        /// </summary>
        public static async Task<IPEndPoint?> GetPublicEndPointAsync(string server = "stun.l.google.com", int port = DefaultStunPort, int timeoutMs = DefaultTimeoutMs, CancellationToken ct = default)
        {
            using (var client = new UdpClient(0))
            {
                return await GetPublicEndPointAsync(client, server, port, timeoutMs, ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously discovers public endpoint reusing an existing UdpClient socket.
        /// </summary>
        public static async Task<IPEndPoint?> GetPublicEndPointAsync(UdpClient client, string server = "stun.l.google.com", int port = DefaultStunPort, int timeoutMs = DefaultTimeoutMs, CancellationToken ct = default)
        {
            var addresses = await Dns.GetHostAddressesAsync(server).ConfigureAwait(false);
            if (addresses == null || addresses.Length == 0) return null;

            var serverEp = new IPEndPoint(addresses[0], port);
            var req = BuildBindingRequest();

#if NET8_0_OR_GREATER
            await client.SendAsync(req.Request, serverEp, ct).ConfigureAwait(false);
#else
            await client.SendAsync(req.Request, req.Request.Length, serverEp).ConfigureAwait(false);
#endif

            var receiveTask = client.ReceiveAsync();
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var timeoutTask = Task.Delay(timeoutMs, delayCts.Token);

            var completedTask = await Task.WhenAny(receiveTask, timeoutTask).ConfigureAwait(false);
            if (completedTask == receiveTask)
            {
                delayCts.Cancel();
                var response = await receiveTask.ConfigureAwait(false);
                return ParseBindingResponse(response.Buffer, req.TransactionId);
            }

            return null;
        }

        private static IPEndPoint? SendBindingRequest(string server, int port, int localPort, int timeoutMs)
        {
            using (var client = new UdpClient(localPort))
            {
                return SendBindingRequest(client, server, port, timeoutMs);
            }
        }

        private static IPEndPoint? SendBindingRequest(UdpClient client, string server, int port, int timeoutMs)
        {
            client.Client.ReceiveTimeout = timeoutMs;
            var req = BuildBindingRequest();

            var addresses = Dns.GetHostAddresses(server);
            if (addresses == null || addresses.Length == 0) return null;

            var serverEp = new IPEndPoint(addresses[0], port);
            client.Send(req.Request, req.Request.Length, serverEp);

            var remoteEp = new IPEndPoint(IPAddress.Any, 0);
            var response = client.Receive(ref remoteEp);

            return ParseBindingResponse(response, req.TransactionId);
        }

        /// <summary>
        /// Builds an RFC 5389 20-byte Binding Request message.
        /// </summary>
        internal static StunBindingRequest BuildBindingRequest()
        {
            var transactionId = new byte[12];
            new Random().NextBytes(transactionId);

            var request = new byte[20];
            // Message Type: Binding Request (0x0001)
            request[0] = 0x00;
            request[1] = 0x01;
            // Message Length: 0 (no attributes in initial request)
            request[2] = 0x00;
            request[3] = 0x00;
            // Magic Cookie (0x2112A442)
            request[4] = 0x21;
            request[5] = 0x12;
            request[6] = 0xA4;
            request[7] = 0x42;
            // Transaction ID (96 bits)
            Array.Copy(transactionId, 0, request, 8, 12);

            return new StunBindingRequest(request, transactionId);
        }

        /// <summary>
        /// Parses an RFC 5389 Binding Response packet, verifying cookie, transaction ID, and attributes.
        /// </summary>
        internal static IPEndPoint? ParseBindingResponse(byte[]? data, byte[] transactionId)
        {
            if (data == null || data.Length < 20)
                return null;

            // Verify Binding Response type (0x0101)
            ushort msgType = (ushort)((data[0] << 8) | data[1]);
            if (msgType != BindingResponse)
                return null;

            // Verify Magic Cookie
            uint cookie = (uint)((data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7]);
            if (cookie != MagicCookie)
                return null;

            // Verify Transaction ID
            for (int i = 0; i < 12; i++)
            {
                if (data[8 + i] != transactionId[i])
                    return null;
            }

            ushort msgLength = (ushort)((data[2] << 8) | data[3]);
            int offset = 20;

            // Parse attributes
            while (offset + 4 <= 20 + msgLength && offset + 4 <= data.Length)
            {
                ushort attrType = (ushort)((data[offset] << 8) | data[offset + 1]);
                ushort attrLength = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
                offset += 4;

                if (attrType == XorMappedAddressAttr && attrLength >= 8 && offset + attrLength <= data.Length)
                {
                    return ParseXorMappedAddress(data, offset);
                }
                else if (attrType == MappedAddressAttr && attrLength >= 8 && offset + attrLength <= data.Length)
                {
                    return ParseMappedAddress(data, offset);
                }

                // Align to 4-byte boundary
                offset += attrLength;
                if (offset % 4 != 0)
                {
                    offset += 4 - (offset % 4);
                }
            }

            return null;
        }

        private static IPEndPoint? ParseXorMappedAddress(byte[] data, int offset)
        {
            // Family: 0x01 = IPv4
            byte family = data[offset + 1];
            if (family != 0x01) return null;

            // XOR Port with high 16 bits of Magic Cookie (0x2112)
            ushort xorPort = (ushort)((data[offset + 2] << 8) | data[offset + 3]);
            int port = xorPort ^ 0x2112;

            // XOR IP with Magic Cookie
            byte[] ipBytes = new byte[4];
            ipBytes[0] = (byte)(data[offset + 4] ^ 0x21);
            ipBytes[1] = (byte)(data[offset + 5] ^ 0x12);
            ipBytes[2] = (byte)(data[offset + 6] ^ 0xA4);
            ipBytes[3] = (byte)(data[offset + 7] ^ 0x42);

            return new IPEndPoint(new IPAddress(ipBytes), port);
        }

        private static IPEndPoint? ParseMappedAddress(byte[] data, int offset)
        {
            byte family = data[offset + 1];
            if (family != 0x01) return null;

            int port = (data[offset + 2] << 8) | data[offset + 3];
            byte[] ipBytes = new byte[] { data[offset + 4], data[offset + 5], data[offset + 6], data[offset + 7] };

            return new IPEndPoint(new IPAddress(ipBytes), port);
        }

        /// <summary>
        /// Retrieves the primary non-loopback local IPv4 address.
        /// </summary>
        public static IPAddress GetLocalIPAddress()
        {
            try
            {
                using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect("8.8.8.8", 80);
                    if (socket.LocalEndPoint is IPEndPoint ep)
                    {
                        return ep.Address;
                    }
                }
            }
            catch
            {
                // Fallback to loopback
            }
            return IPAddress.Loopback;
        }
    }

    /// <summary>
    /// Holds the raw STUN binding request packet and transaction ID.
    /// </summary>
    internal readonly struct StunBindingRequest
    {
        public byte[] Request { get; }
        public byte[] TransactionId { get; }

        public StunBindingRequest(byte[] request, byte[] transactionId)
        {
            Request = request;
            TransactionId = transactionId;
        }
    }
}
