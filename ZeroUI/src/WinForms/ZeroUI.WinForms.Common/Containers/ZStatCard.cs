using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Theme;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Containers
{
    /// <summary>
    /// Modern KPI Metric Card component for ZeroUI executive dashboards and analytical summaries.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Editors")]
    [DefaultProperty("Value")]
    [Description("KPI Metric Card component for dashboards and analytical summaries")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroStatistic.bmp")]
    public class ZStatCard : ControlBase
    {
        private string _title = "Metric Title";
        private string _value = "0";
        private string? _prefix;
        private string? _suffix;
        private TrendDirection _trend = TrendDirection.None;
        private string? _trendText;

        private Color _valueColor = Color.Empty;
        private int _borderRadius = 8;

        public ZStatCard()
        {
            Size = new Size(200, 95);
            BackColor = CurrentPalette.CardBackground;
        }

        protected override void OnThemeChanged(ZeroSkin skin)
        {
            base.OnThemeChanged(skin);
            BackColor = CurrentPalette.CardBackground;
            Invalidate();
        }

        [Category("Data")]
        [DefaultValue("Metric Title")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }


        [Category("Data")]
        [DefaultValue("0")]
        public string Value
        {
            get => _value;
            set { _value = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(null)]
        public string? Prefix
        {
            get => _prefix;
            set { _prefix = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(null)]
        public string? Suffix
        {
            get => _suffix;
            set { _suffix = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue(TrendDirection.None)]
        public TrendDirection Trend
        {
            get => _trend;
            set { _trend = value; Invalidate(); }
        }

        [Obsolete("ZeroTrend is deprecated. Use Trend instead.")]
        [Browsable(false)]
        public ZeroTrendDirection ZeroTrend
        {
            get => (ZeroTrendDirection)_trend;
            set => Trend = (TrendDirection)value;
        }

        [Category("Data")]
        [DefaultValue(null)]
        public string? TrendText
        {
            get => _trendText;
            set { _trendText = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color ValueColor
        {
            get => _valueColor;
            set { _valueColor = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);

            var palette = CurrentPalette;

            // 1. Card Container
            using (var path = CreateRoundedRectangle(rect, _borderRadius))
            {
                using var bgBrush = new SolidBrush(BackColor);
                g.FillPath(bgBrush, path);

                using var borderPen = new Pen(palette.Border, 1f);
                g.DrawPath(borderPen, path);
            }

            // 2. Title Text (Small Gray)
            Rectangle titleRect = new Rectangle(16, 12, Width - 32, 18);
            TextRenderer.DrawText(
                g,
                _title,
                ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Regular),
                titleRect,
                palette.TextSecondary,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // 3. Value with Prefix & Suffix (Large Bold)
            string fullVal = $"{_prefix}{_value} {_suffix}".Trim();
            Rectangle valRect = new Rectangle(16, 32, Width - 32, 34);
            Color effValCol = (_valueColor != Color.Empty && _valueColor != Color.FromArgb(17, 24, 39)) ? _valueColor : palette.TextPrimary;
            TextRenderer.DrawText(
                g,
                fullVal,
                ZeroFontCache.Get("Segoe UI", 18f, FontStyle.Bold),
                valRect,
                effValCol,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // 4. Trend Indicator (Optional footer note)
            if (_trend != TrendDirection.None && !string.IsNullOrEmpty(_trendText))
            {
                var (trendChar, trendColor) = _trend == TrendDirection.Up
                    ? ("▲", palette.Success)
                    : ("▼", palette.Danger);

                string trendFull = $"{trendChar} {_trendText}";
                Rectangle trendRect = new Rectangle(16, 68, Width - 32, 18);
                TextRenderer.DrawText(
                    g,
                    trendFull,
                    ZeroFontCache.Get("Segoe UI", 8f, FontStyle.Bold),
                    trendRect,
                    trendColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius) =>
            ZeroUIConfig.CreateRoundedRectangle(rect, radius);
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZStatCard"/>.
    /// </summary>
    [Obsolete("ZeroStatistic is deprecated. Use StatisticCard instead.")]
    [ToolboxItem(false)]
    public class ZeroStatistic : StatisticCard
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZStatCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StatisticCard is deprecated and will be removed in 5 release cycles. Please migrate to ZStatCard instead.")]
    [ToolboxItem(false)]
    public class StatisticCard : ZStatCard
    {
    }

    #endregion
}
