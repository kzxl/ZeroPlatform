using System;

namespace ZeroSystem;

public static partial class StartupManager
{
    #region Windows UserLogin (Registry Run Key)

    private static (bool Success, string Message) RegisterUserLoginWindows(string appName, string exePath, string args)
    {
        string fullCmd = string.IsNullOrEmpty(args) ? $"\"{exePath}\"" : $"\"{exePath}\" {args}";
        string escapedCmd = fullCmd.Replace("\"", "\\\"");
        string regArgs = $"add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"{appName}\" /t REG_SZ /d \"{escapedCmd}\" /f";

        var (code, output, err) = RunProcess("reg.exe", regArgs);
        if (code == 0)
        {
            return (true, $"User autostart registered for '{appName}'. Command: {fullCmd}");
        }

        return (false, $"Registry error: {err} {output}".Trim());
    }

    private static (bool Success, string Message) UnregisterUserLoginWindows(string appName)
    {
        string regArgs = $"delete \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"{appName}\" /f";
        var (code, output, err) = RunProcess("reg.exe", regArgs);
        if (code == 0)
        {
            return (true, $"User autostart unregistered for '{appName}'.");
        }

        return (false, $"Registry error: {err} {output}".Trim());
    }

    private static StartupRegistrationStatus GetUserLoginStatusWindows(string appName)
    {
        var status = new StartupRegistrationStatus
        {
            AppName = appName,
            Scope = StartupScope.UserLogin
        };

        var (code, output, _) = RunProcess("reg.exe", $"query \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run\" /v \"{appName}\"");
        if (code == 0 && output.Contains(appName))
        {
            status.IsEnabled = true;
            status.Details = output.Trim();
        }
        else
        {
            status.IsEnabled = false;
            status.Details = "Registry entry not found.";
        }

        return status;
    }

    #endregion

    #region Windows SystemBoot (Task Scheduler /sc onstart)

    private static (bool Success, string Message) RegisterSystemBootWindows(string appName, string exePath, string args)
    {
        string fullCmd = string.IsNullOrEmpty(args) ? $"\\\"{exePath}\\\"" : $"\\\"{exePath}\\\" {args}";
        string schArgs = $"/create /tn \"{appName}\" /tr \"{fullCmd}\" /sc onstart /ru SYSTEM /rl HIGHEST /f";

        var (code, output, err) = RunProcess("schtasks.exe", schArgs);
        if (code == 0)
        {
            return (true, $"System boot task '{appName}' created successfully.\nCommand: {fullCmd}\nTrigger: Machine startup (SYSTEM account).");
        }

        return (false, $"Task Scheduler error (Run as Administrator required):\n{err} {output}".Trim());
    }

    private static (bool Success, string Message) UnregisterSystemBootWindows(string appName)
    {
        var (code, output, err) = RunProcess("schtasks.exe", $"/delete /tn \"{appName}\" /f");
        if (code == 0)
        {
            return (true, $"System boot task '{appName}' deleted.");
        }

        return (false, $"Failed to delete task: {err} {output}".Trim());
    }

    private static StartupRegistrationStatus GetSystemBootStatusWindows(string appName)
    {
        var status = new StartupRegistrationStatus
        {
            AppName = appName,
            Scope = StartupScope.SystemBoot
        };

        var (code, output, _) = RunProcess("schtasks.exe", $"/query /tn \"{appName}\" /fo LIST");
        if (code == 0)
        {
            status.IsEnabled = true;
            status.Details = output.Trim();
        }
        else
        {
            status.IsEnabled = false;
            status.Details = "System task not found in Task Scheduler.";
        }

        return status;
    }

    private static (bool Success, string Message) StartSystemServiceWindows(string appName)
    {
        var (code, output, err) = RunProcess("schtasks.exe", $"/run /tn \"{appName}\"");
        return (code == 0, code == 0 ? $"Service '{appName}' started." : $"{err} {output}".Trim());
    }

    private static (bool Success, string Message) StopSystemServiceWindows(string appName, string? processImageName)
    {
        RunProcess("schtasks.exe", $"/end /tn \"{appName}\"");
        if (!string.IsNullOrEmpty(processImageName))
        {
            var (code, output, _) = RunProcess("taskkill.exe", $"/f /im {processImageName}");
            return (true, $"Stopped task '{appName}' and process '{processImageName}': {output.Trim()}");
        }

        return (true, $"Stopped task '{appName}'.");
    }

    #endregion
}
