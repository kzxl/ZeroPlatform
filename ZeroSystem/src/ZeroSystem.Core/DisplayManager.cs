using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

#region Display Data Models

/// <summary>
/// Screen rectangle in physical or virtual pixel coordinates.
/// </summary>
public readonly record struct DisplayBounds(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

/// <summary>
/// Detailed diagnostic snapshot of an active monitor or display output.
/// </summary>
public sealed record MonitorSnapshot(
    string DeviceName,
    DisplayBounds Bounds,
    DisplayBounds WorkArea,
    bool IsPrimary,
    uint DpiX,
    uint DpiY,
    float ScalePercent);

/// <summary>
/// Composite bounding box spanning all active monitors in the virtual desktop.
/// </summary>
public readonly record struct VirtualDesktopBounds(int Left, int Top, int Width, int Height);

#endregion

/// <summary>
/// Sovereign display and multi-monitor manager.
/// Queries monitor layout, work areas, per-monitor DPI scaling, and virtual screen geometry without WinForms / WPF dependencies.
/// </summary>
public static class DisplayManager
{
    /// <summary>
    /// Enumerates all active display monitors connected to the system.
    /// </summary>
    public static IReadOnlyList<MonitorSnapshot> GetMonitors()
    {
        var monitors = new List<MonitorSnapshot>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return monitors;

        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (IntPtr hMonitor, IntPtr hdcMonitor, ref NativeMethods.RECT lprcMonitor, IntPtr dwData) =>
            {
                var mi = new NativeMethods.MONITORINFOEX
                {
                    cbSize = (uint)Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
                };

                if (NativeMethods.GetMonitorInfo(hMonitor, ref mi))
                {
                    bool isPrimary = (mi.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0;

                    uint dpiX = 96;
                    uint dpiY = 96;

                    try
                    {
                        // 0 = MDT_EFFECTIVE_DPI
                        int hr = NativeMethods.GetDpiForMonitor(hMonitor, 0, out dpiX, out dpiY);
                        if (hr != 0 || dpiX == 0)
                        {
                            dpiX = 96;
                            dpiY = 96;
                        }
                    }
                    catch
                    {
                        // SHCore not available fallback
                        dpiX = 96;
                        dpiY = 96;
                    }

                    float scalePercent = (float)dpiX / 96.0f * 100.0f;

                    var bounds = new DisplayBounds(
                        mi.rcMonitor.Left,
                        mi.rcMonitor.Top,
                        mi.rcMonitor.Width,
                        mi.rcMonitor.Height);

                    var workArea = new DisplayBounds(
                        mi.rcWork.Left,
                        mi.rcWork.Top,
                        mi.rcWork.Width,
                        mi.rcWork.Height);

                    monitors.Add(new MonitorSnapshot(
                        DeviceName: mi.szDevice ?? "DISPLAY",
                        Bounds: bounds,
                        WorkArea: workArea,
                        IsPrimary: isPrimary,
                        DpiX: dpiX,
                        DpiY: dpiY,
                        ScalePercent: scalePercent));
                }

                return true; // continue enumeration
            },
            IntPtr.Zero);

        return monitors;
    }

    /// <summary>
    /// Gets the primary display monitor, or null if no monitors are detected.
    /// </summary>
    public static MonitorSnapshot? GetPrimaryMonitor()
    {
        var monitors = GetMonitors();
        foreach (var m in monitors)
        {
            if (m.IsPrimary) return m;
        }

        return monitors.Count > 0 ? monitors[0] : null;
    }

    /// <summary>
    /// Gets the bounding rectangle of the virtual screen spanning all monitors.
    /// </summary>
    public static VirtualDesktopBounds GetVirtualScreenBounds()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new VirtualDesktopBounds(0, 0, 1920, 1080);

        int left = NativeMethods.GetSystemMetrics(NativeMethods.SM_XVIRTUALSCREEN);
        int top = NativeMethods.GetSystemMetrics(NativeMethods.SM_YVIRTUALSCREEN);
        int width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXVIRTUALSCREEN);
        int height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYVIRTUALSCREEN);

        if (width <= 0) width = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        if (height <= 0) height = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

        return new VirtualDesktopBounds(left, top, width, height);
    }
}
