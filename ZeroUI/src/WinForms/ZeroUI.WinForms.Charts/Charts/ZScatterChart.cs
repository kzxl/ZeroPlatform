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
    /// Cartesian 2D Scatter and 3D Bubble plot control with Ordinary Least Squares (OLS)
    /// linear regression trendlines and R² correlation coefficient.
    /// Thin WinForms rendering layer consuming ZeroUI.Core.Analytics.ScatterPlotEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ChartControl.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Cartesian 2D scatter and bubble correlation chart with linear regression trendlines")]
    public class ZScatterChart : Control
    {
        private readonly List<ScatterSeries> _series = new List<ScatterSeries>();
        private readonly ToolTip _toolTip = new ToolTip();

        private string _title = "Bivariate Correlation & Regression";
        private string _xAxisTitle = "X Dimension";
        private string _yAxisTitle = "Y Dimension";
        private bool _showRegressionLine = true;
        private bool _showGridLines = true;
        private float _markerSize = 8f;

        private class RenderPoint
        {
            public ScatterSeries Series { get; set; } = null!;
            public ScatterDataPoint Point { get; set; } = null!;
            public float ScreenX { get; set; }
            public float ScreenY { get; set; }
            public float Radius { get; set; }
            public Color Color { get; set; }
        }

        private readonly List<RenderPoint> _renderedPoints = new List<RenderPoint>();
        private RenderPoint? _hoverPoint;

        public List<ScatterSeries> Series => _series;

        [Category("Appearance")]
        [DefaultValue("Bivariate Correlation & Regression")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("X Dimension")]
        public string XAxisTitle
        {
            get => _xAxisTitle;
            set { _xAxisTitle = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Y Dimension")]
        public string YAxisTitle
        {
            get => _yAxisTitle;
            set { _yAxisTitle = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowRegressionLine
        {
            get => _showRegressionLine;
            set { _showRegressionLine = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(8f)]
        public float MarkerSize
        {
            get => _markerSize;
            set { _markerSize = Math.Max(2f, value); Invalidate(); }
        }

        public ZScatterChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(560, 340);
            Font = new Font("Segoe UI", 9f);
        }

        public void LoadSampleData()
        {
            _series.Clear();

            var s1 = new ScatterSeries("Dataset Alpha", null, (uint)Color.FromArgb(59, 130, 246).ToArgb())
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

            var s2 = new ScatterSeries("Benchmark Beta", null, (uint)Color.FromArgb(16, 185, 129).ToArgb())
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
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            RenderPoint? newHover = null;

            for (int i = _renderedPoints.Count - 1; i >= 0; i--)
            {
                var pt = _renderedPoints[i];
                float dx = e.X - pt.ScreenX;
                float dy = e.Y - pt.ScreenY;
                if ((dx * dx) + (dy * dy) <= (pt.Radius + 3f) * (pt.Radius + 3f))
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
                    _toolTip.SetToolTip(this, info);
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                }
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverPoint != null)
            {
                _hoverPoint = null;
                _toolTip.SetToolTip(this, null);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            var bgColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(255, 255, 255);
            var textPrimary = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(30, 41, 59);
            var textSecondary = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            var gridColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            var axisColor = isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);

            g.Clear(bgColor);

            // Title
            float topMargin = 38f;
            if (!string.IsNullOrEmpty(_title))
            {
                using (var titleFont = new Font(Font.FontFamily, 10.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(textPrimary))
                {
                    g.DrawString(_title, titleFont, brush, 16f, 10f);
                }
            }

            var activeSeries = _series.Count > 0 ? _series : GetPreviewSeries();
            var bounds = ScatterPlotEngine.ComputeBounds(activeSeries);
            _renderedPoints.Clear();

            float leftMargin = 55f;
            float rightMargin = 25f;
            float bottomMargin = 45f;
            float plotX = leftMargin;
            float plotY = topMargin;
            float plotWidth = Math.Max(10f, Width - leftMargin - rightMargin);
            float plotHeight = Math.Max(10f, Height - topMargin - bottomMargin);

            double spanX = Math.Max(1e-9, bounds.MaxX - bounds.MinX);
            double spanY = Math.Max(1e-9, bounds.MaxY - bounds.MinY);

            // Draw Grids & Ticks
            int gridTicks = 5;
            using (var gridPen = new Pen(gridColor, 1f) { DashStyle = DashStyle.Dash })
            using (var textBrush = new SolidBrush(textSecondary))
            using (var smallFont = new Font(Font.FontFamily, 8f))
            {
                // Horizontal Grids & Y-Ticks
                for (int i = 0; i <= gridTicks; i++)
                {
                    float ratio = (float)i / gridTicks;
                    float y = plotY + plotHeight - (ratio * plotHeight);
                    double val = bounds.MinY + (ratio * spanY);

                    if (_showGridLines && i > 0 && i < gridTicks)
                    {
                        g.DrawLine(gridPen, plotX, y, plotX + plotWidth, y);
                    }
                    g.DrawString(val.ToString("F0"), smallFont, textBrush, plotX - 42f, y - 7f);
                }

                // Vertical Grids & X-Ticks
                for (int i = 0; i <= gridTicks; i++)
                {
                    float ratio = (float)i / gridTicks;
                    float x = plotX + (ratio * plotWidth);
                    double val = bounds.MinX + (ratio * spanX);

                    if (_showGridLines && i > 0 && i < gridTicks)
                    {
                        g.DrawLine(gridPen, x, plotY, x, plotY + plotHeight);
                    }
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                    {
                        g.DrawString(val.ToString("F0"), smallFont, textBrush, x, plotY + plotHeight + 6f, sf);
                    }
                }

                // Axis Titles
                if (!string.IsNullOrEmpty(_yAxisTitle))
                {
                    var state = g.Save();
                    g.TranslateTransform(12f, plotY + (plotHeight / 2f));
                    g.RotateTransform(-90f);
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                    {
                        g.DrawString(_yAxisTitle, smallFont, textBrush, 0, 0, sf);
                    }
                    g.Restore(state);
                }

                if (!string.IsNullOrEmpty(_xAxisTitle))
                {
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                    {
                        g.DrawString(_xAxisTitle, smallFont, textBrush, plotX + (plotWidth / 2f), plotY + plotHeight + 24f, sf);
                    }
                }
            }

            // Draw Regression Lines
            Color[] defaultColors = {
                Color.FromArgb(59, 130, 246),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(249, 115, 22),
                Color.FromArgb(168, 85, 247)
            };

            int sIndex = 0;
            foreach (var s in activeSeries)
            {
                var baseColor = s.ColorRgba.ToColor(defaultColors[sIndex % defaultColors.Length]);
                sIndex++;

                if ((_showRegressionLine || s.ShowRegressionLine) && s.Points.Count >= 2)
                {
                    var reg = ScatterPlotEngine.ComputeLinearRegression(s.Points);
                    if (reg != null)
                    {
                        float x1 = plotX + (float)((reg.MinX - bounds.MinX) / spanX) * plotWidth;
                        float y1 = plotY + plotHeight - (float)((reg.StartY - bounds.MinY) / spanY) * plotHeight;
                        float x2 = plotX + (float)((reg.MaxX - bounds.MinX) / spanX) * plotWidth;
                        float y2 = plotY + plotHeight - (float)((reg.EndY - bounds.MinY) / spanY) * plotHeight;

                        using (var regPen = new Pen(Color.FromArgb(180, baseColor), 2f) { DashStyle = DashStyle.Dash })
                        {
                            g.DrawLine(regPen, x1, y1, x2, y2);
                        }

                        // Regression equation caption
                        using (var smallFont = new Font(Font.FontFamily, 7.5f))
                        using (var regBrush = new SolidBrush(baseColor))
                        {
                            g.DrawString($"R² = {reg.RSquared:F2}", smallFont, regBrush, x2 - 45f, y2 - 14f);
                        }
                    }
                }
            }

            // Draw Scatter & Bubble Points
            sIndex = 0;
            foreach (var s in activeSeries)
            {
                var baseColor = s.ColorRgba.ToColor(defaultColors[sIndex % defaultColors.Length]);
                sIndex++;

                foreach (var pt in s.Points)
                {
                    float sx = plotX + (float)((pt.X - bounds.MinX) / spanX) * plotWidth;
                    float sy = plotY + plotHeight - (float)((pt.Y - bounds.MinY) / spanY) * plotHeight;

                    float radius = _markerSize / 2f;
                    if (s.IsBubble && bounds.MaxSize > bounds.MinSize)
                    {
                        float sizeNorm = (float)((pt.Size - bounds.MinSize) / (bounds.MaxSize - bounds.MinSize));
                        radius = 4f + sizeNorm * 18f;
                    }

                    var renderPt = new RenderPoint
                    {
                        Series = s,
                        Point = pt,
                        ScreenX = sx,
                        ScreenY = sy,
                        Radius = radius,
                        Color = baseColor
                    };
                    _renderedPoints.Add(renderPt);

                    bool isHover = (_hoverPoint != null && _hoverPoint.Point == pt);
                    int alpha = s.IsBubble ? 160 : 210;
                    if (isHover) alpha = Math.Min(255, alpha + 50);

                    using (var brush = new SolidBrush(Color.FromArgb(alpha, baseColor)))
                    using (var borderPen = new Pen(isHover ? Color.White : Color.FromArgb(230, baseColor), isHover ? 2f : 1f))
                    {
                        g.FillEllipse(brush, sx - radius, sy - radius, radius * 2f, radius * 2f);
                        g.DrawEllipse(borderPen, sx - radius, sy - radius, radius * 2f, radius * 2f);
                    }
                }
            }

            // Draw Baseline Axis
            using (var axisPen = new Pen(axisColor, 1.2f))
            {
                g.DrawLine(axisPen, plotX, plotY + plotHeight, plotX + plotWidth, plotY + plotHeight);
                g.DrawLine(axisPen, plotX, plotY, plotX, plotY + plotHeight);
            }
        }

        private static List<ScatterSeries> GetPreviewSeries()
        {
            var list = new List<ScatterSeries>();
            var s = new ScatterSeries("Sample", null, (uint)Color.FromArgb(59, 130, 246).ToArgb());
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZScatterChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ScatterChart is deprecated and will be removed in 5 release cycles. Please migrate to ZScatterChart instead.")]
    [ToolboxItem(false)]
    public class ScatterChart : ZScatterChart
    {
    }

    #endregion
}
