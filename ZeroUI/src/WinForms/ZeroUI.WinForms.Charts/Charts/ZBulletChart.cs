using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Stephen Few's Bullet Graph control engineered for high-density executive KPI dashboards.
    /// Thin WinForms View layer rendering qualitative thresholds and progress computed by ZeroUI.Core.Analytics.BulletBenchmarkEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "BulletChart.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Stephen Few linear bullet graph for dense KPI benchmarking against targets and qualitative ranges")]
    public class ZBulletChart : Control
    {
        private readonly List<BulletItem> _items = new List<BulletItem>();
        private readonly ToolTip _toolTip = new ToolTip();

        private string _title = "Executive KPI Performance Benchmarks";
        private string _valuePrefix = "";
        private string _valueSuffix = "";
        private int _hoverItemIndex = -1;
        private bool _showAxisTicks = true;
        private bool _showValueLabels = true;
        private int _leftLabelWidth = 160;
        private Color _barColor = Color.FromArgb(30, 41, 59);
        private Color _targetMarkerColor = Color.FromArgb(239, 68, 68);
        private Color _comparativeMarkerColor = Color.FromArgb(59, 130, 246);

        public List<BulletItem> Items => _items;

        [Category("Appearance")]
        [DefaultValue("Executive KPI Performance Benchmarks")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string ValuePrefix
        {
            get => _valuePrefix;
            set { _valuePrefix = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowAxisTicks
        {
            get => _showAxisTicks;
            set { _showAxisTicks = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowValueLabels
        {
            get => _showValueLabels;
            set { _showValueLabels = value; Invalidate(); }
        }

        [Category("Layout")]
        [DefaultValue(160)]
        public int LeftLabelWidth
        {
            get => _leftLabelWidth;
            set { _leftLabelWidth = Math.Max(80, value); Invalidate(); }
        }

        [Category("Appearance")]
        public Color DefaultBarColor
        {
            get => _barColor;
            set { _barColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color TargetMarkerColor
        {
            get => _targetMarkerColor;
            set { _targetMarkerColor = value; Invalidate(); }
        }

        public ZBulletChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(580, 280);

            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 150;
            _toolTip.ReshowDelay = 100;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void LoadSampleData()
        {
            _items.Clear();

            var rev = new BulletItem("Revenue", "USD in thousands", 275, 250, 300, 0);
            rev.Ranges.Add(new BulletRange("Poor", 150));
            rev.Ranges.Add(new BulletRange("Satisfactory", 225));
            rev.Ranges.Add(new BulletRange("Good", 300));
            _items.Add(rev);

            var profit = new BulletItem("Profit Margin", "% of gross revenue", 22.5, 25.0, 30, 0);
            profit.Ranges.Add(new BulletRange("Poor", 15));
            profit.Ranges.Add(new BulletRange("Satisfactory", 22));
            profit.Ranges.Add(new BulletRange("Good", 30));
            _items.Add(profit);

            var orders = new BulletItem("Order Volume", "Units fulfilled (k)", 78, 70, 100, 0);
            orders.Ranges.Add(new BulletRange("Poor", 50));
            orders.Ranges.Add(new BulletRange("Satisfactory", 75));
            orders.Ranges.Add(new BulletRange("Good", 100));
            _items.Add(orders);

            var csat = new BulletItem("CSAT Score", "Customer satisfaction %", 92, 85, 100, 0);
            csat.Ranges.Add(new BulletRange("Poor", 70));
            csat.Ranges.Add(new BulletRange("Satisfactory", 85));
            csat.Ranges.Add(new BulletRange("Good", 100));
            _items.Add(csat);

            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            var textPrimary = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(30, 41, 59);
            var textSecondary = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            var gridColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);

            // Title
            int headerHeight = 36;
            if (!string.IsNullOrEmpty(_title))
            {
                using (var titleFont = new Font(Font.FontFamily, 10.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(textPrimary))
                {
                    g.DrawString(_title, titleFont, brush, 14, 10);
                }
            }

            var itemsToRender = _items.Count > 0 ? _items : GetPreviewItems();
            if (itemsToRender.Count == 0) return;

            int rightPadding = _showValueLabels ? 75 : 20;
            int leftMargin = 14;
            int rightMargin = Width - rightPadding;
            int chartX = leftMargin + _leftLabelWidth;
            int chartWidth = Math.Max(40, rightMargin - chartX);

            int availableHeight = Height - headerHeight - 16;
            int rowHeight = Math.Max(38, availableHeight / itemsToRender.Count);

            for (int i = 0; i < itemsToRender.Count; i++)
            {
                var item = itemsToRender[i];
                int rowY = headerHeight + (i * rowHeight);
                int trackHeight = Math.Min(24, rowHeight - 16);
                int trackY = rowY + (rowHeight - trackHeight) / 2;

                bool isHovered = (i == _hoverItemIndex);

                // Highlight hover row background
                if (isHovered)
                {
                    var hoverBg = isDark ? Color.FromArgb(35, 59, 130, 246) : Color.FromArgb(20, 59, 130, 246);
                    using (var brush = new SolidBrush(hoverBg))
                    {
                        g.FillRectangle(brush, 4, rowY, Width - 8, rowHeight);
                    }
                }

                // Title & Subtitle on left
                var labelRect = new Rectangle(leftMargin, rowY, _leftLabelWidth - 10, rowHeight);
                DrawItemLabels(g, item, labelRect, textPrimary, textSecondary);

                // Compute normalized metrics via Core BulletBenchmarkEngine
                var metrics = BulletBenchmarkEngine.Compute(item);

                // Qualitative ranges (Background bands)
                for (int r = 0; r < metrics.Ranges.Count; r++)
                {
                    var range = metrics.Ranges[r];
                    float rx = chartX + (float)(range.NormalizedStart * chartWidth);
                    float rw = (float)((range.NormalizedEnd - range.NormalizedStart) * chartWidth);

                    Color bandColor = range.ColorRgba.ToColor(GetDefaultRangeColor(r, metrics.Ranges.Count, isDark));
                    using (var brush = new SolidBrush(bandColor))
                    {
                        g.FillRectangle(brush, rx, trackY, rw, trackHeight);
                    }
                }

                // Grid / Axis ticks along track
                if (_showAxisTicks)
                {
                    using (var pen = new Pen(gridColor, 1f))
                    {
                        g.DrawRectangle(pen, chartX, trackY, chartWidth, trackHeight);
                    }
                }

                // Actual value bar (centered horizontally inside track, thinner height)
                int barHeight = Math.Max(6, trackHeight / 2);
                int barY = trackY + (trackHeight - barHeight) / 2;
                float actualWidth = (float)(metrics.NormalizedActual * chartWidth);

                if (actualWidth > 0)
                {
                    Color defaultBar = isDark ? Color.FromArgb(226, 232, 240) : _barColor;
                    Color barCol = item.BarColorRgba.ToColor(defaultBar);
                    using (var brush = new SolidBrush(barCol))
                    {
                        g.FillRectangle(brush, chartX, barY, actualWidth, barHeight);
                    }
                }

                // Comparative marker (optional prior period line)
                if (metrics.NormalizedComparative.HasValue)
                {
                    float compX = chartX + (float)(metrics.NormalizedComparative.Value * chartWidth);
                    using (var compPen = new Pen(_comparativeMarkerColor, 2f))
                    {
                        g.DrawLine(compPen, compX, trackY - 1, compX, trackY + trackHeight + 1);
                    }
                }

                // Target marker (prominent vertical line spanning past track)
                float targetX = chartX + (float)(metrics.NormalizedTarget * chartWidth);
                int markerExtend = 3;
                Color targetCol = item.TargetColorRgba.ToColor(_targetMarkerColor);
                using (var targetPen = new Pen(targetCol, 3f))
                {
                    g.DrawLine(targetPen, targetX, trackY - markerExtend, targetX, trackY + trackHeight + markerExtend);
                }

                // Right value labels (Actual / Target)
                if (_showValueLabels)
                {
                    string actualStr = $"{_valuePrefix}{item.ActualValue:N0}{_valueSuffix}";
                    string targetStr = $"tgt: {item.TargetValue:N0}";

                    using (var fontBold = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
                    using (var fontSmall = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
                    using (var brushBold = new SolidBrush(textPrimary))
                    using (var brushSmall = new SolidBrush(textSecondary))
                    {
                        g.DrawString(actualStr, fontBold, brushBold, chartX + chartWidth + 8, trackY - 2);
                        g.DrawString(targetStr, fontSmall, brushSmall, chartX + chartWidth + 8, trackY + 12);
                    }
                }
            }
        }

        private void DrawItemLabels(Graphics g, BulletItem item, Rectangle rect, Color primary, Color secondary)
        {
            using (var titleFont = new Font(Font.FontFamily, 8.75f, FontStyle.Bold))
            using (var subFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
            using (var primaryBrush = new SolidBrush(primary))
            using (var subBrush = new SolidBrush(secondary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                var titleRect = new Rectangle(rect.X, rect.Y + (rect.Height / 2) - 14, rect.Width, 14);
                g.DrawString(item.Title, titleFont, primaryBrush, titleRect, sf);

                if (!string.IsNullOrEmpty(item.Subtitle))
                {
                    var subRect = new Rectangle(rect.X, rect.Y + (rect.Height / 2) + 1, rect.Width, 14);
                    g.DrawString(item.Subtitle, subFont, subBrush, subRect, sf);
                }
            }
        }

        private Color GetDefaultRangeColor(int index, int total, bool isDark)
        {
            if (isDark)
            {
                int[] baseGrays = { 30, 48, 70, 95 };
                int gray = baseGrays[Math.Min(index, baseGrays.Length - 1)];
                return Color.FromArgb(gray, gray, gray + 4);
            }
            else
            {
                int[] baseGrays = { 210, 228, 242, 248 };
                int gray = baseGrays[Math.Min(index, baseGrays.Length - 1)];
                return Color.FromArgb(gray, gray, gray);
            }
        }

        private List<BulletItem> GetPreviewItems()
        {
            return new List<BulletItem>
            {
                new BulletItem("Revenue", "USD (k)", 260, 240, 300, 0),
                new BulletItem("Profit %", "% of revenue", 21, 25, 30, 0),
                new BulletItem("Satisfaction", "CSAT rating %", 94, 90, 100, 0)
            };
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var itemsToRender = _items.Count > 0 ? _items : GetPreviewItems();
            if (itemsToRender.Count == 0) return;

            int headerHeight = 36;
            int availableHeight = Height - headerHeight - 16;
            int rowHeight = Math.Max(38, availableHeight / itemsToRender.Count);

            int newHover = -1;
            if (e.Y >= headerHeight && e.Y < headerHeight + (itemsToRender.Count * rowHeight))
            {
                newHover = (e.Y - headerHeight) / rowHeight;
                if (newHover >= itemsToRender.Count) newHover = -1;
            }

            if (newHover != _hoverItemIndex)
            {
                _hoverItemIndex = newHover;
                Invalidate();

                if (_hoverItemIndex >= 0 && _hoverItemIndex < itemsToRender.Count)
                {
                    var item = itemsToRender[_hoverItemIndex];
                    var metrics = BulletBenchmarkEngine.Compute(item);
                    string tooltip = $"{item.Title} ({item.Subtitle})\n" +
                                     $"Actual: {_valuePrefix}{item.ActualValue:N1}{_valueSuffix}\n" +
                                     $"Target: {_valuePrefix}{item.TargetValue:N1}{_valueSuffix} ({metrics.PercentOfTarget:F1}% of target)\n" +
                                     $"Range: {item.Minimum:N0} - {item.Maximum:N0}";
                    _toolTip.SetToolTip(this, tooltip);
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverItemIndex != -1)
            {
                _hoverItemIndex = -1;
                _toolTip.SetToolTip(this, null);
                Invalidate();
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

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBulletChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BulletChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBulletChart instead.")]
    [ToolboxItem(false)]
    public class BulletChart : ZBulletChart
    {
    }

    #endregion
}
