using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

#region Connection Data Models

/// <summary>
/// State of an active TCP connection.
/// </summary>
public enum TcpConnectionStatus
{
    Unknown = 0,
    Closed = 1,
    Listening = 2,
    SynSent = 3,
    SynReceived = 4,
    Established = 5,
    FinWait1 = 6,
    FinWait2 = 7,
    CloseWait = 8,
    Closing = 9,
    LastAck = 10,
    TimeWait = 11,
    DeleteTcb = 12
}

/// <summary>
/// Snapshot of an active TCP socket endpoint and its owning Process ID.
/// </summary>
public sealed record TcpConnectionSnapshot(
    IPEndPoint LocalEndpoint,
    IPEndPoint RemoteEndpoint,
    TcpConnectionStatus State,
    int OwningProcessId);

/// <summary>
/// Snapshot of an active UDP listening endpoint and its owning Process ID.
/// </summary>
public sealed record UdpEndpointSnapshot(
    IPEndPoint LocalEndpoint,
    int OwningProcessId);

#endregion

/// <summary>
/// Sovereign network connection and socket tracker using native IP Helper APIs (iphlpapi.dll).
/// Maps every listening or connected TCP/UDP port to its owning process ID with zero Netstat / WMI overhead.
/// </summary>
public static class NetworkConnectionTracker
{
    /// <summary>
    /// Enumerates all active IPv4 TCP sockets mapped to their owning Process IDs.
    /// </summary>
    public static IReadOnlyList<TcpConnectionSnapshot> GetActiveTcpConnections()
    {
        var result = new List<TcpConnectionSnapshot>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return result;

        uint size = 0;
        // First query buffer size needed
        uint ret = NativeMethods.GetExtendedTcpTable(
            IntPtr.Zero,
            ref size,
            true,
            NativeMethods.AF_INET,
            NativeMethods.TCP_TABLE_OWNER_PID_ALL);

        if (size == 0) return result;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            ret = NativeMethods.GetExtendedTcpTable(
                buffer,
                ref size,
                true,
                NativeMethods.AF_INET,
                NativeMethods.TCP_TABLE_OWNER_PID_ALL);

            if (ret == 0)
            {
                int numEntries = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                int rowSize = Marshal.SizeOf<NativeMethods.MIB_TCPROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_TCPROW_OWNER_PID>(rowPtr);

                    var localIp = new IPAddress(row.dwLocalAddr);
                    int localPort = DecodePort(row.dwLocalPort);

                    var remoteIp = new IPAddress(row.dwRemoteAddr);
                    int remotePort = DecodePort(row.dwRemotePort);

                    var state = (TcpConnectionStatus)row.dwState;

                    result.Add(new TcpConnectionSnapshot(
                        LocalEndpoint: new IPEndPoint(localIp, localPort),
                        RemoteEndpoint: new IPEndPoint(remoteIp, remotePort),
                        State: state,
                        OwningProcessId: (int)row.dwOwningPid));

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return result;
    }

    /// <summary>
    /// Enumerates all active IPv4 UDP sockets mapped to their owning Process IDs.
    /// </summary>
    public static IReadOnlyList<UdpEndpointSnapshot> GetActiveUdpEndpoints()
    {
        var result = new List<UdpEndpointSnapshot>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return result;

        uint size = 0;
        uint ret = NativeMethods.GetExtendedUdpTable(
            IntPtr.Zero,
            ref size,
            true,
            NativeMethods.AF_INET,
            NativeMethods.UDP_TABLE_OWNER_PID);

        if (size == 0) return result;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            ret = NativeMethods.GetExtendedUdpTable(
                buffer,
                ref size,
                true,
                NativeMethods.AF_INET,
                NativeMethods.UDP_TABLE_OWNER_PID);

            if (ret == 0)
            {
                int numEntries = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                int rowSize = Marshal.SizeOf<NativeMethods.MIB_UDPROW_OWNER_PID>();

                for (int i = 0; i < numEntries; i++)
                {
                    var row = Marshal.PtrToStructure<NativeMethods.MIB_UDPROW_OWNER_PID>(rowPtr);

                    var localIp = new IPAddress(row.dwLocalAddr);
                    int localPort = DecodePort(row.dwLocalPort);

                    result.Add(new UdpEndpointSnapshot(
                        LocalEndpoint: new IPEndPoint(localIp, localPort),
                        OwningProcessId: (int)row.dwOwningPid));

                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return result;
    }

    /// <summary>
    /// Finds the Process ID that is currently listening on or connected to the specified local TCP port.
    /// </summary>
    public static int? FindProcessOwningTcpPort(int port)
    {
        var connections = GetActiveTcpConnections();
        foreach (var conn in connections)
        {
            if (conn.LocalEndpoint.Port == port)
            {
                return conn.OwningProcessId;
            }
        }

        return null;
    }

    private static int DecodePort(uint portNetworkByteOrder)
    {
        // Convert network byte order (big-endian) to host integer
        return (int)(((portNetworkByteOrder & 0xFF) << 8) | ((portNetworkByteOrder >> 8) & 0xFF));
    }
}
