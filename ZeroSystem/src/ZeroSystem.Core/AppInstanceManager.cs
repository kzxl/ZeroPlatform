using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Sovereign single-instance application coordinator.
/// Ensures single-instance application lifetime via system Mutex and activates existing instances cleanly.
/// </summary>
public static class AppInstanceManager
{
    /// <summary>
    /// Attempts to acquire an exclusive system-wide lock for the application.
    /// If acquired, returns true and provides an <see cref="IDisposable"/> token that must be held for the process lifetime.
    /// If another instance already holds the lock, returns false.
    /// </summary>
    /// <param name="appIdentifier">Unique identifier (e.g. "ZeroUniverse.ZProbe" or a GUID).</param>
    /// <param name="instanceLock">The token to hold and dispose upon process exit.</param>
    /// <param name="isGlobal">If true, lock spans all Windows user sessions (Global\); otherwise per-session (Local\).</param>
    public static bool TryAcquireSingleInstance(
        string appIdentifier,
        out IDisposable? instanceLock,
        bool isGlobal = false)
    {
        instanceLock = null;
        if (string.IsNullOrWhiteSpace(appIdentifier))
            return false;

        string prefix = isGlobal ? @"Global\" : @"Local\";
        string mutexName = prefix + appIdentifier.Replace('\\', '_');

        Mutex? mutex = null;
        try
        {
            mutex = new Mutex(true, mutexName, out bool createdNew);
            if (createdNew)
            {
                instanceLock = new SingleInstanceToken(mutex);
                return true;
            }

            // Could not acquire initial ownership
            mutex.Dispose();
            return false;
        }
        catch (AbandonedMutexException)
        {
            // Previous instance terminated unexpectedly; ownership transferred to us
            if (mutex != null)
            {
                instanceLock = new SingleInstanceToken(mutex);
                return true;
            }
            return false;
        }
        catch
        {
            mutex?.Dispose();
            return false;
        }
    }

    /// <summary>
    /// Activates and brings the specified window handle to the foreground.
    /// Restores minimized windows automatically.
    /// </summary>
    public static bool BringWindowToForeground(IntPtr hWnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || hWnd == IntPtr.Zero)
            return false;

        try
        {
            if (NativeMethods.IsIconic(hWnd))
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
            }
            else
            {
                NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }

            return NativeMethods.SetForegroundWindow(hWnd);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Finds and brings the main window of a target process to the foreground.
    /// </summary>
    public static bool BringProcessToForeground(int processId)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || processId <= 0)
            return false;

        try
        {
            using var p = Process.GetProcessById(processId);
            IntPtr hwnd = p.MainWindowHandle;
            if (hwnd != IntPtr.Zero)
            {
                return BringWindowToForeground(hwnd);
            }
        }
        catch
        {
            // Process might have exited or lack main window
        }

        return false;
    }

    #region Private Token

    private sealed class SingleInstanceToken : IDisposable
    {
        private Mutex? _mutex;

        public SingleInstanceToken(Mutex mutex)
        {
            _mutex = mutex;
        }

        public void Dispose()
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch
                {
                    // Ignore release errors on exit
                }

                _mutex.Dispose();
                _mutex = null;
            }
        }
    }

    #endregion
}
