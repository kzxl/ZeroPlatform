using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern KPI Metric Card component for ZeroUI executive dashboards and analytical summaries in WPF.
    /// Features title, large formatted metric, prefix/suffix units, and trend direction indicators.
    /// </summary>
    public class ZStatisticControl : FrameworkElement
    {
        #region Dependency Properties

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZStatisticControl),
                new FrameworkPropertyMetadata("Metric Title", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(string), typeof(ZStatisticControl),
                new FrameworkPropertyMetadata("0", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PrefixProperty =
            DependencyProperty.Register(nameof(Prefix), typeof(string), typeof(ZStatisticControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(ZStatisticControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrendProperty =
            DependencyProperty.Register(nameof(Trend), typeof(TrendDirection), typeof(ZStatisticControl),
                new FrameworkPropertyMetadata(TrendDirection.None, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrendTextProperty =
            DependencyProperty.Register(nameof(TrendText), typeof(string), typeof(ZStatisticControl),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(ZStatisticControl),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Properties

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string? Prefix
        {
            get => (string?)GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public string? Suffix
        {
            get => (string?)GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public TrendDirection Trend
        {
            get => (TrendDirection)GetValue(TrendProperty);
            set => SetValue(TrendProperty, value);
        }

        public string? TrendText
        {
            get => (string?)GetValue(TrendTextProperty);
            set => SetValue(TrendTextProperty, value);
        }

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        #endregion

        public ZStatisticControl()
        {
            Width = 200;
            Height = 90;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            double w = double.IsNaN(Width) ? 190 : Width;
            double h = double.IsNaN(Height) ? 88 : Height;
            return new Size(w, h);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Background Card
            Rect rect = new Rect(0.5, 0.5, w - 1.0, h - 1.0);
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, ZeroWpfTheme.BorderPen, rect, CornerRadius, CornerRadius);

            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double leftMargin = 16.0;

            // 2. Title Text
            var titleText = new FormattedText(
                Title ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.MediumTypeface,
                11.5,
                ZeroWpfTheme.TextSecondary,
                dpi);

            dc.DrawText(titleText, new Point(leftMargin, 12));

            // 3. Metric Value Line (Prefix + Value + Suffix)
            double curX = leftMargin;
            double valueBaselineY = 32.0;

            if (!string.IsNullOrEmpty(Prefix))
            {
                var prefixText = new FormattedText(
                    Prefix,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.MediumTypeface,
                    15.0,
                    ZeroWpfTheme.TextSecondary,
                    dpi);

                dc.DrawText(prefixText, new Point(curX, valueBaselineY + 6));
                curX += prefixText.Width + 2;
            }

            var valText = new FormattedText(
                Value ?? "0",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.BoldTypeface,
                24.0,
                ZeroWpfTheme.TextPrimary,
                dpi);

            dc.DrawText(valText, new Point(curX, valueBaselineY));
            curX += valText.Width + 3;

            if (!string.IsNullOrEmpty(Suffix))
            {
                var suffixText = new FormattedText(
                    Suffix,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.MediumTypeface,
                    13.0,
                    ZeroWpfTheme.TextSecondary,
                    dpi);

                dc.DrawText(suffixText, new Point(curX, valueBaselineY + 8));
            }

            // 4. Trend Pill (Bottom or Right)
            if (Trend != TrendDirection.None && !string.IsNullOrEmpty(TrendText))
            {
                bool isUp = Trend == TrendDirection.Up;
                Brush trendBrush = isUp ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.DangerAccent;
                string trendArrow = isUp ? "▲ " : "▼ ";

                var trendFt = new FormattedText(
                    trendArrow + TrendText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    11.0,
                    trendBrush,
                    dpi);

                double trendY = 64.0;
                dc.DrawText(trendFt, new Point(leftMargin, trendY));
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
    /// KPI card metric alias for <see cref="StatisticControl"/>.
    /// </summary>
    public class StatisticCard : StatisticControl
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="StatisticControl"/>.
    /// </summary>
    [Obsolete("ZeroStatistic is deprecated. Use StatisticControl or StatisticCard instead.")]
    public class ZeroStatistic : StatisticControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZStatisticControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StatisticControl is deprecated and will be removed in 5 release cycles. Please migrate to ZStatisticControl instead.")]
    public class StatisticControl : ZStatisticControl { }

    /// <summary>
    /// Legacy alias for <see cref="ZStatisticControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroStatisticControl is deprecated and will be removed in 5 release cycles. Please migrate to ZStatisticControl instead.")]
    public class ZeroStatisticControl : ZStatisticControl { }

    #endregion

}
