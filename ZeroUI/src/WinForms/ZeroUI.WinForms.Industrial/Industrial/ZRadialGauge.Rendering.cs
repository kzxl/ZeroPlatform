using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public partial class ZRadialGauge
    {
        /// <summary>
        /// Formats the gauge numeric value using <see cref="ValueFormat"/>, safely falling back to "0.#" on empty or format error.
        /// </summary>
        private string FormatValue(double val)
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

            float w = Width;
            float h = Height;
            float cx = w / 2f;
            float cy = h / 2f;
            float radius = Math.Min(cx, cy) - 10f;
            if (radius < 20f) return;

            var colors = ZeroTheme.Colors;

            // 1. Dial Background Bezel
            using (var bezelBrush = new SolidBrush(ZeroTheme.IsDark ? Color.FromArgb(24, 26, 32) : Color.FromArgb(248, 249, 251)))
            {
                g.FillEllipse(bezelBrush, cx - radius, cy - radius, radius * 2f, radius * 2f);
            }

            using (var bezelPen = new Pen(colors.Border, 1.5f))
            {
                g.DrawEllipse(bezelPen, cx - radius, cy - radius, radius * 2f, radius * 2f);
            }

            float arcRadius = radius - 14f;
            var arcRect = new RectangleF(cx - arcRadius, cy - arcRadius, arcRadius * 2f, arcRadius * 2f);

            // 2. Base Track Arc
            using (var trackPen = new Pen(colors.Border, 6f))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawArc(trackPen, arcRect, _startAngle, _sweepAngle);
            }

            // 3. Colored Threshold Bands
            if (_thresholds.Count > 0)
            {
                for (int i = 0; i < _thresholds.Count; i++)
                {
                    var t = _thresholds[i];
                    float tStart = GaugeMath.ValueToAngle(t.From, _minimum, _maximum, _startAngle, _sweepAngle);
                    float tEnd = GaugeMath.ValueToAngle(t.To, _minimum, _maximum, _startAngle, _sweepAngle);
                    float tSweep = tEnd - tStart;

                    if (tSweep > 0.5f)
                    {
                        Color bandColor = Color.FromArgb((int)t.ArgbColor);
                        using (var bandPen = new Pen(bandColor, 6f))
                        {
                            g.DrawArc(bandPen, arcRect, tStart, tSweep);
                        }
                    }
                }
            }

            // 4. Tick Marks and Scale Numbers
            float tickOuterR = arcRadius - 8f;
            float tickMajorInnerR = tickOuterR - 8f;
            float tickMinorInnerR = tickOuterR - 4f;

            int totalSteps = _majorTicks * Math.Max(1, _minorTicks);
            using (var majorPen = new Pen(colors.TextSecondary, 1.5f))
            using (var minorPen = new Pen(Color.FromArgb(120, colors.TextSecondary), 1f))
            using (var labelBrush = new SolidBrush(colors.TextSecondary))
            {
                var labelFont = ZeroFontCache.Get("Segoe UI", 7.5f * DpiScale, FontStyle.Regular);
                for (int step = 0; step <= totalSteps; step++)
                {
                    float ratio = (float)step / totalSteps;
                    float angleDeg = _startAngle + ratio * _sweepAngle;
                    double angleRad = angleDeg * Math.PI / 180.0;

                    float cos = (float)Math.Cos(angleRad);
                    float sin = (float)Math.Sin(angleRad);

                    bool isMajor = (step % Math.Max(1, _minorTicks) == 0);
                    float innerR = isMajor ? tickMajorInnerR : tickMinorInnerR;
                    Pen penToUse = isMajor ? majorPen : minorPen;

                    float x1 = cx + innerR * cos;
                    float y1 = cy + innerR * sin;
                    float x2 = cx + tickOuterR * cos;
                    float y2 = cy + tickOuterR * sin;

                    g.DrawLine(penToUse, x1, y1, x2, y2);

                    // Draw Scale Numbers on Major Ticks
                    if (isMajor && radius >= 50f)
                    {
                        double tickVal = _minimum + ratio * (_maximum - _minimum);
                        string numStr = tickVal >= 1000 ? $"{tickVal / 1000:0.#}k" : $"{tickVal:0}";
                        float numR = innerR - 10f;
                        float nx = cx + numR * cos;
                        float ny = cy + numR * sin;

                        SizeF sz = g.MeasureString(numStr, labelFont);
                        g.DrawString(numStr, labelFont, labelBrush, nx - sz.Width / 2f, ny - sz.Height / 2f);
                    }
                }
            }

            // 5. Gauge Title and Readout
            using (var titleBrush = new SolidBrush(colors.TextSecondary))
            {
                var titleFont = ZeroFontCache.Get("Segoe UI", 8f * DpiScale, FontStyle.Regular);
                var valFont = ZeroFontCache.Get("Segoe UI", 11f * DpiScale, FontStyle.Bold);

                // Title
                if (!string.IsNullOrEmpty(_title))
                {
                    SizeF tSz = g.MeasureString(_title, titleFont);
                    g.DrawString(_title, titleFont, titleBrush, cx - tSz.Width / 2f, cy + radius * 0.35f);
                }

                // Digital Value Readout
                RenderDigitalReadout(g, cx, cy, radius, valFont, colors);
            }

            // 6. Inertial Needle Pointer
            float needleAngleDeg = GaugeMath.ValueToAngle(_value, _minimum, _maximum, _startAngle, _sweepAngle);
            double needleRad = needleAngleDeg * Math.PI / 180.0;
            float nCos = (float)Math.Cos(needleRad);
            float nSin = (float)Math.Sin(needleRad);
            float needleR = arcRadius - 4f;

            float tipX = cx + needleR * nCos;
            float tipY = cy + needleR * nSin;

            // Needle Polygon (Tapered triangle from base to tip)
            float baseHalfW = 3.5f;
            float perpCos = -nSin;
            float perpSin = nCos;

            PointF[] needlePoly = new PointF[]
            {
                new PointF(cx + perpCos * baseHalfW, cy + perpSin * baseHalfW),
                new PointF(tipX, tipY),
                new PointF(cx - perpCos * baseHalfW, cy - perpSin * baseHalfW),
                new PointF(cx - nCos * 8f, cy - nSin * 8f) // Counter-balance tail
            };

            Color needleColor = Color.FromArgb(239, 68, 68); // Signal Red needle
            using (var needleBrush = new SolidBrush(needleColor))
            {
                g.FillPolygon(needleBrush, needlePoly);
            }

            // 7. Metallic Center Pivot Cap
            float capR = 7f;
            using (var capBrush = new LinearGradientBrush(
                new RectangleF(cx - capR, cy - capR, capR * 2f, capR * 2f),
                Color.FromArgb(240, 240, 245),
                Color.FromArgb(120, 120, 130),
                45f))
            {
                g.FillEllipse(capBrush, cx - capR, cy - capR, capR * 2f, capR * 2f);
            }
            using (var capPen = new Pen(Color.FromArgb(90, 90, 100), 1f))
            {
                g.DrawEllipse(capPen, cx - capR, cy - capR, capR * 2f, capR * 2f);
            }
        }

        private void RenderDigitalReadout(Graphics g, float cx, float cy, float radius, Font valFont, ZeroThemePalette colors)
        {
            string numStr = FormatValue(_value);
            string fullStr = string.IsNullOrEmpty(_unit) ? numStr : $"{numStr} {_unit}".Trim();

            GaugeSeverity sev = GaugeMath.EvaluateSeverity(_value, _thresholds);
            Color readoutColor = sev == GaugeSeverity.Critical
                ? Color.FromArgb(239, 68, 68)
                : (sev == GaugeSeverity.Warning ? Color.FromArgb(245, 158, 11) : colors.TextPrimary);

            float readoutY = cy + radius * 0.52f;

            using (var valBrush = new SolidBrush(readoutColor))
            {
                if (!_useTabularReadout)
                {
                    SizeF vSz = g.MeasureString(fullStr, valFont);
                    g.DrawString(fullStr, valFont, valBrush, cx - vSz.Width / 2f, readoutY);
                    return;
                }

                // Tabular Digit Spacing Engine in GDI+
                var sf = StringFormat.GenericTypographic;
                SizeF refSz = g.MeasureString("0", valFont, PointF.Empty, sf);
                float digitSlotWidth = refSz.Width;

                float[] slotWidths = new float[fullStr.Length];
                SizeF[] glyphSizes = new SizeF[fullStr.Length];
                float totalWidth = 0f;

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
                    totalWidth += slotWidths[i];
                }

                float currentX = cx - totalWidth / 2f;
                for (int i = 0; i < fullStr.Length; i++)
                {
                    char c = fullStr[i];
                    float slotW = slotWidths[i];
                    SizeF sz = glyphSizes[i];
                    float drawX = currentX + (slotW - sz.Width) / 2f;
                    g.DrawString(c.ToString(), valFont, valBrush, drawX, readoutY, sf);
                    currentX += slotW;
                }
            }
        }
    }
}
