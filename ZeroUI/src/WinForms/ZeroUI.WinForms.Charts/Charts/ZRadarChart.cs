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
    /// Represents a data series in a radar or spider chart.
    /// </summary>
    public class RadarSeries
    {
        public string Name { get; set; } = string.Empty;
        public Color Color { get; set; } = Color.FromArgb(79, 70, 229);
        public List<double> Values { get; } = new List<double>();
        public bool Filled { get; set; } = true;
        public int FillAlpha { get; set; } = 45;
        public int LineWidth { get; set; } = 2;

        public RadarSeries(string name, Color color, IEnumerable<double>? values = null)
        {
            Name = name;
            Color = color;
            if (values != null) Values.AddRange(values);
        }
    }

    /// <summary>
    /// High-performance multi-axis Radar / Spider Chart control for multi-dimensional performance benchmarking.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroRadarChart.bmp")]
    public class ZRadarChart : Control
    {
        private readonly List<string> _axes = new List<string>();
        private readonly List<RadarSeries> _series = new List<RadarSeries>();

        private double _maxValue = 100.0;
        private int _webRings = 5;
        private bool _showLabels = true;
        private bool _showLegend = true;

        private Point? _hoverPoint;
        private string? _hoverText;

        public ZRadarChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(400, 320);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        [Category("Appearance")]
        [DefaultValue(100.0)]
        public double MaxValue
        {
            get => _maxValue;
            set { _maxValue = Math.Max(1.0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(5)]
        public int WebRings
        {
            get => _webRings;
            set { _webRings = Math.Max(2, Math.Min(10, value)); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowLabels
        {
            get => _showLabels;
            set { _showLabels = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowLegend
        {
            get => _showLegend;
            set { _showLegend = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<string> Axes => _axes;

        [Browsable(false)]
        public List<RadarSeries> Series => _series;

        public void SetAxes(params string[] axes)
        {
            _axes.Clear();
            _axes.AddRange(axes);
            Invalidate();
        }

        public RadarSeries AddSeries(string name, Color color, params double[] values)
        {
            var s = new RadarSeries(name, color, values);
            _series.Add(s);
            Invalidate();
            return s;
        }

        public void ClearSeries()
        {
            _series.Clear();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_axes.Count < 3 || _series.Count == 0) return;

            int legendH = _showLegend ? 32 : 0;
            Rectangle chartArea = new Rectangle(0, legendH, Width, Height - legendH);
            float cx = chartArea.Left + chartArea.Width / 2f;
            float cy = chartArea.Top + chartArea.Height / 2f;
            float radius = Math.Min(chartArea.Width, chartArea.Height) / 2f - 40f;
            if (radius <= 20) return;

            Point? matchedPoint = null;
            string? matchedText = null;

            int axisCount = _axes.Count;
            for (int sIdx = 0; sIdx < _series.Count; sIdx++)
            {
                var s = _series[sIdx];
                for (int a = 0; a < axisCount; a++)
                {
                    double val = a < s.Values.Count ? s.Values[a] : 0.0;
                    double r = Math.Min(1.0, val / _maxValue) * radius;
                    double angle = -Math.PI / 2 + a * (2 * Math.PI / axisCount);
                    float px = (float)(cx + r * Math.Cos(angle));
                    float py = (float)(cy + r * Math.Sin(angle));

                    float dx = e.X - px;
                    float dy = e.Y - py;
                    if (dx * dx + dy * dy <= 64) // 8px hit radius
                    {
                        matchedPoint = new Point((int)px, (int)py);
                        matchedText = $"{s.Name}\n{_axes[a]}: {val:F1} / {_maxValue:F0}";
                        break;
                    }
                }
                if (matchedPoint.HasValue) break;
            }

            if (_hoverPoint != matchedPoint || _hoverText != matchedText)
            {
                _hoverPoint = matchedPoint;
                _hoverText = matchedText;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverPoint.HasValue)
            {
                _hoverPoint = null;
                _hoverText = null;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            // Optional Legend on Top
            int legendH = 0;
            if (_showLegend && _series.Count > 0)
            {
                legendH = 32;
                float curX = 16f;
                using var textBrush = new SolidBrush(palette.TextSecondary);
                var font = ZeroFontCache.Get(8.5f, FontStyle.Bold);

                foreach (var s in _series)
                {
                    using var sBrush = new SolidBrush(s.Color);
                    g.FillEllipse(sBrush, curX, 10, 10, 10);
                    curX += 14;

                    var sz = g.MeasureString(s.Name, font);
                    g.DrawString(s.Name, font, textBrush, curX, 8);
                    curX += sz.Width + 18;
                }
            }

            if (_axes.Count < 3)
            {
                using var noteBrush = new SolidBrush(palette.TextSecondary);
                g.DrawString("Add at least 3 axes to visualize radar chart.", Font, noteBrush, ClientRectangle, ZeroStringFormats.Center);
                return;
            }

            Rectangle chartArea = new Rectangle(0, legendH, Width, Height - legendH);
            float cx = chartArea.Left + chartArea.Width / 2f;
            float cy = chartArea.Top + chartArea.Height / 2f;
            float radius = Math.Min(chartArea.Width, chartArea.Height) / 2f - 40f;
            if (radius <= 20) return;

            int axisCount = _axes.Count;

            // 1. Draw Concentric Web Rings
            Color webColor = ZeroTheme.IsDark ? Color.FromArgb(45, 55, 72) : Color.FromArgb(226, 232, 240);
            Color radialColor = ZeroTheme.IsDark ? Color.FromArgb(60, 70, 90) : Color.FromArgb(203, 213, 225);

            using (var ringPen = new Pen(webColor, 1f) { DashStyle = DashStyle.Dash })
            using (var labelBrush = new SolidBrush(palette.TextSecondary))
            {
                var ringFont = ZeroFontCache.Get(7.5f, FontStyle.Regular);
                for (int ring = 1; ring <= _webRings; ring++)
                {
                    float r = radius * ring / _webRings;
                    var ringPoints = new PointF[axisCount];
                    for (int a = 0; a < axisCount; a++)
                    {
                        double angle = -Math.PI / 2 + a * (2 * Math.PI / axisCount);
                        ringPoints[a] = new PointF(
                            (float)(cx + r * Math.Cos(angle)),
                            (float)(cy + r * Math.Sin(angle)));
                    }
                    g.DrawPolygon(ringPen, ringPoints);

                    // Draw ring value label along top axis
                    double ringVal = _maxValue * ring / _webRings;
                    g.DrawString($"{ringVal:F0}", ringFont, labelBrush, cx + 3, cy - r - 2);
                }
            }

            // 2. Draw Radial Spokes & Axis Labels
            using (var radialPen = new Pen(radialColor, 1f))
            using (var axisBrush = new SolidBrush(palette.TextPrimary))
            {
                var axisFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
                for (int a = 0; a < axisCount; a++)
                {
                    double angle = -Math.PI / 2 + a * (2 * Math.PI / axisCount);
                    float ex = (float)(cx + radius * Math.Cos(angle));
                    float ey = (float)(cy + radius * Math.Sin(angle));
                    g.DrawLine(radialPen, cx, cy, ex, ey);

                    if (_showLabels)
                    {
                        float lx = (float)(cx + (radius + 18) * Math.Cos(angle));
                        float ly = (float)(cy + (radius + 18) * Math.Sin(angle));

                        string axisName = _axes[a];
                        var sz = g.MeasureString(axisName, axisFont);

                        float drawX = lx - sz.Width / 2f;
                        float drawY = ly - sz.Height / 2f;

                        // Clamp to bounds
                        drawX = Math.Max(4, Math.Min(Width - sz.Width - 4, drawX));
                        drawY = Math.Max(legendH + 2, Math.Min(Height - sz.Height - 4, drawY));

                        g.DrawString(axisName, axisFont, axisBrush, drawX, drawY);
                    }
                }
            }

            // 3. Draw Series Polygons
            using (var vertexPen = new Pen(palette.Surface, 1.5f))
            {
                for (int sIdx = 0; sIdx < _series.Count; sIdx++)
                {
                    var s = _series[sIdx];
                    var polyPoints = new PointF[axisCount];

                    for (int a = 0; a < axisCount; a++)
                    {
                        double val = a < s.Values.Count ? s.Values[a] : 0.0;
                        double r = Math.Min(1.0, Math.Max(0.0, val / _maxValue)) * radius;
                        double angle = -Math.PI / 2 + a * (2 * Math.PI / axisCount);
                        polyPoints[a] = new PointF(
                            (float)(cx + r * Math.Cos(angle)),
                            (float)(cy + r * Math.Sin(angle)));
                    }

                    if (s.Filled)
                    {
                        using var fillBrush = new SolidBrush(Color.FromArgb(s.FillAlpha, s.Color));
                        g.FillPolygon(fillBrush, polyPoints);
                    }

                    using (var linePen = new Pen(s.Color, s.LineWidth))
                    {
                        g.DrawPolygon(linePen, polyPoints);
                    }

                    // Draw Vertex Markers
                    using var vertexBrush = new SolidBrush(s.Color);
                    for (int a = 0; a < axisCount; a++)
                    {
                        g.FillEllipse(vertexBrush, polyPoints[a].X - 3.5f, polyPoints[a].Y - 3.5f, 7f, 7f);
                        g.DrawEllipse(vertexPen, polyPoints[a].X - 3.5f, polyPoints[a].Y - 3.5f, 7f, 7f);
                    }
                }
            }

            // 4. Hover Tooltip
            if (_hoverPoint.HasValue && !string.IsNullOrEmpty(_hoverText))
            {
                var pt = _hoverPoint.Value;
                var tipFont = ZeroFontCache.Get(8.5f, FontStyle.Regular);
                var tipSize = g.MeasureString(_hoverText, tipFont);
                float boxW = tipSize.Width + 16;
                float boxH = tipSize.Height + 10;
                float boxX = pt.X + 10;
                float boxY = pt.Y - boxH - 4;

                if (boxX + boxW > Width - 8) boxX = pt.X - boxW - 8;
                if (boxY < legendH + 4) boxY = pt.Y + 12;

                var boxRect = new RectangleF(boxX, boxY, boxW, boxH);
                using var bgBrush = new SolidBrush(Color.FromArgb(230, 15, 23, 42));
                using var borderPen = new Pen(Color.FromArgb(100, 148, 163, 184), 1f);
                using var textBrush = new SolidBrush(Color.White);

                g.FillRectangle(bgBrush, boxRect);
                g.DrawRectangle(borderPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);
                g.DrawString(_hoverText, tipFont, textBrush, boxX + 8, boxY + 5);

                // Highlight active vertex
                using var ringPen = new Pen(Color.White, 2f);
                g.DrawEllipse(ringPen, pt.X - 5, pt.Y - 5, 10, 10);
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
    /// Legacy alias for <see cref="ZRadarChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroRadarChart is deprecated and will be removed in 5 release cycles. Please migrate to ZRadarChart instead.")]
    [ToolboxItem(false)]
    public class ZeroRadarChart : ZRadarChart
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZRadarChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RadarChart is deprecated and will be removed in 5 release cycles. Please migrate to ZRadarChart instead.")]
    [ToolboxItem(false)]
    public class RadarChart : ZRadarChart
    {
    }

    #endregion
}
