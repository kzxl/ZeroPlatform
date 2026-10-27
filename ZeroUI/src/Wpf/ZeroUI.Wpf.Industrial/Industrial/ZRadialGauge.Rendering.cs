using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public partial class ZRadialGauge
    {
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

        /// <summary>
        /// Formats the gauge numeric value using <see cref="ValueFormat"/>, safely falling back to "0.#" on empty or format error.
        /// </summary>
        internal string FormatValue(double val)
        {
            string fmt = ValueFormat;
            if (string.IsNullOrWhiteSpace(fmt))
            {
                return val.ToString("0.#", CultureInfo.InvariantCulture);
            }

            try
            {
                if (fmt.Contains("{0"))
                {
                    return string.Format(CultureInfo.InvariantCulture, fmt, val);
                }

                return val.ToString(fmt, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return val.ToString("0.#", CultureInfo.InvariantCulture);
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

            // Background Card
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1));

            // Title
            if (!string.IsNullOrEmpty(Title))
            {
                var titleFt = CreateFormattedText(Title, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextSecondary, dpi);
                dc.DrawText(titleFt, new Point(14, 12));
            }

            // Gauge Center & Radius
            Point center = new Point(w / 2.0, h * 0.58);
            double radius = Math.Min(w, h) * 0.38;
            if (radius <= 10) return;

            // Draw Background Track Arc
            DrawArc(dc, center, radius, _startAngle, _sweepAngle, ZeroWpfTheme.BgInput, 10.0);

            double range = Math.Max(1.0, Maximum - Minimum);

            // Draw Threshold Arcs
            if (_thresholds.Count > 0)
            {
                foreach (var th in _thresholds)
                {
                    double tFrom = Math.Max(Minimum, Math.Min(Maximum, th.From));
                    double tTo = Math.Max(Minimum, Math.Min(Maximum, th.To));
                    if (tTo <= tFrom) continue;

                    double rFrom = (tFrom - Minimum) / range;
                    double rTo = (tTo - Minimum) / range;

                    double arcStart = _startAngle + _sweepAngle * rFrom;
                    double arcSweep = _sweepAngle * (rTo - rFrom);

                    Color c = Color.FromArgb(
                        (byte)((th.ArgbColor >> 24) & 0xFF),
                        (byte)((th.ArgbColor >> 16) & 0xFF),
                        (byte)((th.ArgbColor >> 8) & 0xFF),
                        (byte)(th.ArgbColor & 0xFF));

                    var brush = new SolidColorBrush(c);
                    brush.Freeze();
                    DrawArc(dc, center, radius, arcStart, arcSweep, brush, 6.0);
                }
            }

            // Draw Major & Minor Ticks
            int totalMajor = _majorTicks;
            for (int i = 0; i <= totalMajor; i++)
            {
                double tickRatio = (double)i / totalMajor;
                double angleDeg = _startAngle + _sweepAngle * tickRatio;
                double angleRad = angleDeg * Math.PI / 180.0;
                double cos = Math.Cos(angleRad);
                double sin = Math.Sin(angleRad);

                Point pOuter = new Point(center.X + (radius + 2) * cos, center.Y + (radius + 2) * sin);
                Point pInner = new Point(center.X + (radius - 8) * cos, center.Y + (radius - 8) * sin);

                dc.DrawLine(ZeroWpfTheme.GridLinePen, pInner, pOuter);

                // Tick numeric label
                if (radius > 35)
                {
                    double tickVal = Minimum + tickRatio * range;
                    Point pText = new Point(center.X + (radius - 18) * cos, center.Y + (radius - 18) * sin);
                    var tFt = CreateFormattedText($"{tickVal:0}", ZeroWpfTheme.RegularTypeface, 8.5, ZeroWpfTheme.TextMuted, dpi);
                    dc.DrawText(tFt, new Point(pText.X - tFt.Width / 2.0, pText.Y - tFt.Height / 2.0));
                }

                // Minor ticks
                if (_minorTicks > 0 && i < totalMajor)
                {
                    for (int m = 1; m <= _minorTicks; m++)
                    {
                        double mRatio = tickRatio + ((double)m / (_minorTicks + 1)) * (1.0 / totalMajor);
                        double mAngle = (_startAngle + _sweepAngle * mRatio) * Math.PI / 180.0;
                        Point mpOuter = new Point(center.X + radius * Math.Cos(mAngle), center.Y + radius * Math.Sin(mAngle));
                        Point mpInner = new Point(center.X + (radius - 4) * Math.Cos(mAngle), center.Y + (radius - 4) * Math.Sin(mAngle));
                        dc.DrawLine(ZeroWpfTheme.GridLinePen, mpInner, mpOuter);
                    }
                }
            }

            // Needle Angle based on indicated damped value
            double needleVal = Math.Max(Minimum, Math.Min(Maximum, _indicatedValue));
            float needleAngleDeg = GaugeMath.ValueToAngle(needleVal, Minimum, Maximum, _startAngle, _sweepAngle);
            double needleAngleRad = needleAngleDeg * Math.PI / 180.0;

            // Draw Needle
            Point needleTip = new Point(center.X + (radius - 6) * Math.Cos(needleAngleRad), center.Y + (radius - 6) * Math.Sin(needleAngleRad));
            Pen needlePen = new Pen(ZeroWpfTheme.PrimaryAccent, 2.5);
            needlePen.Freeze();
            dc.DrawLine(needlePen, center, needleTip);

            // Center Pin
            dc.DrawEllipse(ZeroWpfTheme.PrimaryAccent, null, center, 6, 6);
            dc.DrawEllipse(ZeroWpfTheme.BgCard, null, center, 2.5, 2.5);

            // Digital Value Readout (with Tabular Digit Pitch centering to eliminate horizontal jitter)
            RenderDigitalReadout(dc, center, needleVal, dpi);

            // Unit Readout
            if (!string.IsNullOrEmpty(Unit))
            {
                var unitFt = CreateFormattedText(Unit, ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(unitFt, new Point(center.X - unitFt.Width / 2.0, center.Y + 38));
            }
        }

        private void RenderDigitalReadout(DrawingContext dc, Point center, double needleVal, double dpi)
        {
            string valStr = FormatValue(needleVal);
            if (string.IsNullOrEmpty(valStr)) return;

            if (!UseTabularReadout)
            {
                // Standard monolithic text centering
                var valFt = CreateFormattedText(valStr, ZeroWpfTheme.BoldTypeface, 18.0, ZeroWpfTheme.TextPrimary, dpi);
                dc.DrawText(valFt, new Point(center.X - valFt.Width / 2.0, center.Y + 16));
                return;
            }

            // Tabular Digit Spacing Engine (Eliminates horizontal jump/jitter on proportional font characters)
            // 1. Measure standard reference digit slot using '0' (widest typical digit in Segoe UI)
            var refDigitFt = CreateFormattedText("0", ZeroWpfTheme.BoldTypeface, 18.0, ZeroWpfTheme.TextPrimary, dpi);
            double digitSlotWidth = refDigitFt.Width;

            // 2. Compute slot widths for each character
            double[] slotWidths = new double[valStr.Length];
            FormattedText[] glyphs = new FormattedText[valStr.Length];
            double totalWidth = 0.0;

            for (int i = 0; i < valStr.Length; i++)
            {
                char c = valStr[i];
                var cFt = CreateFormattedText(c.ToString(), ZeroWpfTheme.BoldTypeface, 18.0, ZeroWpfTheme.TextPrimary, dpi);
                glyphs[i] = cFt;

                if (char.IsDigit(c))
                {
                    slotWidths[i] = Math.Max(digitSlotWidth, cFt.Width);
                }
                else
                {
                    slotWidths[i] = cFt.Width;
                }
                totalWidth += slotWidths[i];
            }

            // 3. Render each character centered within its dedicated slot
            double currentX = center.X - totalWidth / 2.0;
            double currentY = center.Y + 16;

            for (int i = 0; i < valStr.Length; i++)
            {
                double slotW = slotWidths[i];
                var cFt = glyphs[i];
                double charDrawX = currentX + (slotW - cFt.Width) / 2.0;
                dc.DrawText(cFt, new Point(charDrawX, currentY));
                currentX += slotW;
            }
        }

        private static void DrawArc(DrawingContext dc, Point center, double radius, double startAngleDeg, double sweepAngleDeg, Brush brush, double thickness)
        {
            if (sweepAngleDeg <= 0) return;

            var geom = new PathGeometry();
            var fig = new PathFigure();

            double startRad = startAngleDeg * Math.PI / 180.0;
            double endRad = (startAngleDeg + sweepAngleDeg) * Math.PI / 180.0;

            Point pStart = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
            Point pEnd = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));

            fig.StartPoint = pStart;
            fig.Segments.Add(new ArcSegment(pEnd, new Size(radius, radius), 0, sweepAngleDeg > 180, SweepDirection.Clockwise, true));

            geom.Figures.Add(fig);
            geom.Freeze();

            var pen = new Pen(brush, thickness);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geom);
        }
    }
}
