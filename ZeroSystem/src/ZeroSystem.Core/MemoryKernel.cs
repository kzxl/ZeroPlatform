using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Snapshot of physical and virtual system memory.
/// </summary>
public readonly record struct SystemMemoryStatus(
    uint MemoryLoadPercentage,
    ulong TotalPhysicalBytes,
    ulong AvailablePhysicalBytes,
    ulong TotalPageFileBytes,
    ulong AvailablePageFileBytes,
    ulong TotalVirtualBytes,
    ulong AvailableVirtualBytes);

/// <summary>
/// Low-level operating system memory optimizer and kernel standby list manager.
/// </summary>
public static class MemoryKernel
{
    /// <summary>
    /// Queries current system-wide memory load and capacity.
    /// </summary>
    public static SystemMemoryStatus GetMemoryStatus()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var gcMem = (ulong)GC.GetTotalMemory(false);
            return new SystemMemoryStatus(0, gcMem, gcMem, 0, 0, 0, 0);
        }

        var buffer = new NativeMethods.MEMORYSTATUSEX
        {
            dwLength = (uint)Marshal.SizeOf<NativeMethods.MEMORYSTATUSEX>()
        };

        if (NativeMethods.GlobalMemoryStatusEx(ref buffer))
        {
            return new SystemMemoryStatus(
                buffer.dwMemoryLoad,
                buffer.ullTotalPhys,
                buffer.ullAvailPhys,
                buffer.ullTotalPageFile,
                buffer.ullAvailPageFile,
                buffer.ullTotalVirtual,
                buffer.ullAvailVirtual);
        }

        return default;
    }

    /// <summary>
    /// Purges Windows standby memory cache using NtSetSystemInformation (MemoryPurgeStandbyList = 4).
    /// Requires elevated privileges (SeProfileSingleProcessPrivilege / SeIncreaseQuotaPrivilege).
    /// </summary>
    public static bool PurgeStandbyList()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        ProcessGuard.TryEnablePrivilege(NativeMethods.SE_PROFILE_SINGLE_PROCESS_NAME);
        ProcessGuard.TryEnablePrivilege(NativeMethods.SE_INCREASE_QUOTA_NAME);

        try
        {
            int command = NativeMethods.MemoryPurgeStandbyList;
            int status = NativeMethods.NtSetSystemInformation(
                NativeMethods.SystemMemoryListInformation,
                ref command,
                Marshal.SizeOf<int>());

            return status >= 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Trims the working set of a specific process.
    /// </summary>
    public static bool TrimWorkingSet(IntPtr processHandle)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || processHandle == IntPtr.Zero) return false;

        try
        {
            return NativeMethods.EmptyWorkingSet(processHandle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Optimizes working sets across all accessible processes, skipping system processes, self, and foreground window.
    /// </summary>
    public static (int TrimmedCount, long InitialBytes, long ReclaimedBytes) OptimizeWorkingSets(
        long minWorkingSetThresholdBytes = 15 * 1024 * 1024,
        bool protectForegroundProcess = true)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return (0, 0, 0);

        var processes = Process.GetProcesses();
        long initialTotal = 0;
        long finalTotal = 0;
        int count = 0;
        uint foregroundPid = protectForegroundProcess ? ProcessGuard.GetForegroundProcessId() : 0;
        int currentPid = Environment.ProcessId;

        foreach (var proc in processes)
        {
            try
            {
                if (ProcessGuard.IsSystemOrCurrentProcess(proc.Id) || proc.Id == foregroundPid) continue;

                long ws = proc.WorkingSet64;
                if (ws < minWorkingSetThresholdBytes) continue;

                initialTotal += ws;
                if (TrimWorkingSet(proc.Handle))
                {
                    count++;
                    proc.Refresh();
                    finalTotal += proc.WorkingSet64;
                }
                else
                {
                    finalTotal += ws;
                }
            }
            catch
            {
                // Access denied on protected system services
            }
            finally
            {
                proc.Dispose();
            }
        }

        long reclaimed = Math.Max(0, initialTotal - finalTotal);
        return (count, initialTotal, reclaimed);
    }
}
