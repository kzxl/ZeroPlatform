using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public enum StatusLevel
    {
        Running,
        Standby,
        Fault,
        Offline
    }

    [Obsolete("ZeroStatusLevel is deprecated. Use StatusLevel instead.")]
    public enum ZeroStatusLevel
    {
        Running = StatusLevel.Running,
        Standby = StatusLevel.Standby,
        Fault = StatusLevel.Fault,
        Offline = StatusLevel.Offline
    }

    /// <summary>
    /// Modern industrial status badge pill with LED indicator dot.
    /// </summary>
    public class ZStatusBadge : FrameworkElement
    {
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(
                nameof(Status),
                typeof(StatusLevel),
                typeof(ZStatusBadge),
                new FrameworkPropertyMetadata(StatusLevel.Running, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty StatusTextProperty =
            DependencyProperty.Register(
                nameof(StatusText),
                typeof(string),
                typeof(ZStatusBadge),
                new FrameworkPropertyMetadata("Running", FrameworkPropertyMetadataOptions.AffectsRender));

        public StatusLevel Status
        {
            get => (StatusLevel)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
            set => SetValue(StatusTextProperty, value);
        }

        public ZStatusBadge()
        {
            Height = 26;
            MinWidth = 80;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

#if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
#else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
#endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

#if NETFRAMEWORK
            double dpi = 1.0;
#else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
#endif

            (Brush dotBrush, Brush bgBrush, Brush textBrush) = Status switch
            {
                StatusLevel.Running => (ZeroWpfTheme.SuccessAccent, new SolidColorBrush(Color.FromArgb(30, 166, 227, 161)), ZeroWpfTheme.SuccessAccent),
                StatusLevel.Standby => (ZeroWpfTheme.WarningAccent, new SolidColorBrush(Color.FromArgb(30, 249, 226, 175)), ZeroWpfTheme.WarningAccent),
                StatusLevel.Fault => (ZeroWpfTheme.DangerAccent, new SolidColorBrush(Color.FromArgb(30, 243, 139, 168)), ZeroWpfTheme.DangerAccent),
                _ => (ZeroWpfTheme.TextMuted, new SolidColorBrush(Color.FromArgb(30, 100, 116, 139)), ZeroWpfTheme.TextMuted)
            };

            // Pill container
            dc.DrawRoundedRectangle(bgBrush, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1), h / 2.0, h / 2.0);

            // LED Dot
            double dotRadius = 3.5;
            double dotX = 12;
            double dotY = h / 2.0;
            dc.DrawEllipse(dotBrush, null, new Point(dotX, dotY), dotRadius, dotRadius);

            // Text
            string display = string.IsNullOrEmpty(StatusText) ? Status.ToString() : StatusText;
            var ft = CreateFormattedText(display, ZeroWpfTheme.BoldTypeface, 11.0, textBrush, dpi);
            dc.DrawText(ft, new Point(dotX + dotRadius + 6, dotY - ft.Height / 2.0));
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZStatusBadge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StatusBadge is deprecated and will be removed in 5 release cycles. Please migrate to ZStatusBadge instead.")]
    public class StatusBadge : ZStatusBadge { }

    /// <summary>
    /// Legacy alias for <see cref="ZStatusBadge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroStatusBadge is deprecated and will be removed in 5 release cycles. Please migrate to ZStatusBadge instead.")]
    public class ZeroStatusBadge : ZStatusBadge { }

    #endregion

}
