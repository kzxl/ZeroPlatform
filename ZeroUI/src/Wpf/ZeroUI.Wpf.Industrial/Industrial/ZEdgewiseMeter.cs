using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Industrial high-density edgewise profile panel meter for control rooms and SCADA racks in ZeroUI WPF.
    /// Features calibrated tick graduations, color-coded alarm zones (ISA-18.2 / ISA-101),
    /// multi-style pointers (Flag, Bar, Line), and dual vertical/horizontal orientation.
    /// </summary>
    public class ZEdgewiseMeter : FrameworkElement, IScadaBindable
    {
        #region Dependency Properties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(65.4, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceValue));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register(nameof(Orientation), typeof(EdgewiseOrientation), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(EdgewiseOrientation.Vertical, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PointerStyleProperty =
            DependencyProperty.Register(nameof(PointerStyle), typeof(EdgewisePointerStyle), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(EdgewisePointerStyle.Flag, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata("PRESSURE", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata("bar", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DecimalPlacesProperty =
            DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty HighAlarmThresholdProperty =
            DependencyProperty.Register(nameof(HighAlarmThreshold), typeof(double), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(85.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty LowAlarmThresholdProperty =
            DependencyProperty.Register(nameof(LowAlarmThreshold), typeof(double), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(15.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CautionThresholdProperty =
            DependencyProperty.Register(nameof(CautionThreshold), typeof(double), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(75.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowAlarmZonesProperty =
            DependencyProperty.Register(nameof(ShowAlarmZones), typeof(bool), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BarColorProperty =
            DependencyProperty.Register(nameof(BarColor), typeof(Color), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(Color.FromRgb(0, 190, 255), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoundTagPathProperty =
            DependencyProperty.Register(nameof(BoundTagPath), typeof(string), typeof(ZEdgewiseMeter),
                new FrameworkPropertyMetadata(null));

        #endregion

        #region Properties

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public EdgewiseOrientation Orientation
        {
            get => (EdgewiseOrientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public EdgewisePointerStyle PointerStyle
        {
            get => (EdgewisePointerStyle)GetValue(PointerStyleProperty);
            set => SetValue(PointerStyleProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public int DecimalPlaces
        {
            get => (int)GetValue(DecimalPlacesProperty);
            set => SetValue(DecimalPlacesProperty, value);
        }

        public double HighAlarmThreshold
        {
            get => (double)GetValue(HighAlarmThresholdProperty);
            set => SetValue(HighAlarmThresholdProperty, value);
        }

        public double LowAlarmThreshold
        {
            get => (double)GetValue(LowAlarmThresholdProperty);
            set => SetValue(LowAlarmThresholdProperty, value);
        }

        public double CautionThreshold
        {
            get => (double)GetValue(CautionThresholdProperty);
            set => SetValue(CautionThresholdProperty, value);
        }

        public bool ShowAlarmZones
        {
            get => (bool)GetValue(ShowAlarmZonesProperty);
            set => SetValue(ShowAlarmZonesProperty, value);
        }

        public Color BarColor
        {
            get => (Color)GetValue(BarColorProperty);
            set => SetValue(BarColorProperty, value);
        }

        public string? BoundTagPath
        {
            get => (string?)GetValue(BoundTagPathProperty);
            set => SetValue(BoundTagPathProperty, value);
        }

        #endregion

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var v))
            {
                Value = v;
            }
        }

        private static readonly Typeface TitleTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface ValueTypeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        private static readonly Typeface SmallTypeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        static ZEdgewiseMeter()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZEdgewiseMeter), new FrameworkPropertyMetadata(typeof(ZEdgewiseMeter)));
        }

        public ZEdgewiseMeter()
        {
            Width = 72;
            Height = 240;
        }

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            if (d is ZEdgewiseMeter meter && baseValue is double v)
            {
                return Math.Max(meter.Minimum, Math.Min(meter.Maximum, v));
            }
            return baseValue;
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w < 20 || h < 20) return;

            // 1. Frame Bezel
            var bezelBrush = new LinearGradientBrush(
                Color.FromRgb(70, 74, 80),
                Color.FromRgb(28, 30, 34),
                new Point(0, 0), new Point(0, 1));
            bezelBrush.Freeze();
            var bezelPen = new Pen(new SolidColorBrush(Color.FromRgb(90, 95, 102)), 1.5);
            bezelPen.Freeze();
            dc.DrawRectangle(bezelBrush, bezelPen, new Rect(1, 1, w - 2, h - 2));

            // 2. Inner Dial Plate
            var dialBrush = new SolidColorBrush(Color.FromRgb(18, 20, 24));
            dialBrush.Freeze();
            var innerRect = new Rect(5, 5, w - 10, h - 10);
            dc.DrawRectangle(dialBrush, null, innerRect);

            if (Orientation == EdgewiseOrientation.Vertical)
            {
                DrawVerticalMeter(dc, innerRect);
            }
            else
            {
                DrawHorizontalMeter(dc, innerRect);
            }
        }

        private void DrawVerticalMeter(DrawingContext dc, Rect bounds)
        {
            // Title
            double headerHeight = 38.0;
            var titleBrush = new SolidColorBrush(Color.FromRgb(170, 175, 185));
            titleBrush.Freeze();
            var titleFt = CreateFormattedText(Title, TitleTypeface, 8.5, titleBrush);
            dc.DrawText(titleFt, new Point(bounds.X + (bounds.Width - titleFt.Width) / 2.0, bounds.Y + 2.0));

            // Value Readout
            string valText = Value.ToString($"F{DecimalPlaces}");
            Brush valBrush = Value >= HighAlarmThreshold ? Brushes.Crimson :
                             Value <= LowAlarmThreshold ? Brushes.Goldenrod :
                             Brushes.Cyan;
            var valFt = CreateFormattedText(valText, ValueTypeface, 11.0, valBrush);
            dc.DrawText(valFt, new Point(bounds.X + (bounds.Width - valFt.Width) / 2.0, bounds.Y + 16.0));

            // Scale Track
            double trackTop = bounds.Y + headerHeight + 6.0;
            double trackBottom = bounds.Bottom - 18.0;
            double trackHeight = trackBottom - trackTop;
            double trackX = bounds.X + bounds.Width * 0.38;
            double trackWidth = 8.0;

            if (trackHeight <= 20) return;

            // Alarm Zones
            if (ShowAlarmZones)
            {
                DrawVerticalAlarmZones(dc, trackX, trackTop, trackWidth, trackHeight);
            }

            // Track Background
            var trackBgBrush = new SolidColorBrush(Color.FromRgb(32, 36, 42));
            trackBgBrush.Freeze();
            dc.DrawRectangle(trackBgBrush, null, new Rect(trackX, trackTop, trackWidth, trackHeight));

            // Bargraph fill
            double norm = (Value - Minimum) / (Maximum - Minimum);
            norm = Math.Max(0.0, Math.Min(1.0, norm));
            double barFillHeight = trackHeight * norm;

            if (PointerStyle == EdgewisePointerStyle.Bar)
            {
                var barBrush = new LinearGradientBrush(
                    Color.FromRgb(10, 120, 200),
                    Value >= HighAlarmThreshold ? Color.FromRgb(240, 40, 45) : BarColor,
                    new Point(0, 1), new Point(0, 0));
                barBrush.Freeze();
                dc.DrawRectangle(barBrush, null, new Rect(trackX, trackBottom - barFillHeight, trackWidth, barFillHeight));
            }

            // Ticks
            DrawVerticalTicks(dc, bounds.X + 2.0, trackX - 2.0, trackTop, trackBottom, trackHeight);

            // Pointer
            double pointerY = trackBottom - barFillHeight;
            if (PointerStyle == EdgewisePointerStyle.Flag)
            {
                double flagW = bounds.Width - (trackX + trackWidth) - 4.0;
                var flagGeo = new StreamGeometry();
                using (var ctx = flagGeo.Open())
                {
                    ctx.BeginFigure(new Point(trackX + trackWidth + 1.0, pointerY), true, true);
                    ctx.LineTo(new Point(trackX + trackWidth + 7.0, pointerY - 5.0), true, false);
                    ctx.LineTo(new Point(trackX + trackWidth + flagW, pointerY - 5.0), true, false);
                    ctx.LineTo(new Point(trackX + trackWidth + flagW, pointerY + 5.0), true, false);
                    ctx.LineTo(new Point(trackX + trackWidth + 7.0, pointerY + 5.0), true, false);
                }
                flagGeo.Freeze();

                var flagBrush = new LinearGradientBrush(
                    Color.FromRgb(255, 230, 40),
                    Color.FromRgb(200, 160, 0),
                    new Point(0, 0), new Point(1, 1));
                flagBrush.Freeze();
                dc.DrawGeometry(flagBrush, new Pen(Brushes.DarkSlateGray, 1.0), flagGeo);
            }
            else if (PointerStyle == EdgewisePointerStyle.Line)
            {
                var linePen = new Pen(Brushes.Crimson, 2.0);
                linePen.Freeze();
                dc.DrawLine(linePen, new Point(bounds.X + 4.0, pointerY), new Point(bounds.Right - 4.0, pointerY));
            }

            // Unit caption
            var unitBrush = new SolidColorBrush(Color.FromRgb(140, 145, 155));
            unitBrush.Freeze();
            var unitFt = CreateFormattedText(Unit, SmallTypeface, 8.5, unitBrush);
            dc.DrawText(unitFt, new Point(bounds.X + (bounds.Width - unitFt.Width) / 2.0, trackBottom + 2.0));
        }

        private void DrawVerticalAlarmZones(DrawingContext dc, double trackX, double trackTop, double trackWidth, double trackHeight)
        {
            double zoneX = trackX - 3.0;
            double zoneW = 3.0;

            // Normal green strip
            var normBrush = new SolidColorBrush(Color.FromRgb(40, 180, 70));
            normBrush.Freeze();
            dc.DrawRectangle(normBrush, null, new Rect(zoneX, trackTop, zoneW, trackHeight));

            // High caution zone
            if (CautionThreshold < Maximum)
            {
                double cautFrac = (Maximum - CautionThreshold) / (Maximum - Minimum);
                double cautH = trackHeight * cautFrac;
                var cautBrush = new SolidColorBrush(Color.FromRgb(240, 175, 20));
                cautBrush.Freeze();
                dc.DrawRectangle(cautBrush, null, new Rect(zoneX, trackTop, zoneW, cautH));
            }

            // High alarm zone
            if (HighAlarmThreshold < Maximum)
            {
                double hiFrac = (Maximum - HighAlarmThreshold) / (Maximum - Minimum);
                double hiH = trackHeight * hiFrac;
                var hiBrush = new SolidColorBrush(Color.FromRgb(235, 40, 45));
                hiBrush.Freeze();
                dc.DrawRectangle(hiBrush, null, new Rect(zoneX, trackTop, zoneW, hiH));
            }
        }

        private void DrawVerticalTicks(DrawingContext dc, double leftX, double rightX, double topY, double bottomY, double height)
        {
            var tickPen = new Pen(new SolidColorBrush(Color.FromRgb(140, 145, 155)), 1.0);
            tickPen.Freeze();
            var textBrush = new SolidColorBrush(Color.FromRgb(160, 165, 175));
            textBrush.Freeze();

            int majorSteps = 4;
            for (int i = 0; i <= majorSteps; i++)
            {
                double y = bottomY - (height * i / majorSteps);
                dc.DrawLine(tickPen, new Point(rightX - 5.0, y), new Point(rightX, y));

                double val = Minimum + (Maximum - Minimum) * i / majorSteps;
                var ft = CreateFormattedText(val.ToString("G3"), SmallTypeface, 7.5, textBrush);
                dc.DrawText(ft, new Point(rightX - 7.0 - ft.Width, y - ft.Height / 2.0));

                if (i < majorSteps)
                {
                    double midY = y - (height / (majorSteps * 2));
                    dc.DrawLine(tickPen, new Point(rightX - 2.5, midY), new Point(rightX, midY));
                }
            }
        }

        private void DrawHorizontalMeter(DrawingContext dc, Rect bounds)
        {
            double labelW = Math.Max(60.0, bounds.Width * 0.28);
            var titleBrush = new SolidColorBrush(Color.FromRgb(170, 175, 185));
            titleBrush.Freeze();
            var titleFt = CreateFormattedText(Title, TitleTypeface, 8.5, titleBrush);
            dc.DrawText(titleFt, new Point(bounds.X + 2.0, bounds.Y + 2.0));

            string valText = $"{Value.ToString($"F{DecimalPlaces}")} {Unit}";
            var valBrush = new SolidColorBrush(Color.FromRgb(0, 230, 255));
            valBrush.Freeze();
            var valFt = CreateFormattedText(valText, ValueTypeface, 10.0, valBrush);
            dc.DrawText(valFt, new Point(bounds.X + 2.0, bounds.Y + 16.0));

            double trackLeft = bounds.X + labelW + 4.0;
            double trackRight = bounds.Right - 8.0;
            double trackW = trackRight - trackLeft;
            double trackY = bounds.Y + bounds.Height * 0.45;
            double trackH = 8.0;

            if (trackW <= 10) return;

            var bgBrush = new SolidColorBrush(Color.FromRgb(32, 36, 42));
            bgBrush.Freeze();
            dc.DrawRectangle(bgBrush, null, new Rect(trackLeft, trackY, trackW, trackH));

            double norm = (Value - Minimum) / (Maximum - Minimum);
            norm = Math.Max(0.0, Math.Min(1.0, norm));
            double barW = trackW * norm;

            if (PointerStyle == EdgewisePointerStyle.Bar)
            {
                var brush = new LinearGradientBrush(
                    Color.FromRgb(10, 120, 200),
                    BarColor,
                    new Point(0, 0), new Point(1, 0));
                brush.Freeze();
                dc.DrawRectangle(brush, null, new Rect(trackLeft, trackY, barW, trackH));
            }

            double pointerX = trackLeft + barW;
            if (PointerStyle == EdgewisePointerStyle.Flag)
            {
                var flagGeo = new StreamGeometry();
                using (var ctx = flagGeo.Open())
                {
                    ctx.BeginFigure(new Point(pointerX, trackY + trackH + 1.0), true, true);
                    ctx.LineTo(new Point(pointerX - 4.0, trackY + trackH + 6.0), true, false);
                    ctx.LineTo(new Point(pointerX - 4.0, bounds.Bottom - 2.0), true, false);
                    ctx.LineTo(new Point(pointerX + 4.0, bounds.Bottom - 2.0), true, false);
                    ctx.LineTo(new Point(pointerX + 4.0, trackY + trackH + 6.0), true, false);
                }
                flagGeo.Freeze();
                dc.DrawGeometry(Brushes.Gold, null, flagGeo);
            }
            else if (PointerStyle == EdgewisePointerStyle.Line)
            {
                var linePen = new Pen(Brushes.Crimson, 2.0);
                linePen.Freeze();
                dc.DrawLine(linePen, new Point(pointerX, bounds.Y + 2.0), new Point(pointerX, bounds.Bottom - 2.0));
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
    /// Legacy alias for <see cref="ZEdgewiseMeter"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("EdgewiseMeter is deprecated and will be removed in 5 release cycles. Please migrate to ZEdgewiseMeter instead.")]
    public class EdgewiseMeter : ZEdgewiseMeter { }

    /// <summary>
    /// Legacy alias for <see cref="ZEdgewiseMeter"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroEdgewiseMeter is deprecated and will be removed in 5 release cycles. Please migrate to ZEdgewiseMeter instead.")]
    public class ZeroEdgewiseMeter : ZEdgewiseMeter { }

    #endregion

}
