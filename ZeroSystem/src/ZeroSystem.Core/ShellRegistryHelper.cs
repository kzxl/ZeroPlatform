using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ZeroSystem;

/// <summary>
/// Manages Windows Explorer shell context menu integration under HKCU\Software\Classes without requiring elevation.
/// </summary>
[SupportedOSPlatform("windows")]
public static class ShellRegistryHelper
{
    /// <summary>
    /// Registers a custom verb for an extension or file type under HKCU\Software\Classes.
    /// E.g. targetKey: "*" (all files), "Directory" (all folders), or ".ztar"
    /// </summary>
    public static bool RegisterContextMenuVerb(string targetKey, string verbName, string menuDisplayText, string commandLine, string? iconPath = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        try
        {
            var subPath = $@"Software\Classes\{targetKey}\shell\{verbName}";
            using var verbKey = Registry.CurrentUser.CreateSubKey(subPath);
            if (verbKey == null) return false;

            verbKey.SetValue("", menuDisplayText);
            if (!string.IsNullOrEmpty(iconPath))
            {
                verbKey.SetValue("Icon", iconPath);
            }

            using var cmdKey = verbKey.CreateSubKey("command");
            if (cmdKey == null) return false;

            cmdKey.SetValue("", commandLine);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Removes a custom verb from HKCU\Software\Classes.
    /// </summary>
    public static bool UnregisterContextMenuVerb(string targetKey, string verbName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        try
        {
            var parentPath = $@"Software\Classes\{targetKey}\shell";
            using var parentKey = Registry.CurrentUser.OpenSubKey(parentPath, writable: true);
            if (parentKey != null)
            {
                parentKey.DeleteSubKeyTree(verbName, throwOnMissingSubKey: false);
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Checks if a custom verb is currently registered.
    /// </summary>
    public static bool IsContextMenuVerbRegistered(string targetKey, string verbName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        try
        {
            var subPath = $@"Software\Classes\{targetKey}\shell\{verbName}";
            using var key = Registry.CurrentUser.OpenSubKey(subPath);
            return key != null;
        }
        catch
        {
            return false;
        }
    }
}
