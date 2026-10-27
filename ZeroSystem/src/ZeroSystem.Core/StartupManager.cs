using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ZeroSystem.Native;

namespace ZeroSystem;

/// <summary>
/// Specifies the autostart execution scope for an application.
/// </summary>
public enum StartupScope
{
    /// <summary>
    /// Starts when the user logs into their desktop session. Does not require administrative elevation.
    /// Windows: Registry Run Key; Linux: ~/.config/autostart; macOS: ~/Library/LaunchAgents.
    /// </summary>
    UserLogin,

    /// <summary>
    /// Starts as a background service at system boot under elevated privileges (e.g. SYSTEM / root).
    /// Windows: Task Scheduler (/sc onstart); Linux: systemd (/etc/systemd/system); macOS: /Library/LaunchDaemons.
    /// </summary>
    SystemBoot
}

/// <summary>
/// Represents the registration status of an autostart application.
/// </summary>
public sealed class StartupRegistrationStatus
{
    public bool IsEnabled { get; set; }
    public StartupScope Scope { get; set; }
    public string AppName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

/// <summary>
/// Sovereign, zero-dependency platform manager for background execution, console visibility,
/// and user login / system boot autostart across Windows, Linux, and macOS.
/// Partitioned into partial classes by operating system platform.
/// </summary>
public static partial class StartupManager
{
    #region OS Platform Helpers

    public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    public static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    #endregion

    #region Console Visibility Management

    /// <summary>
    /// Hides the current console window on Windows to run silently in the background.
    /// Safe to call on any platform or inside non-console processes (no-op).
    /// </summary>
    public static bool HideConsoleWindow()
    {
        if (!IsWindows) return false;

        try
        {
            IntPtr hWnd = NativeMethods.GetConsoleWindow();
            if (hWnd != IntPtr.Zero)
            {
                return NativeMethods.ShowWindow(hWnd, NativeMethods.SW_HIDE);
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Shows the current console window on Windows if previously hidden.
    /// </summary>
    public static bool ShowConsoleWindow()
    {
        if (!IsWindows) return false;

        try
        {
            IntPtr hWnd = NativeMethods.GetConsoleWindow();
            if (hWnd != IntPtr.Zero)
            {
                return NativeMethods.ShowWindow(hWnd, NativeMethods.SW_SHOW);
            }
        }
        catch { }

        return false;
    }

    /// <summary>
    /// Checks whether the current process console window is currently visible.
    /// </summary>
    public static bool IsConsoleVisible()
    {
        if (!IsWindows) return true;

        try
        {
            IntPtr hWnd = NativeMethods.GetConsoleWindow();
            if (hWnd != IntPtr.Zero)
            {
                return NativeMethods.IsWindowVisible(hWnd);
            }
        }
        catch { }

        return false;
    }

    #endregion

    #region Path & Logging Utilities

    /// <summary>
    /// Resolves the current executing binary path across .NET versions and OS platforms.
    /// </summary>
    public static string GetCurrentExecutablePath()
    {
        string? processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            return processPath;
        }

        try
        {
            string? mainModule = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(mainModule))
            {
                return mainModule;
            }
        }
        catch { }

        return AppDomain.CurrentDomain.FriendlyName;
    }

    /// <summary>
    /// Returns a standard persistent application logging directory and file path.
    /// </summary>
    /// <param name="appName">Application identifier (e.g. "ZPrint", "ZeroApp").</param>
    /// <param name="logFileName">Log file name (default: "service.log").</param>
    public static string GetDefaultLogPath(string appName, string logFileName = "service.log")
    {
        if (string.IsNullOrWhiteSpace(appName)) throw new ArgumentException("App name cannot be empty.", nameof(appName));

        string baseDir;
        if (IsWindows)
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
        }
        else
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".{appName.ToLowerInvariant()}");
        }

        if (!Directory.Exists(baseDir))
        {
            try { Directory.CreateDirectory(baseDir); } catch { }
        }

