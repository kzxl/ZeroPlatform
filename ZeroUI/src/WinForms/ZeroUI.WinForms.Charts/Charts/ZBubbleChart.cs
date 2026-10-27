using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Data point for <see cref="ZBubbleChart"/>.
    /// </summary>
    public class BubblePoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Size { get; set; }
        public string Label { get; set; } = string.Empty;
        public Color? Color { get; set; }
        public object? Tag { get; set; }

        public BubblePoint() { }
        public BubblePoint(double x, double y, double size, string label = "", Color? color = null)
        {
            X = x;
            Y = y;
            Size = size;
            Label = label;
            Color = color;
        }
    }

    /// <summary>
    /// Multi-dimensional bubble chart plotting (X, Y) positions with variable bubble sizes.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Scatter bubble chart with coordinate axes, gridlines, and variable bubble radii")]
    public class ZBubbleChart : Control
    {
        private List<BubblePoint> _dataSource = new List<BubblePoint>();
        private string _xAxisTitle = "X Axis";
        private string _yAxisTitle = "Y Axis";
        private bool _showLabels = true;
        private bool _showGridLines = true;
        private BubblePoint? _hoveredPoint;
        private ToolTip _toolTip = new ToolTip();

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("List of data bubbles to plot.")]
        public List<BubblePoint> DataSource
        {
            get => _dataSource;
            set
            {
                _dataSource = value ?? new List<BubblePoint>();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("X Axis")]
        public string XAxisTitle
        {
            get => _xAxisTitle;
            set { _xAxisTitle = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Y Axis")]
        public string YAxisTitle
        {
            get => _yAxisTitle;
            set { _yAxisTitle = value ?? string.Empty; Invalidate(); }
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
        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; Invalidate(); }
        }

        public ZBubbleChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(400, 300);
            BackColor = Color.Transparent;

            // Default demo data
            _dataSource.Add(new BubblePoint(10, 25, 15, "Node A", Color.FromArgb(52, 152, 219)));
            _dataSource.Add(new BubblePoint(35, 60, 30, "Node B", Color.FromArgb(46, 204, 113)));
            _dataSource.Add(new BubblePoint(55, 40, 22, "Node C", Color.FromArgb(241, 196, 15)));
            _dataSource.Add(new BubblePoint(80, 85, 45, "Node D", Color.FromArgb(231, 76, 60)));

            ZeroTheme.ThemeChanged += (s, e) => Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            var (plotRect, minX, maxX, minY, maxY, minSize, maxSize) = CalculateBounds();
            BubblePoint? hit = null;

            if (_dataSource.Count > 0 && plotRect.Contains(e.Location))
            {
                for (int i = _dataSource.Count - 1; i >= 0; i--)
                {
                    var p = _dataSource[i];
                    var (cx, cy, r) = MapPoint(p, plotRect, minX, maxX, minY, maxY, minSize, maxSize);
                    float dx = e.X - cx;
                    float dy = e.Y - cy;
                    if (dx * dx + dy * dy <= r * r)
                    {
                        hit = p;
                        break;
                    }
                }
            }

            if (hit != _hoveredPoint)
            {
                _hoveredPoint = hit;
                if (_hoveredPoint != null)
                {
                    string tipText = string.IsNullOrEmpty(_hoveredPoint.Label)
                        ? $"X: {_hoveredPoint.X:0.#}, Y: {_hoveredPoint.Y:0.#}, Size: {_hoveredPoint.Size:0.#}"
                        : $"{_hoveredPoint.Label}\nX: {_hoveredPoint.X:0.#}, Y: {_hoveredPoint.Y:0.#}, Size: {_hoveredPoint.Size:0.#}";
                    _toolTip.Show(tipText, this, e.X + 12, e.Y + 12, 2000);
                }
                else
                {
                    _toolTip.Hide(this);
                }
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredPoint = null;
            _toolTip.Hide(this);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var colors = ZeroTheme.Colors;
            var (plotRect, minX, maxX, minY, maxY, minSize, maxSize) = CalculateBounds();
            if (plotRect.Width <= 20 || plotRect.Height <= 20) return;

            // 1. Grid Lines
            if (_showGridLines)
            {
                using var gridPen = new Pen(colors.Border, 1f) { DashStyle = DashStyle.Dash };
                for (int i = 1; i <= 4; i++)
                {
                    float y = plotRect.Bottom - (plotRect.Height * (i / 5f));
                    g.DrawLine(gridPen, plotRect.Left, y, plotRect.Right, y);

                    float x = plotRect.Left + (plotRect.Width * (i / 5f));
                    g.DrawLine(gridPen, x, plotRect.Top, x, plotRect.Bottom);
                }
            }

            // 2. Axes
            using (var axisPen = new Pen(colors.BorderDefault, 1.5f))
            {
                g.DrawLine(axisPen, plotRect.Left, plotRect.Bottom, plotRect.Right, plotRect.Bottom); // X axis
                g.DrawLine(axisPen, plotRect.Left, plotRect.Top, plotRect.Left, plotRect.Bottom);    // Y axis
            }

            // 3. Axis Titles & Ticks
            var labelFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Regular);
            using (var textBrush = new SolidBrush(colors.TextSecondary))
            {
                // Y min / max
                g.DrawString(maxY.ToString("0.#"), labelFont, textBrush, 2, plotRect.Top - 6);
                g.DrawString(minY.ToString("0.#"), labelFont, textBrush, 2, plotRect.Bottom - 10);

                // X min / max
                g.DrawString(minX.ToString("0.#"), labelFont, textBrush, plotRect.Left - 4, plotRect.Bottom + 4);
                string maxXStr = maxX.ToString("0.#");
                var maxXSz = g.MeasureString(maxXStr, labelFont);
                g.DrawString(maxXStr, labelFont, textBrush, plotRect.Right - maxXSz.Width, plotRect.Bottom + 4);

                // Axis titles
                if (!string.IsNullOrEmpty(_xAxisTitle))
                {
                    var xTitleSz = g.MeasureString(_xAxisTitle, labelFont);
                    g.DrawString(_xAxisTitle, labelFont, textBrush, plotRect.Left + (plotRect.Width - xTitleSz.Width) / 2, Height - 16);
                }
            }

            // 4. Bubbles
            foreach (var point in _dataSource)
            {
                var (cx, cy, r) = MapPoint(point, plotRect, minX, maxX, minY, maxY, minSize, maxSize);
                Color baseColor = point.Color ?? colors.PrimaryAccent;
                bool isHovered = (point == _hoveredPoint);

                int alpha = isHovered ? 230 : 160;
                using var fillBrush = new SolidBrush(Color.FromArgb(alpha, baseColor));
                using var strokePen = new Pen(baseColor, isHovered ? 2.5f : 1.2f);

                float d = r * 2;
                g.FillEllipse(fillBrush, cx - r, cy - r, d, d);
                g.DrawEllipse(strokePen, cx - r, cy - r, d, d);

                if (_showLabels && !string.IsNullOrEmpty(point.Label) && r >= 10)
                {
                    var nameFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
                    var nameSz = g.MeasureString(point.Label, nameFont);
                    if (nameSz.Width <= d + 10)
                    {
                        using var nameBrush = new SolidBrush(colors.TextPrimary);
                        g.DrawString(point.Label, nameFont, nameBrush, cx - nameSz.Width / 2, cy - nameSz.Height / 2);
                    }
                }
            }
        }

        private (Rectangle plotRect, double minX, double maxX, double minY, double maxY, double minSize, double maxSize) CalculateBounds()
        {
            Rectangle plot = new Rectangle(40, 20, Width - 60, Height - 55);

            if (_dataSource.Count == 0)
                return (plot, 0, 100, 0, 100, 1, 50);

            double minX = _dataSource.Min(p => p.X);
            double maxX = _dataSource.Max(p => p.X);
            double minY = _dataSource.Min(p => p.Y);
            double maxY = _dataSource.Max(p => p.Y);
            double minSize = _dataSource.Min(p => p.Size);
            double maxSize = _dataSource.Max(p => p.Size);

            if (Math.Abs(maxX - minX) < double.Epsilon) { minX -= 10; maxX += 10; }
            if (Math.Abs(maxY - minY) < double.Epsilon) { minY -= 10; maxY += 10; }
            if (Math.Abs(maxSize - minSize) < double.Epsilon) { maxSize = minSize + 1; }

            return (plot, minX, maxX, minY, maxY, minSize, maxSize);
        }

        private (float cx, float cy, float radius) MapPoint(BubblePoint p, Rectangle plot, double minX, double maxX, double minY, double maxY, double minSize, double maxSize)
        {
            float cx = plot.Left + (float)((p.X - minX) / (maxX - minX) * plot.Width);
            float cy = plot.Bottom - (float)((p.Y - minY) / (maxY - minY) * plot.Height);

            const float minR = 6f;
            const float maxR = 28f;
            float r = minR + (float)((p.Size - minSize) / (maxSize - minSize) * (maxR - minR));

            return (cx, cy, r);
        }
    }

    [Obsolete("ZeroBubbleChart is deprecated. Use ZBubbleChart instead.")]
    [ToolboxItem(false)]
    public class ZeroBubbleChart : ZBubbleChart { }
}
