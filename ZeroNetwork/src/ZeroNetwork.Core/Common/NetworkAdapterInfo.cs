using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// Represents detailed properties and configuration of a network interface card (NIC).
    /// </summary>
    public class NetworkAdapterInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public NetworkInterfaceType InterfaceType { get; set; }
        public OperationalStatus Status { get; set; }
        public string MacAddress { get; set; } = string.Empty;
        public string RawMacAddress { get; set; } = string.Empty;
        public List<string> IPv4Addresses { get; set; } = new List<string>();
        public List<string> IPv6Addresses { get; set; } = new List<string>();
        public List<string> Gateways { get; set; } = new List<string>();
        public List<string> DnsAddresses { get; set; } = new List<string>();
        public List<string> DhcpServers { get; set; } = new List<string>();
        public string SubnetMask { get; set; } = string.Empty;
        public int? CidrPrefix { get; set; }
        public string BroadcastAddress { get; set; } = string.Empty;
        public string GatewayMacAddress { get; set; } = string.Empty;
        public bool IsVirtual { get; set; }
        public bool SupportsMulticast { get; set; }
        public long Speed { get; set; }

        public override string ToString()
        {
            string ipStr = IPv4Addresses.Count > 0 ? string.Join(", ", IPv4Addresses) : "No-IP";
            return $"[{Name}] {Description} | {ipStr} | MAC: {MacAddress} | Virtual: {IsVirtual}";
        }
    }
}
