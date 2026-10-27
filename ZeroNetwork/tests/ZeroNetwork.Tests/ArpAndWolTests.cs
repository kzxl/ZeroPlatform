using System.Net;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Common;
using ZeroNetwork.Discovery;
using ZeroNetwork.Sockets;
using IPNetwork = ZeroNetwork.Common.IPNetwork;

namespace ZeroNetwork.Tests
{
    public class ArpAndWolTests
    {
        [Fact]
        public void WakeOnLan_CreateMagicPacket_ValidStructure()
        {
            var mac = MacAddress.Parse("00:11:22:33:44:55");
            byte[] packet = WakeOnLan.CreateMagicPacket(mac);

            Assert.Equal(102, packet.Length);

            // First 6 bytes are 0xFF
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(0xFF, packet[i]);
            }

            // 16 repetitions of 00 11 22 33 44 55
            byte[] expectedMac = mac.GetAddressBytes();
            for (int rep = 0; rep < 16; rep++)
            {
                int offset = 6 + (rep * 6);
                for (int b = 0; b < 6; b++)
                {
                    Assert.Equal(expectedMac[b], packet[offset + b]);
                }
            }
        }

        [Fact]
        public void WakeOnLan_CreateMagicPacket_WithSecureOnPassword_Returns108Bytes()
        {
            var mac = MacAddress.Parse("00:11:22:33:44:55");
            byte[] password = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC };

            byte[] packet = WakeOnLan.CreateMagicPacket(mac, password);
            Assert.Equal(108, packet.Length);

            // Verify password at offset 102
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(password[i], packet[102 + i]);
            }
        }

        [Fact]
        public async Task ActiveArpScanner_ScanSubnetAsync_RunsWithoutThrowing()
        {
            var subnet = IPNetwork.Parse("127.0.0.1/32");
            int callbackCount = 0;

            var devices = await ActiveArpScanner.ScanSubnetAsync(
                subnet,
                timeoutMs: 100,
                maxConcurrency: 1,
                onDeviceDiscovered: _ => callbackCount++);

            Assert.NotNull(devices);
        }

        [Fact]
        public void ArpTable_GetTable_DoesNotThrow()
        {
            var table = ArpTable.GetTable();
            Assert.NotNull(table);
        }

        [Fact]
        public void UdpMulticastClient_CreateAndLifecycle_Succeeds()
        {
            using (var client = new UdpMulticastClient(0, IPAddress.Loopback))
            {
                client.StartListening();
                Assert.True(client.IsListening);
                client.StopListening();
                Assert.False(client.IsListening);
            }
        }
    }
}
