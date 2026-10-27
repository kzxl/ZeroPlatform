using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Modern circular gauge meter for OEE, Yield rate, and equipment efficiency in WPF.
    /// Provides 100% parity with WinForms CircularGauge.
    /// </summary>
    public class ZCircularGauge : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(double),
                typeof(ZCircularGauge),
                new FrameworkPropertyMetadata(85.0, FrameworkPropertyMetadataOptions.AffectsRender, null, CoerceValue));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(ZCircularGauge),
                new FrameworkPropertyMetadata("OEE Rate", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SuffixProperty =
            DependencyProperty.Register(
                nameof(Suffix),
                typeof(string),
                typeof(ZCircularGauge),
                new FrameworkPropertyMetadata("%", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ValueFormatProperty =
            DependencyProperty.Register(
                nameof(ValueFormat),
                typeof(string),
                typeof(ZCircularGauge),
                new FrameworkPropertyMetadata("0.0", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ThicknessProperty =
            DependencyProperty.Register(
                nameof(Thickness),
                typeof(double),
                typeof(ZCircularGauge),
                new FrameworkPropertyMetadata(8.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GaugeBrushProperty =
            DependencyProperty.Register(
                nameof(GaugeBrush),
                typeof(Brush),
                typeof(ZCircularGauge),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(16, 185, 129)), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TrackBrushProperty =
            DependencyProperty.Register(
                nameof(TrackBrush),
                typeof(Brush),
                typeof(ZCircularGauge),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(229, 231, 235)), FrameworkPropertyMetadataOptions.AffectsRender));

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            if (baseValue is double v)
            {
                return Math.Max(0.0, Math.Min(100.0, v));
            }
            return 0.0;
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Suffix
        {
            get => (string)GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public string ValueFormat
        {
            get => (string)GetValue(ValueFormatProperty);
            set => SetValue(ValueFormatProperty, value);
        }

        public double Thickness
        {
            get => (double)GetValue(ThicknessProperty);
            set => SetValue(ThicknessProperty, value);
        }

        public Brush GaugeBrush
        {
            get => (Brush)GetValue(GaugeBrushProperty);
            set => SetValue(GaugeBrushProperty, value);
        }

        public Brush TrackBrush
        {
            get => (Brush)GetValue(TrackBrushProperty);
            set => SetValue(TrackBrushProperty, value);
        }

        public ZCircularGauge()
        {
            Width = 110;
            Height = 110;
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

        private string FormatValue(double val)
        {
            string fmt = ValueFormat;
            string suffix = Suffix ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fmt)) return val.ToString("0.0", CultureInfo.InvariantCulture) + suffix;

            try
            {
                if (fmt.Contains("{0"))
                {
                    return string.Format(CultureInfo.InvariantCulture, fmt, val) + suffix;
                }
                return val.ToString(fmt, CultureInfo.InvariantCulture) + suffix;
            }
            catch
            {
                return val.ToString("0.0", CultureInfo.InvariantCulture) + suffix;
            }
        }

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

            double th = Math.Max(2.0, Thickness);
            double pad = th + 4.0;
            double size = Math.Min(w, h) - (pad * 2.0);
            if (size <= 10.0) return;

            Point center = new Point(w / 2.0, (h / 2.0) - 2.0);
            double radius = size / 2.0;

            double startAngle = 135.0;
            double sweepLength = 270.0;

            // 1. Draw Background Track Arc
            Pen trackPen = new Pen(TrackBrush, th)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            DrawArc(dc, center, radius, startAngle, sweepLength, trackPen);

            // 2. Draw Active Progress Arc
            double activeSweep = (Value / 100.0) * sweepLength;
            if (activeSweep > 0.5)
            {
                Pen gaugePen = new Pen(GaugeBrush, th)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                DrawArc(dc, center, radius, startAngle, activeSweep, gaugePen);
            }

            // 3. Draw Center Value
            string valText = FormatValue(Value);
            var valFt = CreateFormattedText(
                valText,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                Math.Max(10.0, radius * 0.42),
                new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                dpi);
            dc.DrawText(valFt, new Point(center.X - (valFt.Width / 2.0), center.Y - (valFt.Height / 2.0) - 4.0));

            // 4. Draw Title below Value
            if (!string.IsNullOrEmpty(Title))
            {
                var titleFt = CreateFormattedText(
                    Title,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                    Math.Max(8.0, radius * 0.22),
                    new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                    dpi);
                dc.DrawText(titleFt, new Point(center.X - (titleFt.Width / 2.0), center.Y + (valFt.Height / 2.0) - 2.0));
            }
        }

        private static void DrawArc(DrawingContext dc, Point center, double radius, double startAngleDeg, double sweepAngleDeg, Pen pen)
        {
            double startRad = (startAngleDeg * Math.PI) / 180.0;
            double endRad = ((startAngleDeg + sweepAngleDeg) * Math.PI) / 180.0;

            Point startPt = new Point(center.X + (radius * Math.Cos(startRad)), center.Y + (radius * Math.Sin(startRad)));
            Point endPt = new Point(center.X + (radius * Math.Cos(endRad)), center.Y + (radius * Math.Sin(endRad)));

            bool isLargeArc = sweepAngleDeg > 180.0;

            PathGeometry geom = new PathGeometry();
            PathFigure fig = new PathFigure { StartPoint = startPt, IsFilled = false };
            fig.Segments.Add(new ArcSegment(endPt, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));
            geom.Figures.Add(fig);

            dc.DrawGeometry(null, pen, geom);
        }
    }
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCircularGauge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CircularGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZCircularGauge instead.")]
    public class CircularGauge : ZCircularGauge { }

    /// <summary>
    /// Legacy alias for <see cref="ZCircularGauge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCircularGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZCircularGauge instead.")]
    public class ZeroCircularGauge : ZCircularGauge { }

    #endregion

}
