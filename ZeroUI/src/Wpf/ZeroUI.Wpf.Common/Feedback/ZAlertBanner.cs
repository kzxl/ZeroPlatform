using System;

namespace ZeroUI.Wpf.Feedback
{
    public enum ZeroAlertSeverity
    {
        Info = 0,
        Success = 1,
        Warning = 2,
        Error = 3
    }

    /// <summary>
    /// Modern dismissible alert banner for factory floor stoppage, defect alarms, and system broadcasts.
    /// Provides 100% parity with WinForms AlertBanner control.
    /// </summary>
    public class ZAlertBanner : ZInfoBar
    {
        public ZeroAlertSeverity AlertSeverity
        {
            get => (ZeroAlertSeverity)(int)Severity;
            set => Severity = (InfoBarSeverity)(int)value;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZAlertBanner"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("AlertBanner is deprecated and will be removed in 5 release cycles. Please migrate to ZAlertBanner instead.")]
    public class AlertBanner : ZAlertBanner
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZAlertBanner"/>.
    /// </summary>
    [Obsolete("ZeroAlertBanner is deprecated. Please use ZAlertBanner instead.")]
    public class ZeroAlertBanner : ZAlertBanner
    {
    }

    #endregion
}
