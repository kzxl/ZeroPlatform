using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// Dashboard-style radial gauge chart for WPF with vector needle pointer, threshold zones,
    /// and digital readout.
    /// </summary>
    public class ZGaugeChart : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(ZGaugeChart),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinProperty =
            DependencyProperty.Register(nameof(Min), typeof(double), typeof(ZGaugeChart),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaxProperty =
            DependencyProperty.Register(nameof(Max), typeof(double), typeof(ZGaugeChart),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UnitProperty =
            DependencyProperty.Register(nameof(Unit), typeof(string), typeof(ZGaugeChart),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZGaugeChart),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowLabelProperty =
            DependencyProperty.Register(nameof(ShowLabel), typeof(bool), typeof(ZGaugeChart),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Min
        {
            get => (double)GetValue(MinProperty);
            set => SetValue(MinProperty, value);
        }

        public double Max
        {
            get => (double)GetValue(MaxProperty);
            set => SetValue(MaxProperty, value);
        }

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public bool ShowLabel
        {
            get => (bool)GetValue(ShowLabelProperty);
            set => SetValue(ShowLabelProperty, value);
        }

        public ZGaugeChart()
        {
            Width = 200;
            Height = 200;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        private void OnUnloaded(object sender, RoutedEventArgs e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        private void OnThemeChanged()
        {
            if (Dispatcher.CheckAccess()) InvalidateVisual();
            else Dispatcher.BeginInvoke((Action)InvalidateVisual);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : Height;
            double diameter = Math.Min(w, h) - 24;
            if (diameter <= 20) return;

            Point center = new Point(w / 2.0, h / 2.0 + 10.0);
            double radius = diameter / 2.0;

            const double startAngle = 135.0;
            const double sweepAngle = 270.0;
            const double trackThickness = 12.0;

            // 1. Draw Background Track Arc
            var trackPen = new Pen(ZeroWpfTheme.BorderSubtle, trackThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            dc.DrawGeometry(null, trackPen, CreateArcGeometry(center, radius, startAngle, startAngle + sweepAngle));

            // 2. Threshold Zones: Normal (Green), Warning (Amber), Danger (Red)
            var greenPen = new Pen(new SolidColorBrush(Color.FromRgb(46, 204, 113)), trackThickness - 4);
            var amberPen = new Pen(new SolidColorBrush(Color.FromRgb(241, 196, 15)), trackThickness - 4);
            var redPen = new Pen(new SolidColorBrush(Color.FromRgb(231, 76, 60)), trackThickness - 4);

            dc.DrawGeometry(null, greenPen, CreateArcGeometry(center, radius, startAngle, startAngle + sweepAngle * 0.6));
            dc.DrawGeometry(null, amberPen, CreateArcGeometry(center, radius, startAngle + sweepAngle * 0.6, startAngle + sweepAngle * 0.85));
            dc.DrawGeometry(null, redPen, CreateArcGeometry(center, radius, startAngle + sweepAngle * 0.85, startAngle + sweepAngle));

            // 3. Needle
            double range = Max - Min;
            double fraction = range > 0 ? Math.Max(0.0, Math.Min(1.0, (Value - Min) / range)) : 0.0;
            double needleAngleDeg = startAngle + fraction * sweepAngle;
            double needleAngleRad = needleAngleDeg * Math.PI / 180.0;
            double needleLen = radius - trackThickness - 6.0;

            Point tip = new Point(center.X + Math.Cos(needleAngleRad) * needleLen, center.Y + Math.Sin(needleAngleRad) * needleLen);
            double baseRadL = needleAngleRad - Math.PI / 2.0;
            double baseRadR = needleAngleRad + Math.PI / 2.0;
            const double baseW = 5.0;

            var needleGeo = new StreamGeometry();
            using (var ctx = needleGeo.Open())
            {
                ctx.BeginFigure(tip, true, true);
                ctx.LineTo(new Point(center.X + Math.Cos(baseRadL) * baseW, center.Y + Math.Sin(baseRadL) * baseW), true, false);
                ctx.LineTo(new Point(center.X + Math.Cos(baseRadR) * baseW, center.Y + Math.Sin(baseRadR) * baseW), true, false);
            }
            needleGeo.Freeze();
            dc.DrawGeometry(ZeroWpfTheme.PrimaryAccent, null, needleGeo);

            // Center Cap
            dc.DrawEllipse(ZeroWpfTheme.TextPrimary, null, center, 8, 8);
            dc.DrawEllipse(ZeroWpfTheme.BgPrimary, null, center, 3, 3);

            // 4. Digital Readout
            if (ShowLabel)
            {
                string text = Value.ToString("0.0");
                if (!string.IsNullOrEmpty(Unit)) text += " " + Unit;

                var ft = new FormattedText(
                    text,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    14,
                    ZeroWpfTheme.TextPrimary,
                    1.0);

                dc.DrawText(ft, new Point(center.X - ft.Width / 2.0, center.Y + 18.0));
            }

            // Title
            if (!string.IsNullOrEmpty(Title))
            {
                var titleFt = new FormattedText(
                    Title,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.MediumTypeface,
                    11,
                    ZeroWpfTheme.TextSecondary,
                    1.0);

                dc.DrawText(titleFt, new Point(center.X - titleFt.Width / 2.0, 4.0));
            }
        }

        private static PathGeometry CreateArcGeometry(Point center, double radius, double startAngleDeg, double endAngleDeg)
        {
            double startRad = startAngleDeg * Math.PI / 180.0;
            double endRad = endAngleDeg * Math.PI / 180.0;

            Point startPoint = new Point(center.X + Math.Cos(startRad) * radius, center.Y + Math.Sin(startRad) * radius);
            Point endPoint = new Point(center.X + Math.Cos(endRad) * radius, center.Y + Math.Sin(endRad) * radius);

            bool isLargeArc = Math.Abs(endAngleDeg - startAngleDeg) > 180.0;

            var segment = new ArcSegment(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true);
            var figure = new PathFigure(startPoint, new[] { segment }, false);
            var geo = new PathGeometry(new[] { figure });
            geo.Freeze();
            return geo;
        }
    }

    [Obsolete("ZeroGaugeChart is deprecated. Use ZGaugeChart instead.")]
    public class ZeroGaugeChart : ZGaugeChart { }
}
