using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Provides process protection, foreground window inspection, and security token privilege adjustment.
/// </summary>
public static class ProcessGuard
{
    /// <summary>
    /// Gets the process ID associated with the currently focused foreground window.
    /// Returns 0 if unable to identify the foreground process.
    /// </summary>
    public static uint GetForegroundProcessId()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return 0;

        try
        {
            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                return pid;
            }
        }
        catch
        {
            // Ignored on non-interactive environments
        }

        return 0;
    }

    /// <summary>
    /// Checks if a given process ID belongs to the system (PID 0, 4) or the current process.
    /// </summary>
    public static bool IsSystemOrCurrentProcess(int processId)
    {
        return processId <= 4 || processId == Environment.ProcessId;
    }

    /// <summary>
    /// Enables a specific Windows security privilege (e.g. SeProfileSingleProcessPrivilege, SeIncreaseQuotaPrivilege) on the current process token.
    /// </summary>
    public static bool TryEnablePrivilege(string privilegeName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        IntPtr hToken = IntPtr.Zero;
        try
        {
            using var currentProcess = Process.GetCurrentProcess();
            if (!NativeMethods.OpenProcessToken(
                    currentProcess.Handle,
                    NativeMethods.TOKEN_ADJUST_PRIVILEGES | NativeMethods.TOKEN_QUERY,
                    out hToken))
            {
                return false;
            }

            if (!NativeMethods.LookupPrivilegeValue(null, privilegeName, out var luid))
            {
                return false;
            }

            var tp = new NativeMethods.TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = NativeMethods.SE_PRIVILEGE_ENABLED
            };

            bool success = NativeMethods.AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            return success && Marshal.GetLastWin32Error() != 1300; // ERROR_NOT_ALL_ASSIGNED = 1300
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(hToken);
            }
        }
    }
}
