using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial multi-state pilot light indicator compliant with ISA-101 and IEC 60073 for ZeroUI WPF.
    /// Features optical glass Fresnel sheen, realistic outer bezel, configurable blink/pulse rates,
    /// customizable geometry shapes, and high-performance vector rendering.
    /// </summary>
    public class ZMultiStateLed : FrameworkElement, IScadaBindable
    {
        #region Dependency Properties

        public static readonly DependencyProperty StateProperty =
            DependencyProperty.Register(nameof(State), typeof(MultiStateLedState), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(MultiStateLedState.Normal, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BlinkRateProperty =
            DependencyProperty.Register(nameof(BlinkRate), typeof(MultiStateLedBlink), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(MultiStateLedBlink.None, FrameworkPropertyMetadataOptions.AffectsRender, OnBlinkRateChanged));

        public static readonly DependencyProperty ShapeProperty =
            DependencyProperty.Register(nameof(Shape), typeof(MultiStateLedShape), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(MultiStateLedShape.Circle, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata("RUN", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LabelPositionProperty =
            DependencyProperty.Register(nameof(LabelPosition), typeof(LedLabelPosition), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(LedLabelPosition.Bottom, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CustomColorProperty =
            DependencyProperty.Register(nameof(CustomColor), typeof(Color?), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HaloIntensityProperty =
            DependencyProperty.Register(nameof(HaloIntensity), typeof(double), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(0.6, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowReflectionProperty =
            DependencyProperty.Register(nameof(ShowReflection), typeof(bool), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZMultiStateLed),
                new FrameworkPropertyMetadata(null));

        #endregion

        #region Properties

        public MultiStateLedState State
        {
            get => (MultiStateLedState)GetValue(StateProperty);
            set => SetValue(StateProperty, value);
        }

        public MultiStateLedBlink BlinkRate
        {
            get => (MultiStateLedBlink)GetValue(BlinkRateProperty);
            set => SetValue(BlinkRateProperty, value);
        }

        public MultiStateLedShape Shape
        {
            get => (MultiStateLedShape)GetValue(ShapeProperty);
            set => SetValue(ShapeProperty, value);
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public LedLabelPosition LabelPosition
        {
            get => (LedLabelPosition)GetValue(LabelPositionProperty);
            set => SetValue(LabelPositionProperty, value);
        }

        public Color? CustomColor
        {
            get => (Color?)GetValue(CustomColorProperty);
            set => SetValue(CustomColorProperty, value);
        }

        public double HaloIntensity
        {
            get => (double)GetValue(HaloIntensityProperty);
            set => SetValue(HaloIntensityProperty, value);
        }

        public bool ShowReflection
        {
            get => (bool)GetValue(ShowReflectionProperty);
            set => SetValue(ShowReflectionProperty, value);
        }

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        #endregion

        private DispatcherTimer? _animTimer;
        private double _animPhase = 0.0;
        private bool _blinkLit = true;

        private static readonly Typeface LabelTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

        static ZMultiStateLed()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZMultiStateLed), new FrameworkPropertyMetadata(typeof(ZMultiStateLed)));
        }

        public ZMultiStateLed()
        {
            Width = 64;
            Height = 76;
            Loaded += (s, e) => UpdateAnimation();
            Unloaded += (s, e) => StopAnimation();
        }

        private static void OnBlinkRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZMultiStateLed led)
            {
                led.UpdateAnimation();
            }
        }

        private void UpdateAnimation()
        {
            if (!IsLoaded) return;

            if (BlinkRate == MultiStateLedBlink.None)
            {
                StopAnimation();
                _blinkLit = true;
                _animPhase = 0.0;
                InvalidateVisual();
            }
            else
            {
                if (_animTimer == null)
                {
                    _animTimer = new DispatcherTimer(DispatcherPriority.Render);
                    _animTimer.Interval = TimeSpan.FromMilliseconds(25);
                    _animTimer.Tick += OnAnimTick;
                    _animTimer.Start();
                }
            }
        }

        private void StopAnimation()
        {
            if (_animTimer != null)
            {
                _animTimer.Stop();
                _animTimer.Tick -= OnAnimTick;
                _animTimer = null;
            }
        }

        private void OnAnimTick(object? sender, EventArgs e)
        {
            const double dt = 0.025;
            if (BlinkRate == MultiStateLedBlink.Slow)
            {
                _animPhase += dt * 2.0;
                _blinkLit = ((int)_animPhase % 2) == 0;
            }
            else if (BlinkRate == MultiStateLedBlink.Fast)
            {
                _animPhase += dt * 4.0;
                _blinkLit = ((int)_animPhase % 2) == 0;
            }
            else if (BlinkRate == MultiStateLedBlink.Pulse)
            {
                _animPhase += dt * 3.5;
                if (_animPhase > 6.28318) _animPhase -= 6.28318;
                _blinkLit = true;
            }
            InvalidateVisual();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            string valStr = tag.Value?.ToString() ?? string.Empty;
            if (Enum.TryParse<MultiStateLedState>(valStr, true, out var st))
            {
                State = st;
            }
            else if (int.TryParse(valStr, out var i) && Enum.IsDefined(typeof(MultiStateLedState), i))
            {
                State = (MultiStateLedState)i;
            }
            else if (bool.TryParse(valStr, out var b))
            {
                State = b ? MultiStateLedState.Normal : MultiStateLedState.Off;
            }
        }

        public Color GetEffectiveColor()
        {
            if (CustomColor.HasValue) return CustomColor.Value;

            switch (State)
            {
                case MultiStateLedState.Off: return Color.FromRgb(60, 65, 70);
                case MultiStateLedState.Normal: return Color.FromRgb(40, 205, 65);
                case MultiStateLedState.Warning: return Color.FromRgb(245, 175, 20);
                case MultiStateLedState.Alarm: return Color.FromRgb(235, 40, 45);
                case MultiStateLedState.Maintenance: return Color.FromRgb(30, 140, 245);
                case MultiStateLedState.Standby: return Color.FromRgb(220, 230, 245);
                case MultiStateLedState.Unknown:
                default:
                    return Color.FromRgb(120, 125, 130);
            }
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 10 || h < 10) return;

            CalculateLayout(w, h, out Rect ledBounds, out Rect labelBounds);

            Color baseColor = GetEffectiveColor();
            bool isDark = State == MultiStateLedState.Off || (!_blinkLit && BlinkRate != MultiStateLedBlink.Pulse);

            double pulseFactor = 1.0;
            if (BlinkRate == MultiStateLedBlink.Pulse && State != MultiStateLedState.Off)
            {
                pulseFactor = 0.35 + 0.65 * (Math.Sin(_animPhase) * 0.5 + 0.5);
            }

            // 1. Halo Glow
            if (!isDark && HaloIntensity > 0.05 && State != MultiStateLedState.Off)
            {
                DrawHalo(dc, ledBounds, baseColor, HaloIntensity * pulseFactor);
            }

            // 2. Metallic Bezel / Frame
            DrawBezel(dc, ledBounds);

            // 3. Core Lens Illumination
            double lensInset = Math.Max(3.0, Math.Min(ledBounds.Width, ledBounds.Height) * 0.08);
            var lensBounds = new Rect(ledBounds.X + lensInset, ledBounds.Y + lensInset, ledBounds.Width - lensInset * 2.0, ledBounds.Height - lensInset * 2.0);

            DrawLens(dc, lensBounds, baseColor, isDark, pulseFactor);

            // 4. Glass Reflection Sheen
            if (ShowReflection && !isDark)
            {
                DrawReflection(dc, lensBounds);
            }

            // 5. Label
            if (LabelPosition != LedLabelPosition.None && !string.IsNullOrEmpty(Label) && labelBounds.Width > 5 && labelBounds.Height > 5)
            {
                DrawLabel(dc, labelBounds);
            }
        }

        private void CalculateLayout(double w, double h, out Rect ledBounds, out Rect labelBounds)
        {
            if (LabelPosition == LedLabelPosition.None || string.IsNullOrEmpty(Label))
            {
                double side = Math.Min(w, h) - 6.0;
                ledBounds = new Rect((w - side) / 2.0, (h - side) / 2.0, side, side);
                labelBounds = Rect.Empty;
                return;
            }

            double labelHeight = Math.Max(16.0, h * 0.22);
            double labelWidth = Math.Max(24.0, w * 0.40);

            switch (LabelPosition)
            {
                case LedLabelPosition.Bottom:
                    double sideB = Math.Min(w - 6.0, h - labelHeight - 8.0);
                    ledBounds = new Rect((w - sideB) / 2.0, 4.0, sideB, sideB);
                    labelBounds = new Rect(0, h - labelHeight - 2.0, w, labelHeight);
                    break;
                case LedLabelPosition.Top:
                    double sideT = Math.Min(w - 6.0, h - labelHeight - 8.0);
                    ledBounds = new Rect((w - sideT) / 2.0, labelHeight + 4.0, sideT, sideT);
                    labelBounds = new Rect(0, 2.0, w, labelHeight);
                    break;
                case LedLabelPosition.Right:
                    double sideR = Math.Min(w - labelWidth - 8.0, h - 6.0);
                    ledBounds = new Rect(4.0, (h - sideR) / 2.0, sideR, sideR);
                    labelBounds = new Rect(w - labelWidth - 2.0, 0, labelWidth, h);
                    break;
                case LedLabelPosition.Left:
                    double sideL = Math.Min(w - labelWidth - 8.0, h - 6.0);
                    ledBounds = new Rect(w - sideL - 4.0, (h - sideL) / 2.0, sideL, sideL);
                    labelBounds = new Rect(2.0, 0, labelWidth, h);
                    break;
                default:
                    double sideDef = Math.Min(w, h) - 6.0;
                    ledBounds = new Rect((w - sideDef) / 2.0, (h - sideDef) / 2.0, sideDef, sideDef);
                    labelBounds = Rect.Empty;
                    break;
            }
        }

        private void DrawHalo(DrawingContext dc, Rect bounds, Color color, double intensity)
        {
            double haloExpansion = Math.Min(bounds.Width, bounds.Height) * 0.35 * intensity;
            var haloRect = new Rect(bounds.X - haloExpansion, bounds.Y - haloExpansion, bounds.Width + haloExpansion * 2.0, bounds.Height + haloExpansion * 2.0);

            byte alpha = (byte)(80 * Math.Min(1.0, intensity));
            var haloBrush = new RadialGradientBrush(Color.FromArgb(alpha, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B));
            haloBrush.Freeze();

            DrawShapeGeometry(dc, haloRect, haloBrush, null);
        }

        private void DrawBezel(DrawingContext dc, Rect bounds)
        {
            var bezelBrush = new LinearGradientBrush(
                Color.FromRgb(130, 135, 142),
                Color.FromRgb(40, 42, 45),
                new Point(0, 0), new Point(1, 1));
            bezelBrush.Freeze();
            var bezelPen = new Pen(new SolidColorBrush(Color.FromRgb(30, 32, 35)), 1.5);
            bezelPen.Freeze();

            DrawShapeGeometry(dc, bounds, bezelBrush, bezelPen);
        }

        private void DrawLens(DrawingContext dc, Rect bounds, Color color, bool isDark, double pulseFactor)
        {
            if (isDark)
            {
                var darkBrush = new LinearGradientBrush(
                    Color.FromRgb(60, 65, 70),
                    Color.FromRgb(30, 33, 36),
                    new Point(0, 0), new Point(0, 1));
                darkBrush.Freeze();
                var darkPen = new Pen(new SolidColorBrush(Color.FromRgb(20, 22, 24)), 1.0);
                darkPen.Freeze();
                DrawShapeGeometry(dc, bounds, darkBrush, darkPen);
            }
            else
            {
                byte r = (byte)Math.Min(255, color.R * pulseFactor);
                byte gr = (byte)Math.Min(255, color.G * pulseFactor);
                byte b = (byte)Math.Min(255, color.B * pulseFactor);

                Color centerCol = Color.FromRgb((byte)Math.Min(255, r + 45), (byte)Math.Min(255, gr + 45), (byte)Math.Min(255, b + 45));
                Color edgeCol = Color.FromRgb((byte)(r * 0.65), (byte)(gr * 0.65), (byte)(b * 0.65));

                var lensBrush = new RadialGradientBrush(centerCol, edgeCol);
                lensBrush.Center = new Point(0.45, 0.4);
                lensBrush.GradientOrigin = new Point(0.45, 0.4);
                lensBrush.Freeze();

                var edgePen = new Pen(new SolidColorBrush(Color.FromArgb(180, edgeCol.R, edgeCol.G, edgeCol.B)), 1.5);
                edgePen.Freeze();

                DrawShapeGeometry(dc, bounds, lensBrush, edgePen);
            }
        }

        private void DrawReflection(DrawingContext dc, Rect bounds)
        {
            double rw = bounds.Width * 0.65;
            double rh = bounds.Height * 0.35;
            var sheenRect = new Rect(bounds.X + (bounds.Width - rw) / 2.0, bounds.Y + bounds.Height * 0.08, rw, rh);

            var sheenBrush = new LinearGradientBrush(
                Color.FromArgb(140, 255, 255, 255),
                Color.FromArgb(0, 255, 255, 255),
                new Point(0.5, 0), new Point(0.5, 1));
            sheenBrush.Freeze();

            dc.DrawEllipse(sheenBrush, null, new Point(sheenRect.X + rw / 2.0, sheenRect.Y + rh / 2.0), rw / 2.0, rh / 2.0);
        }

        private void DrawLabel(DrawingContext dc, Rect bounds)
        {
            var textBrush = new SolidColorBrush(Color.FromRgb(215, 220, 225));
            textBrush.Freeze();
            var ft = CreateFormattedText(Label, LabelTypeface, Math.Max(8.0, Math.Min(12.0, bounds.Height * 0.55)), textBrush);
            Point pt = new Point(bounds.X + (bounds.Width - ft.Width) / 2.0, bounds.Y + (bounds.Height - ft.Height) / 2.0);
            dc.DrawText(ft, pt);
        }

        private void DrawShapeGeometry(DrawingContext dc, Rect rect, Brush brush, Pen? pen)
        {
            switch (Shape)
            {
                case MultiStateLedShape.Circle:
                    dc.DrawEllipse(brush, pen, new Point(rect.X + rect.Width / 2.0, rect.Y + rect.Height / 2.0), rect.Width / 2.0, rect.Height / 2.0);
                    break;
                case MultiStateLedShape.Square:
                    dc.DrawRectangle(brush, pen, rect);
                    break;
                case MultiStateLedShape.RoundedRectangle:
                    double r = Math.Min(rect.Width, rect.Height) * 0.22;
                    dc.DrawRoundedRectangle(brush, pen, rect, r, r);
                    break;
            }
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush)
        {
#if NETFRAMEWORK
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
#else
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, 1.0);
#endif
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZMultiStateLed"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MultiStateLed is deprecated and will be removed in 5 release cycles. Please migrate to ZMultiStateLed instead.")]
    public class MultiStateLed : ZMultiStateLed { }

    /// <summary>
    /// Legacy alias for <see cref="ZMultiStateLed"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroMultiStateLed is deprecated and will be removed in 5 release cycles. Please migrate to ZMultiStateLed instead.")]
    public class ZeroMultiStateLed : ZMultiStateLed { }

    #endregion

}
