using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using ZeroNetwork.Common;
using IPNetwork = ZeroNetwork.Common.IPNetwork;

namespace ZeroNetwork.Discovery
{
    /// <summary>
    /// Provides discovery and inspection of local network interfaces, physical MAC addresses, IP configurations, and subnets.
    /// </summary>
    public static class NetworkInfo
    {
        private const string LoopbackIPv4 = "127.0.0.1";

        /// <summary>
        /// Gets the primary operational local IPv4 address, filtering out loopback and virtual adapters (WSL, Hyper-V, VMware, VPN).
        /// Prioritizes physical Ethernet, then Wireless.
        /// </summary>
        public static string GetLocalIPv4(NetworkInterfaceType? preferredType = null)
        {
            try
            {
                NetworkInterface[] allNics = NetworkInterface.GetAllNetworkInterfaces();
                NetworkInterface? bestNic = null;
                int bestScore = -1;

                for (int i = 0; i < allNics.Length; i++)
                {
                    var nic = allNics[i];
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    bool isVirtual = VirtualAdapterFilter.IsVirtual(nic);
                    int score = 0;

                    if (!isVirtual) score += 100;
                    if (preferredType.HasValue && nic.NetworkInterfaceType == preferredType.Value) score += 50;
                    else if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 30;
                    else if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 20;

                    if (score > bestScore)
                    {
                        // Check if it has at least one valid IPv4 address
                        var ipProps = nic.GetIPProperties();
                        bool hasIpv4 = false;
                        foreach (var addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                            {
                                hasIpv4 = true;
                                break;
                            }
                        }

                        if (hasIpv4)
                        {
                            bestScore = score;
                            bestNic = nic;
                        }
                    }
                }

                if (bestNic != null)
                {
                    var ipProps = bestNic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                        {
                            return addr.Address.ToString();
                        }
                    }
                }

                // Fallback using Dns.GetHostAddresses
                foreach (var ip in Dns.GetHostAddresses(Dns.GetHostName()))
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }

