using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial 4-field production scoreboard (Plan, Actual, NG, Remaining)
    /// with an integrated target completion progress bar, Per-Monitor V2 High-DPI scaling, and SCADA tag telemetry synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial production counter scoreboard displaying Plan, Actual, NG, and Remaining")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroProductionCounter.bmp")]
    public class ZProductionCounter : ControlBase, IScadaBindable
    {
        private int _plan = 2500;
        private int _actual = 2140;
        private int _ng = 18;
        private string _title = "SHIFT PRODUCTION TARGET";
        private string _percentFormat = "0.#";
        private bool _isHovered;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Production Metrics")]
        [DefaultValue(2500)]
        public int Plan
        {
            get => _plan;
            set { _plan = Math.Max(1, value); Invalidate(); }
        }

        [Category("Production Metrics")]
        [DefaultValue(2140)]
        public int Actual
        {
            get => _actual;
            set { _actual = Math.Max(0, value); Invalidate(); }
        }

        [Category("Production Metrics")]
        [DefaultValue(18)]
        public int NG
        {
            get => _ng;
            set { _ng = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("SHIFT PRODUCTION TARGET")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Custom format string for the completion percentage readout. Defaults to '0.#'.")]
        [DefaultValue("0.#")]
        public string PercentFormat
        {
            get => _percentFormat;
            set { _percentFormat = value ?? "0.#"; Invalidate(); }
        }

        public int Remaining => Math.Max(0, _plan - _actual);
        public double CompletionPercent => Math.Min(100.0, (_actual / (double)_plan) * 100.0);

        public ZProductionCounter()
        {
            Size = new Size(320, 110);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                return;
            }
            if (int.TryParse(tag.Value?.ToString(), out var act))
            {
                Actual = act;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        private string FormatPercent(double pct)
        {
            string fmt = _percentFormat;
            if (string.IsNullOrWhiteSpace(fmt)) return pct.ToString("0.#", CultureInfo.InvariantCulture) + "%";

            try
            {
                if (fmt.Contains("{0"))
                {
                    return string.Format(CultureInfo.InvariantCulture, fmt, pct);
                }
                return pct.ToString(fmt, CultureInfo.InvariantCulture) + "%";
            }
            catch
            {
                return pct.ToString("0.#", CultureInfo.InvariantCulture) + "%";
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = DpiScale;
            bool isDark = ZeroTheme.IsDark;
            Color panelBg = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
            Color borderCol = _isHovered ? Color.FromArgb(59, 130, 246) : (isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240));
            Color textPrimary = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);
            Color textSecondary = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            // 1. Container
            var rect = new RectangleF(1f * scale, 1f * scale, Width - 3f * scale, Height - 3f * scale);
            using (var bgBrush = new SolidBrush(panelBg))
            using (var borderPen = new Pen(borderCol, _isHovered ? 2f * scale : 1.2f * scale))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            // 2. Title Header
            using (var titleFont = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            using (var titleBrush = new SolidBrush(textSecondary))
            {
                g.DrawString(_title, titleFont, titleBrush, 10f * scale, 6f * scale);

                string pctStr = FormatPercent(CompletionPercent);
                var pctSize = g.MeasureString(pctStr, titleFont);
                using (var pctBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                {
                    g.DrawString(pctStr, titleFont, pctBrush, Width - pctSize.Width - 10f * scale, 6f * scale);
                }
            }

            // 3. Four Metric Columns (PLAN, ACTUAL, NG, REMAINING)
            string[] headers = { "PLAN", "ACTUAL", "NG", "REMAIN" };
            string[] values = { $"{_plan:N0}", $"{_actual:N0}", $"{_ng:N0}", $"{Remaining:N0}" };
            Color[] colors = {
                textPrimary,
                Color.FromArgb(56, 189, 248), // Cyan
                Color.FromArgb(239, 68, 68),  // Red
                Color.FromArgb(245, 158, 11)  // Amber
            };

            float colW = (Width - 20f * scale) / 4f;
            float topY = 26f * scale;

            using (var hFont = new Font(Font.FontFamily, 7f, FontStyle.Bold))
            using (var vFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var hBrush = new SolidBrush(textSecondary))
            {
                for (int i = 0; i < 4; i++)
                {
                    float cx = 10f * scale + i * colW;
                    g.DrawString(headers[i], hFont, hBrush, cx, topY);

                    using (var vBrush = new SolidBrush(colors[i]))
                    {
                        g.DrawString(values[i], vFont, vBrush, cx, topY + 14f * scale);
                    }
                }
            }

            // 4. Target Progress Bar at Bottom
            float barY = Height - 14f * scale;
            float barH = 6f * scale;
            var barTrackRect = new RectangleF(10f * scale, barY, Width - 20f * scale, barH);
            using (var trackBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240)))
            {
                g.FillRectangle(trackBrush, barTrackRect);
            }

            float fillW = (float)(barTrackRect.Width * (CompletionPercent / 100.0));
            if (fillW > 0)
            {
                var barFillRect = new RectangleF(barTrackRect.X, barTrackRect.Y, fillW, barH);
                using (var fillBrush = new SolidBrush(Color.FromArgb(34, 197, 94)))
                {
                    g.FillRectangle(fillBrush, barFillRect);
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZProductionCounter"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroProductionCounter is deprecated and will be removed in 5 release cycles. Please migrate to ZProductionCounter instead.")]
    [ToolboxItem(false)]
    public class ZeroProductionCounter : ZProductionCounter
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZProductionCounter"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ProductionCounter is deprecated and will be removed in 5 release cycles. Please migrate to ZProductionCounter instead.")]
    [ToolboxItem(false)]
    public class ProductionCounter : ZProductionCounter
    {
    }

    #endregion
}
