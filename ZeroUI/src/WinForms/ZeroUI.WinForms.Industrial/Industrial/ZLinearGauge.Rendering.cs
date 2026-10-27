using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public partial class ZLinearGauge
    {
        private string FormatValue(float val)
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            float scale = DpiScale;
            int w = Width;
            int h = Height;
            var theme = ZeroTheme.Colors;

            // 1. Header: Title (left) and Telemetry Value (right)
            var titleFont = ZeroFontCache.Get("Segoe UI", 9f * scale, FontStyle.Bold);
            using (var titleBrush = new SolidBrush(theme.TextPrimary))
            {
                g.DrawString(_title, titleFont, titleBrush, 4 * scale, 2 * scale);
            }

            Color statusColor = (_value >= _criticalThreshold)
                ? Color.FromArgb(239, 68, 68)   // Red
                : (_value >= _warningThreshold)
                    ? Color.FromArgb(245, 158, 11) // Amber
                    : Color.FromArgb(16, 185, 129); // Green

            // Value Text rendering with Tabular Digit Spacing Engine
            RenderDigitalReadout(g, w, 2 * scale, statusColor);

            // 2. Main Gauge Bar Dimensions
            int barY = (int)(26 * scale);
            int barH = Math.Max(8, (int)(14 * scale));
            int barX = (int)(4 * scale);
            int barW = Math.Max(10, w - (int)(8 * scale));

            // Track Background
            var trackRect = new Rectangle(barX, barY, barW, barH);
            using (var trackPath = CreateRoundedRectangle(trackRect, (int)(4 * scale)))
            using (var trackBrush = new SolidBrush(theme.Border))
            {
                g.FillPath(trackBrush, trackPath);
            }

            // Fill Level
            float range = _maximum - _minimum;
            float ratio = range > 0 ? (_value - _minimum) / range : 0f;
            int fillW = Math.Max((int)(4 * scale), (int)(barW * ratio));

            var fillRect = new Rectangle(barX, barY, fillW, barH);
            using (var fillPath = CreateRoundedRectangle(fillRect, (int)(4 * scale)))
            using (var fillBrush = new LinearGradientBrush(new Point(barX, barY), new Point(barX + barW, barY), Color.FromArgb(16, 185, 129), Color.FromArgb(239, 68, 68)))
            {
                g.FillPath(fillBrush, fillPath);
            }

            // Glass Sheen Highlight on Top Half
            using (var sheenBrush = new SolidBrush(Color.FromArgb(50, Color.White)))
            {
                g.FillRectangle(sheenBrush, barX, barY, fillW, barH / 2);
            }

            // 3. Graduations & Scale Ticks
            int tickY = barY + barH + (int)(4 * scale);
            using var tickPen = new Pen(theme.Border, 1f);
            var tickFont = ZeroFontCache.Get("Segoe UI", 7.5f * scale, FontStyle.Regular);
            using var tickBrush = new SolidBrush(theme.TextSecondary);

            int tickSteps = 4;
            for (int i = 0; i <= tickSteps; i++)
            {
                float stepRatio = (float)i / tickSteps;
                int tx = barX + (int)(barW * stepRatio);
                g.DrawLine(tickPen, tx, tickY, tx, tickY + (int)(4 * scale));

                float stepVal = _minimum + (range * stepRatio);
                string stepStr = $"{stepVal:F0}";
                var textSize = g.MeasureString(stepStr, tickFont);
                float textX = Math.Max(0, tx - (textSize.Width / 2));
                if (i == tickSteps) textX = Math.Min(w - textSize.Width, textX);
                g.DrawString(stepStr, tickFont, tickBrush, textX, tickY + (int)(6 * scale));
            }
        }

        private void RenderDigitalReadout(Graphics g, float totalWidth, float topY, Color statusColor)
        {
            string numStr = FormatValue(_value);
            string fullStr = string.IsNullOrEmpty(_unit) ? numStr : $"{numStr} {_unit}".Trim();

            var valFont = ZeroFontCache.Get("Segoe UI", 9.5f * DpiScale, FontStyle.Bold);
            using var valBrush = new SolidBrush(statusColor);

            if (!_useTabularReadout)
            {
                var valSize = g.MeasureString(fullStr, valFont);
                g.DrawString(fullStr, valFont, valBrush, totalWidth - valSize.Width - 4 * DpiScale, topY);
                return;
            }

            // Tabular Digit Spacing Engine in GDI+
            var sf = StringFormat.GenericTypographic;
            SizeF refSz = g.MeasureString("0", valFont, PointF.Empty, sf);
            float digitSlotWidth = refSz.Width;

            float[] slotWidths = new float[fullStr.Length];
            SizeF[] glyphSizes = new SizeF[fullStr.Length];
            float totalStrWidth = 0f;

            for (int i = 0; i < fullStr.Length; i++)
            {
                char c = fullStr[i];
                string s = c.ToString();
                SizeF sz = g.MeasureString(s, valFont, PointF.Empty, sf);
                glyphSizes[i] = sz;

                if (char.IsDigit(c))
                {
                    slotWidths[i] = Math.Max(digitSlotWidth, sz.Width);
                }
                else
                {
                    slotWidths[i] = sz.Width;
                }
                totalStrWidth += slotWidths[i];
            }

            float rightEdge = totalWidth - 4 * DpiScale;
            float currentX = rightEdge - totalStrWidth;

            for (int i = 0; i < fullStr.Length; i++)
            {
                char c = fullStr[i];
                float slotW = slotWidths[i];
                SizeF sz = glyphSizes[i];
                float drawX = currentX + (slotW - sz.Width) / 2f;
                g.DrawString(c.ToString(), valFont, valBrush, drawX, topY, sf);
                currentX += slotW;
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);
    }
}
