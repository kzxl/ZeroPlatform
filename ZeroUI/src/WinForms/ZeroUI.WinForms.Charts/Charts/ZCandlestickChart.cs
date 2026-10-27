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
    /// <summary>
    /// Represents a single OHLC candlestick and volume data point.
    /// </summary>
    public class CandlestickItem
    {
        public DateTime Date { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        public bool IsBullish => Close >= Open;

        public CandlestickItem(DateTime date, double open, double high, double low, double close, double volume)
        {
            Date = date;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
        }
    }

    /// <summary>
    /// High-performance Financial Candlestick OHLC + Volume chart control for commodity prices and market analytics.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroCandlestickChart.bmp")]
    public class ZCandlestickChart : Control
    {
        private readonly List<CandlestickItem> _items = new List<CandlestickItem>();

        private Color _bullishColor = Color.FromArgb(16, 185, 129); // Emerald
        private Color _bearishColor = Color.FromArgb(239, 68, 68);   // Crimson
        private bool _showVolume = true;
        private bool _showMovingAverage = true;
        private int _maPeriod = 5;
        private string _valuePrefix = "$";
        private string _title = "Commodity Price Trend (OHLC)";

        private Point? _crosshair;
        private int _hoverIndex = -1;

        public ZCandlestickChart()
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
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BullishColor
        {
            get => _bullishColor;
            set { _bullishColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BearishColor
        {
            get => _bearishColor;
            set { _bearishColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowVolume
        {
            get => _showVolume;
            set { _showVolume = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowMovingAverage
        {
            get => _showMovingAverage;
            set { _showMovingAverage = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(5)]
        public int MaPeriod
        {
            get => _maPeriod;
            set { _maPeriod = Math.Max(2, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("$")]
        public string ValuePrefix
        {
            get => _valuePrefix;
            set { _valuePrefix = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<CandlestickItem> Items => _items;

        public void SetData(IEnumerable<CandlestickItem> items)
        {
            _items.Clear();
            if (items != null) _items.AddRange(items);
            Invalidate();
        }

        public void AddCandle(DateTime date, double open, double high, double low, double close, double volume)
        {
            _items.Add(new CandlestickItem(date, open, high, low, close, volume));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _crosshair = e.Location;

            if (_items.Count > 0)
            {
                int padLeft = 16;
                int padRight = 60;
                int plotW = Width - padLeft - padRight;
                if (plotW > 0)
                {
                    float candleSpacing = (float)plotW / _items.Count;
                    int idx = (int)((e.X - padLeft) / candleSpacing);
                    _hoverIndex = Math.Max(0, Math.Min(_items.Count - 1, idx));
                }
            }

            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _crosshair = null;
            _hoverIndex = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            // 0. Header (Title & Latest Price Summary)
            int headerH = 34;
            using (var titleBrush = new SolidBrush(palette.TextPrimary))
            {
                var titleFont = ZeroFontCache.Get(9.5f, FontStyle.Bold);
                g.DrawString(_title, titleFont, titleBrush, 16, 8);
            }

            if (_items.Count == 0)
            {
                using var noteBrush = new SolidBrush(palette.TextSecondary);
                g.DrawString("No candlestick data available.", Font, noteBrush, ClientRectangle, ZeroStringFormats.Center);
                return;
            }

            var latest = _items[_items.Count - 1];
            double diff = latest.Close - latest.Open;
            double pct = latest.Open > 0 ? (diff / latest.Open) * 100.0 : 0.0;
            Color summaryColor = diff >= 0 ? _bullishColor : _bearishColor;
            string summaryText = $"{_valuePrefix}{latest.Close:F2}  {(diff >= 0 ? "+" : "")}{diff:F2} ({(pct >= 0 ? "+" : "")}{pct:F1}%)";

            using (var sumBrush = new SolidBrush(summaryColor))
            {
                var sumFont = ZeroFontCache.Get(9f, FontStyle.Bold);
                var sz = g.MeasureString(summaryText, sumFont);
                g.DrawString(summaryText, sumFont, sumBrush, Width - sz.Width - 16, 9);
            }

            // Dimensions
            int padLeft = 16;
            int padRight = 60;
            int padBottom = 26;
            int totalPlotH = Height - headerH - padBottom;
            int plotW = Width - padLeft - padRight;
            if (totalPlotH <= 40 || plotW <= 40) return;

            int volH = _showVolume ? (int)(totalPlotH * 0.22) : 0;
            int priceH = totalPlotH - volH - (_showVolume ? 8 : 0);

            int priceTop = headerH;
            int priceBottom = priceTop + priceH;
            int volTop = priceBottom + (_showVolume ? 8 : 0);
            int volBottom = volTop + volH;

            // Calculate Ranges
            double minPrice = double.MaxValue;
            double maxPrice = double.MinValue;
            double maxVol = 0.001;

            foreach (var c in _items)
            {
                if (c.Low < minPrice) minPrice = c.Low;
                if (c.High > maxPrice) maxPrice = c.High;
                if (c.Volume > maxVol) maxVol = c.Volume;
            }

            double priceRange = Math.Max(0.01, maxPrice - minPrice);
            minPrice -= priceRange * 0.05;
            maxPrice += priceRange * 0.05;
            priceRange = maxPrice - minPrice;

            // 1. Draw Horizontal Price Grid Lines
            Color gridColor = ZeroTheme.IsDark ? Color.FromArgb(40, 50, 70) : Color.FromArgb(230, 235, 245);
            using (var gridPen = new Pen(gridColor, 1f) { DashStyle = DashStyle.Dash })
            using (var labelBrush = new SolidBrush(palette.TextSecondary))
            {
                var labelFont = ZeroFontCache.Get(8f, FontStyle.Regular);
                int gridLines = 4;
                for (int i = 0; i <= gridLines; i++)
                {
                    float y = priceTop + priceH * i / (float)gridLines;
                    g.DrawLine(gridPen, padLeft, y, padLeft + plotW, y);

                    double pVal = maxPrice - (priceRange * i / gridLines);
                    g.DrawString($"{_valuePrefix}{pVal:F1}", labelFont, labelBrush, padLeft + plotW + 5, y - 6);
                }
            }

            // 2. Render Candlesticks & Volume Bars
            float candleSlotW = (float)plotW / _items.Count;
            float candleBarW = Math.Max(2f, Math.Min(16f, candleSlotW * 0.7f));

            var maPoints = new List<PointF>(_showMovingAverage ? _items.Count : 0);

            using var bullWickPen = new Pen(_bullishColor, 1.25f);
            using var bearWickPen = new Pen(_bearishColor, 1.25f);
            using var bullBodyBrush = new SolidBrush(_bullishColor);
            using var bearBodyBrush = new SolidBrush(_bearishColor);
            using var bullBodyPen = new Pen(_bullishColor, 1f);
            using var bearBodyPen = new Pen(_bearishColor, 1f);
            using var bullVolBrush = new SolidBrush(Color.FromArgb(90, _bullishColor));
            using var bearVolBrush = new SolidBrush(Color.FromArgb(90, _bearishColor));
            using var dateBrush = new SolidBrush(palette.TextSecondary);
            var dateFont = ZeroFontCache.Get(7.5f, FontStyle.Regular);

            for (int i = 0; i < _items.Count; i++)
            {
                var c = _items[i];
                float cx = padLeft + (i + 0.5f) * candleSlotW;

                float highY = (float)(priceTop + (maxPrice - c.High) / priceRange * priceH);
                float lowY = (float)(priceTop + (maxPrice - c.Low) / priceRange * priceH);
                float openY = (float)(priceTop + (maxPrice - c.Open) / priceRange * priceH);
                float closeY = (float)(priceTop + (maxPrice - c.Close) / priceRange * priceH);

                bool isBull = c.IsBullish;
                Pen wickPen = isBull ? bullWickPen : bearWickPen;
                SolidBrush bodyBrush = isBull ? bullBodyBrush : bearBodyBrush;
                Pen bodyPen = isBull ? bullBodyPen : bearBodyPen;
                SolidBrush volBrush = isBull ? bullVolBrush : bearVolBrush;

                // Wick Line
                g.DrawLine(wickPen, cx, highY, cx, lowY);

                // Candle Body
                float bodyTop = Math.Min(openY, closeY);
                float bodyH = Math.Max(2f, Math.Abs(closeY - openY));
                var bodyRect = new RectangleF(cx - candleBarW / 2f, bodyTop, candleBarW, bodyH);

                g.FillRectangle(bodyBrush, bodyRect);
                g.DrawRectangle(bodyPen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);

                // Volume Bar
                if (_showVolume)
                {
                    float vH = (float)(c.Volume / maxVol * volH);
                    float vY = volBottom - vH;
                    var vRect = new RectangleF(cx - candleBarW / 2f, vY, candleBarW, vH);
                    g.FillRectangle(volBrush, vRect);
                }

                // Moving Average calculation
                if (_showMovingAverage && i >= _maPeriod - 1)
                {
                    double sum = 0;
                    for (int k = i - _maPeriod + 1; k <= i; k++) sum += _items[k].Close;
                    double maVal = sum / _maPeriod;
                    float maY = (float)(priceTop + (maxPrice - maVal) / priceRange * priceH);
                    maPoints.Add(new PointF(cx, maY));
                }

                // X-Axis Date Labels (Every few ticks)
                int labelStride = Math.Max(1, _items.Count / 6);
                if (i % labelStride == 0 || i == _items.Count - 1)
                {
                    string dateStr = c.Date.ToString("MM/dd");
                    var dSz = g.MeasureString(dateStr, dateFont);
                    g.DrawString(dateStr, dateFont, dateBrush, cx - dSz.Width / 2f, Height - padBottom + 4);
                }
            }

            // 3. Draw Moving Average Curve
            if (_showMovingAverage && maPoints.Count >= 2)
            {
                using var maPen = new Pen(Color.FromArgb(245, 158, 11), 1.75f); // Amber
                g.DrawLines(maPen, maPoints.ToArray());
            }

            // 4. Interactive Crosshair & Inspection HUD
            if (_crosshair.HasValue && _hoverIndex >= 0 && _hoverIndex < _items.Count)
            {
                var pt = _crosshair.Value;
                var hItem = _items[_hoverIndex];
                float cx = padLeft + (_hoverIndex + 0.5f) * candleSlotW;

                // Vertical Crosshair Line
                using (var crossPen = new Pen(Color.FromArgb(120, palette.TextSecondary), 1f) { DashStyle = DashStyle.Dot })
                {
                    g.DrawLine(crossPen, cx, priceTop, cx, Height - padBottom);
                    if (pt.Y >= priceTop && pt.Y <= priceBottom)
                    {
                        g.DrawLine(crossPen, padLeft, pt.Y, padLeft + plotW, pt.Y);
                    }
                }

                // HUD Tooltip
                string hud = $"{hItem.Date:yyyy-MM-dd}\nOpen: {_valuePrefix}{hItem.Open:F2}  High: {_valuePrefix}{hItem.High:F2}\nLow:  {_valuePrefix}{hItem.Low:F2}  Close: {_valuePrefix}{hItem.Close:F2}\nVol:  {hItem.Volume:N0}";
                var hudFont = ZeroFontCache.Get(8f, FontStyle.Regular);
                var hudSize = g.MeasureString(hud, hudFont);
                float hudW = hudSize.Width + 16;
                float hudH = hudSize.Height + 10;
                float hudX = cx + 12;
                float hudY = priceTop + 8;

                if (hudX + hudW > Width - padRight) hudX = cx - hudW - 12;

                var hudRect = new RectangleF(hudX, hudY, hudW, hudH);
                using var hudBg = new SolidBrush(Color.FromArgb(235, 15, 23, 42));
                using var hudBorder = new Pen(Color.FromArgb(100, 148, 163, 184), 1f);
                using var hudTextBrush = new SolidBrush(Color.White);

                g.FillRectangle(hudBg, hudRect);
                g.DrawRectangle(hudBorder, hudRect.X, hudRect.Y, hudRect.Width, hudRect.Height);
                g.DrawString(hud, hudFont, hudTextBrush, hudX + 8, hudY + 5);
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
    /// Legacy alias for <see cref="ZCandlestickChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroCandlestickChart is deprecated and will be removed in 5 release cycles. Please migrate to ZCandlestickChart instead.")]
    [ToolboxItem(false)]
    public class ZeroCandlestickChart : ZCandlestickChart
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCandlestickChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CandlestickChart is deprecated and will be removed in 5 release cycles. Please migrate to ZCandlestickChart instead.")]
    [ToolboxItem(false)]
    public class CandlestickChart : ZCandlestickChart
    {
    }

    #endregion
}
