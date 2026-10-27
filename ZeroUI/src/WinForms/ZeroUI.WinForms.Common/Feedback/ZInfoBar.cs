using System;
using System.ComponentModel;
using System.Drawing;
using ZeroUI.WinForms.Icons;

namespace ZeroUI.WinForms.Feedback
{
    public enum InfoBarSeverity
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Modern in-window banner notification alert matching WinUI 3 / Fluent standards.
    /// Provides 100% parity with WPF InfoBar control.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Feedback")]
    [DefaultProperty("Title")]
    [DefaultEvent("Closed")]
    [Description("Modern in-window banner notification alert matching WinUI 3 standards")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroAlertBanner.bmp")]
    public class ZInfoBar : ZAlertBanner
    {
        [Category("Appearance")]
        [DefaultValue(InfoBarSeverity.Info)]
        public new InfoBarSeverity Severity
        {
            get => (InfoBarSeverity)(int)base.Severity;
            set => base.Severity = (ZeroAlertSeverity)(int)value;
        }

        [Category("Behavior")]
        [DefaultValue(true)]
        public bool IsOpen
        {
            get => Visible;
            set => Visible = value;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZInfoBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("InfoBar is deprecated and will be removed in 5 release cycles. Please migrate to ZInfoBar instead.")]
    [ToolboxItem(false)]
    public class InfoBar : ZInfoBar
    {
    }

    #endregion
}
