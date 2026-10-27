using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Manages MS-DOS device name mapping via DefineDosDevice (DDD_RAW_TARGET_PATH).
/// Commonly used to mount VSS shadow copies or raw NT disk paths into standard drive letters.
/// </summary>
public static class DosDeviceManager
{
    /// <summary>
    /// Scans available logical drive letters from 'Z' down to 'D' to locate an unused letter.
    /// </summary>
    public static char? FindAvailableDriveLetter()
    {
        var taken = new HashSet<char>();
        foreach (var d in Directory.GetLogicalDrives())
        {
            if (!string.IsNullOrEmpty(d))
            {
                taken.Add(char.ToUpperInvariant(d[0]));
            }
        }

        for (char c = 'Z'; c >= 'D'; c--)
        {
            if (!taken.Contains(c))
                return c;
        }

        if (!taken.Contains('B')) return 'B';
        return null;
    }

    /// <summary>
    /// Mounts an NT device path (e.g. \\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1) to an unused DOS drive letter.
    /// </summary>
    public static bool TryMountDevice(string targetDevicePath, out char mountedDriveLetter, out string errorMessage)
    {
        mountedDriveLetter = '\0';
        errorMessage = string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            errorMessage = "DosDeviceManager is only supported on Windows.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetDevicePath))
        {
            errorMessage = "Target device path cannot be empty.";
            return false;
        }

        var driveOpt = FindAvailableDriveLetter();
        if (!driveOpt.HasValue)
        {
            errorMessage = "No unused drive letters available to mount device.";
            return false;
        }

        var driveLetter = driveOpt.Value;
        var deviceName = $"{driveLetter}:";

        var targetDevice = targetDevicePath.TrimEnd('\\');
        if (targetDevice.StartsWith(@"\\?\GLOBALROOT", StringComparison.OrdinalIgnoreCase))
        {
            targetDevice = targetDevice.Substring(@"\\?\GLOBALROOT".Length);
        }

        bool result = NativeMethods.DefineDosDevice(NativeMethods.DDD_RAW_TARGET_PATH, deviceName, targetDevice);
        if (!result)
        {
            int errCode = Marshal.GetLastWin32Error();
            errorMessage = $"DefineDosDevice failed with Win32 error code {errCode}.";
            return false;
        }

        mountedDriveLetter = driveLetter;
        return true;
    }

    /// <summary>
    /// Removes a previously mounted DOS device letter.
    /// </summary>
    public static bool TryUnmountDevice(char driveLetter, string? targetDevicePath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            errorMessage = "DosDeviceManager is only supported on Windows.";
            return false;
        }

        var deviceName = $"{char.ToUpperInvariant(driveLetter)}:";
        string? targetDevice = null;

        if (!string.IsNullOrWhiteSpace(targetDevicePath))
        {
            targetDevice = targetDevicePath.TrimEnd('\\');
            if (targetDevice.StartsWith(@"\\?\GLOBALROOT", StringComparison.OrdinalIgnoreCase))
            {
                targetDevice = targetDevice.Substring(@"\\?\GLOBALROOT".Length);
            }
        }

        bool result = false;
        if (targetDevice != null)
        {
            result = NativeMethods.DefineDosDevice(
                NativeMethods.DDD_REMOVE_DEFINITION | NativeMethods.DDD_EXACT_MATCH_ON_REMOVE,
                deviceName,
                targetDevice);
        }

        if (!result)
        {
            result = NativeMethods.DefineDosDevice(NativeMethods.DDD_REMOVE_DEFINITION, deviceName, null);
        }

        if (!result)
        {
            int errCode = Marshal.GetLastWin32Error();
            errorMessage = $"Failed to remove DOS device for '{deviceName}' (Win32 error: {errCode}).";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Converts an original absolute file path (e.g. C:\Data\app.db) into its corresponding path on the mounted drive (e.g. Z:\Data\app.db).
    /// </summary>
    public static string ResolveMountedPath(string originalFullPath, char mountedDriveLetter)
    {
        if (string.IsNullOrWhiteSpace(originalFullPath)) return originalFullPath;

        var fullPath = Path.GetFullPath(originalFullPath);
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root)) return originalFullPath;

        var relativePath = fullPath.Substring(root.Length);
        return $"{char.ToUpperInvariant(mountedDriveLetter)}:\\{relativePath}";
    }
}
