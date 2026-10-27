using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using ZeroNetwork.Common;

namespace ZeroNetwork.Discovery
{
    /// <summary>
    /// Utility to accurately identify and filter out virtual, container, VPN, and hypervisor network interfaces
    /// using hybrid keyword heuristics and IEEE 24-bit OUI hardware MAC vendor identification.
    /// </summary>
    public static class VirtualAdapterFilter
    {
        private static readonly List<string> VirtualKeywords = new List<string>
        {
            "hyper-v",
            "virtual",
            "vmware",
            "wsl",
            "vbox",
            "tap",
            "tun",
            "vpn",
            "loopback",
            "docker",
            "teredo",
            "isatap",
            "pseudo",
            "npcap",
            "winpcap",
            "wireguard",
            "tailscale",
            "zerotier",
            "hamachi",
            "cisco",
            "fortinet",
            "pulse",
            "checkpoint",
            "bluetooth",
            "qemu",
            "kvm",
            "wintun",
            "nordlynx",
            "openvpn",
            "anyconnect",
            "vmnet",
            "vethernet"
        };

        private static readonly HashSet<int> CustomVirtualOuis = new HashSet<int>();
        private static readonly HashSet<string> AllowedAdapterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        /// <summary>
        /// Adds a custom keyword to treat interfaces containing this keyword in name/description as virtual.
        /// </summary>
        public static void AddVirtualKeyword(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return;
            lock (_lock)
            {
                if (!VirtualKeywords.Contains(keyword))
                {
                    VirtualKeywords.Add(keyword);
                }
            }
        }

        /// <summary>
        /// Adds a custom 24-bit IEEE OUI prefix to treat as virtual (e.g. 0x00155D).
        /// </summary>
        public static void AddVirtualOui(int oui24Bit)
        {
            lock (_lock)
            {
                CustomVirtualOuis.Add(oui24Bit & 0xFFFFFF);
            }
        }

        /// <summary>
        /// Explicitly whitelists an adapter name so that it is never classified as virtual.
        /// </summary>
        public static void AddAllowedAdapterName(string adapterName)
        {
            if (string.IsNullOrWhiteSpace(adapterName)) return;
            lock (_lock)
            {
                AllowedAdapterNames.Add(adapterName.Trim());
            }
        }

        /// <summary>
        /// Clears all user-defined keywords, custom OUIs, and allowed whitelist rules.
        /// </summary>
        public static void ResetCustomRules()
        {
            lock (_lock)
            {
                CustomVirtualOuis.Clear();
                AllowedAdapterNames.Clear();
            }
        }

        /// <summary>
        /// Determines whether the specified network interface is a virtual, software-emulated, or tunnel adapter.
        /// </summary>
        public static bool IsVirtual(NetworkInterface nic)
        {
            if (nic == null) return true;

            string name = nic.Name ?? string.Empty;
            string desc = nic.Description ?? string.Empty;

            lock (_lock)
            {
                if (AllowedAdapterNames.Contains(name))
                {
                    return false;
                }
            }

            // Loopback or tunnel interfaces are always virtual
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                return true;
            }

            // Keyword check using OrdinalIgnoreCase (avoids string allocation)
            lock (_lock)
            {
                for (int i = 0; i < VirtualKeywords.Count; i++)
                {
                    string kw = VirtualKeywords[i];
                    if (desc.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            // Physical MAC address check
            byte[] rawBytes = nic.GetPhysicalAddress().GetAddressBytes();
            if (rawBytes == null || rawBytes.Length < 6)
            {
                return true; // No physical hardware address
            }

            var mac = new MacAddress(rawBytes);
            if (mac.IsEmpty || mac.IsBroadcast)
            {
                return true;
            }

            // Check known hypervisor OUI vendors
            if (mac.IsKnownVirtualVendor(out _))
            {
                return true;
            }

            lock (_lock)
            {
                if (CustomVirtualOuis.Contains(mac.GetOuiPrefix()))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
