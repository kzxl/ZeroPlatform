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
    /// Cartesian 2D Scatter and 3D Bubble correlation chart with Ordinary Least Squares (OLS)
    /// linear regression trendlines and R² correlation coefficient in WPF.
    /// Thin WPF rendering layer consuming ZeroUI.Core.Analytics.ScatterPlotEngine.
    /// </summary>
    public class ZScatterChart : FrameworkElement
    {
        private readonly List<ScatterSeries> _series = new List<ScatterSeries>();

        private string _title = "Bivariate Correlation & Regression";
        private string _xAxisTitle = "X Dimension";
        private string _yAxisTitle = "Y Dimension";
        private bool _showRegressionLine = true;
        private bool _showGridLines = true;
        private double _markerSize = 8.0;

        private class WpfRenderPoint
        {
            public ScatterSeries Series { get; set; } = null!;
            public ScatterDataPoint Point { get; set; } = null!;
            public Point ScreenPoint { get; set; }
            public double Radius { get; set; }
            public Color Color { get; set; }
        }

        private readonly List<WpfRenderPoint> _renderedPoints = new List<WpfRenderPoint>();
        private WpfRenderPoint? _hoverPoint;

        public List<ScatterSeries> Series => _series;

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

        public bool ShowRegressionLine
        {
            get => _showRegressionLine;
            set { _showRegressionLine = value; InvalidateVisual(); }
        }

        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; InvalidateVisual(); }
        }

        public double MarkerSize
        {
            get => _markerSize;
            set { _markerSize = Math.Max(2.0, value); InvalidateVisual(); }
        }

        public ZScatterChart()
        {
            ClipToBounds = true;
            MinHeight = 220;
            MinWidth = 320;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
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

        public void LoadSampleData()
        {
            _series.Clear();

            var s1 = new ScatterSeries("Dataset Alpha", null, (uint)0xFF3B82F6)
            {
                ShowRegressionLine = true,
                IsBubble = true
            };
            var rand = new Random(77);
            for (int i = 0; i < 35; i++)
            {
                double x = 10.0 + (i * 2.2) + (rand.NextDouble() * 8.0 - 4.0);
                double y = 25.0 + (x * 0.85) + (rand.NextDouble() * 16.0 - 8.0);
                double size = 8.0 + (rand.NextDouble() * 18.0);
                s1.Points.Add(new ScatterDataPoint(x, y, size, $"Sample #{i + 1}"));
            }

            var s2 = new ScatterSeries("Benchmark Beta", null, (uint)0xFF10B981)
            {
                ShowRegressionLine = false,
                IsBubble = false
            };
            for (int i = 0; i < 20; i++)
            {
                double x = 20.0 + (i * 3.5);
                double y = 15.0 + (x * 0.6) + (rand.NextDouble() * 10.0 - 5.0);
                s2.Points.Add(new ScatterDataPoint(x, y, 7.0, $"Ref #{i + 1}"));
            }

            _series.Add(s1);
            _series.Add(s2);
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            WpfRenderPoint? newHover = null;

            for (int i = _renderedPoints.Count - 1; i >= 0; i--)
            {
                var pt = _renderedPoints[i];
                double dx = pos.X - pt.ScreenPoint.X;
                double dy = pos.Y - pt.ScreenPoint.Y;
                if ((dx * dx) + (dy * dy) <= (pt.Radius + 3.0) * (pt.Radius + 3.0))
                {
                    newHover = pt;
                    break;
                }
            }

            if (newHover != _hoverPoint)
            {
                _hoverPoint = newHover;
                if (_hoverPoint != null)
                {
                    string info = $"{_hoverPoint.Series.Name}\n{_hoverPoint.Point.Label ?? "Point"}: ({_hoverPoint.Point.X:F1}, {_hoverPoint.Point.Y:F1})";
                    if (_hoverPoint.Series.IsBubble) info += $"\nMagnitude: {_hoverPoint.Point.Size:F1}";
                    ToolTip = info;
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
            if (_hoverPoint != null)
            {
                _hoverPoint = null;
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

            var activeSeries = _series.Count > 0 ? _series : GetPreviewSeries();
            var bounds = ScatterPlotEngine.ComputeBounds(activeSeries);
            _renderedPoints.Clear();

            double leftMargin = 55.0;
            double rightMargin = 25.0;
            double bottomMargin = 45.0;
            double plotX = leftMargin;
            double plotY = topMargin;
            double plotW = Math.Max(10.0, w - leftMargin - rightMargin);
            double plotH = Math.Max(10.0, h - topMargin - bottomMargin);

            double spanX = Math.Max(1e-9, bounds.MaxX - bounds.MinX);
            double spanY = Math.Max(1e-9, bounds.MaxY - bounds.MinY);

            // Grids & Ticks
            int gridTicks = 5;
            for (int i = 0; i <= gridTicks; i++)
            {
                double ratio = (double)i / gridTicks;
                double y = plotY + plotH - (ratio * plotH);
                double valY = bounds.MinY + (ratio * spanY);

                if (_showGridLines && i > 0 && i < gridTicks)
                {
                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(plotX, y), new Point(plotX + plotW, y));
                }

                var tickYText = new FormattedText(
                    valY.ToString("F0"),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    9.5,
                    ZeroWpfTheme.TextSecondary,
                    1.0);
                dc.DrawText(tickYText, new Point(plotX - tickYText.Width - 6, y - (tickYText.Height / 2.0)));
            }

            for (int i = 0; i <= gridTicks; i++)
            {
                double ratio = (double)i / gridTicks;
                double x = plotX + (ratio * plotW);
                double valX = bounds.MinX + (ratio * spanX);

                if (_showGridLines && i > 0 && i < gridTicks)
                {
                    dc.DrawLine(ZeroWpfTheme.GridLinePen, new Point(x, plotY), new Point(x, plotY + plotH));
                }

                var tickXText = new FormattedText(
                    valX.ToString("F0"),
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    9.5,
                    ZeroWpfTheme.TextSecondary,
                    1.0);
                dc.DrawText(tickXText, new Point(x - (tickXText.Width / 2.0), plotY + plotH + 5.0));
            }

            Color[] defaultColors = {
                Color.FromRgb(59, 130, 246),
                Color.FromRgb(16, 185, 129),
                Color.FromRgb(249, 115, 22),
                Color.FromRgb(168, 85, 247)
            };

            // Draw Regression Lines
            int sIdx = 0;
            foreach (var s in activeSeries)
            {
                var baseColor = s.ColorRgba.ToColor(defaultColors[sIdx % defaultColors.Length]);
                sIdx++;

                if ((_showRegressionLine || s.ShowRegressionLine) && s.Points.Count >= 2)
                {
                    var reg = ScatterPlotEngine.ComputeLinearRegression(s.Points);
                    if (reg != null)
                    {
                        double x1 = plotX + ((reg.MinX - bounds.MinX) / spanX) * plotW;
                        double y1 = plotY + plotH - ((reg.StartY - bounds.MinY) / spanY) * plotH;
                        double x2 = plotX + ((reg.MaxX - bounds.MinX) / spanX) * plotW;
                        double y2 = plotY + plotH - ((reg.EndY - bounds.MinY) / spanY) * plotH;

                        var regPen = new Pen(new SolidColorBrush(Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B)), 1.8);
                        regPen.DashStyle = DashStyles.Dash;
                        regPen.Freeze();

                        dc.DrawLine(regPen, new Point(x1, y1), new Point(x2, y2));

                        var r2Text = new FormattedText(
                            $"R² = {reg.RSquared:F2}",
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            ZeroWpfTheme.RegularTypeface,
                            9.0,
                            new SolidColorBrush(baseColor),
                            1.0);
                        dc.DrawText(r2Text, new Point(x2 - r2Text.Width, y2 - 14.0));
                    }
                }
            }

            // Draw Scatter & Bubble Points
            sIdx = 0;
            foreach (var s in activeSeries)
            {
                var baseColor = s.ColorRgba.ToColor(defaultColors[sIdx % defaultColors.Length]);
                sIdx++;

                foreach (var pt in s.Points)
                {
                    double sx = plotX + ((pt.X - bounds.MinX) / spanX) * plotW;
                    double sy = plotY + plotH - ((pt.Y - bounds.MinY) / spanY) * plotH;

                    double radius = _markerSize / 2.0;
                    if (s.IsBubble && bounds.MaxSize > bounds.MinSize)
                    {
                        double sizeNorm = (pt.Size - bounds.MinSize) / (bounds.MaxSize - bounds.MinSize);
                        radius = 4.0 + sizeNorm * 18.0;
                    }

                    var renderPt = new WpfRenderPoint
                    {
                        Series = s,
                        Point = pt,
                        ScreenPoint = new Point(sx, sy),
                        Radius = radius,
                        Color = baseColor
                    };
                    _renderedPoints.Add(renderPt);

                    bool isHover = (_hoverPoint != null && _hoverPoint.Point == pt);
                    byte alpha = s.IsBubble ? (byte)160 : (byte)210;
                    if (isHover) alpha = (byte)Math.Min(255, alpha + 50);

                    var fillBrush = new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B));
                    fillBrush.Freeze();

                    var borderPen = new Pen(isHover ? Brushes.White : new SolidColorBrush(Color.FromArgb(220, baseColor.R, baseColor.G, baseColor.B)), isHover ? 2.0 : 1.0);
                    borderPen.Freeze();

                    dc.DrawEllipse(fillBrush, borderPen, new Point(sx, sy), radius, radius);
                }
            }

            // Draw Baseline Axis
            var axisPen = new Pen(ZeroWpfTheme.BorderDefault, 1.2);
            axisPen.Freeze();
            dc.DrawLine(axisPen, new Point(plotX, plotY + plotH), new Point(plotX + plotW, plotY + plotH));
            dc.DrawLine(axisPen, new Point(plotX, plotY), new Point(plotX, plotY + plotH));
        }

        private static List<ScatterSeries> GetPreviewSeries()
        {
            var list = new List<ScatterSeries>();
            var s = new ScatterSeries("Sample", null, (uint)0xFF3B82F6);
            var r = new Random(99);
            for (int i = 0; i < 25; i++)
            {
                double x = 10 + i * 3.5;
                double y = 15 + x * 0.9 + (r.NextDouble() * 12 - 6);
                s.Points.Add(new ScatterDataPoint(x, y, 10.0));
            }
            list.Add(s);
            return list;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZScatterChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ScatterChart is deprecated and will be removed in 5 release cycles. Please migrate to ZScatterChart instead.")]
    public class ScatterChart : ZScatterChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZScatterChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroScatterChart is deprecated and will be removed in 5 release cycles. Please migrate to ZScatterChart instead.")]
    public class ZeroScatterChart : ZScatterChart { }

    #endregion

}
