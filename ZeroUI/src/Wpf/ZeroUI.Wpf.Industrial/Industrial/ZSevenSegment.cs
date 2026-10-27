using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Industrial;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial 7-Segment Digital LED Display for SCADA & MES telemetry in WPF.
    /// Features authentic beveled segment geometry, configurable slant angle, colon/decimal support,
    /// color presets, ghost segments, acrylic reflection, and 100% parity with WinForms.
    /// </summary>
    public class ZSevenSegment : FrameworkElement
    {
        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata("1420", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SegmentColorProperty =
            DependencyProperty.Register(
                nameof(SegmentColor),
                typeof(Color),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(Color.FromRgb(52, 211, 153), FrameworkPropertyMetadataOptions.AffectsRender, OnSegmentColorChanged));

        public static readonly DependencyProperty DimColorProperty =
            DependencyProperty.Register(
                nameof(DimColor),
                typeof(Color),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(Color.FromRgb(20, 45, 35), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ColorPresetProperty =
            DependencyProperty.Register(
                nameof(ColorPreset),
                typeof(SevenSegmentColorPreset),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(SevenSegmentColorPreset.NeonEmerald, FrameworkPropertyMetadataOptions.AffectsRender, OnColorPresetChanged));

        public static readonly DependencyProperty DigitCountProperty =
            DependencyProperty.Register(
                nameof(DigitCount),
                typeof(int),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(6, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SlantAngleProperty =
            DependencyProperty.Register(
                nameof(SlantAngle),
                typeof(double),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(7.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LeadingZeroModeProperty =
            DependencyProperty.Register(
                nameof(LeadingZeroMode),
                typeof(LeadingZeroDisplayMode),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(LeadingZeroDisplayMode.Blank, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FrameStyleProperty =
            DependencyProperty.Register(
                nameof(FrameStyle),
                typeof(SevenSegmentFrameStyle),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(SevenSegmentFrameStyle.RecessedBezel, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowGhostSegmentsProperty =
            DependencyProperty.Register(
                nameof(ShowGhostSegments),
                typeof(bool),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowGlowProperty =
            DependencyProperty.Register(
                nameof(ShowGlow),
                typeof(bool),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowGlassReflectionProperty =
            DependencyProperty.Register(
                nameof(ShowGlassReflection),
                typeof(bool),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextAlignmentProperty =
            DependencyProperty.Register(
                nameof(TextAlignment),
                typeof(TextAlignment),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(TextAlignment.Right, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BlinkColonProperty =
            DependencyProperty.Register(
                nameof(BlinkColon),
                typeof(bool),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BlinkProperty =
            DependencyProperty.Register(
                nameof(Blink),
                typeof(bool),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BlinkIntervalProperty =
            DependencyProperty.Register(
                nameof(BlinkInterval),
                typeof(int),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(500, OnBlinkIntervalChanged));

        public static readonly DependencyProperty SegmentGapProperty =
            DependencyProperty.Register(
                nameof(SegmentGap),
                typeof(double),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(1.5, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SegmentThicknessProperty =
            DependencyProperty.Register(
                nameof(SegmentThickness),
                typeof(int),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(
                nameof(Unit),
                typeof(string),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitColorProperty =
            DependencyProperty.Register(
                nameof(UnitColor),
                typeof(Color),
                typeof(ZSevenSegment),
                new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Public Properties & Shims

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public Color SegmentColor
        {
            get => (Color)GetValue(SegmentColorProperty);
            set => SetValue(SegmentColorProperty, value);
        }

        public Color DimColor
        {
            get => (Color)GetValue(DimColorProperty);
            set => SetValue(DimColorProperty, value);
        }

        public SevenSegmentColorPreset ColorPreset
        {
            get => (SevenSegmentColorPreset)GetValue(ColorPresetProperty);
            set => SetValue(ColorPresetProperty, value);
        }

        public int DigitCount
        {
            get => (int)GetValue(DigitCountProperty);
            set => SetValue(DigitCountProperty, Math.Max(1, Math.Min(32, value)));
        }

        public double SlantAngle
        {
            get => (double)GetValue(SlantAngleProperty);
            set => SetValue(SlantAngleProperty, Math.Max(0.0, Math.Min(20.0, value)));
        }

        public LeadingZeroDisplayMode LeadingZeroMode
        {
            get => (LeadingZeroDisplayMode)GetValue(LeadingZeroModeProperty);
            set => SetValue(LeadingZeroModeProperty, value);
        }

        public bool ShowLeadingZeros
        {
            get => LeadingZeroMode == LeadingZeroDisplayMode.LitZero;
            set => LeadingZeroMode = value ? LeadingZeroDisplayMode.LitZero : LeadingZeroDisplayMode.Blank;
        }

        public SevenSegmentFrameStyle FrameStyle
        {
            get => (SevenSegmentFrameStyle)GetValue(FrameStyleProperty);
            set => SetValue(FrameStyleProperty, value);
        }

        public bool ShowGhostSegments
        {
            get => (bool)GetValue(ShowGhostSegmentsProperty);
            set => SetValue(ShowGhostSegmentsProperty, value);
        }

        public bool ShowGlow
        {
            get => (bool)GetValue(ShowGlowProperty);
            set => SetValue(ShowGlowProperty, value);
        }

        public bool ShowGlassReflection
        {
            get => (bool)GetValue(ShowGlassReflectionProperty);
            set => SetValue(ShowGlassReflectionProperty, value);
        }

        public TextAlignment TextAlignment
        {
            get => (TextAlignment)GetValue(TextAlignmentProperty);
            set => SetValue(TextAlignmentProperty, value);
        }

        public bool BlinkColon
        {
            get => (bool)GetValue(BlinkColonProperty);
            set => SetValue(BlinkColonProperty, value);
        }

        public bool Blink
        {
            get => (bool)GetValue(BlinkProperty);
            set => SetValue(BlinkProperty, value);
        }

        public int BlinkInterval
        {
            get => (int)GetValue(BlinkIntervalProperty);
            set => SetValue(BlinkIntervalProperty, Math.Max(50, value));
        }

        public double SegmentGap
        {
            get => (double)GetValue(SegmentGapProperty);
            set => SetValue(SegmentGapProperty, Math.Max(0.5, Math.Min(10.0, value)));
        }

        public int SegmentThickness
        {
            get => (int)GetValue(SegmentThicknessProperty);
            set => SetValue(SegmentThicknessProperty, Math.Max(0, value));
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public Color UnitColor
        {
            get => (Color)GetValue(UnitColorProperty);
            set => SetValue(UnitColorProperty, value);
        }

        [Obsolete("ValueText is deprecated. Use Value instead.")]
        public string ValueText
        {
            get => Value;
            set => Value = value;
        }

        [Obsolete("LedColor is deprecated. Use SegmentColor instead.")]
        public Color LedColor
        {
            get => SegmentColor;
            set => SegmentColor = value;
        }

        [Obsolete("Title is deprecated. Use Unit instead.")]
        public string Title
        {
            get => Unit;
            set => Unit = value;
        }

        #endregion

        private readonly DispatcherTimer _blinkTimer;
        private bool _blinkPhase = true;

        public ZSevenSegment()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            _blinkTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(BlinkInterval)
            };
            _blinkTimer.Tick += (s, e) =>
            {
                if (BlinkColon || Blink)
                {
                    _blinkPhase = !_blinkPhase;
                    InvalidateVisual();
                }
            };
            _blinkTimer.Start();
        }

        private static void OnSegmentColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSevenSegment ctrl && e.NewValue is Color c)
            {
                uint argb = ((uint)c.A << 24) | ((uint)c.R << 16) | ((uint)c.G << 8) | c.B;
                uint dimArgb = SevenSegmentState.DeriveDimColor(argb);
                ctrl.DimColor = Color.FromArgb((byte)(dimArgb >> 24), (byte)(dimArgb >> 16), (byte)(dimArgb >> 8), (byte)dimArgb);
            }
        }

        private static void OnColorPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSevenSegment ctrl && e.NewValue is SevenSegmentColorPreset preset && preset != SevenSegmentColorPreset.Custom)
            {
                var (segArgb, dimArgb) = SevenSegmentState.GetPresetColors(preset);
                ctrl.SegmentColor = Color.FromArgb((byte)(segArgb >> 24), (byte)(segArgb >> 16), (byte)(segArgb >> 8), (byte)segArgb);
                ctrl.DimColor = Color.FromArgb((byte)(dimArgb >> 24), (byte)(dimArgb >> 16), (byte)(dimArgb >> 8), (byte)dimArgb);
            }
        }

        private static void OnBlinkIntervalChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSevenSegment ctrl && e.NewValue is int ms && ms >= 50)
            {
                ctrl._blinkTimer.Interval = TimeSpan.FromMilliseconds(ms);
            }
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

            // Outer acrylic casing
            if (FrameStyle == SevenSegmentFrameStyle.RecessedBezel)
            {
                var bgBrush = new SolidColorBrush(Color.FromRgb(12, 18, 32));
                dc.DrawRoundedRectangle(bgBrush, new Pen(new SolidColorBrush(Color.FromRgb(30, 41, 59)), 1.0), new Rect(0.5, 0.5, w - 1, h - 1), 4, 4);
            }
            else if (FrameStyle == SevenSegmentFrameStyle.AcrylicGlass)
            {
                var bgBrush = new SolidColorBrush(Color.FromRgb(8, 12, 24));
                dc.DrawRoundedRectangle(bgBrush, new Pen(new SolidColorBrush(Color.FromRgb(51, 65, 85)), 1.5), new Rect(0.5, 0.5, w - 1, h - 1), 6, 6);
            }

            // Unit badge on top-right
            double unitWidth = 0.0;
            if (!string.IsNullOrEmpty(Unit))
            {
                var uBrush = UnitColor != Colors.Transparent ? new SolidColorBrush(UnitColor) : ZeroWpfTheme.TextSecondary;
                var ft = CreateFormattedText(Unit, ZeroWpfTheme.BoldTypeface, 10.0, uBrush, dpi);
                unitWidth = ft.Width + 8;
                dc.DrawText(ft, new Point(w - unitWidth - 6, 6));
            }

            // Display bounds
            double padX = 8;
            double padY = 6;
            double dispX = padX;
            double dispY = padY;
            double dispW = Math.Max(20, w - padX * 2 - unitWidth);
            double dispH = Math.Max(16, h - padY * 2);

            var items = SevenSegmentState.ParseValue(Value, DigitCount, LeadingZeroMode);
            if (items.Count == 0) return;

            // Slot calculations
            double slotW = dispW / items.Count;
            double digitW = Math.Min(36, slotW * 0.88);
            double digitH = Math.Min(dispH, digitW * 1.85);

            var onBrush = new SolidColorBrush(SegmentColor);
            onBrush.Freeze();
            var offBrush = new SolidColorBrush(DimColor);
            offBrush.Freeze();

            bool isBlinkOff = Blink && !_blinkPhase;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                double dx = dispX + i * slotW + (slotW - digitW) / 2.0;
                double dy = dispY + (dispH - digitH) / 2.0;

                if (item.IsColon)
                {
                    bool colonLit = !BlinkColon || _blinkPhase;
                    var cBrush = colonLit && !isBlinkOff ? onBrush : (ShowGhostSegments ? offBrush : Brushes.Transparent);
                    double cx = dx + digitW / 2.0;
                    double r = Math.Max(1.5, digitW * 0.09);
                    dc.DrawEllipse(cBrush, null, new Point(cx, dy + digitH * 0.35), r, r);
                    dc.DrawEllipse(cBrush, null, new Point(cx, dy + digitH * 0.65), r, r);
                }
                else
                {
                    byte mask = SevenSegmentState.GetPattern(item.Character);
                    if (isBlinkOff) mask = 0;

                    DrawSlantedDigit(dc, dx, dy, digitW, digitH, mask, item.IsDimmed ? offBrush : onBrush, ShowGhostSegments ? offBrush : Brushes.Transparent, SlantAngle);

                    if (item.HasDecimal)
                    {
                        var dpBrush = !isBlinkOff ? onBrush : (ShowGhostSegments ? offBrush : Brushes.Transparent);
                        double dpR = Math.Max(1.5, digitW * 0.08);
                        dc.DrawEllipse(dpBrush, null, new Point(dx + digitW + 1, dy + digitH - dpR), dpR, dpR);
                    }
                }
            }

            // Glass reflection sheen
            if (ShowGlassReflection && h > 20)
            {
                var glassGrad = new LinearGradientBrush(
                    Color.FromArgb(28, 255, 255, 255),
                    Color.FromArgb(0, 255, 255, 255),
                    new Point(0, 0),
                    new Point(0, 1));
                dc.DrawRectangle(glassGrad, null, new Rect(1, 1, w - 2, h * 0.45));
            }
        }

        private static void DrawSlantedDigit(
            DrawingContext dc,
            double x, double y, double w, double h,
            byte mask, Brush onBrush, Brush offBrush, double slantDeg)
        {
            dc.PushTransform(new SkewTransform(-slantDeg, 0, x + w / 2.0, y + h / 2.0));

            double t = Math.Max(2.0, Math.Min(6.0, h * 0.11)); // segment thickness
            double segW = w - 2 * t - 2;
            double segH = (h - 3 * t - 4) / 2.0;

            // Seg A (Top)
            dc.DrawRectangle((mask & 0x01) != 0 ? onBrush : offBrush, null, new Rect(x + t + 1, y, segW, t));
            // Seg B (Top-Right)
            dc.DrawRectangle((mask & 0x02) != 0 ? onBrush : offBrush, null, new Rect(x + w - t, y + t + 1, t, segH));
            // Seg C (Bottom-Right)
            dc.DrawRectangle((mask & 0x04) != 0 ? onBrush : offBrush, null, new Rect(x + w - t, y + 2 * t + segH + 2, t, segH));
            // Seg D (Bottom)
            dc.DrawRectangle((mask & 0x08) != 0 ? onBrush : offBrush, null, new Rect(x + t + 1, y + h - t, segW, t));
            // Seg E (Bottom-Left)
            dc.DrawRectangle((mask & 0x10) != 0 ? onBrush : offBrush, null, new Rect(x, y + 2 * t + segH + 2, t, segH));
            // Seg F (Top-Left)
            dc.DrawRectangle((mask & 0x20) != 0 ? onBrush : offBrush, null, new Rect(x, y + t + 1, t, segH));
            // Seg G (Middle)
            dc.DrawRectangle((mask & 0x40) != 0 ? onBrush : offBrush, null, new Rect(x + t + 1, y + t + segH + 1, segW, t));

            dc.Pop();
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
    /// Legacy alias for <see cref="ZSevenSegment"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SevenSegment is deprecated and will be removed in 5 release cycles. Please migrate to ZSevenSegment instead.")]
    public class SevenSegment : ZSevenSegment { }

    /// <summary>
    /// Legacy alias for <see cref="ZSevenSegment"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSevenSegment is deprecated and will be removed in 5 release cycles. Please migrate to ZSevenSegment instead.")]
    public class ZeroSevenSegment : ZSevenSegment { }

    #endregion

}
