using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public partial class ZPidFaceplate
    {
        private string FormatTelemetry(double val)
        {
            string fmt = _valueFormat;
            if (string.IsNullOrWhiteSpace(fmt)) return val.ToString("0.0", CultureInfo.InvariantCulture);

            try
            {
                if (fmt.Contains("{0"))
                {
                    return string.Format(CultureInfo.InvariantCulture, fmt, val);
                }
                return val.ToString(fmt, CultureInfo.InvariantCulture);
            }
            catch
            {
                return val.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = DpiScale;
            var palette = CurrentPalette;
            bool isDark = ZeroTheme.IsDark;

            // 1. Faceplate Outer Bezel
            var borderRect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var brushBg = new SolidBrush(palette.CardBackground))
            {
                g.FillRectangle(brushBg, borderRect);
            }
            using (var penBorder = new Pen(palette.Border, 1.5f * scale))
            {
                g.DrawRectangle(penBorder, borderRect);
            }

            // 2. Loop Header Strip
            int headerH = (int)(34 * scale);
            var headerRect = new Rectangle(0, 0, Width, headerH);
            using (var brushHeader = new SolidBrush(palette.HeaderBackground))
            {
                g.FillRectangle(brushHeader, headerRect);
            }
            using (var penHeader = new Pen(palette.Border, 1f * scale))
            {
                g.DrawLine(penHeader, 0, headerH, Width, headerH);
            }

            using var fontTag = new Font("Segoe UI", 9.5f * scale, FontStyle.Bold);
            using var fontDesc = new Font("Segoe UI", 7f * scale, FontStyle.Regular);
            using var brushText = new SolidBrush(palette.TextPrimary);
            using var brushSecondary = new SolidBrush(palette.TextSecondary);

            g.DrawString(_loopTag, fontTag, brushText, 10 * scale, 3 * scale);
            g.DrawString(_loopDescription, fontDesc, brushSecondary, 10 * scale, 18 * scale);

            // 3. Mode Buttons (AUTO, MAN, CAS)
            int modeBtnY = headerH + (int)(6 * scale);
            int modeBtnW = (int)(52 * scale);
            int modeBtnH = (int)(22 * scale);

            _btnAutoRect = new Rectangle((int)(10 * scale), modeBtnY, modeBtnW, modeBtnH);
            _btnManRect = new Rectangle((int)(66 * scale), modeBtnY, modeBtnW, modeBtnH);
            _btnCasRect = new Rectangle((int)(122 * scale), modeBtnY, modeBtnW, modeBtnH);

            DrawModeButton(g, _btnAutoRect, "AUTO", _mode == ZeroPidMode.Auto, palette, scale);
            DrawModeButton(g, _btnManRect, "MAN", _mode == ZeroPidMode.Manual, palette, scale);
            DrawModeButton(g, _btnCasRect, "CAS", _mode == ZeroPidMode.Cascade, palette, scale);

            // SP Adjustment Buttons (+ / -)
            _btnSpMinusRect = new Rectangle(Width - (int)(62 * scale), modeBtnY, (int)(24 * scale), modeBtnH);
            _btnSpPlusRect = new Rectangle(Width - (int)(34 * scale), modeBtnY, (int)(24 * scale), modeBtnH);
            DrawButton(g, _btnSpMinusRect, "-", palette.Surface, palette.TextPrimary, scale);
            DrawButton(g, _btnSpPlusRect, "+", palette.Surface, palette.TextPrimary, scale);

            // 4. Vertical Process Variable (PV) & Setpoint (SP) Dual Bargraph
            int barY = modeBtnY + modeBtnH + (int)(10 * scale);
            int barH = (int)(140 * scale);
            int barW = (int)(26 * scale);

            int pvX = (int)(40 * scale);
            int spX = (int)(100 * scale);

            // Scale axis ticks
            DrawScaleAxis(g, (int)(12 * scale), barY, barH, _minScale, _maxScale, palette, scale);

            // PV Bar
            DrawVerticalBar(g, pvX, barY, barW, barH, _processVariable, _minScale, _maxScale, palette.Primary, palette);
            // SP Bar
            DrawVerticalBar(g, spX, barY, barW, barH, _setPoint, _minScale, _maxScale, palette.Success, palette);

            // Readouts below bars
            using var fontVal = new Font("Segoe UI", 9f * scale, FontStyle.Bold);
            using var fontLbl = new Font("Segoe UI", 7f * scale, FontStyle.Bold);

            var sf = new StringFormat { Alignment = StringAlignment.Center };
            g.DrawString("PV", fontLbl, new SolidBrush(palette.Primary), pvX + barW / 2f, barY + barH + (4 * scale), sf);
            g.DrawString(FormatTelemetry(_processVariable), fontVal, brushText, pvX + barW / 2f, barY + barH + (16 * scale), sf);

            g.DrawString("SP", fontLbl, new SolidBrush(palette.Success), spX + barW / 2f, barY + barH + (4 * scale), sf);
            g.DrawString(FormatTelemetry(_setPoint), fontVal, brushText, spX + barW / 2f, barY + barH + (16 * scale), sf);

            // Right Info Box (Tuning & Dev)
            int infoX = (int)(150 * scale);
            int infoY = barY + (int)(4 * scale);
            using var fontParam = new Font("Segoe UI", 7.5f * scale, FontStyle.Regular);
            double dev = _processVariable - _setPoint;
            Color devCol = Math.Abs(dev) > 3.0 ? palette.Danger : palette.Success;
            g.DrawString($"Dev: {dev.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture)} {_engineeringUnit}", fontParam, new SolidBrush(devCol), infoX, infoY);
            g.DrawString($"Kp: {_kp.ToString("0.00", CultureInfo.InvariantCulture)}", fontParam, brushSecondary, infoX, infoY + (18 * scale));
            g.DrawString($"Ti: {_ti.ToString("0.0", CultureInfo.InvariantCulture)} s", fontParam, brushSecondary, infoX, infoY + (34 * scale));
            g.DrawString($"Td: {_td.ToString("0.0", CultureInfo.InvariantCulture)} s", fontParam, brushSecondary, infoX, infoY + (50 * scale));

            // 5. Horizontal Manipulated Variable Output (MV 0-100%)
            int mvY = Height - (int)(44 * scale);
            int mvX = (int)(40 * scale);
            int mvW = Width - (int)(50 * scale);
            int mvH = (int)(16 * scale);

            using var fontMv = new Font("Segoe UI", 7.5f * scale, FontStyle.Bold);
            g.DrawString("MV", fontMv, brushSecondary, 12 * scale, mvY + (1 * scale));

            var mvTrack = new Rectangle(mvX, mvY, mvW, mvH);
            using (var brushTrack = new SolidBrush(palette.Surface))
            {
                g.FillRectangle(brushTrack, mvTrack);
            }
            using (var penTrack = new Pen(palette.Border, 1f * scale))
            {
                g.DrawRectangle(penTrack, mvTrack);
            }

            int fillW = (int)(mvW * (_manipulatedVariable / 100.0));
            if (fillW > 0)
            {
                using var brushFill = new SolidBrush(palette.Warning);
                g.FillRectangle(brushFill, mvX + 1, mvY + 1, fillW, mvH - 1);
            }

            // MV percentage text
            using (var brushMvText = new SolidBrush(palette.TextPrimary))
            {
                var sfMv = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString($"{_manipulatedVariable.ToString("0.0", CultureInfo.InvariantCulture)}%", fontDesc, brushMvText, mvTrack, sfMv);
            }
        }

        private static void DrawVerticalBar(Graphics g, int x, int y, int w, int h, double val, double min, double max, Color fillCol, ZeroThemePalette palette)
        {
            var trackRect = new Rectangle(x, y, w, h);
            using (var brushTrack = new SolidBrush(palette.Surface))
            {
                g.FillRectangle(brushTrack, trackRect);
            }
            using (var penTrack = new Pen(palette.Border, 1f))
            {
                g.DrawRectangle(penTrack, trackRect);
            }

            double clamped = Math.Max(min, Math.Min(max, val));
            double range = max - min;
            double ratio = range > 0.0001 ? (clamped - min) / range : 0.0;
            int barFillH = (int)(h * ratio);

            if (barFillH > 0)
            {
                int fillY = y + h - barFillH;
                using var brushFill = new SolidBrush(fillCol);
                g.FillRectangle(brushFill, x + 1, fillY, w - 1, barFillH);
            }
        }

        private static void DrawScaleAxis(Graphics g, int x, int y, int h, double min, double max, ZeroThemePalette palette, float scale)
        {
            using var fontTick = new Font("Segoe UI", 6.5f * scale, FontStyle.Regular);
            using var brushTick = new SolidBrush(palette.TextSecondary);
            using var penTick = new Pen(palette.Border, 1f * scale);

            var sf = new StringFormat { Alignment = StringAlignment.Far };

            // 100%, 50%, 0% ticks
            g.DrawString(max.ToString("0", CultureInfo.InvariantCulture), fontTick, brushTick, x + (20 * scale), y - (4 * scale), sf);
            g.DrawLine(penTick, x + (22 * scale), y, x + (26 * scale), y);

            g.DrawString(((max + min) / 2).ToString("0", CultureInfo.InvariantCulture), fontTick, brushTick, x + (20 * scale), y + h / 2f - (4 * scale), sf);
            g.DrawLine(penTick, x + (22 * scale), y + h / 2f, x + (26 * scale), y + h / 2f);

            g.DrawString(min.ToString("0", CultureInfo.InvariantCulture), fontTick, brushTick, x + (20 * scale), y + h - (8 * scale), sf);
            g.DrawLine(penTick, x + (22 * scale), y + h, x + (26 * scale), y + h);
        }

        private static void DrawModeButton(Graphics g, Rectangle r, string text, bool selected, ZeroThemePalette palette, float scale)
        {
            Color bg = selected ? palette.Primary : palette.Surface;
            Color fg = selected ? Color.White : palette.TextPrimary;

            using (var brush = new SolidBrush(bg))
            {
                g.FillRectangle(brush, r);
            }
            using (var pen = new Pen(selected ? palette.Primary : palette.Border, 1f * scale))
            {
                g.DrawRectangle(pen, r);
            }
            using var font = new Font("Segoe UI", 7.5f * scale, FontStyle.Bold);
            using var brushFg = new SolidBrush(fg);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brushFg, r, sf);
        }

        private static void DrawButton(Graphics g, Rectangle r, string text, Color bg, Color fg, float scale)
        {
            using (var brush = new SolidBrush(bg))
            {
                g.FillRectangle(brush, r);
            }
            using (var pen = new Pen(Color.FromArgb(120, Color.Gray), 1f * scale))
            {
                g.DrawRectangle(pen, r);
            }
            using var font = new Font("Segoe UI", 8.5f * scale, FontStyle.Bold);
            using var brushFg = new SolidBrush(fg);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(text, font, brushFg, r, sf);
        }
    }
}
