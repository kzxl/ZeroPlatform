using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Analytics;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// Quality engineering Pareto Chart implementing the 80/20 rule (Juran / Pareto Principle) in WPF.
    /// Thin WPF View layer rendering frequency bars and cumulative spline computed by ZeroUI.Core.Analytics.ParetoEngine.
    /// </summary>
    public class ZParetoChart : FrameworkElement
    {
        private readonly List<ParetoItem> _items = new List<ParetoItem>();

        private string _title = "Pareto Defect Distribution (80/20 Rule)";
        private double _cutoffPercentage = 80.0;
        private bool _showCutoffLine = true;
        private bool _showValuesOnBars = true;
        private bool _showCumulativePoints = true;
        private int _hoverIndex = -1;

        private Color _barColor = Color.FromRgb(79, 70, 229);
        private Color _lineColor = Color.FromRgb(245, 158, 11);
        private Color _cutoffLineColor = Color.FromRgb(239, 68, 68);

        private List<(ParetoResultRow Row, Rect BarRect, Point PointOnLine)> _renderedRows =
            new List<(ParetoResultRow Row, Rect BarRect, Point PointOnLine)>();

        public List<ParetoItem> Items => _items;

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public double CutoffPercentage
        {
            get => _cutoffPercentage;
            set { _cutoffPercentage = Math.Max(1.0, Math.Min(99.0, value)); InvalidateVisual(); }
        }

        public bool ShowCutoffLine
        {
            get => _showCutoffLine;
            set { _showCutoffLine = value; InvalidateVisual(); }
        }

        public bool ShowValuesOnBars
        {
            get => _showValuesOnBars;
            set { _showValuesOnBars = value; InvalidateVisual(); }
        }

        public bool ShowCumulativePoints
        {
            get => _showCumulativePoints;
            set { _showCumulativePoints = value; InvalidateVisual(); }
        }

        public Color BarColor
        {
            get => _barColor;
            set { _barColor = value; InvalidateVisual(); }
        }

        public Color LineColor
        {
            get => _lineColor;
            set { _lineColor = value; InvalidateVisual(); }
        }

        public Color CutoffLineColor
        {
            get => _cutoffLineColor;
            set { _cutoffLineColor = value; InvalidateVisual(); }
        }

        public ZParetoChart()
        {
            ClipToBounds = true;
            MinHeight = 220;
            MinWidth = 340;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));

            double headerH = 42.0;
            if (!string.IsNullOrEmpty(_title))
            {
                var titleText = new FormattedText(
                    _title,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    13.0,
                    ZeroWpfTheme.TextPrimary,
                    1.0);
                dc.DrawText(titleText, new Point(14, 10));
            }

            var sourceItems = _items.Count > 0 ? _items : GetPreviewItems();
            if (sourceItems.Count == 0) return;

            // Execute evaluation via Core ParetoEngine
            var analysis = ParetoEngine.Compute(sourceItems, _cutoffPercentage);
            if (analysis.Rows.Count == 0) return;

            string statSummary = $"Total: {analysis.TotalSum:N0} defects  •  Vital Few: Top {analysis.VitalFewCount} of {analysis.Rows.Count} categories reach {_cutoffPercentage:N0}%";
            var statText = new FormattedText(
                statSummary,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.RegularTypeface,
                9.5,
                ZeroWpfTheme.TextSecondary,
                1.0);
            dc.DrawText(statText, new Point(14, 28));

            double padLeft = 50.0;
            double padRight = 45.0;
            double padBottom = 44.0;
            double chartX = padLeft;
            double chartY = headerH;
            double chartW = Math.Max(50.0, w - padLeft - padRight);
            double chartH = Math.Max(50.0, h - chartY - padBottom);

            double maxFreq = analysis.Rows[0].Item.Value * 1.15;
            if (maxFreq <= 0) maxFreq = 10.0;

            // Draw horizontal grid lines
            int gridSteps = 4;
            for (int s = 0; s <= gridSteps; s++)
            {
                double pct = (double)s / gridSteps;
                double gy = chartY + chartH - (pct * chartH);

                var gridPen = new Pen(ZeroWpfTheme.BorderSubtle, 1.0);
                if (gridPen.CanFreeze) gridPen.Freeze();
                dc.DrawLine(gridPen, new Point(chartX, gy), new Point(chartX + chartW, gy));

                // Left Y (Frequency)
                double freqVal = pct * maxFreq;
                string freqStr = freqVal >= 1000 ? $"{freqVal / 1000.0:F1}k" : $"{freqVal:N0}";
                var freqText = new FormattedText(
                    freqStr,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    9.0,
                    ZeroWpfTheme.TextSecondary,
                    1.0);
                dc.DrawText(freqText, new Point(chartX - freqText.Width - 6, gy - (freqText.Height / 2.0)));

                // Right Y (Cumulative %)
                string pctStr = $"{pct * 100:F0}%";
                var pctText = new FormattedText(
                    pctStr,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    9.0,
                    ZeroWpfTheme.TextSecondary,
                    1.0);
                dc.DrawText(pctText, new Point(chartX + chartW + 6, gy - (pctText.Height / 2.0)));
            }

            // Draw 80% Cutoff Reference Line
            if (_showCutoffLine)
            {
                double cutoffY = chartY + chartH - ((_cutoffPercentage / 100.0) * chartH);
                var cutoffPen = new Pen(new SolidColorBrush(_cutoffLineColor), 1.5)
                {
                    DashStyle = DashStyles.Dash
                };
                if (cutoffPen.CanFreeze) cutoffPen.Freeze();
                dc.DrawLine(cutoffPen, new Point(chartX, cutoffY), new Point(chartX + chartW, cutoffY));

                var badgeText = new FormattedText(
                    $"{_cutoffPercentage:N0}% Cutoff",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    9.0,
                    new SolidColorBrush(_cutoffLineColor),
                    1.0);
                dc.DrawText(badgeText, new Point(chartX + 6, cutoffY - 14));
            }

            // Draw Bars
            int n = analysis.Rows.Count;
            double slotWidth = chartW / n;
            double barWidth = Math.Max(6.0, slotWidth * 0.65);

            _renderedRows = new List<(ParetoResultRow Row, Rect BarRect, Point PointOnLine)>(n);
            var polyPoints = new List<Point>(n);

            for (int i = 0; i < n; i++)
            {
                var row = analysis.Rows[i];
                double slotCenterX = chartX + (i + 0.5) * slotWidth;
                double barX = slotCenterX - (barWidth / 2.0);

                double barH = (row.Item.Value / maxFreq) * chartH;
                double barY = chartY + chartH - barH;
                var barRect = new Rect(barX, barY, barWidth, barH);

                double ptY = chartY + chartH - ((row.CumulativePercentage / 100.0) * chartH);
                var pointOnLine = new Point(slotCenterX, ptY);
                polyPoints.Add(pointOnLine);

                _renderedRows.Add((row, barRect, pointOnLine));

                // Bar fill
                bool isHovered = (i == _hoverIndex);
                Color fillCol = row.Item.ColorRgba.ToColor(_barColor);
                if (isHovered)
                {
                    fillCol = Color.FromArgb(255, (byte)Math.Min(255, fillCol.R + 40), (byte)Math.Min(255, fillCol.G + 40), (byte)Math.Min(255, fillCol.B + 40));
                }

                var fillBrush = new SolidColorBrush(fillCol);
                fillBrush.Freeze();
                dc.DrawRectangle(fillBrush, null, barRect);

                // Value label on top
                if (_showValuesOnBars && barWidth >= 16)
                {
                    var valText = new FormattedText(
                        $"{row.Item.Value:N0}",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface,
                        9.0,
                        ZeroWpfTheme.TextPrimary,
                        1.0);
                    dc.DrawText(valText, new Point(slotCenterX - (valText.Width / 2.0), barY - 14));
                }

                // Category X-Axis label
                var catText = new FormattedText(
                    row.Item.Category,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    9.0,
                    ZeroWpfTheme.TextPrimary,
                    1.0);
                dc.DrawText(catText, new Point(slotCenterX - (catText.Width / 2.0), chartY + chartH + 6));
            }

            // Draw Cumulative Percentage Curve
            if (polyPoints.Count > 1)
            {
                var curveGeom = new StreamGeometry();
                using (var ctx = curveGeom.Open())
                {
                    ctx.BeginFigure(polyPoints[0], isFilled: false, isClosed: false);
                    for (int i = 1; i < polyPoints.Count; i++)
                    {
                        ctx.LineTo(polyPoints[i], isStroked: true, isSmoothJoin: true);
                    }
                }
                curveGeom.Freeze();

                var linePen = new Pen(new SolidColorBrush(_lineColor), 2.5);
                linePen.Freeze();
                dc.DrawGeometry(null, linePen, curveGeom);
            }

            // Draw Cumulative Data Points
            if (_showCumulativePoints)
            {
                var dotBrush = new SolidColorBrush(_lineColor);
                dotBrush.Freeze();

                for (int i = 0; i < polyPoints.Count; i++)
                {
                    var pt = polyPoints[i];
                    bool isHovered = (i == _hoverIndex);
                    double r = isHovered ? 5.5 : 3.5;

                    dc.DrawEllipse(Brushes.White, null, pt, r + 1.0, r + 1.0);
                    dc.DrawEllipse(dotBrush, null, pt, r, r);
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

            var pt = e.GetPosition(this);
            int newHover = -1;

            for (int i = 0; i < _renderedRows.Count; i++)
            {
                var row = _renderedRows[i];
                var hitRect = new Rect(row.BarRect.X - 4, 0, row.BarRect.Width + 8, ActualHeight);
                if (hitRect.Contains(pt))
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                InvalidateVisual();

                if (_hoverIndex >= 0 && _hoverIndex < _renderedRows.Count)
                {
                    var item = _renderedRows[_hoverIndex].Row;
                    ToolTip = $"{item.Item.Category}\n" +
                              $"Defect Count: {item.Item.Value:N0}\n" +
                              $"Individual Share: {item.Percentage:F1}%\n" +
                              $"Cumulative: {item.CumulativePercentage:F1}%" +
                              (item.IsVitalFew ? " (Vital Few 80%)" : "");
                    Cursor = Cursors.Hand;
                }
                else
                {
                    ToolTip = null;
                    Cursor = Cursors.Arrow;
                }
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                ToolTip = null;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZParetoChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ParetoChart is deprecated and will be removed in 5 release cycles. Please migrate to ZParetoChart instead.")]
    public class ParetoChart : ZParetoChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZParetoChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroParetoChart is deprecated and will be removed in 5 release cycles. Please migrate to ZParetoChart instead.")]
    public class ZeroParetoChart : ZParetoChart { }

    #endregion

}
