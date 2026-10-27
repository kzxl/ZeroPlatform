using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
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
    /// Modern Enterprise Statistical Box-and-Whisker (BoxPlot) Chart for ZeroUI WinForms.
    /// Visualizes five-number statistical summaries (Min, Q1, Median, Q3, Max) and outliers
    /// for industrial SPC quality control, tolerance inspection, and batch variability analysis.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Statistical Box-and-Whisker chart for industrial SPC quality inspection")]
    [ToolboxBitmap(typeof(ZeroIcons), "BoxPlotChart.bmp")]
    public class ZBoxPlotChart : Control
    {
        private readonly List<BoxPlotDataPoint> _dataPoints = new List<BoxPlotDataPoint>();
        private string _chartTitle = "Statistical Distribution (SPC)";
        private double? _upperSpecLimit = null;
        private double? _lowerSpecLimit = null;

        [Category("Appearance")]
        [DefaultValue("Statistical Distribution (SPC)")]
        public string ChartTitle
        {
            get => _chartTitle;
            set { _chartTitle = value; Invalidate(); }
        }

        [Category("Data")]
        public double? UpperSpecLimit
        {
            get => _upperSpecLimit;
            set { _upperSpecLimit = value; Invalidate(); }
        }

        [Category("Data")]
        public double? LowerSpecLimit
        {
            get => _lowerSpecLimit;
            set { _lowerSpecLimit = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<BoxPlotDataPoint> DataPoints => _dataPoints;

        public ZBoxPlotChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Size = new Size(480, 300);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void AddPoint(BoxPlotDataPoint point)
        {
            if (point == null) return;
            _dataPoints.Add(point);
            Invalidate();
        }

        public void Clear()
        {
            _dataPoints.Clear();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var colors = ZeroTheme.Colors;

            // Chart Bounds
            int paddingLeft = 60;
            int paddingRight = 30;
            int paddingTop = 40;
            int paddingBottom = 40;

            int plotW = Width - paddingLeft - paddingRight;
            int plotH = Height - paddingTop - paddingBottom;
            if (plotW <= 0 || plotH <= 0) return;

            // Draw Title
            using (var titleFont = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (var brush = new SolidBrush(colors.TextPrimary))
            {
                g.DrawString(_chartTitle, titleFont, brush, paddingLeft, 10);
            }

            // Calculate Value Range
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

            // Draw Y Grid lines
            int gridTicks = 5;
            using (var gridPen = new Pen(colors.Border, 1f) { DashStyle = DashStyle.Dash })
            using (var textBrush = new SolidBrush(colors.TextSecondary))
            using (var tickFont = new Font("Segoe UI", 8.5f))
            {
                for (int i = 0; i <= gridTicks; i++)
                {
                    double frac = (double)i / gridTicks;
                    float y = paddingTop + plotH - (float)(frac * plotH);
                    double val = minVal + frac * range;

                    g.DrawLine(gridPen, paddingLeft, y, paddingLeft + plotW, y);
                    g.DrawString(val.ToString("F1"), tickFont, textBrush, 10, y - 8);
                }
            }

            // Draw Spec Limit Lines (USL / LSL)
            if (_upperSpecLimit.HasValue)
            {
                float uslY = paddingTop + plotH - (float)((_upperSpecLimit.Value - minVal) / range * plotH);
                using (var pen = new Pen(colors.Danger, 1.5f) { DashStyle = DashStyle.DashDot })
                using (var brush = new SolidBrush(colors.Danger))
                using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    g.DrawLine(pen, paddingLeft, uslY, paddingLeft + plotW, uslY);
                    g.DrawString($"USL: {_upperSpecLimit.Value:F1}", font, brush, paddingLeft + plotW - 60, uslY - 14);
                }
            }
            if (_lowerSpecLimit.HasValue)
            {
                float lslY = paddingTop + plotH - (float)((_lowerSpecLimit.Value - minVal) / range * plotH);
                using (var pen = new Pen(colors.Danger, 1.5f) { DashStyle = DashStyle.DashDot })
                using (var brush = new SolidBrush(colors.Danger))
                using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    g.DrawLine(pen, paddingLeft, lslY, paddingLeft + plotW, lslY);
                    g.DrawString($"LSL: {_lowerSpecLimit.Value:F1}", font, brush, paddingLeft + plotW - 60, lslY + 2);
                }
            }

            if (_dataPoints.Count == 0) return;

            // Draw Boxes and Whiskers
            float colWidth = (float)plotW / _dataPoints.Count;
            float boxWidth = Math.Min(40, colWidth * 0.6f);

            for (int i = 0; i < _dataPoints.Count; i++)
            {
                var pt = _dataPoints[i];
                float cx = paddingLeft + i * colWidth + colWidth / 2f;

                float yMin = paddingTop + plotH - (float)((pt.Min - minVal) / range * plotH);
                float yQ1 = paddingTop + plotH - (float)((pt.Q1 - minVal) / range * plotH);
                float yMed = paddingTop + plotH - (float)((pt.Median - minVal) / range * plotH);
                float yQ3 = paddingTop + plotH - (float)((pt.Q3 - minVal) / range * plotH);
                float yMax = paddingTop + plotH - (float)((pt.Max - minVal) / range * plotH);

                Color boxColor = pt.BoxColor ?? colors.Primary;

                // 1. Draw Whisker Stem (Vertical line from Min to Q1, and Q3 to Max)
                using (var whiskerPen = new Pen(colors.TextSecondary, 1.5f))
                {
                    g.DrawLine(whiskerPen, cx, yMin, cx, yQ1);
                    g.DrawLine(whiskerPen, cx, yQ3, cx, yMax);

                    // Whisker Caps
                    float capW = boxWidth * 0.4f;
                    g.DrawLine(whiskerPen, cx - capW / 2, yMin, cx + capW / 2, yMin);
                    g.DrawLine(whiskerPen, cx - capW / 2, yMax, cx + capW / 2, yMax);
                }

                // 2. Draw Central Box (Q1 to Q3)
                var boxRect = new RectangleF(cx - boxWidth / 2, yQ3, boxWidth, yQ1 - yQ3);
                using (var fillBrush = new SolidBrush(Color.FromArgb(100, boxColor)))
                {
                    g.FillRectangle(fillBrush, boxRect);
                }
                using (var boxPen = new Pen(boxColor, 1.5f))
                {
                    g.DrawRectangle(boxPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);
                }

                // 3. Draw Median Line
                using (var medPen = new Pen(Color.White, 2f))
                {
                    g.DrawLine(medPen, cx - boxWidth / 2, yMed, cx + boxWidth / 2, yMed);
                }

                // 4. Draw Outliers
                using (var outBrush = new SolidBrush(colors.Danger))
                {
                    float outRadius = 3f;
                    foreach (var o in pt.Outliers)
                    {
                        float oY = paddingTop + plotH - (float)((o - minVal) / range * plotH);
                        g.FillEllipse(outBrush, cx - outRadius, oY - outRadius, outRadius * 2, outRadius * 2);
                    }
                }

                // 5. Draw X Label
                using (var font = new Font("Segoe UI", 9f))
                using (var brush = new SolidBrush(colors.TextPrimary))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(pt.Label, font, brush, cx, paddingTop + plotH + 8, sf);
                }
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
    /// Legacy alias for BoxPlotChart.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroBoxPlotChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBoxPlotChart instead.")]
    [ToolboxItem(false)]
    public class ZeroBoxPlotChart : ZBoxPlotChart
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZBoxPlotChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("BoxPlotChart is deprecated and will be removed in 5 release cycles. Please migrate to ZBoxPlotChart instead.")]
    [ToolboxItem(false)]
    public class BoxPlotChart : ZBoxPlotChart
    {
    }

    #endregion
}
