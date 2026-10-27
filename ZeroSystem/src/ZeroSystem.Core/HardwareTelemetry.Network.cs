using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace ZeroSystem;

public static partial class HardwareTelemetry
{
    /// <summary>
    /// Enumerates network adapters, MAC addresses, connection speed, and assigned IP addresses.
    /// </summary>
    public static IReadOnlyList<NetworkAdapterInfo> GetNetworkAdapters()
    {
        var adapters = new List<NetworkAdapterInfo>();

        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                var ips = new List<string>();
                try
                {
                    var ipProps = nic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        ips.Add(addr.Address.ToString());
                    }
                }
                catch
                {
                    // IP query fallback
                }

                string mac = nic.GetPhysicalAddress().ToString();
                if (mac.Length == 12)
                {
                    mac = $"{mac[0..2]}:{mac[2..4]}:{mac[4..6]}:{mac[6..8]}:{mac[8..10]}:{mac[10..12]}";
                }

                adapters.Add(new NetworkAdapterInfo(
                    Id: nic.Id,
                    Name: nic.Name,
                    Description: nic.Description,
                    MacAddress: mac,
                    Status: nic.OperationalStatus,
                    SpeedBitsPerSecond: nic.Speed,
                    IpAddresses: ips));
            }
        }
        catch
        {
            // Network query fallback
        }

        return adapters;
    }
}
