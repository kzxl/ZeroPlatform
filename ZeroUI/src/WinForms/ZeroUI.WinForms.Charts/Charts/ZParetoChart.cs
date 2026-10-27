using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Quality engineering Pareto Chart implementing the 80/20 rule (Juran / Pareto Principle).
    /// Thin WinForms View layer rendering frequency bars, cumulative spline, and 80% cutoff computed by ZeroUI.Core.Analytics.ParetoEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ParetoChart.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Pareto 80/20 quality defect frequency and cumulative distribution chart")]
    public class ZParetoChart : Control
    {
        private readonly List<ParetoItem> _items = new List<ParetoItem>();
        private readonly ToolTip _toolTip = new ToolTip();

        private string _title = "Pareto Defect Distribution (80/20 Rule)";
        private double _cutoffPercentage = 80.0;
        private bool _showCutoffLine = true;
        private bool _showValuesOnBars = true;
        private bool _showCumulativePoints = true;
        private int _hoverIndex = -1;

        private Color _barColor = Color.FromArgb(79, 70, 229);       // Indigo-600
        private Color _lineColor = Color.FromArgb(245, 158, 11);     // Amber-500
        private Color _cutoffLineColor = Color.FromArgb(239, 68, 68); // Red-500

        private List<(ParetoResultRow Row, RectangleF BarRect, PointF PointOnLine)> _renderedRows =
            new List<(ParetoResultRow Row, RectangleF BarRect, PointF PointOnLine)>();

        public List<ParetoItem> Items => _items;

        [Category("Appearance")]
        [DefaultValue("Pareto Defect Distribution (80/20 Rule)")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(80.0)]
        public double CutoffPercentage
        {
            get => _cutoffPercentage;
            set { _cutoffPercentage = Math.Max(1.0, Math.Min(99.0, value)); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowCutoffLine
        {
            get => _showCutoffLine;
            set { _showCutoffLine = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowValuesOnBars
        {
            get => _showValuesOnBars;
            set { _showValuesOnBars = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowCumulativePoints
        {
            get => _showCumulativePoints;
            set { _showCumulativePoints = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BarColor
        {
            get => _barColor;
            set { _barColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color LineColor
        {
            get => _lineColor;
            set { _lineColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color CutoffLineColor
        {
            get => _cutoffLineColor;
            set { _cutoffLineColor = value; Invalidate(); }
        }

        public ZParetoChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(580, 320);

            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 150;
            _toolTip.ReshowDelay = 100;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void LoadSampleData()
        {
            _items.Clear();
            _items.Add(new ParetoItem("Surface Scratch", 142));
            _items.Add(new ParetoItem("Dimension Variance", 89));
            _items.Add(new ParetoItem("Packaging Crushed", 56));
            _items.Add(new ParetoItem("Missing Label", 34));
            _items.Add(new ParetoItem("Burr & Flashing", 21));
            _items.Add(new ParetoItem("Solder Bridge", 14));
            _items.Add(new ParetoItem("Color Mismatch", 8));
            _items.Add(new ParetoItem("Other Contamination", 5));
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

            // Title and Summary Header
            int headerHeight = 42;
            if (!string.IsNullOrEmpty(_title))
            {
                using (var titleFont = new Font(Font.FontFamily, 10.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(textPrimary))
                {
                    g.DrawString(_title, titleFont, brush, 14, 10);
                }
            }

            var sourceItems = _items.Count > 0 ? _items : GetPreviewItems();
            if (sourceItems.Count == 0) return;

            // Execute evaluation via Core ParetoEngine
            var analysis = ParetoEngine.Compute(sourceItems, _cutoffPercentage);
            if (analysis.Rows.Count == 0) return;

            string statSummary = $"Total: {analysis.TotalSum:N0} defects  •  Vital Few: Top {analysis.VitalFewCount} of {analysis.Rows.Count} categories reach {_cutoffPercentage:N0}%";
            using (var statFont = new Font(Font.FontFamily, 8f, FontStyle.Regular))
            using (var statBrush = new SolidBrush(textSecondary))
            {
                g.DrawString(statSummary, statFont, statBrush, 14, 28);
            }

            // Margins
            int leftMargin = 50;  // Left Y-Axis (Frequency)
            int rightMargin = 45; // Right Y-Axis (Percentage)
            int bottomMargin = 48; // Category Labels
            int chartX = leftMargin;
            int chartY = headerHeight;
            int chartWidth = Math.Max(50, Width - leftMargin - rightMargin);
            int chartHeight = Math.Max(50, Height - chartY - bottomMargin);

            // Compute Y-Axis scale
            double maxFreq = analysis.Rows[0].Item.Value * 1.15;
            if (maxFreq <= 0) maxFreq = 10;

            // Draw horizontal grid lines (0%, 25%, 50%, 75%, 100%)
            int gridSteps = 4;
            for (int s = 0; s <= gridSteps; s++)
            {
                float pct = (float)s / gridSteps;
                float gy = chartY + chartHeight - (pct * chartHeight);

                using (var pen = new Pen(gridColor, 1f))
                {
                    g.DrawLine(pen, chartX, gy, chartX + chartWidth, gy);
                }

                // Left Y label (Frequency count)
                double freqVal = pct * maxFreq;
                string freqStr = freqVal >= 1000 ? $"{freqVal / 1000.0:F1}k" : $"{freqVal:N0}";
                using (var brush = new SolidBrush(textSecondary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(freqStr, Font, brush, new RectangleF(0, gy - 8, leftMargin - 6, 16), sf);
                }

                // Right Y label (Cumulative %)
                string pctStr = $"{pct * 100:F0}%";
                using (var brush = new SolidBrush(textSecondary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
                {
                    g.DrawString(pctStr, Font, brush, new RectangleF(chartX + chartWidth + 6, gy - 8, rightMargin - 6, 16), sf);
                }
            }

            // Draw 80% Cutoff Reference Line
            if (_showCutoffLine)
            {
                float cutoffY = chartY + chartHeight - (float)((_cutoffPercentage / 100.0) * chartHeight);
                using (var pen = new Pen(_cutoffLineColor, 1.5f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawLine(pen, chartX, cutoffY, chartX + chartWidth, cutoffY);
                }

                string cutoffLabel = $"{_cutoffPercentage:N0}% Cutoff";
                using (var badgeFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold))
                using (var badgeBrush = new SolidBrush(_cutoffLineColor))
                {
                    g.DrawString(cutoffLabel, badgeFont, badgeBrush, chartX + 6, cutoffY - 14);
                }
            }

            // Draw Bars
            int n = analysis.Rows.Count;
            float slotWidth = (float)chartWidth / n;
            float barWidth = Math.Max(6, slotWidth * 0.65f);

            _renderedRows = new List<(ParetoResultRow Row, RectangleF BarRect, PointF PointOnLine)>(n);
            var polyPoints = new List<PointF>();

            for (int i = 0; i < n; i++)
            {
                var row = analysis.Rows[i];
                float slotCenterX = chartX + (i + 0.5f) * slotWidth;
                float barX = slotCenterX - (barWidth / 2f);

                float barH = (float)((row.Item.Value / maxFreq) * chartHeight);
                float barY = chartY + chartHeight - barH;
                var barRect = new RectangleF(barX, barY, barWidth, barH);

                float ptY = chartY + chartHeight - (float)((row.CumulativePercentage / 100.0) * chartHeight);
                var pointOnLine = new PointF(slotCenterX, ptY);
                polyPoints.Add(pointOnLine);

                _renderedRows.Add((row, barRect, pointOnLine));

                // Bar fill
                bool isHovered = (i == _hoverIndex);
                Color fillCol = row.Item.ColorRgba.ToColor(_barColor);
                if (isHovered)
                {
                    fillCol = ControlPaint.Light(fillCol, 0.25f);
                }

                using (var brush = new SolidBrush(fillCol))
                {
                    g.FillRectangle(brush, barRect);
                }

                // Bar top border
                using (var pen = new Pen(ControlPaint.Light(fillCol, 0.4f), 1f))
                {
                    g.DrawRectangle(pen, barRect.X, barRect.Y, barRect.Width, barRect.Height);
                }

                // Bar Value Label on top
                if (_showValuesOnBars && barWidth >= 16)
                {
                    string valStr = $"{row.Item.Value:N0}";
                    using (var vFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
                    using (var vBrush = new SolidBrush(textPrimary))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Far })
                    {
                        g.DrawString(valStr, vFont, vBrush, new RectangleF(barX - 4, barY - 14, barWidth + 8, 14), sf);
                    }
                }

                // Category X-Axis Label
                using (var catFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
                using (var catBrush = new SolidBrush(textPrimary))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter })
                {
                    var labelRect = new RectangleF(chartX + (i * slotWidth), chartY + chartHeight + 6, slotWidth, bottomMargin - 8);
                    g.DrawString(row.Item.Category, catFont, catBrush, labelRect, sf);
                }
            }

            // Draw Cumulative Percentage Curve
            if (polyPoints.Count > 1)
            {
                using (var linePen = new Pen(_lineColor, 2.5f))
                {
                    linePen.LineJoin = LineJoin.Round;
                    g.DrawLines(linePen, polyPoints.ToArray());
                }
            }

            // Draw Cumulative Data Points
            if (_showCumulativePoints)
            {
                float dotRadius = 4f;
                using (var fillBrush = new SolidBrush(BackColor == Color.Transparent ? (isDark ? Color.FromArgb(15, 23, 42) : Color.White) : BackColor))
                using (var dotPen = new Pen(_lineColor, 2f))
                {
                    for (int i = 0; i < polyPoints.Count; i++)
                    {
                        var pt = polyPoints[i];
                        bool isHovered = (i == _hoverIndex);
                        float r = isHovered ? dotRadius + 2f : dotRadius;

                        g.FillEllipse(fillBrush, pt.X - r, pt.Y - r, r * 2, r * 2);
                        g.DrawEllipse(dotPen, pt.X - r, pt.Y - r, r * 2, r * 2);

                        if (isHovered)
                        {
                            using (var centerBrush = new SolidBrush(_lineColor))
                            {
                                g.FillEllipse(centerBrush, pt.X - 2.5f, pt.Y - 2.5f, 5f, 5f);
                            }
                        }
                    }
                }
            }
        }

        private List<ParetoItem> GetPreviewItems()
        {
            return new List<ParetoItem>
            {
                new ParetoItem("Scratch", 120),
                new ParetoItem("Dent", 75),
                new ParetoItem("Label", 45),
                new ParetoItem("Solder", 25),
                new ParetoItem("Misalign", 15)
            };
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_renderedRows.Count == 0) return;

            int newHover = -1;
            for (int i = 0; i < _renderedRows.Count; i++)
            {
                var row = _renderedRows[i];
                var hitRect = new RectangleF(row.BarRect.X - 4, 0, row.BarRect.Width + 8, Height);
                if (hitRect.Contains(e.Location))
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                Invalidate();

                if (_hoverIndex >= 0 && _hoverIndex < _renderedRows.Count)
                {
                    var item = _renderedRows[_hoverIndex].Row;
                    string tooltip = $"{item.Item.Category}\n" +
                                     $"Defect Count: {item.Item.Value:N0}\n" +
                                     $"Individual Share: {item.Percentage:F1}%\n" +
                                     $"Cumulative: {item.CumulativePercentage:F1}%" +
                                     (item.IsVitalFew ? " (Vital Few 80%)" : "");
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
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
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
    /// Legacy alias for <see cref="ZParetoChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ParetoChart is deprecated and will be removed in 5 release cycles. Please migrate to ZParetoChart instead.")]
    [ToolboxItem(false)]
    public class ParetoChart : ZParetoChart
    {
    }

    #endregion
}
