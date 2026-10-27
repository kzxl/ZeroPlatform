using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Information about a process or service holding an exclusive or shared lock on a file or folder.
/// </summary>
public sealed record LockingProcessInfo(
    int ProcessId,
    string ProcessName,
    string ServiceShortName,
    string ApplicationType,
    bool IsRestartable);

/// <summary>
/// Sovereign file lock inspector and unlocker utilizing the native Windows Restart Manager API.
/// Identifies which processes or services are locking a file without third-party tools (e.g. Handle/ProcessExplorer).
/// </summary>
public static class FileLockManager
{
    /// <summary>
    /// Checks whether a specific file or folder is currently locked by any running process.
    /// </summary>
    public static bool IsFileLocked(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        // If file exists, try a quick non-locking test first
        if (File.Exists(path))
        {
            try
            {
                using var fs = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                // Might be readonly or permission issue; inspect via Restart Manager
            }
        }

        var lockers = GetLockingProcesses(path);
        return lockers.Count > 0;
    }

    /// <summary>
    /// Retrieves the list of processes and services currently locking the specified file or folder.
    /// </summary>
    public static IReadOnlyList<LockingProcessInfo> GetLockingProcesses(string path)
    {
        var result = new List<LockingProcessInfo>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || string.IsNullOrWhiteSpace(path))
            return result;

        string sessionKey = Guid.NewGuid().ToString();
        int res = NativeMethods.RmStartSession(out uint sessionHandle, 0, sessionKey);
        if (res != 0) return result;

        try
        {
            string[] resources = [path];
            res = NativeMethods.RmRegisterResources(sessionHandle, (uint)resources.Length, resources, 0, null, 0, null);
            if (res != 0) return result;

            uint pnProcInfoNeeded = 0;
            uint pnProcInfo = 0;
            uint lpdwRebootReasons = 0;

            // First call gets the count of locking applications needed
            res = NativeMethods.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, null, out lpdwRebootReasons);
            if (res == NativeMethods.ERROR_MORE_DATA && pnProcInfoNeeded > 0)
            {
                var processInfo = new NativeMethods.RM_PROCESS_INFO[pnProcInfoNeeded];
                pnProcInfo = pnProcInfoNeeded;

                res = NativeMethods.RmGetList(sessionHandle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, out lpdwRebootReasons);
                if (res == 0)
                {
                    for (int i = 0; i < pnProcInfo; i++)
                    {
                        var info = processInfo[i];
                        string procName = info.strAppName;

                        if (string.IsNullOrWhiteSpace(procName))
                        {
                            try
                            {
                                using var p = Process.GetProcessById(info.Process.dwProcessId);
                                procName = p.ProcessName;
                            }
                            catch
                            {
                                procName = "Unknown";
                            }
                        }

                        result.Add(new LockingProcessInfo(
                            ProcessId: info.Process.dwProcessId,
                            ProcessName: procName,
                            ServiceShortName: info.strServiceShortName ?? string.Empty,
                            ApplicationType: info.ApplicationType.ToString(),
                            IsRestartable: info.bRestartable));
                    }
                }
            }
        }
        finally
        {
            NativeMethods.RmEndSession(sessionHandle);
        }

        return result;
    }

    /// <summary>
    /// Attempts to terminate all non-system processes that are actively locking the specified file.
    /// </summary>
    public static bool TryKillLockingProcesses(string path, out List<int> killedPids)
    {
        killedPids = [];
        var lockers = GetLockingProcesses(path);
        if (lockers.Count == 0) return true;

        bool allKilled = true;
        foreach (var locker in lockers)
        {
            if (ProcessGuard.IsSystemOrCurrentProcess(locker.ProcessId))
            {
                allKilled = false;
                continue;
            }

            try
            {
                using var p = Process.GetProcessById(locker.ProcessId);
                p.Kill();
                p.WaitForExit(3000);
                killedPids.Add(locker.ProcessId);
            }
            catch
            {
                allKilled = false;
            }
        }

        return allKilled;
    }
}
