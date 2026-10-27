using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    public partial class ZLinearGauge
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
            var titleFt = CreateFormattedText(Title, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextSecondary, dpi);
            dc.DrawText(titleFt, new Point(12, 10));

            double range = Math.Max(1.0, Maximum - Minimum);
            double clampedVal = Math.Max(Minimum, Math.Min(Maximum, _indicatedValue));
            double ratio = (clampedVal - Minimum) / range;

            // Pick fill brush based on threshold ranges or severity
            GaugeSeverity sev = GaugeMath.EvaluateSeverity(clampedVal, _thresholds);
            Brush fillBrush = sev switch
            {
                GaugeSeverity.Critical => ZeroWpfTheme.DangerAccent,
                GaugeSeverity.Warning => ZeroWpfTheme.WarningAccent,
                _ => ZeroWpfTheme.PrimaryAccent
            };

            if (IsHorizontal)
            {
                double trackX = 12;
                double trackY = 32;
                double trackW = Math.Max(10, w - 24);
                double trackH = 14;

                // Track
                dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, null, new Rect(trackX, trackY, trackW, trackH), 4, 4);

                // Fill
                if (ratio > 0)
                {
                    dc.DrawRoundedRectangle(fillBrush, null, new Rect(trackX, trackY, trackW * ratio, trackH), 4, 4);
                }

                // Readout with Tabular Digit Spacing Engine
                RenderDigitalReadout(dc, trackX, trackY + trackH + 6, clampedVal, Unit, dpi, isCenter: false, originCenterX: 0);
            }
            else
            {
                // Vertical Bar
                double barW = 22;
                double barX = 24;
                double barTop = 32;
                double barH = Math.Max(10, h - 68);

                // Track
                dc.DrawRoundedRectangle(ZeroWpfTheme.BgInput, null, new Rect(barX, barTop, barW, barH), 4, 4);

                // Fill from bottom up
                if (ratio > 0)
                {
                    double fillH = barH * ratio;
                    dc.DrawRoundedRectangle(fillBrush, null, new Rect(barX, barTop + barH - fillH, barW, fillH), 4, 4);
                }

                // Ticks on the right
                int ticks = 5;
                for (int i = 0; i <= ticks; i++)
                {
                    double tRatio = (double)i / ticks;
                    double ty = barTop + barH - tRatio * barH;
                    double tVal = Minimum + tRatio * range;

                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(barX + barW + 4, ty), new Point(barX + barW + 10, ty));

                    var tFt = CreateFormattedText($"{tVal:0}", ZeroWpfTheme.RegularTypeface, 9.0, ZeroWpfTheme.TextMuted, dpi);
                    dc.DrawText(tFt, new Point(barX + barW + 14, ty - tFt.Height / 2.0));
                }

                // Digital readout at bottom (centered to control width)
                RenderDigitalReadout(dc, 0, h - 26, clampedVal, Unit, dpi, isCenter: true, originCenterX: w / 2.0);
            }
        }

        private void RenderDigitalReadout(DrawingContext dc, double startX, double startY, double needleVal, string unit, double dpi, bool isCenter, double originCenterX)
        {
            string numStr = FormatValue(needleVal);
            string fullStr = string.IsNullOrEmpty(unit) ? numStr : $"{numStr} {unit}".Trim();
            if (string.IsNullOrEmpty(fullStr)) return;

            if (!UseTabularReadout)
            {
                var valFt = CreateFormattedText(fullStr, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                double drawX = isCenter ? (originCenterX - valFt.Width / 2.0) : startX;
                dc.DrawText(valFt, new Point(drawX, startY));
                return;
            }

            // Tabular Digit Spacing Engine
            var refDigitFt = CreateFormattedText("0", ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
            double digitSlotWidth = refDigitFt.Width;

            double[] slotWidths = new double[fullStr.Length];
            FormattedText[] glyphs = new FormattedText[fullStr.Length];
            double totalWidth = 0.0;

            for (int i = 0; i < fullStr.Length; i++)
            {
                char c = fullStr[i];
                var cFt = CreateFormattedText(c.ToString(), ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
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

            double currentX = isCenter ? (originCenterX - totalWidth / 2.0) : startX;
            for (int i = 0; i < fullStr.Length; i++)
            {
                double slotW = slotWidths[i];
                var cFt = glyphs[i];
                double charDrawX = currentX + (slotW - cFt.Width) / 2.0;
                dc.DrawText(cFt, new Point(charDrawX, startY));
                currentX += slotW;
            }
        }
    }
}
