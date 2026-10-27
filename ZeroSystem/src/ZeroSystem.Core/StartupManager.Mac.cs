using System;
using System.IO;
using System.Text;

namespace ZeroSystem;

public static partial class StartupManager
{
    #region macOS UserLogin (LaunchAgents)

    private static (bool Success, string Message) RegisterUserLoginMac(string appName, string exePath, string args)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
            Directory.CreateDirectory(dir);
            string plistPath = Path.Combine(dir, $"com.zerouniverse.{appName.ToLowerInvariant()}.plist");

            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">");
            sb.AppendLine("<plist version=\"1.0\">");
            sb.AppendLine("<dict>");
            sb.AppendLine($"    <key>Label</key><string>com.zerouniverse.{appName.ToLowerInvariant()}</string>");
            sb.AppendLine("    <key>ProgramArguments</key>");
            sb.AppendLine("    <array>");
            sb.AppendLine($"        <string>{exePath}</string>");
            if (!string.IsNullOrEmpty(args))
            {
                foreach (var token in args.Split(' '))
                {
                    if (!string.IsNullOrEmpty(token)) sb.AppendLine($"        <string>{token}</string>");
                }
            }
            sb.AppendLine("    </array>");
            sb.AppendLine("    <key>RunAtLoad</key><true/>");
            sb.AppendLine("    <key>KeepAlive</key><true/>");
            sb.AppendLine("</dict>");
            sb.AppendLine("</plist>");

            File.WriteAllText(plistPath, sb.ToString());
            return (true, $"macOS LaunchAgent created at: {plistPath}");
        }
        catch (Exception ex)
        {
            return (false, $"Failed to create macOS LaunchAgent: {ex.Message}");
        }
    }

    private static (bool Success, string Message) UnregisterUserLoginMac(string appName)
    {
        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", $"com.zerouniverse.{appName.ToLowerInvariant()}.plist");
        if (File.Exists(file))
        {
            File.Delete(file);
            return (true, $"Removed LaunchAgent: {file}");
        }

        return (true, "No LaunchAgent found.");
    }

    private static StartupRegistrationStatus GetUserLoginStatusMac(string appName)
    {
        var status = new StartupRegistrationStatus
        {
            AppName = appName,
            Scope = StartupScope.UserLogin
        };

        string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", $"com.zerouniverse.{appName.ToLowerInvariant()}.plist");
        status.IsEnabled = File.Exists(file);
        status.Details = status.IsEnabled ? $"Plist: {file}" : "LaunchAgent plist not found.";

        return status;
    }

    #endregion
}
