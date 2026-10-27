using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ZeroNetwork.Common;

namespace ZeroNetwork.Discovery
{
    /// <summary>
    /// Represents an individual entry in the ARP (Address Resolution Protocol) neighbor table.
    /// </summary>
    public readonly struct ArpEntry
    {
        public IPAddress IPAddress { get; }
        public MacAddress MacAddress { get; }
        public string InterfaceIndex { get; }
        public string EntryType { get; }

        public ArpEntry(IPAddress ip, MacAddress mac, string ifIndex, string entryType)
        {
            IPAddress = ip;
            MacAddress = mac;
            InterfaceIndex = ifIndex;
            EntryType = entryType;
        }

        public override string ToString() => $"{IPAddress,-15} -> {MacAddress} ({EntryType})";
    }

    /// <summary>
    /// High-performance Address Resolution Protocol (ARP) table inspector and resolver.
    /// Maps IP addresses to physical MAC hardware addresses across the local network segment.
    /// </summary>
    public static class ArpTable
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        private static extern int SendARP(uint destIP, uint srcIP, byte[] pMacAddr, ref int phyAddrLen);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetIpNetTable(IntPtr pIpNetTable, ref int pdwSize, bool bOrder);

        private static bool IsWindows()
        {
#if NETFRAMEWORK
            return Environment.OSVersion.Platform == PlatformID.Win32NT;
#else
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        }

        /// <summary>
        /// Retrieves all current active entries in the local OS ARP cache table.
        /// Works across Windows (IpHlpApi) and Linux (/proc/net/arp).
        /// </summary>
        public static List<ArpEntry> GetTable()
        {
            var entries = new List<ArpEntry>();

            if (IsWindows())
            {
                GetWindowsArpTable(entries);
            }
            else
            {
                GetLinuxArpTable(entries);
            }

            return entries;
        }

        /// <summary>
        /// Actively resolves the physical MAC address for a given target IPv4 address using SendARP (Windows) or cache lookup.
        /// </summary>
        public static Task<MacAddress?> ResolveMacAsync(IPAddress ip, int timeoutMs = 1000)
        {
            if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork)
                return Task.FromResult<MacAddress?>(null);

            return Task.Run<MacAddress?>(() =>
            {
                if (IsWindows())
                {
                    byte[] ipBytes = ip.GetAddressBytes();
                    uint destIp = (uint)(ipBytes[0] | (ipBytes[1] << 8) | (ipBytes[2] << 16) | (ipBytes[3] << 24));
                    byte[] macBytes = new byte[6];
                    int len = 6;

                    int res = SendARP(destIp, 0, macBytes, ref len);
                    if (res == 0 && len == 6)
                    {
                        var mac = new MacAddress(macBytes);
                        if (!mac.IsEmpty) return mac;
                    }
                }

                // Fallback: check table
                var table = GetTable();
                for (int i = 0; i < table.Count; i++)
                {
                    if (table[i].IPAddress.Equals(ip))
                    {
                        return table[i].MacAddress;
                    }
                }

                return null;
            });
        }

        private static void GetWindowsArpTable(List<ArpEntry> entries)
        {
            try
            {
                int bufferSize = 0;
                GetIpNetTable(IntPtr.Zero, ref bufferSize, false);
                if (bufferSize <= 0) return;

                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    int ret = GetIpNetTable(buffer, ref bufferSize, false);
                    if (ret != 0) return;

                    int numEntries = Marshal.ReadInt32(buffer);
                    IntPtr currentPtr = new IntPtr(buffer.ToInt64() + 4);

                    // MIB_IPNETROW struct is 24 bytes:
                    // dwIndex (4), dwPhysAddrLen (4), bPhysAddr[8] (8), dwAddr (4), dwType (4)
                    for (int i = 0; i < numEntries; i++)
                    {
                        int ifIndex = Marshal.ReadInt32(currentPtr);
                        int physAddrLen = Marshal.ReadInt32(currentPtr, 4);

                        byte[] macBytes = new byte[6];
                        if (physAddrLen >= 6)
                        {
                            for (int b = 0; b < 6; b++)
                            {
                                macBytes[b] = Marshal.ReadByte(currentPtr, 8 + b);
                            }
                        }

                        int ipInt = Marshal.ReadInt32(currentPtr, 16);
                        int type = Marshal.ReadInt32(currentPtr, 20);

                        byte[] ipBytes = BitConverter.GetBytes(ipInt);
                        var ip = new IPAddress(ipBytes);
                        var mac = new MacAddress(macBytes);

                        string typeName = type switch
                        {
                            3 => "Dynamic",
                            4 => "Static",
                            2 => "Invalid",
                            _ => "Other"
                        };

                        if (!mac.IsEmpty && !mac.IsBroadcast && !IPAddress.IsLoopback(ip) && !ip.Equals(IPAddress.Any))
                        {
                            entries.Add(new ArpEntry(ip, mac, ifIndex.ToString(), typeName));
                        }

                        currentPtr = new IntPtr(currentPtr.ToInt64() + 24);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            catch
            {
                // Suppress ARP enumeration failures
            }
        }

        private static void GetLinuxArpTable(List<ArpEntry> entries)
        {
            try
            {
                const string arpFilePath = "/proc/net/arp";
                if (!File.Exists(arpFilePath)) return;

                using var reader = new StreamReader(arpFilePath);
                string? line = reader.ReadLine(); // Skip header: IP HW-Type Flags HW-Address Mask Device
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 6)
                    {
                        if (IPAddress.TryParse(parts[0], out var ip) && MacAddress.TryParse(parts[3], out var mac))
                        {
                            if (!mac.IsEmpty && !mac.IsBroadcast)
                            {
                                entries.Add(new ArpEntry(ip, mac, parts[5], "Dynamic"));
                            }
                        }
                    }
                }
            }
            catch
            {
                // Ignore
            }
        }
    }
}
