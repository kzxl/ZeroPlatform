using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern WPF label control featuring automatic text ellipsis trimming (AutoEllipsis),
    /// dynamic tooltip expansion on text truncation, and skin-aware typography.
    /// </summary>
    public class ZLabel : Control
    {
        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(ZLabel),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure, OnTextChanged));

        public static readonly DependencyProperty AutoEllipsisProperty =
            DependencyProperty.Register(
                nameof(AutoEllipsis),
                typeof(bool),
                typeof(ZLabel),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender, OnAutoEllipsisChanged));

        public static readonly DependencyProperty TextTrimmingProperty =
            DependencyProperty.Register(
                nameof(TextTrimming),
                typeof(TextTrimming),
                typeof(ZLabel),
                new FrameworkPropertyMetadata(TextTrimming.CharacterEllipsis, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextWrappingProperty =
            DependencyProperty.Register(
                nameof(TextWrapping),
                typeof(TextWrapping),
                typeof(ZLabel),
                new FrameworkPropertyMetadata(TextWrapping.NoWrap, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

        public static readonly DependencyProperty ShowTooltipWhenTruncatedProperty =
            DependencyProperty.Register(
                nameof(ShowTooltipWhenTruncated),
                typeof(bool),
                typeof(ZLabel),
                new FrameworkPropertyMetadata(true));

        #endregion

        #region Properties

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        [DefaultValue(true)]
        public bool AutoEllipsis
        {
            get => (bool)GetValue(AutoEllipsisProperty);
            set => SetValue(AutoEllipsisProperty, value);
        }

        public TextTrimming TextTrimming
        {
            get => (TextTrimming)GetValue(TextTrimmingProperty);
            set => SetValue(TextTrimmingProperty, value);
        }

        public TextWrapping TextWrapping
        {
            get => (TextWrapping)GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        [DefaultValue(true)]
        public bool ShowTooltipWhenTruncated
        {
            get => (bool)GetValue(ShowTooltipWhenTruncatedProperty);
            set => SetValue(ShowTooltipWhenTruncatedProperty, value);
        }

        public bool IsTextTruncated { get; private set; }

        #endregion

        static ZLabel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZLabel), new FrameworkPropertyMetadata(typeof(ZLabel)));
        }

        public ZLabel()
        {
            FontSize = 12.0;
            FontFamily = new FontFamily("Segoe UI");
            HorizontalContentAlignment = HorizontalAlignment.Left;
            VerticalContentAlignment = VerticalAlignment.Center;
            Foreground = new SolidColorBrush(Color.FromRgb(30, 35, 45));

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZLabel label)
            {
                label.UpdateTruncationStatus();
            }
        }

        private static void OnAutoEllipsisChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZLabel label)
            {
                label.TextTrimming = (bool)e.NewValue ? TextTrimming.CharacterEllipsis : TextTrimming.None;
                label.UpdateTruncationStatus();
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (string.IsNullOrEmpty(Text))
            {
                return new Size(0, Math.Max(FontSize * 1.3, 16.0));
            }

            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var ft = CreateFormattedText(Text, typeface, FontSize, Foreground ?? Brushes.Black);

            double w = ft.Width + Padding.Left + Padding.Right;
            double h = ft.Height + Padding.Top + Padding.Bottom;

            return new Size(Math.Min(w, constraint.Width), Math.Min(h, constraint.Height));
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            UpdateOverflowTooltip();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            UpdateOverflowTooltip();
        }

        private void UpdateTruncationStatus()
        {
            if (string.IsNullOrEmpty(Text) || ActualWidth <= 0)
            {
                IsTextTruncated = false;
                return;
            }

            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            var ft = CreateFormattedText(Text, typeface, FontSize, Foreground ?? Brushes.Black);

            double availableWidth = Math.Max(0, ActualWidth - Padding.Left - Padding.Right);
            IsTextTruncated = ft.Width > availableWidth;
        }

        private void UpdateOverflowTooltip()
        {
            UpdateTruncationStatus();

            if (ShowTooltipWhenTruncated && AutoEllipsis && IsTextTruncated && !string.IsNullOrEmpty(Text))
            {
                ToolTip = Text;
            }
            else
            {
                if (ToolTip is string s && s == Text)
                {
                    ToolTip = null;
                }
            }
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // Background
            if (Background != null)
            {
                dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));
            }

            if (string.IsNullOrEmpty(Text)) return;

            double availW = Math.Max(0, w - Padding.Left - Padding.Right);
            double availH = Math.Max(0, h - Padding.Top - Padding.Bottom);
            if (availW <= 0 || availH <= 0) return;

            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
            Brush textBrush = Foreground ?? ZeroWpfTheme.TextPrimary ?? Brushes.DarkSlateGray;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            var ft = CreateFormattedText(Text, typeface, FontSize, textBrush, dpi);

            // Ellipsis Trimming & Max width
            if (AutoEllipsis || TextTrimming != TextTrimming.None)
            {
                ft.MaxTextWidth = availW;
                ft.Trimming = TextTrimming;
            }

            if (TextWrapping == TextWrapping.Wrap)
            {
                ft.MaxTextWidth = availW;
            }

            // Alignment positioning
            double x = Padding.Left;
            switch (HorizontalContentAlignment)
            {
                case HorizontalAlignment.Center:
                    x = Padding.Left + Math.Max(0, (availW - ft.Width) / 2.0);
                    break;
                case HorizontalAlignment.Right:
                    x = Padding.Left + Math.Max(0, availW - ft.Width);
                    break;
            }

            double y = Padding.Top;
            switch (VerticalContentAlignment)
            {
                case VerticalAlignment.Center:
                    y = Padding.Top + Math.Max(0, (availH - ft.Height) / 2.0);
                    break;
                case VerticalAlignment.Bottom:
                    y = Padding.Top + Math.Max(0, availH - ft.Height);
                    break;
            }

            dc.DrawText(ft, new Point(x, y));
            IsTextTruncated = ft.Width > availW;
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

        #endregion
    
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
    /// Convenience alias for <see cref="ZLabel"/>.
    /// </summary>
    public class ZLabelControl : ZLabel { }

    /// <summary>
    /// Legacy alias for <see cref="ZLabel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LabelControl is deprecated and will be removed in 5 release cycles. Please migrate to ZLabel instead.")]
    public class LabelControl : ZLabel { }

    /// <summary>
    /// Legacy alias for <see cref="ZLabel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLabelControl is deprecated and will be removed in 5 release cycles. Please migrate to ZLabel instead.")]
    public class ZeroLabelControl : ZLabel { }

    /// <summary>
    /// Legacy alias for <see cref="ZLabel"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLabel is deprecated and will be removed in 5 release cycles. Please migrate to ZLabel instead.")]
    public class ZeroLabel : ZLabel { }

    #endregion

}
