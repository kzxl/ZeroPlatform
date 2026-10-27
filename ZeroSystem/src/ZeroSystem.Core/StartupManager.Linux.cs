using System;
using System.IO;

namespace ZeroSystem;

public static partial class StartupManager
{
    #region Linux UserLogin (~/.config/autostart)

    private static (bool Success, string Message) RegisterUserLoginLinux(string appName, string exePath, string args)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, $"{appName.ToLowerInvariant()}.desktop");

            string content = $"[Desktop Entry]\nType=Application\nName={appName}\nExec=\"{exePath}\" {args}\nHidden=false\nNoDisplay=false\nX-GNOME-Autostart-enabled=true\n";
            File.WriteAllText(file, content);
            return (true, $"Linux desktop autostart written to: {file}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to create desktop autostart file: {ex.Message}");
        }
    }

    private static (bool Success, string Message) UnregisterUserLoginLinux(string appName)
    {
        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", $"{appName.ToLowerInvariant()}.desktop");
        if (File.Exists(file))
        {
            File.Delete(file);
            return (true, $"Removed autostart file: {file}");
        }

        return (true, "No autostart file found.");
    }

    private static StartupRegistrationStatus GetUserLoginStatusLinux(string appName)
    {
        var status = new StartupRegistrationStatus
        {
            AppName = appName,
            Scope = StartupScope.UserLogin
        };

        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart", $"{appName.ToLowerInvariant()}.desktop");
        status.IsEnabled = File.Exists(file);
        status.Details = status.IsEnabled ? $"Config file: {file}" : "Desktop autostart file not found.";

        return status;
    }

    #endregion

    #region Linux SystemBoot (systemd)

    private static (bool Success, string Message) RegisterSystemBootLinux(string appName, string exePath, string args)
    {
        try
        {
            string unit = $@"[Unit]
Description={appName} Daemon Service
After=network.target

[Service]
Type=simple
ExecStart=""{exePath}"" {args}
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
";
            string unitPath = $"/etc/systemd/system/{appName.ToLowerInvariant()}.service";
            File.WriteAllText(unitPath, unit);
            RunProcess("systemctl", "daemon-reload");
            RunProcess("systemctl", $"enable {appName.ToLowerInvariant()}");
            return (true, $"Systemd service installed to {unitPath} and enabled.\nRun: sudo systemctl start {appName.ToLowerInvariant()}");
        }
        catch (Exception ex)
        {
            return (false, $"Systemd service installation failed (sudo required): {ex.Message}");
        }
    }

    private static (bool Success, string Message) UnregisterSystemBootLinux(string appName)
    {
        string svc = appName.ToLowerInvariant();
        RunProcess("systemctl", $"stop {svc}");
        RunProcess("systemctl", $"disable {svc}");
        string unitPath = $"/etc/systemd/system/{svc}.service";
        if (File.Exists(unitPath))
        {
            try { File.Delete(unitPath); } catch { }
            RunProcess("systemctl", "daemon-reload");
        }

        return (true, $"Systemd service {svc} removed.");
    }

    private static StartupRegistrationStatus GetSystemBootStatusLinux(string appName)
    {
        var status = new StartupRegistrationStatus
        {
            AppName = appName,
            Scope = StartupScope.SystemBoot
        };

        var (code, output, _) = RunProcess("systemctl", $"status {appName.ToLowerInvariant()}");
        status.IsEnabled = code == 0;
        status.Details = output.Trim();

        return status;
    }

    private static (bool Success, string Message) StartSystemServiceLinux(string appName)
    {
        var (code, output, err) = RunProcess("systemctl", $"start {appName.ToLowerInvariant()}");
        return (code == 0, code == 0 ? $"Service '{appName}' started." : $"{err} {output}".Trim());
    }

    private static (bool Success, string Message) StopSystemServiceLinux(string appName)
    {
        var (code, output, err) = RunProcess("systemctl", $"stop {appName.ToLowerInvariant()}");
        return (code == 0, code == 0 ? $"Service '{appName}' stopped." : $"{err} {output}".Trim());
    }

    #endregion
}
