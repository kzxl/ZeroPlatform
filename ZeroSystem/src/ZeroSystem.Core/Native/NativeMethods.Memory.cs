using System;
using System.Runtime.InteropServices;

namespace ZeroSystem.Native;

internal static partial class NativeMethods
{
    // Memory & Working Set
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("ntdll.dll", SetLastError = true)]
    public static extern int NtSetSystemInformation(int SystemInformationClass, ref int SystemInformation, int SystemInformationLength);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    public const int SystemMemoryListInformation = 80;
    public const int MemoryPurgeStandbyList = 4;

    // Memory Lock, Large Pages & Prefetch
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VirtualLock(IntPtr lpAddress, UIntPtr dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool VirtualUnlock(IntPtr lpAddress, UIntPtr dwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern UIntPtr GetLargePageMinimum();

    [StructLayout(LayoutKind.Sequential)]
    public struct WIN32_MEMORY_RANGE_ENTRY
    {
        public IntPtr VirtualAddress;
        public UIntPtr NumberOfBytes;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PrefetchVirtualMemory(
        IntPtr hProcess,
        UIntPtr NumberOfEntries,
        [In] WIN32_MEMORY_RANGE_ENTRY[] VirtualAddresses,
        uint Flags);
}
