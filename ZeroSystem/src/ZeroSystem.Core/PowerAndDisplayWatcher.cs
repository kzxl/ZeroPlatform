using System;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Monitors system AC/battery power and foreground fullscreen state (e.g. gaming, video playback).
/// </summary>
public static class PowerAndDisplayWatcher
{
    /// <summary>
    /// Checks if the device is currently operating on battery power (AC offline).
    /// </summary>
    public static bool IsRunningOnBattery()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        try
        {
            if (NativeMethods.GetSystemPowerStatus(out var status))
            {
                // ACLineStatus: 0 = Offline, 1 = Online, 255 = Unknown
                return status.ACLineStatus == 0;
            }
        }
        catch
        {
            // Ignored
        }

        return false;
    }

    /// <summary>
    /// Checks whether the currently active foreground window occupies the entire display area (e.g. Fullscreen game, PowerPoint, movie).
    /// </summary>
    public static bool IsForegroundAppFullScreen()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        try
        {
            IntPtr fgHwnd = NativeMethods.GetForegroundWindow();
            if (fgHwnd == IntPtr.Zero) return false;

            IntPtr desktopHwnd = NativeMethods.GetDesktopWindow();
            IntPtr shellHwnd = NativeMethods.GetShellWindow();

            // Ignore desktop itself and shell
            if (fgHwnd == desktopHwnd || fgHwnd == shellHwnd) return false;

            if (NativeMethods.GetWindowRect(fgHwnd, out var fgRect) &&
                NativeMethods.GetWindowRect(desktopHwnd, out var dtRect))
            {
                // If foreground window is at least as large as the desktop bounding rect
                return fgRect.Left <= dtRect.Left &&
                       fgRect.Top <= dtRect.Top &&
                       fgRect.Right >= dtRect.Right &&
                       fgRect.Bottom >= dtRect.Bottom;
            }
        }
        catch
        {
            // Ignored
        }

        return false;
    }
}
