using System;
using System.Windows.Input;
using ZeroUI.Core.Security;

namespace ZeroUI.Wpf.Input
{
    /// <summary>
    /// Global activity listener for WPF applications that automatically resets
    /// the <see cref="SessionContext"/> idle timeout countdown on any mouse, keyboard, or stylus interaction.
    /// </summary>
    public static class ZIdleTimeoutMonitor
    {
        private static bool _isMonitoring;
        private static readonly object _syncLock = new object();

        /// <summary>
        /// Installs the global WPF input pre-processor hook.
        /// </summary>
        public static void StartMonitoring()
        {
            lock (_syncLock)
            {
                if (!_isMonitoring)
                {
                    InputManager.Current.PreProcessInput += OnPreProcessInput;
                    _isMonitoring = true;
                }
            }
        }

        /// <summary>
        /// Uninstalls the global WPF input pre-processor hook.
        /// </summary>
        public static void StopMonitoring()
        {
            lock (_syncLock)
            {
                if (_isMonitoring)
                {
                    InputManager.Current.PreProcessInput -= OnPreProcessInput;
                    _isMonitoring = false;
                }
            }
        }

        private static void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
        {
            var raw = e.StagingItem.Input;
            if (raw is KeyEventArgs ||
                raw is MouseEventArgs ||
                raw is StylusEventArgs ||
                raw is TouchEventArgs)
            {
                SessionContext.Current.NotifyActivity();
            }
        }
    }
}
