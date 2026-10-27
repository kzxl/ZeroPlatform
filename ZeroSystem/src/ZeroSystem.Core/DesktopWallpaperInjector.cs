using System;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Interacts with Windows DWM to inject windows behind desktop icons via WorkerW canvas.
/// </summary>
public static class DesktopWallpaperInjector
{
    /// <summary>
    /// Attaches an arbitrary window handle (e.g. MediaElement player, WebView2, DirectX canvas)
    /// directly to the Windows Desktop WorkerW canvas behind desktop icons.
    /// </summary>
    public static bool AttachWindowToDesktop(IntPtr childHwnd)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || childHwnd == IntPtr.Zero)
            return false;

        try
        {
            IntPtr progman = NativeMethods.FindWindow("Progman", null);
            if (progman == IntPtr.Zero) return false;

            // Send 0x052C to Progman to spawn WorkerW behind icons
            NativeMethods.SendMessageTimeout(
                progman,
                NativeMethods.WM_SPAWN_WORKER,
                IntPtr.Zero,
                IntPtr.Zero,
                NativeMethods.SMTO_NORMAL,
                1000,
                out _);

            IntPtr workerw = IntPtr.Zero;

            // Enumerate windows to locate the WorkerW directly behind Progman/SHELLDLL_DefView
            NativeMethods.EnumWindows((toplevelHwnd, lParam) =>
            {
                IntPtr shellView = NativeMethods.FindWindowEx(toplevelHwnd, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    // The WorkerW canvas is the sibling right after the window hosting SHELLDLL_DefView
                    workerw = NativeMethods.FindWindowEx(IntPtr.Zero, toplevelHwnd, "WorkerW", null);
                }
                return true;
            }, IntPtr.Zero);

            if (workerw == IntPtr.Zero)
            {
                // Fallback: search any top-level WorkerW
                workerw = NativeMethods.FindWindow("WorkerW", null);
            }

            if (workerw != IntPtr.Zero)
            {
                NativeMethods.SetParent(childHwnd, workerw);
                return true;
            }
        }
        catch
        {
            // Ignored
        }

        return false;
    }
}
