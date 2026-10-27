using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using ZeroNetwork.Common;
using IPNetwork = ZeroNetwork.Common.IPNetwork;

namespace ZeroNetwork.Discovery
{
    /// <summary>
    /// Constructs and broadcasts standard 102-byte Wake-on-LAN (WoL) Magic Packets (and 108-byte SecureOn password protected packets)
    /// to remotely power on network equipment, industrial PCs, and edge nodes.
    /// </summary>
    public static class WakeOnLan
    {
        public const int DefaultPort = 9;

        /// <summary>
        /// Creates the Magic Packet binary payload for the specified MAC address.
        /// Standard: 102 bytes (6 bytes of 0xFF followed by 16 repetitions of the 6-byte target MAC address).
        /// SecureOn: 108 bytes (102 bytes standard payload + 6 bytes SecureOn password).
        /// </summary>
        public static byte[] CreateMagicPacket(MacAddress mac, byte[]? secureOnPassword = null)
        {
            int size = (secureOnPassword != null && secureOnPassword.Length == 6) ? 108 : 102;
            byte[] packet = new byte[size];

            // First 6 bytes are 0xFF
            for (int i = 0; i < 6; i++)
            {
                packet[i] = 0xFF;
            }

            // 16 repetitions of 6-byte MAC address
            byte[] macBytes = mac.GetAddressBytes();
            for (int rep = 0; rep < 16; rep++)
            {
                Buffer.BlockCopy(macBytes, 0, packet, 6 + (rep * 6), 6);
            }

            // Append optional 6-byte SecureOn password
            if (size == 108 && secureOnPassword != null)
            {
                Buffer.BlockCopy(secureOnPassword, 0, packet, 102, 6);
            }

            return packet;
        }

        /// <summary>
        /// Sends a Wake-on-LAN Magic Packet via UDP broadcast.
        /// </summary>
        /// <param name="mac">Target physical MAC address.</param>
        /// <param name="port">Target UDP port (default: 9, alternative: 7).</param>
        /// <param name="broadcastAddress">Optional broadcast address (default: 255.255.255.255).</param>
        /// <param name="secureOnPassword">Optional 6-byte SecureOn password.</param>
        public static async Task SendAsync(
            MacAddress mac,
            int port = DefaultPort,
            IPAddress? broadcastAddress = null,
            byte[]? secureOnPassword = null)
        {
            if (mac.IsEmpty) throw new ArgumentException("Target MAC address cannot be empty.", nameof(mac));

            byte[] magicPacket = CreateMagicPacket(mac, secureOnPassword);
            IPAddress targetBroadcast = broadcastAddress ?? IPAddress.Broadcast;

            using (var client = new UdpClient())
            {
                client.EnableBroadcast = true;
                var endpoint = new IPEndPoint(targetBroadcast, port);
                await client.SendAsync(magicPacket, magicPacket.Length, endpoint).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Sends a Wake-on-LAN Magic Packet directed to a specific subnet's broadcast address (e.g. 192.168.1.255).
        /// Allows Magic Packets to traverse IP routers configured with directed-broadcast forwarding.
        /// </summary>
        public static Task BroadcastToSubnetAsync(
            MacAddress mac,
            IPNetwork subnet,
            int port = DefaultPort,
            byte[]? secureOnPassword = null)
        {
            return SendAsync(mac, port, subnet.BroadcastAddress, secureOnPassword);
        }

        /// <summary>
        /// Parses a MAC address string and broadcasts a Wake-on-LAN Magic Packet.
        /// </summary>
        public static Task SendAsync(
            string macAddressString,
            int port = DefaultPort,
            string? broadcastIp = null,
            byte[]? secureOnPassword = null)
        {
            var mac = MacAddress.Parse(macAddressString);
            IPAddress? bcast = null;
            if (!string.IsNullOrWhiteSpace(broadcastIp) && IPAddress.TryParse(broadcastIp, out var parsed))
            {
                bcast = parsed;
            }
            return SendAsync(mac, port, bcast, secureOnPassword);
        }
    }
}
