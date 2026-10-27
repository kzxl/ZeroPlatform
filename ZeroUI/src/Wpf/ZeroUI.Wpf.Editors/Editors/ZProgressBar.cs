using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern Fluent progress bar with gradient fill and percentage readout.
    /// </summary>
    public class ZProgressBar : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(ZProgressBar),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum { get; set; } = 0;
        public double Maximum { get; set; } = 100;
        public bool ShowPercentage { get; set; } = true;

        public ZProgressBar()
        {
            Height = 16;
            MinWidth = 100;
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

            double range = Math.Max(1, Maximum - Minimum);
            double clampedVal = Math.Max(Minimum, Math.Min(Maximum, Value));
            double ratio = (clampedVal - Minimum) / range;

            // Track
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1), h / 2.0, h / 2.0);

            // Progress Fill
            if (ratio > 0)
            {
                double fillW = Math.Max(h, (w - 2) * ratio);
                dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, null, new Rect(1, 1, fillW, h - 2), (h - 2) / 2.0, (h - 2) / 2.0);
            }

            // Percentage Text
            if (ShowPercentage && h >= 14)
            {
                string text = $"{ratio * 100:0}%";
                var ft = CreateFormattedText(text, ZeroWpfTheme.BoldTypeface, 9.5, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(ft, new Point((w - ft.Width) / 2.0, (h - ft.Height) / 2.0));
            }
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

    /// <summary>
    /// Backward-compatibility alias for <see cref="ProgressBarControl"/>.
    /// </summary>
    [Obsolete("ZeroProgressBar is deprecated. Use ProgressBarControl instead.")]
    public class ZeroProgressBar : ProgressBarControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZProgressBar"/>.
    /// </summary>
    public class ZProgressBarControl : ZProgressBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZProgressBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ProgressBarControl is deprecated and will be removed in 5 release cycles. Please migrate to ZProgressBar instead.")]
    public class ProgressBarControl : ZProgressBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZProgressBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroProgressBarControl is deprecated and will be removed in 5 release cycles. Please migrate to ZProgressBar instead.")]
    public class ZeroProgressBarControl : ZProgressBar { }

    #endregion

}
