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
    /// Continuous statistical distribution histogram control with Sturges binning
    /// and parametric Gaussian normal distribution bell curve overlay in WPF.
    /// Thin WPF rendering layer consuming ZeroUI.Core.Analytics.HistogramEngine.
    /// </summary>
    public class ZHistogramChart : FrameworkElement
    {
        private readonly List<double> _data = new List<double>();
        private int _binCount = 0;
        private bool _showNormalCurve = true;
        private bool _showGridLines = true;
        private string _title = "Statistical Distribution Histogram";
        private string _xAxisTitle = "Value";
        private string _yAxisTitle = "Frequency";
        private Color _barColor = Color.FromRgb(59, 130, 246);
        private Color _curveColor = Color.FromRgb(239, 68, 68);

        private int _hoverBinIndex = -1;
        private readonly List<Rect> _binRects = new List<Rect>();
        private HistogramResult? _lastResult;

        public List<double> Data => _data;

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public string XAxisTitle
        {
            get => _xAxisTitle;
            set { _xAxisTitle = value ?? string.Empty; InvalidateVisual(); }
        }

        public string YAxisTitle
        {
            get => _yAxisTitle;
            set { _yAxisTitle = value ?? string.Empty; InvalidateVisual(); }
        }

        public int BinCount
        {
            get => _binCount;
            set { _binCount = Math.Max(0, value); InvalidateVisual(); }
        }

        public bool ShowNormalCurve
        {
            get => _showNormalCurve;
            set { _showNormalCurve = value; InvalidateVisual(); }
        }

        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; InvalidateVisual(); }
        }

        public Color BarColor
        {
            get => _barColor;
            set { _barColor = value; InvalidateVisual(); }
        }

        public Color CurveColor
        {
            get => _curveColor;
            set { _curveColor = value; InvalidateVisual(); }
        }

        public ZHistogramChart()
        {
            ClipToBounds = true;
            MinHeight = 220;
            MinWidth = 320;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void SetData(IEnumerable<double> values)
        {
            _data.Clear();
            if (values != null) _data.AddRange(values);
            InvalidateVisual();
        }

        public void LoadSampleData()
        {
            _data.Clear();
            var rand = new Random(42);
            for (int i = 0; i < 300; i++)
            {
                double u1 = 1.0 - rand.NextDouble();
                double u2 = 1.0 - rand.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                _data.Add(50.0 + 8.5 * randStdNormal);
            }
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            int prevHover = _hoverBinIndex;
            _hoverBinIndex = -1;

            for (int i = 0; i < _binRects.Count; i++)
            {
                if (_binRects[i].Contains(pos))
                {
                    _hoverBinIndex = i;
                    break;
                }
            }

            if (prevHover != _hoverBinIndex)
            {
                if (_hoverBinIndex >= 0 && _lastResult != null && _hoverBinIndex < _lastResult.Bins.Count)
                {
                    var bin = _lastResult.Bins[_hoverBinIndex];
                    ToolTip = $"Bin [{bin.LowerBound:F1} - {bin.UpperBound:F1}]\nCount: {bin.Count}\nShare: {bin.RelativeFrequency:P1}";
                }
                else
                {
                    ToolTip = null;
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverBinIndex != -1)
            {
                _hoverBinIndex = -1;
                ToolTip = null;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));

            // Title
            double topMargin = 36.0;
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
                dc.DrawText(titleText, new Point(14, 8));
            }

            var activeData = _data.Count > 0 ? _data : GetPreviewData();
            _lastResult = HistogramEngine.Compute(activeData, _binCount);
            _binRects.Clear();

            if (_lastResult.Bins.Count == 0) return;

            double leftMargin = 50.0;
            double rightMargin = 25.0;
            double bottomMargin = 40.0;
            double plotX = leftMargin;
            double plotY = topMargin;
            double plotW = Math.Max(10.0, w - leftMargin - rightMargin);
            double plotH = Math.Max(10.0, h - topMargin - bottomMargin);

            int maxCount = Math.Max(1, _lastResult.MaxBinCount);
            int yAxisMax = (int)Math.Ceiling(maxCount * 1.15);

            // Grids & Y-Ticks
            int ySteps = 4;
            for (int s = 0; s <= ySteps; s++)
            {
                double ratio = (double)s / ySteps;
                double y = plotY + plotH - (ratio * plotH);
                int val = (int)(ratio * yAxisMax);

                if (_showGridLines && s > 0)
                {
                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(plotX, y), new Point(plotX + plotW, y));
                }

                var tickText = new FormattedText(
                    val.ToString(),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    10,
                    ZeroWpfTheme.TextSecondary,
                    1.0);
                dc.DrawText(tickText, new Point(plotX - tickText.Width - 6, y - (tickText.Height / 2.0)));
            }

            // Draw Histogram Bins
            int k = _lastResult.Bins.Count;
            double colWidth = plotW / k;

            for (int i = 0; i < k; i++)
            {
                var bin = _lastResult.Bins[i];
                double barH = (double)bin.Count / yAxisMax * plotH;
                double barX = plotX + i * colWidth;
                double barY = plotY + plotH - barH;
                var rect = new Rect(barX + 1.5, barY, Math.Max(1.0, colWidth - 3.0), barH);
                _binRects.Add(rect);

                bool isHover = (i == _hoverBinIndex);
                Color curBarColor = isHover
                    ? Color.FromArgb(255, (byte)Math.Min(255, _barColor.R + 40), (byte)Math.Min(255, _barColor.G + 40), (byte)Math.Min(255, _barColor.B + 40))
                    : _barColor;

                var brush = new SolidColorBrush(curBarColor);
                brush.Freeze();
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), 1.0);
                pen.Freeze();

                dc.DrawRectangle(brush, pen, rect);

                // X-Axis tick label
                if (k <= 10 || i % 2 == 0 || i == k - 1)
                {
                    var xTickText = new FormattedText(
                        bin.LowerBound.ToString("F1"),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface,
                        9.5,
                        ZeroWpfTheme.TextSecondary,
                        1.0);
                    dc.DrawText(xTickText, new Point(barX - (xTickText.Width / 2.0), plotY + plotH + 5.0));
                }
                if (i == k - 1)
                {
                    var lastTickText = new FormattedText(
                        bin.UpperBound.ToString("F1"),
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface,
                        9.5,
                        ZeroWpfTheme.TextSecondary,
                        1.0);
                    dc.DrawText(lastTickText, new Point(barX + colWidth - (lastTickText.Width / 2.0), plotY + plotH + 5.0));
                }
            }

            // Draw Gaussian Bell Curve
            if (_showNormalCurve && _lastResult.GaussianCurve.Count > 1)
            {
                double minX = _lastResult.Min;
                double maxX = _lastResult.Max;
                double spanX = Math.Max(1e-9, maxX - minX);

                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    bool first = true;
                    foreach (var cp in _lastResult.GaussianCurve)
                    {
                        double cx = plotX + ((cp.X - minX) / spanX) * plotW;
                        double cy = plotY + plotH - (cp.ScaledCount / yAxisMax * plotH);
                        cy = Math.Max(plotY, Math.Min(plotY + plotH, cy));

                        if (cx >= plotX && cx <= plotX + plotW)
                        {
                            var pt = new Point(cx, cy);
                            if (first)
                            {
                                ctx.BeginFigure(pt, false, false);
                                first = false;
                            }
                            else
                            {
                                ctx.LineTo(pt, true, true);
                            }
                        }
                    }
                }
                geom.Freeze();

                var curvePen = new Pen(new SolidColorBrush(_curveColor), 2.0);
                curvePen.Freeze();
                dc.DrawGeometry(null, curvePen, geom);
            }

            // Draw Baseline Axis
            var axisPen = new Pen(ZeroWpfTheme.BorderDefault, 1.2);
            axisPen.Freeze();
            dc.DrawLine(axisPen, new Point(plotX, plotY + plotH), new Point(plotX + plotW, plotY + plotH));
            dc.DrawLine(axisPen, new Point(plotX, plotY), new Point(plotX, plotY + plotH));
        }

        private static List<double> GetPreviewData()
        {
            var list = new List<double>();
            var r = new Random(123);
            for (int i = 0; i < 150; i++)
            {
                double u1 = 1.0 - r.NextDouble();
                double u2 = 1.0 - r.NextDouble();
                double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                list.Add(50.0 + 10.0 * normal);
            }
            return list;
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
    /// Legacy alias for <see cref="ZHistogramChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HistogramChart is deprecated and will be removed in 5 release cycles. Please migrate to ZHistogramChart instead.")]
    public class HistogramChart : ZHistogramChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZHistogramChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroHistogramChart is deprecated and will be removed in 5 release cycles. Please migrate to ZHistogramChart instead.")]
    public class ZeroHistogramChart : ZHistogramChart { }

    #endregion

}