                return LoopbackIPv4;
            }
            catch
            {
                return LoopbackIPv4;
            }
        }

        /// <summary>
        /// Gets all active IPv4 addresses assigned to the machine, with optional filtering of virtual adapters.
        /// </summary>
        public static List<string> GetAllActiveIPv4(bool excludeVirtual = true)
        {
            var result = new List<string>();
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    var nic = nics[i];
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    if (excludeVirtual && VirtualAdapterFilter.IsVirtual(nic))
                    {
                        continue;
                    }

                    var ipProps = nic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr.Address))
                        {
                            string ipStr = addr.Address.ToString();
                            if (!result.Contains(ipStr))
                            {
                                result.Add(ipStr);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Suppress network inspection exceptions
            }

            return result;
        }

        /// <summary>
        /// Gets the MAC address of the primary active physical network card or specified interface name.
        /// </summary>
        /// <param name="separator">Delimiter between bytes (e.g. "-", ":", or "" for continuous string).</param>
        /// <param name="preferredInterfaceName">Optional interface name or description to select a specific physical NIC.</param>
        public static string GetPhysicalMacAddress(string separator = "-", string? preferredInterfaceName = null)
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();
                NetworkInterface? selected = null;

                if (!string.IsNullOrWhiteSpace(preferredInterfaceName))
                {
                    foreach (var nic in nics)
                    {
                        if ((nic.Name.IndexOf(preferredInterfaceName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                             nic.Description.IndexOf(preferredInterfaceName, StringComparison.OrdinalIgnoreCase) >= 0) &&
                            !VirtualAdapterFilter.IsVirtual(nic))
                        {
                            selected = nic;
                            break;
                        }
                    }
                }

                if (selected == null)
                {
                    int bestScore = -1;
                    foreach (var nic in nics)
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up || VirtualAdapterFilter.IsVirtual(nic))
                            continue;

                        int score = 0;
                        if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) score += 20;
                        else if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) score += 10;

                        if (score > bestScore)
                        {
                            bestScore = score;
                            selected = nic;
                        }
                    }
                }

                if (selected == null) return string.Empty;

                byte[] bytes = selected.GetPhysicalAddress().GetAddressBytes();
                if (bytes == null || bytes.Length < 6) return string.Empty;

                var mac = new MacAddress(bytes);
                return mac.ToString(separator);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets the primary local IPv4 Subnet (<see cref="IPNetwork"/>).
        /// </summary>
        public static IPNetwork? GetPrimarySubnet()
        {
            try
            {
                string localIp = GetLocalIPv4();
                if (localIp == LoopbackIPv4) return null;

                var adapters = GetAdapters(operationalOnly: true, excludeVirtual: true);
                foreach (var adapter in adapters)
                {
                    if (adapter.IPv4Addresses.Contains(localIp) && !string.IsNullOrEmpty(adapter.SubnetMask))
                    {
                        if (IPAddress.TryParse(localIp, out var ip) && IPAddress.TryParse(adapter.SubnetMask, out var mask))
                        {
                            return new IPNetwork(ip, mask);
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return null;
        }

        /// <summary>
        /// Retrieves detailed information of all network adapter interfaces.
        /// </summary>
        public static List<NetworkAdapterInfo> GetAdapters(bool operationalOnly = true, bool excludeVirtual = false)
        {
            var result = new List<NetworkAdapterInfo>();
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();

                for (int i = 0; i < nics.Length; i++)
                {
                    var nic = nics[i];
                    if (operationalOnly && nic.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    bool isVirtual = VirtualAdapterFilter.IsVirtual(nic);
                    if (excludeVirtual && isVirtual) continue;

                    var info = new NetworkAdapterInfo
                    {
                        Id = nic.Id,
                        Name = nic.Name,
                        Description = nic.Description,
                        InterfaceType = nic.NetworkInterfaceType,
                        Status = nic.OperationalStatus,
                        Speed = nic.Speed,
                        SupportsMulticast = nic.SupportsMulticast,
                        IsVirtual = isVirtual
                    };

                    byte[] macBytes = nic.GetPhysicalAddress().GetAddressBytes();
                    if (macBytes != null && macBytes.Length >= 6)
                    {
                        var mac = new MacAddress(macBytes);
                        info.RawMacAddress = mac.ToString(string.Empty);
                        info.MacAddress = mac.ToString("-");
                    }

                    try
                    {
                        var ipProps = nic.GetIPProperties();
                        foreach (var addr in ipProps.UnicastAddresses)
                        {
                            if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string ipStr = addr.Address.ToString();
                                info.IPv4Addresses.Add(ipStr);

                                if (addr.IPv4Mask != null && string.IsNullOrEmpty(info.SubnetMask))
                                {
                                    info.SubnetMask = addr.IPv4Mask.ToString();
                                    try
                                    {
                                        var net = new IPNetwork(addr.Address, addr.IPv4Mask);
                                        info.CidrPrefix = net.CidrPrefix;
                                        info.BroadcastAddress = net.BroadcastAddress.ToString();
                                    }
                                    catch { }
                                }
                            }
                            else if (addr.Address.AddressFamily == AddressFamily.InterNetworkV6)
                            {
                                info.IPv6Addresses.Add(addr.Address.ToString());
                            }
                        }

                        foreach (var gw in ipProps.GatewayAddresses)
                        {
                            if (gw.Address != null && !string.IsNullOrEmpty(gw.Address.ToString()))
                            {
                                info.Gateways.Add(gw.Address.ToString());
                            }
                        }

                        foreach (var dns in ipProps.DnsAddresses)
                        {
                            info.DnsAddresses.Add(dns.ToString());
                        }

                        if (ipProps.DhcpServerAddresses != null)
                        {
                            foreach (var dhcp in ipProps.DhcpServerAddresses)
                            {
                                if (dhcp != null && !string.IsNullOrEmpty(dhcp.ToString()))
                                {
                                    info.DhcpServers.Add(dhcp.ToString());
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Some virtual or disabled adapters throw when accessing IP properties
                    }

                    result.Add(info);
                }
            }
            catch
            {
                // Suppress enumeration failures
            }

            return result;
        }

        /// <summary>
        /// Gets the primary Default Gateway IPv4 address.
        /// </summary>
        public static string GetDefaultGateway()
        {
            try
            {
                var adapters = GetAdapters(operationalOnly: true, excludeVirtual: true);
                foreach (var adapter in adapters)
                {
                    if (adapter.Gateways.Count > 0)
                    {
                        return adapter.Gateways[0];
                    }
                }
            }
            catch
            {
                // Ignore
            }

            return string.Empty;
        }
    }
}
