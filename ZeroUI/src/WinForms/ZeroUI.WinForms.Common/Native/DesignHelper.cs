using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Native
{
    /// <summary>
    /// Utility helper for robust Visual Studio Form Designer and DesignMode detection.
    /// Prevents background threads, animations, and timers from executing inside devenv.exe.
    /// </summary>
    public static class DesignHelper
    {
        private static bool? _isDevenv;

        /// <summary>
        /// Returns true if the control is currently being rendered inside Visual Studio Designer.
        /// </summary>
        public static bool IsInDesignMode(Control? control = null)
        {
            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return true;

            if (control != null)
            {
                if (control.Site != null && control.Site.DesignMode)
                    return true;

                // Traverse parent hierarchy for nested controls
                var parent = control.Parent;
                while (parent != null)
                {
                    if (parent.Site != null && parent.Site.DesignMode)
                        return true;
                    parent = parent.Parent;
                }
            }

            if (!_isDevenv.HasValue)
            {
                try
                {
                    string procName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                    _isDevenv = procName.IndexOf("devenv", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                procName.IndexOf("DesignToolsServer", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch
                {
                    _isDevenv = false;
                }
            }

            return _isDevenv.Value;
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="DesignHelper"/>.
    /// </summary>
    public static class ZeroDesignHelper
    {
        public static bool IsInDesignMode(Control? control = null) => DesignHelper.IsInDesignMode(control);
    }
}
