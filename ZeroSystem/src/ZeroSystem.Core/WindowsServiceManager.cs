using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroSystem.Native;

namespace ZeroSystem;

#region Service Data Models

/// <summary>
/// Status of a Windows system service.
/// </summary>
public enum ServiceState
{
    NotFound = 0,
    Stopped = 1,
    StartPending = 2,
    StopPending = 3,
    Running = 4,
    ContinuePending = 5,
    PausePending = 6,
    Paused = 7
}

/// <summary>
/// Detailed metadata snapshot of a Windows system service.
/// </summary>
public sealed record ServiceDetails(
    string ServiceName,
    ServiceState State,
    int ProcessId,
    bool CanStop,
    uint Win32ExitCode);

#endregion

/// <summary>
/// Sovereign Windows Service Controller.
/// Controls and queries system services using pure Win32 Service Control Manager (Advapi32) APIs with zero System.ServiceProcess dependencies.
/// </summary>
public static class WindowsServiceManager
{
    /// <summary>
    /// Checks whether a Windows service with the specified name exists.
    /// </summary>
    public static bool ServiceExists(string serviceName)
    {
        return GetServiceStatus(serviceName) != ServiceState.NotFound;
    }

    /// <summary>
    /// Gets the current execution state of a Windows service.
    /// </summary>
    public static ServiceState GetServiceStatus(string serviceName)
    {
        var details = GetServiceDetails(serviceName);
        return details?.State ?? ServiceState.NotFound;
    }

    /// <summary>
    /// Queries complete runtime details of a Windows service.
    /// </summary>
    public static ServiceDetails? GetServiceDetails(string serviceName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || string.IsNullOrWhiteSpace(serviceName))
            return null;

        IntPtr scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) return null;

        try
        {
            IntPtr service = NativeMethods.OpenService(scm, serviceName, NativeMethods.SERVICE_QUERY_STATUS);
            if (service == IntPtr.Zero) return null;

            try
            {
                int bufSize = Marshal.SizeOf<NativeMethods.SERVICE_STATUS_PROCESS>();
                IntPtr buf = Marshal.AllocHGlobal(bufSize);
                try
                {
                    if (NativeMethods.QueryServiceStatusEx(
                            service,
                            NativeMethods.SC_STATUS_PROCESS_INFO,
                            buf,
                            (uint)bufSize,
                            out _))
                    {
                        var status = Marshal.PtrToStructure<NativeMethods.SERVICE_STATUS_PROCESS>(buf);
                        var state = (ServiceState)status.dwCurrentState;
                        bool canStop = (status.dwControlsAccepted & NativeMethods.SERVICE_CONTROL_STOP) != 0;

                        return new ServiceDetails(
                            ServiceName: serviceName,
                            State: state,
                            ProcessId: (int)status.dwProcessId,
                            CanStop: canStop,
                            Win32ExitCode: status.dwWin32ExitCode);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            finally
            {
                NativeMethods.CloseServiceHandle(service);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }

        return null;
    }

    /// <summary>
    /// Starts the specified service and optionally waits until it enters the Running state.
    /// </summary>
    public static bool StartService(string serviceName, TimeSpan timeout = default)
    {
        if (timeout == default) timeout = TimeSpan.FromSeconds(10);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || string.IsNullOrWhiteSpace(serviceName))
            return false;

        IntPtr scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) return false;

        try
        {
            IntPtr service = NativeMethods.OpenService(
                scm,
                serviceName,
                NativeMethods.SERVICE_START | NativeMethods.SERVICE_QUERY_STATUS);

            if (service == IntPtr.Zero) return false;

            try
            {
                if (!NativeMethods.StartService(service, 0, IntPtr.Zero))
                {
                    int err = Marshal.GetLastWin32Error();
                    // 1056 = ERROR_SERVICE_ALREADY_RUNNING
                    if (err != 1056) return false;
                }

                return WaitForServiceState(service, ServiceState.Running, timeout);
            }
            finally
            {
                NativeMethods.CloseServiceHandle(service);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    /// <summary>
    /// Stops the specified service and optionally waits until it enters the Stopped state.
    /// </summary>
    public static bool StopService(string serviceName, TimeSpan timeout = default)
    {
        if (timeout == default) timeout = TimeSpan.FromSeconds(10);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || string.IsNullOrWhiteSpace(serviceName))
            return false;

        IntPtr scm = NativeMethods.OpenSCManager(null, null, NativeMethods.SC_MANAGER_CONNECT);
        if (scm == IntPtr.Zero) return false;

        try
        {
            IntPtr service = NativeMethods.OpenService(
                scm,
                serviceName,
                NativeMethods.SERVICE_STOP | NativeMethods.SERVICE_QUERY_STATUS);

            if (service == IntPtr.Zero) return false;

            try
            {
                var status = new NativeMethods.SERVICE_STATUS();
                if (!NativeMethods.ControlService(service, NativeMethods.SERVICE_CONTROL_STOP, ref status))
                {
                    int err = Marshal.GetLastWin32Error();
                    // 1062 = ERROR_SERVICE_NOT_ACTIVE
                    if (err == 1062) return true;
                    return false;
                }

                return WaitForServiceState(service, ServiceState.Stopped, timeout);
            }
            finally
            {
                NativeMethods.CloseServiceHandle(service);
            }
        }
        finally
        {
            NativeMethods.CloseServiceHandle(scm);
        }
    }

    #region Private Helpers

    private static bool WaitForServiceState(IntPtr service, ServiceState desiredState, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        int bufSize = Marshal.SizeOf<NativeMethods.SERVICE_STATUS_PROCESS>();
        IntPtr buf = Marshal.AllocHGlobal(bufSize);

        try
        {
            while (sw.Elapsed < timeout)
            {
                if (NativeMethods.QueryServiceStatusEx(
                        service,
                        NativeMethods.SC_STATUS_PROCESS_INFO,
                        buf,
                        (uint)bufSize,
                        out _))
                {
                    var status = Marshal.PtrToStructure<NativeMethods.SERVICE_STATUS_PROCESS>(buf);
                    if ((ServiceState)status.dwCurrentState == desiredState)
                        return true;
                }

                Thread.Sleep(200);
            }

            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    #endregion
}
