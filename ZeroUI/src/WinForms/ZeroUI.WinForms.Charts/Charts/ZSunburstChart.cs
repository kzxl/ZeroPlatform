using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Multi-tiered radial partition diagram (Sunburst) visualizing complex hierarchical tree proportions.
    /// Thin WinForms rendering layer consuming ZeroUI.Core.Analytics.SunburstLayoutEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ChartControl.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Hierarchical multi-tier radial sunburst partition chart")]
    public class ZSunburstChart : Control
    {
        private SunburstNode? _rootNode;
        private readonly ToolTip _toolTip = new ToolTip();

        private string _title = "Hierarchical Sunburst Breakdown";
        private double _innerHoleRatio = 0.25;
        private SunburstSector? _hoverSector;
        private IReadOnlyList<SunburstSector>? _lastSectors;
        private float _lastCenterX;
        private float _lastCenterY;

        public SunburstNode? RootNode
        {
            get => _rootNode;
            set { _rootNode = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Hierarchical Sunburst Breakdown")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(0.25)]
        [Description("Ratio of the central hole radius relative to the outer boundary")]
        public double InnerHoleRatio
        {
            get => _innerHoleRatio;
            set { _innerHoleRatio = Math.Max(0.05, Math.Min(0.7, value)); Invalidate(); }
        }

        public ZSunburstChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(480, 420);
            Font = new Font("Segoe UI", 9f);
        }

        public void LoadSampleData()
        {
            var root = new SunburstNode("Total Enterprise", 0);

            var sales = new SunburstNode("Sales", 0, (uint)Color.FromArgb(59, 130, 246).ToArgb());
            sales.Children.Add(new SunburstNode("Domestic", 180));
            sales.Children.Add(new SunburstNode("Global", 240));

            var tech = new SunburstNode("Technology", 0, (uint)Color.FromArgb(16, 185, 129).ToArgb());
            tech.Children.Add(new SunburstNode("Software", 140));
            tech.Children.Add(new SunburstNode("Hardware", 95));
            tech.Children.Add(new SunburstNode("Cloud", 165));

            var ops = new SunburstNode("Operations", 0, (uint)Color.FromArgb(249, 115, 22).ToArgb());
            ops.Children.Add(new SunburstNode("Logistics", 110));
            ops.Children.Add(new SunburstNode("Quality", 80));

            root.Children.Add(sales);
            root.Children.Add(tech);
            root.Children.Add(ops);

            _rootNode = root;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SunburstSector? newHover = null;

            if (_lastSectors != null)
            {
                for (int i = _lastSectors.Count - 1; i >= 0; i--)
                {
                    var s = _lastSectors[i];
                    if (s.Contains(e.X, e.Y, _lastCenterX, _lastCenterY))
                    {
                        newHover = s;
                        break;
                    }
                }
            }

            if (newHover != _hoverSector)
            {
                _hoverSector = newHover;
                if (_hoverSector != null && _rootNode != null)
                {
                    double total = _rootNode.GetEffectiveValue();
                    double val = _hoverSector.Node.GetEffectiveValue();
                    double pct = total > 0 ? (val / total) : 0;
                    _toolTip.SetToolTip(this, $"{_hoverSector.Node.Name}\nValue: {val:N0}\nShare: {pct:P1}\nDepth: L{_hoverSector.Depth}");
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
            if (_hoverSector != null)
            {
                _hoverSector = null;
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

            var root = _rootNode ?? GetPreviewRoot();
            float availW = Width - 30f;
            float availH = Height - topMargin - 20f;
            float maxR = Math.Min(availW, availH) / 2f;
            if (maxR < 20f) return;

            _lastCenterX = 15f + (availW / 2f);
            _lastCenterY = topMargin + (availH / 2f);

            _lastSectors = SunburstLayoutEngine.ComputeLayout(root, maxR, _innerHoleRatio);
            if (_lastSectors.Count == 0) return;

            Color[] palette = {
                Color.FromArgb(59, 130, 246),
                Color.FromArgb(16, 185, 129),
                Color.FromArgb(249, 115, 22),
                Color.FromArgb(168, 85, 247),
                Color.FromArgb(236, 72, 153),
                Color.FromArgb(14, 165, 233),
                Color.FromArgb(234, 179, 8)
            };

            var strokeColor = isDark ? Color.FromArgb(15, 23, 42) : Color.White;

            int sectorIdx = 0;
            foreach (var s in _lastSectors)
            {
                Color baseColor;
                if (s.Node.ColorRgba.HasValue)
                {
                    baseColor = Color.FromArgb((int)s.Node.ColorRgba.Value);
                }
                else
                {
                    baseColor = palette[sectorIdx % palette.Length];
                    if (s.Depth > 1)
                    {
                        // Slightly vary lightness for deeper levels
                        int shift = (s.Depth - 1) * 20;
                        baseColor = Color.FromArgb(
                            Math.Min(255, baseColor.R + shift),
                            Math.Min(255, baseColor.G + shift),
                            Math.Min(255, baseColor.B + shift));
                    }
                }
                sectorIdx++;

                bool isHover = (_hoverSector == s);
                if (isHover)
                {
                    baseColor = Color.FromArgb(Math.Min(255, baseColor.R + 30), Math.Min(255, baseColor.G + 30), Math.Min(255, baseColor.B + 30));
                }

                using (var path = CreateSectorPath(_lastCenterX, _lastCenterY, (float)s.InnerRadius, (float)s.OuterRadius, (float)s.StartAngle, (float)s.SweepAngle))
                using (var brush = new SolidBrush(baseColor))
                using (var pen = new Pen(strokeColor, 1.5f))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(pen, path);
                }

                // Sector text label if large enough
                if (s.SweepAngle >= 14.0 && (s.OuterRadius - s.InnerRadius) >= 16f)
                {
                    double midRad = (s.MidAngle * Math.PI) / 180.0;
                    double midR = s.MidRadius;
                    float tx = _lastCenterX + (float)(midR * Math.Cos(midRad));
                    float ty = _lastCenterY + (float)(midR * Math.Sin(midRad));

                    using (var labelFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold))
                    using (var labelBrush = new SolidBrush(Color.White))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString(s.Node.Name, labelFont, labelBrush, tx, ty, sf);
                    }
                }
            }

            // Center Hole Text
            using (var centerFont = new Font(Font.FontFamily, 9f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(textPrimary))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                string centerLabel = _hoverSector != null ? _hoverSector.Node.Name : root.Name;
                double centerVal = _hoverSector != null ? _hoverSector.Node.GetEffectiveValue() : root.GetEffectiveValue();
                g.DrawString($"{centerLabel}\n{centerVal:N0}", centerFont, textBrush, _lastCenterX, _lastCenterY, sf);
            }
        }

        private static GraphicsPath CreateSectorPath(float cx, float cy, float innerR, float outerR, float startAngle, float sweepAngle)
        {
            var path = new GraphicsPath();
            var rectOuter = new RectangleF(cx - outerR, cy - outerR, outerR * 2f, outerR * 2f);
            var rectInner = new RectangleF(cx - innerR, cy - innerR, innerR * 2f, innerR * 2f);

            path.AddArc(rectOuter, startAngle, sweepAngle);
            if (innerR > 1f)
            {
                path.AddArc(rectInner, startAngle + sweepAngle, -sweepAngle);
            }
            else
            {
                path.AddLine(cx, cy, cx, cy);
            }
            path.CloseFigure();
            return path;
        }

        private static SunburstNode GetPreviewRoot()
        {
            var root = new SunburstNode("Overview", 0);
            var a = new SunburstNode("North", 120);
            var b = new SunburstNode("South", 90);
            var c = new SunburstNode("East", 150);
            root.Children.Add(a);
            root.Children.Add(b);
            root.Children.Add(c);
            return root;
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
    /// Legacy alias for <see cref="ZSunburstChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SunburstChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSunburstChart instead.")]
    [ToolboxItem(false)]
    public class SunburstChart : ZSunburstChart
    {
    }

    #endregion
}