        return Path.Combine(baseDir, logFileName);
    }

    #endregion

    #region Registration & Unregistration Dispatch

    /// <summary>
    /// Registers the application to start automatically on login or system boot.
    /// </summary>
    public static (bool Success, string Message) Register(
        string appName,
        string? executablePath = null,
        string arguments = "",
        StartupScope scope = StartupScope.UserLogin)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("App name cannot be empty.", nameof(appName));

        string exe = string.IsNullOrEmpty(executablePath) ? GetCurrentExecutablePath() : executablePath!;
        string trimmedArgs = arguments?.Trim() ?? string.Empty;

        if (scope == StartupScope.UserLogin)
        {
            if (IsWindows) return RegisterUserLoginWindows(appName, exe, trimmedArgs);
            if (IsLinux) return RegisterUserLoginLinux(appName, exe, trimmedArgs);
            if (IsMacOS) return RegisterUserLoginMac(appName, exe, trimmedArgs);
            return (false, "Platform not supported for user autostart.");
        }
        else
        {
            if (IsWindows) return RegisterSystemBootWindows(appName, exe, trimmedArgs);
            if (IsLinux) return RegisterSystemBootLinux(appName, exe, trimmedArgs);
            return (false, "System boot service not supported on this platform.");
        }
    }

    /// <summary>
    /// Removes the application from automatic startup.
    /// </summary>
    public static (bool Success, string Message) Unregister(string appName, StartupScope scope = StartupScope.UserLogin)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("App name cannot be empty.", nameof(appName));

        if (scope == StartupScope.UserLogin)
        {
            if (IsWindows) return UnregisterUserLoginWindows(appName);
            if (IsLinux) return UnregisterUserLoginLinux(appName);
            if (IsMacOS) return UnregisterUserLoginMac(appName);
            return (false, "Platform not supported.");
        }
        else
        {
            if (IsWindows) return UnregisterSystemBootWindows(appName);
            if (IsLinux) return UnregisterSystemBootLinux(appName);
            return (false, "Platform not supported.");
        }
    }

    /// <summary>
    /// Queries the current registration status of the application for the given scope.
    /// </summary>
    public static StartupRegistrationStatus GetStatus(string appName, StartupScope scope = StartupScope.UserLogin)
    {
        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("App name cannot be empty.", nameof(appName));

        if (scope == StartupScope.UserLogin)
        {
            if (IsWindows) return GetUserLoginStatusWindows(appName);
            if (IsLinux) return GetUserLoginStatusLinux(appName);
            if (IsMacOS) return GetUserLoginStatusMac(appName);
            return new StartupRegistrationStatus { AppName = appName, Scope = StartupScope.UserLogin, Details = "Platform not supported." };
        }
        else
        {
            if (IsWindows) return GetSystemBootStatusWindows(appName);
            if (IsLinux) return GetSystemBootStatusLinux(appName);
            return new StartupRegistrationStatus { AppName = appName, Scope = StartupScope.SystemBoot, Details = "Platform not supported." };
        }
    }

    /// <summary>
    /// Starts the system-level background task or service immediately.
    /// </summary>
    public static (bool Success, string Message) StartSystemService(string appName)
    {
        if (IsWindows) return StartSystemServiceWindows(appName);
        if (IsLinux) return StartSystemServiceLinux(appName);
        return (false, "Platform not supported.");
    }

    /// <summary>
    /// Stops the system-level background task and terminates associated processes.
    /// </summary>
    public static (bool Success, string Message) StopSystemService(string appName, string? processImageName = null)
    {
        if (IsWindows) return StopSystemServiceWindows(appName, processImageName);
        if (IsLinux) return StopSystemServiceLinux(appName);
        return (false, "Platform not supported.");
    }

    #endregion

    #region Internal Process Runner Helper

    internal static (int ExitCode, string Output, string Error) RunProcess(string fileName, string arguments)
    {
        try
        {
            using var p = new Process();
            p.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            string err = p.StandardError.ReadToEnd();
            p.WaitForExit(5000);

            return (p.ExitCode, output, err);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    #endregion
}
