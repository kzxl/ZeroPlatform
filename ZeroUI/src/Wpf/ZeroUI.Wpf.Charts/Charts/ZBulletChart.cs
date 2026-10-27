using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Analytics;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// Stephen Few's Bullet Graph control engineered for high-density executive KPI dashboards in WPF.
    /// Thin WPF View layer rendering qualitative bands and progress computed by ZeroUI.Core.Analytics.BulletBenchmarkEngine.
    /// </summary>
    public class ZBulletChart : FrameworkElement
    {
        private readonly List<BulletItem> _items = new List<BulletItem>();

        private string _title = "Executive KPI Performance Benchmarks";
        private string _valuePrefix = "";
        private string _valueSuffix = "";
        private int _hoverItemIndex = -1;
        private bool _showAxisTicks = true;
        private bool _showValueLabels = true;
        private double _leftLabelWidth = 160.0;
        private Color _barColor = Color.FromRgb(30, 41, 59);
        private Color _targetMarkerColor = Color.FromRgb(239, 68, 68);
        private Color _comparativeMarkerColor = Color.FromRgb(59, 130, 246);

        public List<BulletItem> Items => _items;

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public string ValuePrefix
        {
            get => _valuePrefix;
            set { _valuePrefix = value ?? string.Empty; InvalidateVisual(); }
        }

        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? string.Empty; InvalidateVisual(); }
        }

        public bool ShowAxisTicks
        {
            get => _showAxisTicks;
            set { _showAxisTicks = value; InvalidateVisual(); }
        }

        public bool ShowValueLabels
        {
            get => _showValueLabels;
            set { _showValueLabels = value; InvalidateVisual(); }
        }

        public double LeftLabelWidth
        {
            get => _leftLabelWidth;
            set { _leftLabelWidth = Math.Max(80.0, value); InvalidateVisual(); }
        }

        public Color DefaultBarColor
        {
            get => _barColor;
            set { _barColor = value; InvalidateVisual(); }
        }

        public Color TargetMarkerColor
        {
            get => _targetMarkerColor;
            set { _targetMarkerColor = value; InvalidateVisual(); }
        }

        public ZBulletChart()
        {
            ClipToBounds = true;
            MinHeight = 200;
            MinWidth = 360;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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

            double headerH = 36.0;
            if (!string.IsNullOrEmpty(_title))
            {
                var titleText = new FormattedText(
                    _title,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    13,
                    ZeroWpfTheme.TextPrimary,
                    1.0);
                dc.DrawText(titleText, new Point(14, 10));
            }

            var itemsToRender = _items.Count > 0 ? _items : GetPreviewItems();
            if (itemsToRender.Count == 0) return;

            double rightPadding = _showValueLabels ? 75.0 : 20.0;
            double leftMargin = 14.0;
            double rightMargin = w - rightPadding;
            double chartX = leftMargin + _leftLabelWidth;
            double chartWidth = Math.Max(40.0, rightMargin - chartX);

            double availableHeight = h - headerH - 16.0;
            double rowHeight = Math.Max(38.0, availableHeight / itemsToRender.Count);

            bool isDark = ZeroWpfTheme.IsDark;

            for (int i = 0; i < itemsToRender.Count; i++)
            {
                var item = itemsToRender[i];
                double rowY = headerH + (i * rowHeight);
                double trackHeight = Math.Min(24.0, rowHeight - 16.0);
                double trackY = rowY + (rowHeight - trackHeight) / 2.0;

                bool isHovered = (i == _hoverItemIndex);

                // Row hover background
                if (isHovered)
                {
                    var hoverBrush = new SolidColorBrush(Color.FromArgb(25, 59, 130, 246));
                    hoverBrush.Freeze();
                    dc.DrawRectangle(hoverBrush, null, new Rect(4, rowY, w - 8, rowHeight));
                }

                // Title & Subtitle on left
                DrawItemLabels(dc, item, new Rect(leftMargin, rowY, _leftLabelWidth - 10, rowHeight));

                // Compute metrics via Core BulletBenchmarkEngine
                var metrics = BulletBenchmarkEngine.Compute(item);

                // Qualitative ranges (Background bands)
                for (int r = 0; r < metrics.Ranges.Count; r++)
                {
                    var range = metrics.Ranges[r];
                    double rx = chartX + range.NormalizedStart * chartWidth;
                    double rw = (range.NormalizedEnd - range.NormalizedStart) * chartWidth;

                    Color bandCol = range.ColorRgba.ToColor(GetDefaultRangeColor(r, metrics.Ranges.Count, isDark));
                    var bandBrush = new SolidColorBrush(bandCol);
                    bandBrush.Freeze();

                    dc.DrawRectangle(bandBrush, null, new Rect(rx, trackY, rw, trackHeight));
                }

                // Track border
                if (_showAxisTicks)
                {
                    var borderPen = new Pen(ZeroWpfTheme.BorderSubtle, 1.0);
                    if (borderPen.CanFreeze) borderPen.Freeze();
                    dc.DrawRectangle(null, borderPen, new Rect(chartX, trackY, chartWidth, trackHeight));
                }

                // Actual value bar
                double barHeight = Math.Max(6.0, trackHeight / 2.0);
                double barY = trackY + (trackHeight - barHeight) / 2.0;
                double actualWidth = metrics.NormalizedActual * chartWidth;

                if (actualWidth > 0)
                {
                    Color defaultBar = isDark ? Color.FromRgb(226, 232, 240) : _barColor;
                    Color barCol = item.BarColorRgba.ToColor(defaultBar);
                    var barBrush = new SolidColorBrush(barCol);
                    barBrush.Freeze();
                    dc.DrawRectangle(barBrush, null, new Rect(chartX, barY, actualWidth, barHeight));
                }

                // Comparative marker
                if (metrics.NormalizedComparative.HasValue)
                {
                    double compX = chartX + metrics.NormalizedComparative.Value * chartWidth;
                    var compPen = new Pen(new SolidColorBrush(_comparativeMarkerColor), 2.0);
                    if (compPen.CanFreeze) compPen.Freeze();
                    dc.DrawLine(compPen, new Point(compX, trackY - 1), new Point(compX, trackY + trackHeight + 1));
                }

                // Target marker
                double targetX = chartX + metrics.NormalizedTarget * chartWidth;
                Color targetCol = item.TargetColorRgba.ToColor(_targetMarkerColor);
                var targetPen = new Pen(new SolidColorBrush(targetCol), 3.0);
                if (targetPen.CanFreeze) targetPen.Freeze();
                dc.DrawLine(targetPen, new Point(targetX, trackY - 3), new Point(targetX, trackY + trackHeight + 3));

                // Value labels on right
                if (_showValueLabels)
                {
                    string actualStr = $"{_valuePrefix}{item.ActualValue:N0}{_valueSuffix}";
                    string targetStr = $"tgt: {item.TargetValue:N0}";

                    var actualText = new FormattedText(
                        actualStr,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.BoldTypeface,
                        10.5,
                        ZeroWpfTheme.TextPrimary,
                        1.0);

                    var targetText = new FormattedText(
                        targetStr,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface,
                        9.0,
                        ZeroWpfTheme.TextSecondary,
                        1.0);

                    dc.DrawText(actualText, new Point(chartX + chartWidth + 8, trackY - 3));
                    dc.DrawText(targetText, new Point(chartX + chartWidth + 8, trackY + 11));
                }
            }
        }

        private void DrawItemLabels(DrawingContext dc, BulletItem item, Rect rect)
        {
            var titleText = new FormattedText(
                item.Title,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.BoldTypeface,
                11.0,
                ZeroWpfTheme.TextPrimary,
                1.0);

            var subText = new FormattedText(
                item.Subtitle ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.RegularTypeface,
                9.5,
                ZeroWpfTheme.TextSecondary,
                1.0);

            double titleX = Math.Max(rect.Left, rect.Right - titleText.Width);
            double subX = Math.Max(rect.Left, rect.Right - subText.Width);

            dc.DrawText(titleText, new Point(titleX, rect.Top + (rect.Height / 2.0) - 14));
            if (!string.IsNullOrEmpty(item.Subtitle))
            {
                dc.DrawText(subText, new Point(subX, rect.Top + (rect.Height / 2.0) + 1));
            }
        }

        private Color GetDefaultRangeColor(int index, int total, bool isDark)
        {
            if (isDark)
            {
                byte[] baseGrays = { 30, 48, 70, 95 };
                byte gray = baseGrays[Math.Min(index, baseGrays.Length - 1)];
                return Color.FromRgb(gray, gray, (byte)(gray + 4));
            }
            else
            {
                byte[] baseGrays = { 210, 228, 242, 248 };
                byte gray = baseGrays[Math.Min(index, baseGrays.Length - 1)];
                return Color.FromRgb(gray, gray, gray);
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

            var pt = e.GetPosition(this);
            double headerH = 36.0;
            double availableHeight = ActualHeight - headerH - 16.0;
            double rowHeight = Math.Max(38.0, availableHeight / itemsToRender.Count);

            int newHover = -1;
            if (pt.Y >= headerH && pt.Y < headerH + (itemsToRender.Count * rowHeight))
            {
                newHover = (int)((pt.Y - headerH) / rowHeight);
                if (newHover >= itemsToRender.Count) newHover = -1;
            }

            if (newHover != _hoverItemIndex)
            {
                _hoverItemIndex = newHover;
                InvalidateVisual();

                if (_hoverItemIndex >= 0 && _hoverItemIndex < itemsToRender.Count)
                {
                    var item = itemsToRender[_hoverItemIndex];
                    var metrics = BulletBenchmarkEngine.Compute(item);
                    ToolTip = $"{item.Title} ({item.Subtitle})\n" +
                              $"Actual: {_valuePrefix}{item.ActualValue:N1}{_valueSuffix}\n" +
                              $"Target: {_valuePrefix}{item.TargetValue:N1}{_valueSuffix} ({metrics.PercentOfTarget:F1}% of target)\n" +
                              $"Range: {item.Minimum:N0} - {item.Maximum:N0}";
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
            if (_hoverItemIndex != -1)
            {
                _hoverItemIndex = -1;
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
    /// Legacy alias for <see cref="ZBulletChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BulletChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBulletChart instead.")]
    public class BulletChart : ZBulletChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZBulletChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroBulletChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBulletChart instead.")]
    public class ZeroBulletChart : ZBulletChart { }

    #endregion

}
