using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZeroSystem;

public static partial class HardwareTelemetry
{
    private static long _lastIdleTime;
    private static long _lastKernelTime;
    private static long _lastUserTime;
    private static readonly object _cpuLock = new();

    static HardwareTelemetry()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            UpdateCpuTimes(out _lastIdleTime, out _lastKernelTime, out _lastUserTime);
        }
    }

    /// <summary>
    /// Gets CPU model name, core counts, and real-time load percentage.
    /// </summary>
    public static CpuInfo GetCpuInfo()
    {
        string model = "Unknown Processor";
        int logicalCores = Environment.ProcessorCount;
        int physicalCores = logicalCores; // fallback

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                if (key?.GetValue("ProcessorNameString") is string name)
                {
                    model = name.Trim();
                }
            }
            catch
            {
                // Registry read failure fallback
            }

            try
            {
                // Estimate physical cores from distinct Core IDs if available or logical/2 for hyperthreaded
                int coreCount = 0;
                using var cpuRoot = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor");
                if (cpuRoot != null)
                {
                    coreCount = cpuRoot.SubKeyCount;
                }
                if (coreCount > 0)
                {
                    logicalCores = coreCount;
                }
            }
            catch
            {
                // Keep default
            }
        }

        float usage = SampleCpuUsagePercent();

        return new CpuInfo(
            ModelName: model,
            LogicalProcessorCount: logicalCores,
            PhysicalCoreCount: physicalCores,
            CpuUsagePercent: usage);
    }

    /// <summary>
    /// Samples instant CPU usage percentage via native GetSystemTimes.
    /// </summary>
    public static float SampleCpuUsagePercent()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return 0.0f;

        lock (_cpuLock)
        {
            if (!UpdateCpuTimes(out long idle, out long kernel, out long user))
                return 0.0f;

            long usrDiff = user - _lastUserTime;
            long kerDiff = kernel - _lastKernelTime;
            long idlDiff = idle - _lastIdleTime;

            long sysTotal = usrDiff + kerDiff;
            long sysTotalMinusIdle = sysTotal - idlDiff;

            _lastIdleTime = idle;
            _lastKernelTime = kernel;
            _lastUserTime = user;

            if (sysTotal <= 0 || sysTotalMinusIdle < 0)
                return 0.0f;

            float percent = (float)sysTotalMinusIdle * 100.0f / sysTotal;
            return Math.Clamp(percent, 0.0f, 100.0f);
        }
    }

    private static bool UpdateCpuTimes(out long idle, out long kernel, out long user)
    {
        idle = 0;
        kernel = 0;
        user = 0;

        if (!GetSystemTimes(out var ftIdle, out var ftKernel, out var ftUser))
            return false;

        idle = ToLong(ftIdle);
        kernel = ToLong(ftKernel);
        user = ToLong(ftUser);
        return true;
    }

    private static long ToLong(FILETIME ft)
    {
        return ((long)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);
}
