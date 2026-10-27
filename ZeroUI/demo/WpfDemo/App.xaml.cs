using System;
using System.IO;
using System.Windows;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Samples.WpfDemo
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wpf_crash.log");
                File.WriteAllText(p, args.ExceptionObject?.ToString());
            };
            DispatcherUnhandledException += (s, args) =>
            {
                var p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wpf_crash.log");
                File.WriteAllText(p, args.Exception?.ToString());
                args.Handled = false;
            };

            base.OnStartup(e);
            ZeroThemeEngine.Initialize(this);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            var p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wpf_exit.log");
            File.WriteAllText(p, $"ExitCode: {e.ApplicationExitCode}\nStackTrace:\n{Environment.StackTrace}");
            base.OnExit(e);
        }
    }
}
