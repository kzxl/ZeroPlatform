using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    public class BoxPlotDataPoint
    {
        public string Label { get; set; } = "Sample";
        public double Min { get; set; }
        public double Q1 { get; set; }
        public double Median { get; set; }
        public double Q3 { get; set; }
        public double Max { get; set; }
        public List<double> Outliers { get; } = new List<double>();
        public Color? BoxColor { get; set; }

        public BoxPlotDataPoint() { }

        public BoxPlotDataPoint(string label, double min, double q1, double median, double q3, double max, Color? boxColor = null)
        {
            Label = label;
            Min = min;
            Q1 = q1;
            Median = median;
            Q3 = q3;
            Max = max;
            BoxColor = boxColor;
        }

        public static BoxPlotDataPoint FromRawValues(string label, IList<double> values, Color? boxColor = null)
        {
            var pt = new BoxPlotDataPoint { Label = label, BoxColor = boxColor };
            if (values == null || values.Count == 0) return pt;

            var sorted = new List<double>(values);
            sorted.Sort();

            int n = sorted.Count;
            pt.Min = sorted[0];
            pt.Max = sorted[n - 1];
            pt.Median = GetPercentile(sorted, 0.5);
            pt.Q1 = GetPercentile(sorted, 0.25);
            pt.Q3 = GetPercentile(sorted, 0.75);

            double iqr = pt.Q3 - pt.Q1;
            double lowerFence = pt.Q1 - 1.5 * iqr;
            double upperFence = pt.Q3 + 1.5 * iqr;

            foreach (var v in sorted)
            {
                if (v < lowerFence || v > upperFence)
                {
                    pt.Outliers.Add(v);
                }
            }

            return pt;
        }

        private static double GetPercentile(List<double> sorted, double percentile)
        {
            if (sorted.Count == 1) return sorted[0];
            double idx = percentile * (sorted.Count - 1);
            int low = (int)Math.Floor(idx);
            int high = (int)Math.Ceiling(idx);
            if (low == high) return sorted[low];
            double weight = idx - low;
            return sorted[low] * (1.0 - weight) + sorted[high] * weight;
        }
    }

    /// <summary>
    /// Enterprise Statistical Box-and-Whisker (BoxPlot) Chart for ZeroUI WPF.
    /// Visualizes five-number statistical summaries (Min, Q1, Median, Q3, Max) and outliers
    /// for industrial SPC quality control, tolerance inspection, and batch variability analysis.
    /// </summary>
    public class ZBoxPlotChart : FrameworkElement
    {
        private readonly List<BoxPlotDataPoint> _dataPoints = new List<BoxPlotDataPoint>();
        private double? _upperSpecLimit = null;
        private double? _lowerSpecLimit = null;

        public static readonly DependencyProperty ChartTitleProperty =
            DependencyProperty.Register(nameof(ChartTitle), typeof(string), typeof(ZBoxPlotChart),
                new FrameworkPropertyMetadata("Statistical Distribution (SPC)", FrameworkPropertyMetadataOptions.AffectsRender));

        public string ChartTitle
        {
            get => (string)GetValue(ChartTitleProperty);
            set => SetValue(ChartTitleProperty, value);
        }

        public double? UpperSpecLimit
        {
            get => _upperSpecLimit;
            set { _upperSpecLimit = value; InvalidateVisual(); }
        }

        public double? LowerSpecLimit
        {
            get => _lowerSpecLimit;
            set { _lowerSpecLimit = value; InvalidateVisual(); }
        }

        public List<BoxPlotDataPoint> DataPoints => _dataPoints;

        public ZBoxPlotChart()
        {
            ClipToBounds = true;
            MinHeight = 240;
            MinWidth = 320;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void AddPoint(BoxPlotDataPoint point)
        {
            if (point == null) return;
            _dataPoints.Add(point);
            InvalidateVisual();
        }

        public void Clear()
        {
            _dataPoints.Clear();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // Draw background
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));

            double padLeft = 60;
            double padRight = 30;
            double padTop = 40;
            double padBottom = 40;

            double plotW = w - padLeft - padRight;
            double plotH = h - padTop - padBottom;
            if (plotW <= 0 || plotH <= 0) return;

            // 1. Draw Title
            var typeface = ZeroWpfTheme.BoldTypeface;
            var titleText = new FormattedText(
                ChartTitle,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                14,
                ZeroWpfTheme.TextPrimary,
                1.0);
            dc.DrawText(titleText, new Point(padLeft, 10));

            // 2. Calculate value range
            double minVal = double.MaxValue;
            double maxVal = double.MinValue;

            if (_dataPoints.Count > 0)
            {
                foreach (var p in _dataPoints)
                {
                    if (p.Min < minVal) minVal = p.Min;
                    if (p.Max > maxVal) maxVal = p.Max;
                    foreach (var o in p.Outliers)
                    {
                        if (o < minVal) minVal = o;
                        if (o > maxVal) maxVal = o;
                    }
                }
            }
            else
            {
                minVal = 0;
                maxVal = 100;
            }

            if (_upperSpecLimit.HasValue && _upperSpecLimit.Value > maxVal) maxVal = _upperSpecLimit.Value;
            if (_lowerSpecLimit.HasValue && _lowerSpecLimit.Value < minVal) minVal = _lowerSpecLimit.Value;

            double range = maxVal - minVal;
            if (range <= 0.0001) range = 1.0;
            minVal -= range * 0.05;
            maxVal += range * 0.05;
            range = maxVal - minVal;

            // 3. Draw Y Grid lines
            int gridTicks = 5;
            var gridPen = ZeroWpfTheme.GridLinePen;
            var textTypeface = ZeroWpfTheme.RegularTypeface;
            var textBrush = ZeroWpfTheme.TextSecondary;

            for (int i = 0; i <= gridTicks; i++)
            {
                double frac = (double)i / gridTicks;
                double y = padTop + plotH - (frac * plotH);
                double val = minVal + frac * range;

                dc.DrawLine(gridPen, new Point(padLeft, y), new Point(padLeft + plotW, y));

                var label = new FormattedText(
                    val.ToString("F1"),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    textTypeface,
                    11,
                    textBrush,
                    1.0);
                dc.DrawText(label, new Point(10, y - 8));
            }

            // 4. Draw Spec Limits (USL / LSL)
            var dangerBrush = ZeroWpfTheme.DangerAccent;
            var specPen = new Pen(dangerBrush, 1.5);
            specPen.DashStyle = DashStyles.DashDot;
            specPen.Freeze();

            if (_upperSpecLimit.HasValue)
            {
                double uslY = padTop + plotH - ((_upperSpecLimit.Value - minVal) / range * plotH);
                dc.DrawLine(specPen, new Point(padLeft, uslY), new Point(padLeft + plotW, uslY));
                var uslText = new FormattedText(
                    $"USL: {_upperSpecLimit.Value:F1}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    textTypeface,
                    11,
                    dangerBrush,
                    1.0);
                dc.DrawText(uslText, new Point(padLeft + plotW - 60, uslY - 16));
            }

            if (_lowerSpecLimit.HasValue)
            {
                double lslY = padTop + plotH - ((_lowerSpecLimit.Value - minVal) / range * plotH);
                dc.DrawLine(specPen, new Point(padLeft, lslY), new Point(padLeft + plotW, lslY));
                var lslText = new FormattedText(
                    $"LSL: {_lowerSpecLimit.Value:F1}",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    textTypeface,
                    11,
                    dangerBrush,
                    1.0);
                dc.DrawText(lslText, new Point(padLeft + plotW - 60, lslY + 2));
            }

            if (_dataPoints.Count == 0) return;

            // 5. Draw Boxes and Whiskers
            double colWidth = plotW / _dataPoints.Count;
            double boxWidth = Math.Min(44, colWidth * 0.6);

            var primaryBrush = ZeroWpfTheme.PrimaryAccent;
            var whiskerPen = new Pen(textBrush, 1.5);
            whiskerPen.Freeze();

            var medianPen = new Pen(Brushes.White, 2.0);
            medianPen.Freeze();

            for (int i = 0; i < _dataPoints.Count; i++)
            {
                var pt = _dataPoints[i];
                double cx = padLeft + i * colWidth + colWidth / 2.0;

                double yMin = padTop + plotH - ((pt.Min - minVal) / range * plotH);
                double yQ1 = padTop + plotH - ((pt.Q1 - minVal) / range * plotH);
                double yMed = padTop + plotH - ((pt.Median - minVal) / range * plotH);
                double yQ3 = padTop + plotH - ((pt.Q3 - minVal) / range * plotH);
                double yMax = padTop + plotH - ((pt.Max - minVal) / range * plotH);

                Color boxColor = pt.BoxColor ?? ZeroWpfTheme.PrimaryAccent.Color;
                var fillBrush = new SolidColorBrush(Color.FromArgb(100, boxColor.R, boxColor.G, boxColor.B));
                fillBrush.Freeze();
                var borderPen = new Pen(new SolidColorBrush(boxColor), 1.5);
                borderPen.Freeze();

                // Whisker stem (Min to Q1, Q3 to Max)
                dc.DrawLine(whiskerPen, new Point(cx, yMin), new Point(cx, yQ1));
                dc.DrawLine(whiskerPen, new Point(cx, yQ3), new Point(cx, yMax));

                // Whisker caps
                double capW = boxWidth * 0.4;
                dc.DrawLine(whiskerPen, new Point(cx - capW / 2, yMin), new Point(cx + capW / 2, yMin));
                dc.DrawLine(whiskerPen, new Point(cx - capW / 2, yMax), new Point(cx + capW / 2, yMax));

                // Central Box (Q1 to Q3)
                var boxRect = new Rect(cx - boxWidth / 2, yQ3, boxWidth, Math.Max(1.0, yQ1 - yQ3));
                dc.DrawRectangle(fillBrush, borderPen, boxRect);

                // Median line
                dc.DrawLine(medianPen, new Point(cx - boxWidth / 2, yMed), new Point(cx + boxWidth / 2, yMed));

                // Outliers
                foreach (var o in pt.Outliers)
                {
                    double oY = padTop + plotH - ((o - minVal) / range * plotH);
                    dc.DrawEllipse(dangerBrush, null, new Point(cx, oY), 3.0, 3.0);
                }

                // X Label
                var xLabel = new FormattedText(
                    pt.Label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    textTypeface,
                    11,
                    ZeroWpfTheme.TextPrimary,
                    1.0);
                dc.DrawText(xLabel, new Point(cx - xLabel.Width / 2, padTop + plotH + 8));
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
    /// Legacy alias for <see cref="ZBoxPlotChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BoxPlotChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBoxPlotChart instead.")]
    public class BoxPlotChart : ZBoxPlotChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZBoxPlotChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroBoxPlotChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBoxPlotChart instead.")]
    public class ZeroBoxPlotChart : ZBoxPlotChart { }

    #endregion

}
