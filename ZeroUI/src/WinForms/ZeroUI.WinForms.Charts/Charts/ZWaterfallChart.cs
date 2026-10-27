using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    public enum WaterfallItemType
    {
        Start,
        Increment,
        Decrement,
        Total
    }

    /// <summary>
    /// Represents a single step or balance column in a waterfall variance chart.
    /// </summary>
    public class WaterfallItem
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public WaterfallItemType Type { get; set; }
        public Color? CustomColor { get; set; }

        public WaterfallItem(string label, double value, WaterfallItemType type = WaterfallItemType.Increment, Color? customColor = null)
        {
            Label = label;
            Value = value;
            Type = type;
            CustomColor = customColor;
        }
    }

    /// <summary>
    /// High-performance Cumulative Variance Bridge Waterfall Chart control for financial and inventory reconciliation.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroWaterfallChart.bmp")]
    public class ZWaterfallChart : Control
    {
        private readonly List<WaterfallItem> _items = new List<WaterfallItem>();

        private Color _increaseColor = Color.FromArgb(16, 185, 129); // Emerald
        private Color _decreaseColor = Color.FromArgb(239, 68, 68);   // Crimson
        private Color _totalColor = Color.FromArgb(79, 70, 229);      // Indigo
        private string _valuePrefix = "$";
        private string _valueSuffix = "";
        private bool _showConnectors = true;

        private int _hoverIndex = -1;

        public ZWaterfallChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(550, 320);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        [Category("Appearance")]
        public Color IncreaseColor
        {
            get => _increaseColor;
            set { _increaseColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color DecreaseColor
        {
            get => _decreaseColor;
            set { _decreaseColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color TotalColor
        {
            get => _totalColor;
            set { _totalColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("$")]
        public string ValuePrefix
        {
            get => _valuePrefix;
            set { _valuePrefix = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowConnectors
        {
            get => _showConnectors;
            set { _showConnectors = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<WaterfallItem> Items => _items;

        public void AddItem(string label, double value, WaterfallItemType type = WaterfallItemType.Increment, Color? customColor = null)
        {
            _items.Add(new WaterfallItem(label, value, type, customColor));
            Invalidate();
        }

        public void ClearItems()
        {
            _items.Clear();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_items.Count == 0) return;

            int padLeft = 40;
            int padRight = 20;
            int plotW = Width - padLeft - padRight;
            if (plotW <= 0) return;

            float slotW = (float)plotW / _items.Count;
            int newHover = (int)((e.X - padLeft) / slotW);
            newHover = Math.Max(0, Math.Min(_items.Count - 1, newHover));

            if (_hoverIndex != newHover)
            {
                _hoverIndex = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            if (_items.Count == 0)
            {
                using var noteBrush = new SolidBrush(palette.TextSecondary);
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("No waterfall steps defined.", Font, noteBrush, ClientRectangle, sf);
                return;
            }

            int padLeft = 55;
            int padRight = 20;
            int padTop = 30;
            int padBottom = 40;
            int plotW = Width - padLeft - padRight;
            int plotH = Height - padTop - padBottom;
            if (plotW <= 40 || plotH <= 40) return;

            // Compute running cumulative values
            var bottomVals = new double[_items.Count];
            var topVals = new double[_items.Count];

            double current = 0;
            double minY = 0;
            double maxY = 0;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item.Type == WaterfallItemType.Start)
                {
                    bottomVals[i] = 0;
                    topVals[i] = item.Value;
                    current = item.Value;
                }
                else if (item.Type == WaterfallItemType.Total)
                {
                    bottomVals[i] = 0;
                    topVals[i] = current;
                }
                else if (item.Type == WaterfallItemType.Increment || item.Value >= 0)
                {
                    bottomVals[i] = current;
                    topVals[i] = current + Math.Abs(item.Value);
                    current += Math.Abs(item.Value);
                }
                else
                {
                    // Decrement
                    bottomVals[i] = current - Math.Abs(item.Value);
                    topVals[i] = current;
                    current -= Math.Abs(item.Value);
                }

                if (bottomVals[i] < minY) minY = bottomVals[i];
                if (topVals[i] > maxY) maxY = topVals[i];
            }

            double range = Math.Max(1.0, maxY - minY);
            minY -= range * 0.05;
            maxY += range * 0.08;
            range = maxY - minY;

            // 1. Grid Lines
            Color gridColor = ZeroTheme.IsDark ? Color.FromArgb(40, 50, 70) : Color.FromArgb(230, 235, 245);
            using (var gridPen = new Pen(gridColor, 1f) { DashStyle = DashStyle.Dash })
            using (var labelBrush = new SolidBrush(palette.TextSecondary))
            {
                var labelFont = ZeroFontCache.Get(8f, FontStyle.Regular);
                int gridLines = 4;
                for (int i = 0; i <= gridLines; i++)
                {
                    float y = padTop + plotH * i / (float)gridLines;
                    g.DrawLine(gridPen, padLeft, y, padLeft + plotW, y);

                    double val = maxY - (range * i / gridLines);
                    g.DrawString($"{_valuePrefix}{val:F0}{_valueSuffix}", labelFont, labelBrush, 6, y - 6);
                }
            }

            // 2. Bars & Connectors
            float slotW = (float)plotW / _items.Count;
            float barW = Math.Max(12f, Math.Min(48f, slotW * 0.65f));

            var valFont = ZeroFontCache.Get(7.5f, FontStyle.Bold);
            var xFont = ZeroFontCache.Get(8f, FontStyle.Regular);

            using var connectorPen = new Pen(Color.FromArgb(140, palette.TextSecondary), 1f) { DashStyle = DashStyle.Dot };
            using var barBrush = new SolidBrush(Color.Empty);
            using var borderPen = new Pen(palette.Surface, 1f);
            using var valBrush = new SolidBrush(palette.TextPrimary);
            using var xBrush = new SolidBrush(palette.TextSecondary);

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                float cx = padLeft + (i + 0.5f) * slotW;

                float yTop = (float)(padTop + (maxY - topVals[i]) / range * plotH);
                float yBottom = (float)(padTop + (maxY - bottomVals[i]) / range * plotH);
                float barH = Math.Max(2f, Math.Abs(yBottom - yTop));

                Color barColor = item.CustomColor ?? (item.Type switch
                {
                    WaterfallItemType.Start => _totalColor,
                    WaterfallItemType.Total => _totalColor,
                    WaterfallItemType.Increment => _increaseColor,
                    WaterfallItemType.Decrement => _decreaseColor,
                    _ => _increaseColor
                });

                if (i == _hoverIndex)
                {
                    barColor = Color.FromArgb(
                        Math.Min(255, barColor.R + 25),
                        Math.Min(255, barColor.G + 25),
                        Math.Min(255, barColor.B + 25));
                }

                var rect = new RectangleF(cx - barW / 2f, yTop, barW, barH);
                barBrush.Color = barColor;
                g.FillRectangle(barBrush, rect);
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);

                // Connector line to next bar
                if (_showConnectors && i < _items.Count - 1)
                {
                    float nextCx = padLeft + (i + 1.5f) * slotW;
                    float connectY = (item.Type == WaterfallItemType.Decrement) ? yBottom : yTop;
                    g.DrawLine(connectorPen, cx + barW / 2f, connectY, nextCx - barW / 2f, connectY);
                }

                // Value label on bar
                string valStr = (item.Type == WaterfallItemType.Decrement ? "-" : (item.Type == WaterfallItemType.Increment ? "+" : "")) +
                                $"{_valuePrefix}{Math.Abs(item.Value):N0}{_valueSuffix}";

                var sz = g.MeasureString(valStr, valFont);
                float vy = yTop - sz.Height - 3f;
                if (vy < padTop) vy = yTop + 3f;
                g.DrawString(valStr, valFont, valBrush, cx - sz.Width / 2f, vy);

                // X-Axis Label
                var xSz = g.MeasureString(item.Label, xFont);
                g.DrawString(item.Label, xFont, xBrush, cx - xSz.Width / 2f, Height - padBottom + 6);
            }
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for <see cref="ZWaterfallChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroWaterfallChart is deprecated and will be removed in 5 release cycles. Please migrate to ZWaterfallChart instead.")]
    [ToolboxItem(false)]
    public class ZeroWaterfallChart : ZWaterfallChart
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZWaterfallChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("WaterfallChart is deprecated and will be removed in 5 release cycles. Please migrate to ZWaterfallChart instead.")]
    [ToolboxItem(false)]
    public class WaterfallChart : ZWaterfallChart
    {
    }

    #endregion
}
