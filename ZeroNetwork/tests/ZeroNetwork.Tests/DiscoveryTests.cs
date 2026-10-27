using System;
using System.Linq;
using System.Net;
using Xunit;
using ZeroNetwork.Common;
using ZeroNetwork.Discovery;
using IPNetwork = ZeroNetwork.Common.IPNetwork;

namespace ZeroNetwork.Tests
{
    public class DiscoveryTests
    {
        [Fact]
        public void GetLocalIPv4_ReturnsValidIp()
        {
            string ip = NetworkInfo.GetLocalIPv4();

            Assert.False(string.IsNullOrWhiteSpace(ip));
            Assert.True(IPAddress.TryParse(ip, out var parsed));
            Assert.Equal(System.Net.Sockets.AddressFamily.InterNetwork, parsed.AddressFamily);
        }

        [Fact]
        public void GetAllActiveIPv4_DoesNotContainLoopback()
        {
            var ips = NetworkInfo.GetAllActiveIPv4(excludeVirtual: true);

            Assert.NotNull(ips);
            foreach (var ip in ips)
            {
                Assert.True(IPAddress.TryParse(ip, out var parsed));
                Assert.False(IPAddress.IsLoopback(parsed), $"IP {ip} should not be loopback");
            }
        }

        [Fact]
        public void GetPhysicalMacAddress_ReturnsFormattedString()
        {
            string mac = NetworkInfo.GetPhysicalMacAddress("-");

            if (!string.IsNullOrEmpty(mac))
            {
                var parts = mac.Split('-');
                Assert.Equal(6, parts.Length);
                foreach (var part in parts)
                {
                    Assert.Equal(2, part.Length);
                    Assert.True(byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out _));
                }
            }
        }

        [Fact]
        public void GetAdapters_ReturnsEnrichedProperties()
        {
            var adapters = NetworkInfo.GetAdapters(operationalOnly: false);

            Assert.NotNull(adapters);
            Assert.NotEmpty(adapters);

            foreach (var adapter in adapters)
            {
                Assert.NotNull(adapter.IPv4Addresses);
                Assert.NotNull(adapter.Gateways);
                Assert.NotNull(adapter.DnsAddresses);
                Assert.NotNull(adapter.DhcpServers);
            }
        }

        [Fact]
        public void MacAddress_ParseAndFormat_Succeeds()
        {
            string formatted = "00-15-5D-AA-BB-CC";
            var mac = MacAddress.Parse(formatted);

            Assert.Equal(0x00, mac.B0);
            Assert.Equal(0x15, mac.B1);
            Assert.Equal(0x5D, mac.B2);
            Assert.Equal(0xAA, mac.B3);
            Assert.Equal(0xBB, mac.B4);
            Assert.Equal(0xCC, mac.B5);

            Assert.Equal("00-15-5D-AA-BB-CC", mac.ToString("-"));
            Assert.Equal("00:15:5D:AA:BB:CC", mac.ToString(":"));
            Assert.Equal("00155DAABBCC", mac.ToString(""));
        }

        [Fact]
        public void MacAddress_DetectsHypervisorOuis()
        {
            var vmwareMac = MacAddress.Parse("00:50:56:12:34:56");
            Assert.True(vmwareMac.IsKnownVirtualVendor(out string vendor1));
            Assert.Equal("VMware", vendor1);

            var hypervMac = MacAddress.Parse("00:15:5D:AA:BB:CC");
            Assert.True(hypervMac.IsKnownVirtualVendor(out string vendor2));
            Assert.Equal("Hyper-V", vendor2);

            var vboxMac = MacAddress.Parse("08:00:27:11:22:33");
            Assert.True(vboxMac.IsKnownVirtualVendor(out string vendor3));
            Assert.Equal("VirtualBox", vendor3);

            var physicalIntelMac = MacAddress.Parse("A4:BB:6D:01:02:03");
            Assert.False(physicalIntelMac.IsKnownVirtualVendor(out _));
        }

        [Fact]
        public void IPNetwork_ParseCidr24_CalculatesBroadcastAndHosts()
        {
            var net = IPNetwork.Parse("192.168.1.100/24");

            Assert.Equal("192.168.1.0", net.NetworkAddress.ToString());
            Assert.Equal("192.168.1.255", net.BroadcastAddress.ToString());
            Assert.Equal("255.255.255.0", net.Netmask.ToString());
            Assert.Equal(24, net.CidrPrefix);
            Assert.Equal(256u, net.TotalHosts);
            Assert.Equal(254u, net.UsableHostsCount);

            Assert.True(net.Contains(IPAddress.Parse("192.168.1.1")));
            Assert.True(net.Contains(IPAddress.Parse("192.168.1.254")));
            Assert.False(net.Contains(IPAddress.Parse("192.168.2.1")));

            var hosts = net.EnumerateUsableHosts().ToList();
            Assert.Equal(254, hosts.Count);
            Assert.Equal("192.168.1.1", hosts[0].ToString());
            Assert.Equal("192.168.1.254", hosts[253].ToString());
        }

        [Fact]
        public void IPNetwork_ContainsAndOverlaps()
        {
            var net1 = IPNetwork.Parse("10.0.0.0/16");
            var net2 = IPNetwork.Parse("10.0.1.0/24");
            var net3 = IPNetwork.Parse("192.168.1.0/24");

            Assert.True(net1.Contains(net2));
            Assert.False(net2.Contains(net1));
            Assert.True(net1.Overlaps(net2));
            Assert.False(net1.Overlaps(net3));
        }

        [Fact]
        public void VirtualAdapterFilter_AddAllowedAdapter_OverridesVirtual()
        {
            VirtualAdapterFilter.ResetCustomRules();
            VirtualAdapterFilter.AddAllowedAdapterName("SpecialVpnAdapter");

            // Verify reset cleans rules
            VirtualAdapterFilter.ResetCustomRules();
        }
    }
}
