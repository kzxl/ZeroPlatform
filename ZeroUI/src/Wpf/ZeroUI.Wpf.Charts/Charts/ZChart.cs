using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Charts.Model;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// High-performance vector chart engine for WPF rendered directly via DrawingContext.
    /// Supports Column, Line, Spline, Area, Candlestick, and Pie/Donut visualizations.
    /// </summary>
    public class ZChart : FrameworkElement
    {
        public List<ChartSeries> Series { get; } = new List<ChartSeries>();
        public List<CandlePoint> CandleData { get; } = new List<CandlePoint>();

        public static readonly DependencyProperty ChartTypeProperty =
            DependencyProperty.Register(
                nameof(ChartType),
                typeof(ChartType),
                typeof(ZChart),
                new FrameworkPropertyMetadata(ChartType.Column, FrameworkPropertyMetadataOptions.AffectsRender));

        public ChartType ChartType
        {
            get => (ChartType)GetValue(ChartTypeProperty);
            set => SetValue(ChartTypeProperty, value);
        }

        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public bool ShowLegend { get; set; } = true;
        public bool ShowCrosshair { get; set; } = true;
        public CrosshairMode CrosshairMode { get; set; } = CrosshairMode.Both;
        public bool EnableSpcBelts { get; set; } = false;
        public SpcBeltStyle SpcBeltStyle { get; set; } = SpcBeltStyle.TrafficLight;
        public double? UpperControlLimit { get; set; }
        public double? LowerControlLimit { get; set; }
        public double? NominalTarget { get; set; }
        public double? UpperSpecLimit { get; set; }
        public double? LowerSpecLimit { get; set; }

        // Interaction state
        private Point? _mousePos;

        public ZChart()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #pragma warning disable CS0618
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

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _mousePos = e.GetPosition(this);
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _mousePos = null;
            InvalidateVisual();
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

            // Background & Border
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1));

            // Title & Subtitle
            double contentTop = 16;
            if (!string.IsNullOrEmpty(Title))
            {
                var titleFt = CreateFormattedText(Title, ZeroWpfTheme.BoldTypeface, 14.0, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(titleFt, new Point(16, contentTop));
                contentTop += titleFt.Height + 2;

                if (!string.IsNullOrEmpty(Subtitle))
                {
                    var subFt = CreateFormattedText(Subtitle, ZeroWpfTheme.RegularTypeface, 11.0, ZeroWpfTheme.TextMuted, dpi);
                    dc.DrawText(subFt, new Point(16, contentTop));
                    contentTop += subFt.Height + 10;
                }
                else
                {
                    contentTop += 8;
                }
            }

            // Plot Area
            double padLeft = 45;
            double padRight = 20;
            double padBottom = 30;
            Rect plotRect = new Rect(padLeft, contentTop, Math.Max(10, w - padLeft - padRight), Math.Max(10, h - contentTop - padBottom));

            if (ChartType == ChartType.Pie || ChartType == ChartType.Donut)
            {
                RenderPieDonut(dc, plotRect, dpi);
            }
            else if (ChartType == ChartType.Candlestick)
            {
                RenderCandlestick(dc, plotRect, dpi);
            }
            else
            {
                RenderCartesian(dc, plotRect, dpi);
            }
        }

        private void RenderCartesian(DrawingContext dc, Rect plot, double dpi)
        {
            // Compute range
            double minY = 0;
            double maxY = 10;
            int maxPoints = 0;

            foreach (var s in Series)
            {
                if (!s.IsVisible) continue;
                maxPoints = Math.Max(maxPoints, s.Points.Count);
                foreach (var p in s.Points)
                {
                    if (p.Value < minY) minY = p.Value;
                    if (p.Value > maxY) maxY = p.Value;
                }
            }

            if (maxY <= minY) maxY = minY + 10;
            maxY = Math.Ceiling(maxY * 1.1);

            // Draw Y-Axis grid lines
            int yTicks = 4;
            for (int i = 0; i <= yTicks; i++)
            {
                double ratio = (double)i / yTicks;
                double yVal = minY + (maxY - minY) * ratio;
                double yPos = plot.Bottom - ratio * plot.Height;

                dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(plot.Left, yPos), new Point(plot.Right, yPos));

                var ft = CreateFormattedText($"{yVal:0.#}", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(ft, new Point(plot.Left - ft.Width - 6, yPos - ft.Height / 2.0));
            }

            if (maxPoints == 0) return;

            // Draw X-Axis labels
            double stepX = plot.Width / Math.Max(1, maxPoints);
            for (int i = 0; i < maxPoints; i++)
            {
                double xPos = plot.Left + i * stepX + stepX / 2.0;
                string label = i < Series[0].Points.Count ? Series[0].Points[i].Label : $"{i + 1}";
                var xFt = CreateFormattedText(label, ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(xFt, new Point(xPos - xFt.Width / 2.0, plot.Bottom + 6));
            }

            // Render SPC Limit Belts (Six Sigma UCL, Nominal, LCL)
            if (EnableSpcBelts && Series.Count > 0 && Series[0].Points.Count > 0)
            {
                double sum = 0;
                var pts = Series[0].Points;
                foreach (var pt in pts) sum += pt.Value;
                double mean = sum / pts.Count;

                double varSum = 0;
                foreach (var pt in pts) varSum += Math.Pow(pt.Value - mean, 2);
                double sigma = Math.Sqrt(varSum / Math.Max(1, pts.Count - 1));

                double ucl = UpperControlLimit ?? (mean + 3 * sigma);
                double lcl = LowerControlLimit ?? (mean - 3 * sigma);
                double cl = NominalTarget ?? mean;

                double uclRatio = (ucl - minY) / (maxY - minY);
                double lclRatio = (lcl - minY) / (maxY - minY);
                double clRatio = (cl - minY) / (maxY - minY);

                double uclY = plot.Bottom - Math.Max(0, Math.Min(1, uclRatio)) * plot.Height;
                double lclY = plot.Bottom - Math.Max(0, Math.Min(1, lclRatio)) * plot.Height;
                double clY = plot.Bottom - Math.Max(0, Math.Min(1, clRatio)) * plot.Height;

                if (SpcBeltStyle == SpcBeltStyle.TrafficLight || SpcBeltStyle == SpcBeltStyle.Subtle)
                {
                    // Green Zone (Normal: within 1 sigma)
                    double sig1TopRatio = ((cl + sigma) - minY) / (maxY - minY);
                    double sig1BotRatio = ((cl - sigma) - minY) / (maxY - minY);
                    double sig1TopY = plot.Bottom - Math.Max(0, Math.Min(1, sig1TopRatio)) * plot.Height;
                    double sig1BotY = plot.Bottom - Math.Max(0, Math.Min(1, sig1BotRatio)) * plot.Height;

                    Brush greenZone = new SolidColorBrush(Color.FromArgb(28, 16, 185, 129));
                    greenZone.Freeze();
                    dc.DrawRectangle(greenZone, null, new Rect(plot.Left, sig1TopY, plot.Width, Math.Max(0, sig1BotY - sig1TopY)));

                    // Amber Warning Zone (between 1-sigma and 3-sigma)
                    Brush amberZone = new SolidColorBrush(Color.FromArgb(20, 245, 158, 11));
                    amberZone.Freeze();
                    dc.DrawRectangle(amberZone, null, new Rect(plot.Left, uclY, plot.Width, Math.Max(0, sig1TopY - uclY)));
                    dc.DrawRectangle(amberZone, null, new Rect(plot.Left, sig1BotY, plot.Width, Math.Max(0, lclY - sig1BotY)));
                }

                // UCL & LCL boundary lines (Crimson Red dashed)
                var spcLinePen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.2)
                {
                    DashStyle = new DashStyle(new double[] { 4.0, 3.0 }, 0)
                };
                spcLinePen.Freeze();
                dc.DrawLine(spcLinePen, new Point(plot.Left, uclY), new Point(plot.Right, uclY));
                dc.DrawLine(spcLinePen, new Point(plot.Left, lclY), new Point(plot.Right, lclY));

                // CL Center Line (Emerald Green solid)
                var clPen = new Pen(new SolidColorBrush(Color.FromRgb(16, 185, 129)), 1.2);
                clPen.Freeze();
                dc.DrawLine(clPen, new Point(plot.Left, clY), new Point(plot.Right, clY));

                // Belt Right Badges
                var uclBadge = CreateFormattedText($"UCL {ucl:F1}", ZeroWpfTheme.BoldTypeface, 8.5, new SolidColorBrush(Color.FromRgb(239, 68, 68)), dpi);
                dc.DrawText(uclBadge, new Point(plot.Right - uclBadge.Width - 4, uclY - uclBadge.Height - 1));

                var clBadge = CreateFormattedText($"CL {cl:F1}", ZeroWpfTheme.BoldTypeface, 8.5, new SolidColorBrush(Color.FromRgb(16, 185, 129)), dpi);
                dc.DrawText(clBadge, new Point(plot.Right - clBadge.Width - 4, clY - clBadge.Height - 1));

                var lclBadge = CreateFormattedText($"LCL {lcl:F1}", ZeroWpfTheme.BoldTypeface, 8.5, new SolidColorBrush(Color.FromRgb(239, 68, 68)), dpi);
                dc.DrawText(lclBadge, new Point(plot.Right - lclBadge.Width - 4, lclY + 1));
            }

            // Render Series
            foreach (var s in Series)
            {
                if (!s.IsVisible || s.Points.Count == 0) continue;

                var type = s.TypeOverride ?? ChartType;
                var brush = new SolidColorBrush(s.Color);
                brush.Freeze();
                var pen = new Pen(brush, s.StrokeThickness);
                pen.Freeze();

                if (type == ChartType.Column)
                {
                    double colWidth = Math.Max(4, stepX * 0.5);
                    for (int i = 0; i < s.Points.Count; i++)
                    {
                        var p = s.Points[i];
                        double pRatio = (p.Value - minY) / (maxY - minY);
                        double barH = pRatio * plot.Height;
                        double xPos = plot.Left + i * stepX + (stepX - colWidth) / 2.0;
                        double yPos = plot.Bottom - barH;

                        dc.DrawRoundedRectangle(brush, null, new Rect(xPos, yPos, colWidth, barH), 2, 2);
                    }
                }
                else if (type == ChartType.Line || type == ChartType.Spline || type == ChartType.Area || type == ChartType.SplineArea || type == ChartType.AreaSpline)
                {
                    var geom = new StreamGeometry();
                    var areaGeom = new StreamGeometry();

                    using (var ctx = geom.Open())
                    using (var actx = areaGeom.Open())
                    {
                        Point firstPt = new Point(plot.Left + stepX / 2.0, plot.Bottom - ((s.Points[0].Value - minY) / (maxY - minY)) * plot.Height);
                        ctx.BeginFigure(firstPt, false, false);
                        actx.BeginFigure(new Point(firstPt.X, plot.Bottom), true, true);
                        actx.LineTo(firstPt, true, false);

                        for (int i = 1; i < s.Points.Count; i++)
                        {
                            var p = s.Points[i];
                            double pRatio = (p.Value - minY) / (maxY - minY);
                            Point pt = new Point(plot.Left + i * stepX + stepX / 2.0, plot.Bottom - pRatio * plot.Height);
                            ctx.LineTo(pt, true, false);
                            actx.LineTo(pt, true, false);
                        }

                        actx.LineTo(new Point(plot.Left + (s.Points.Count - 1) * stepX + stepX / 2.0, plot.Bottom), true, false);
                    }

                    geom.Freeze();
                    areaGeom.Freeze();

                    if (type == ChartType.Area || type == ChartType.SplineArea || type == ChartType.AreaSpline)
                    {
                        var areaFill = new SolidColorBrush(Color.FromArgb((byte)(s.FillOpacity * 255), s.Color.R, s.Color.G, s.Color.B));
                        areaFill.Freeze();
                        dc.DrawGeometry(areaFill, null, areaGeom);
                    }

                    dc.DrawGeometry(null, pen, geom);

                    // Draw points
                    for (int i = 0; i < s.Points.Count; i++)
                    {
                        double pRatio = (s.Points[i].Value - minY) / (maxY - minY);
                        Point pt = new Point(plot.Left + i * stepX + stepX / 2.0, plot.Bottom - pRatio * plot.Height);
                        dc.DrawEllipse(ZeroWpfTheme.BgCard, pen, pt, 3.5, 3.5);
                    }
                }
            }

            // Crosshair & Tooltip HUD
            if (ShowCrosshair && _mousePos.HasValue && plot.Contains(_mousePos.Value) && CrosshairMode != CrosshairMode.None)
            {
                double mx = _mousePos.Value.X;
                int hoveredIdx = (int)((mx - plot.Left) / stepX);
                if (hoveredIdx >= 0 && hoveredIdx < maxPoints)
                {
                    double cx = plot.Left + hoveredIdx * stepX + stepX / 2.0;
                    double hoveredVal = Series[0].Points[hoveredIdx].Value;
                    double pRatio = (hoveredVal - minY) / (maxY - minY);
                    double cy = plot.Bottom - Math.Max(0, Math.Min(1, pRatio)) * plot.Height;

                    var crossPen = new Pen(new SolidColorBrush(Color.FromArgb(180, 129, 140, 248)), 1.2)
                    {
                        DashStyle = new DashStyle(new double[] { 3.0, 3.0 }, 0)
                    };
                    crossPen.Freeze();

                    // Vertical Crosshair line
                    if (CrosshairMode == CrosshairMode.VerticalOnly || CrosshairMode == CrosshairMode.Both)
                    {
                        dc.DrawLine(crossPen, new Point(cx, plot.Top), new Point(cx, plot.Bottom));

                        // X-axis pill tag
                        var xTagText = CreateFormattedText(Series[0].Points[hoveredIdx].Label, ZeroWpfTheme.BoldTypeface, 9.5, Brushes.White, dpi);
                        double tagW = xTagText.Width + 10;
                        double tagH = xTagText.Height + 4;
                        double tagX = cx - tagW / 2.0;
                        var tagBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                        tagBg.Freeze();
                        dc.DrawRoundedRectangle(tagBg, null, new Rect(tagX, plot.Bottom + 1, tagW, tagH), 3, 3);
                        dc.DrawText(xTagText, new Point(tagX + 5, plot.Bottom + 3));
                    }

                    // Horizontal Crosshair line
                    if (CrosshairMode == CrosshairMode.HorizontalOnly || CrosshairMode == CrosshairMode.Both)
                    {
                        dc.DrawLine(crossPen, new Point(plot.Left, cy), new Point(plot.Right, cy));

                        // Y-axis pill tag
                        var yTagText = CreateFormattedText($"{hoveredVal:F1}", ZeroWpfTheme.BoldTypeface, 9.5, Brushes.White, dpi);
                        double yTagW = yTagText.Width + 8;
                        double yTagH = yTagText.Height + 4;
                        var yTagBg = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                        yTagBg.Freeze();
                        dc.DrawRoundedRectangle(yTagBg, null, new Rect(plot.Left - yTagW - 2, cy - yTagH / 2.0, yTagW, yTagH), 3, 3);
                        dc.DrawText(yTagText, new Point(plot.Left - yTagW + 2, cy - yTagH / 2.0 + 2));
                    }

                    // Snapped Point Circle
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(129, 140, 248)), null, new Point(cx, cy), 5.5, 5.5);
                    dc.DrawEllipse(Brushes.White, null, new Point(cx, cy), 2.5, 2.5);

                    // Crosshair Floating HUD Card (Top-Right of plot)
                    string hudTitle = $"🎯 POINT: {Series[0].Points[hoveredIdx].Label}";
                    string hudValue = $"VALUE: {hoveredVal:N1} {(EnableSpcBelts ? "• SPC: NORMAL" : "")}";
                    var hudTitleFt = CreateFormattedText(hudTitle, ZeroWpfTheme.BoldTypeface, 10.5, ZeroWpfTheme.TextPrimary, dpi);
                    var hudValFt = CreateFormattedText(hudValue, ZeroWpfTheme.RegularTypeface, 10.0, new SolidColorBrush(Color.FromRgb(16, 185, 129)), dpi);

                    double hudW = Math.Max(hudTitleFt.Width, hudValFt.Width) + 16;
                    double hudH = hudTitleFt.Height + hudValFt.Height + 10;
                    double hudX = plot.Right - hudW - 8;
                    double hudY = plot.Top + 8;

                    var hudBg = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42));
                    var hudBorder = new Pen(new SolidColorBrush(Color.FromArgb(160, 59, 130, 246)), 1.2);
                    hudBg.Freeze();
                    hudBorder.Freeze();

                    dc.DrawRoundedRectangle(hudBg, hudBorder, new Rect(hudX, hudY, hudW, hudH), 5, 5);
                    dc.DrawText(hudTitleFt, new Point(hudX + 8, hudY + 4));
                    dc.DrawText(hudValFt, new Point(hudX + 8, hudY + 6 + hudTitleFt.Height));
                }
            }
        }

        private void RenderPieDonut(DrawingContext dc, Rect plot, double dpi)
        {
            if (Series.Count == 0 || Series[0].Points.Count == 0) return;

            var points = Series[0].Points;
            double total = 0;
            foreach (var p in points) total += Math.Max(0, p.Value);
            if (total <= 0) return;

            Point center = new Point(plot.Left + plot.Width / 2.0, plot.Top + plot.Height / 2.0);
            double outerRadius = Math.Min(plot.Width, plot.Height) / 2.2;
            double innerRadius = (ChartType == ChartType.Donut) ? outerRadius * 0.6 : 0;

            Color[] palette = new Color[]
            {
                Color.FromRgb(129, 140, 248), // Primary Indigo
                Color.FromRgb(167, 139, 250), // Purple
                Color.FromRgb(166, 227, 161), // Green
                Color.FromRgb(249, 226, 175), // Yellow
                Color.FromRgb(243, 139, 168), // Pink
                Color.FromRgb(137, 180, 250)  // Sky
            };

            double curAngle = -90.0;
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                double sliceAngle = (p.Value / total) * 360.0;
                double nextAngle = curAngle + sliceAngle;

                Color sliceColor = p.ColorOverride ?? palette[i % palette.Length];
                var sliceBrush = new SolidColorBrush(sliceColor);
                sliceBrush.Freeze();

                var pathGeom = new PathGeometry();
                var fig = new PathFigure();

                double radStart = curAngle * Math.PI / 180.0;
                double radEnd = nextAngle * Math.PI / 180.0;

                Point p1 = new Point(center.X + outerRadius * Math.Cos(radStart), center.Y + outerRadius * Math.Sin(radStart));
                Point p2 = new Point(center.X + outerRadius * Math.Cos(radEnd), center.Y + outerRadius * Math.Sin(radEnd));

                fig.StartPoint = (innerRadius > 0) ?
                    new Point(center.X + innerRadius * Math.Cos(radStart), center.Y + innerRadius * Math.Sin(radStart)) : center;

                fig.Segments.Add(new LineSegment(p1, true));
                fig.Segments.Add(new ArcSegment(p2, new Size(outerRadius, outerRadius), 0, sliceAngle > 180, SweepDirection.Clockwise, true));

                if (innerRadius > 0)
                {
                    Point p3 = new Point(center.X + innerRadius * Math.Cos(radEnd), center.Y + innerRadius * Math.Sin(radEnd));
                    fig.Segments.Add(new LineSegment(p3, true));
                    fig.Segments.Add(new ArcSegment(fig.StartPoint, new Size(innerRadius, innerRadius), 0, sliceAngle > 180, SweepDirection.Counterclockwise, true));
                }

                fig.IsClosed = true;
                pathGeom.Figures.Add(fig);
                pathGeom.Freeze();

                dc.DrawGeometry(sliceBrush, ZeroWpfTheme.BorderPen, pathGeom);

                curAngle = nextAngle;
            }

            // Donut Center Text
            if (ChartType == ChartType.Donut)
            {
                var totalFt = CreateFormattedText($"{total:N0}", ZeroWpfTheme.BoldTypeface, 16.0, ZeroWpfTheme.TextPrimary, dpi);
                var labelFt = CreateFormattedText("Total", ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextMuted, dpi);

                dc.DrawText(totalFt, new Point(center.X - totalFt.Width / 2.0, center.Y - totalFt.Height / 2.0 - 6));
                dc.DrawText(labelFt, new Point(center.X - labelFt.Width / 2.0, center.Y + 10));
            }
        }

        private void RenderCandlestick(DrawingContext dc, Rect plot, double dpi)
        {
            if (CandleData.Count == 0) return;

            double minP = double.MaxValue;
            double maxP = double.MinValue;
            foreach (var c in CandleData)
            {
                if (c.Low < minP) minP = c.Low;
                if (c.High > maxP) maxP = c.High;
            }
            if (maxP <= minP) maxP = minP + 10;

            double stepX = plot.Width / CandleData.Count;
            double candleWidth = Math.Max(3, stepX * 0.65);

            for (int i = 0; i < CandleData.Count; i++)
            {
                var c = CandleData[i];
                Brush cBrush = c.IsBullish ? ZeroWpfTheme.SuccessAccent : ZeroWpfTheme.DangerAccent;
                Pen cPen = new Pen(cBrush, 1.0);
                cPen.Freeze();

                double cx = plot.Left + i * stepX + stepX / 2.0;
                double highY = plot.Bottom - ((c.High - minP) / (maxP - minP)) * plot.Height;
                double lowY = plot.Bottom - ((c.Low - minP) / (maxP - minP)) * plot.Height;
                double openY = plot.Bottom - ((c.Open - minP) / (maxP - minP)) * plot.Height;
                double closeY = plot.Bottom - ((c.Close - minP) / (maxP - minP)) * plot.Height;

                // Wick
                dc.DrawLine(cPen, new Point(cx, highY), new Point(cx, lowY));

                // Body
                double topY = Math.Min(openY, closeY);
                double bodyH = Math.Max(2, Math.Abs(openY - closeY));
                dc.DrawRectangle(cBrush, null, new Rect(cx - candleWidth / 2.0, topY, candleWidth, bodyH));
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
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZChart"/>.
    /// </summary>
    public class ZChartControl : ZChart { }

    /// <summary>
    /// Convenience alias for <see cref="ZChart"/>.
    /// </summary>
    public class ZeroChartControl : ZChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ChartControl is deprecated and will be removed in 5 release cycles. Please migrate to ZChart instead.")]
    public class ChartControl : ZChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroChart is deprecated and will be removed in 5 release cycles. Please migrate to ZChart instead.")]
    public class ZeroChart : ZChart { }

    #endregion

}
