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
    /// Interactive Sankey diagram for visualizing energy, cost, material, and process flows.
    /// Thin WinForms View layer rendering topology and Bézier flow ribbons computed by ZeroUI.Core.Analytics.SankeyLayoutEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "SankeyChart.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Sankey diagram for flow and distribution analytics")]
    public class ZSankeyChart : Control
    {
        private readonly List<SankeyNode> _nodes = new List<SankeyNode>();
        private readonly List<SankeyLink> _links = new List<SankeyLink>();
        private readonly ToolTip _toolTip = new ToolTip();

        private string _title = "Process & Material Flow Distribution (Sankey)";
        private string _valueSuffix = " MWh";
        private int _nodeWidth = 18;
        private int _hoverLinkIndex = -1;
        private string? _hoverNodeName = null;
        private SankeyLayoutResult? _lastLayout = null;

        public List<SankeyNode> Nodes => _nodes;
        public List<SankeyLink> Links => _links;

        [Category("Appearance")]
        [DefaultValue("Process & Material Flow Distribution (Sankey)")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(" MWh")]
        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? ""; Invalidate(); }
        }

        public ZSankeyChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(620, 360);
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.Transparent;

            _toolTip.InitialDelay = 150;
            _toolTip.ReshowDelay = 50;

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public SankeyNode AddNode(string name, Color color, int column = 0)
        {
            var node = new SankeyNode(name, column, (uint)color.ToArgb());
            _nodes.Add(node);
            Invalidate();
            return node;
        }

        public SankeyNode AddNode(string name, int column = 0)
        {
            var node = new SankeyNode(name, column);
            _nodes.Add(node);
            Invalidate();
            return node;
        }

        public SankeyLink AddLink(string source, string target, double value, Color? color = null)
        {
            var link = new SankeyLink(source, target, value, color.HasValue ? (uint?)color.Value.ToArgb() : null);
            _links.Add(link);
            Invalidate();
            return link;
        }

        public void Clear()
        {
            _nodes.Clear();
            _links.Clear();
            _lastLayout = null;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            Color bgColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
            Color textColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color mutedColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            g.Clear(bgColor);

            int titleHeight = string.IsNullOrEmpty(_title) ? 6 : 30;
            if (!string.IsNullOrEmpty(_title))
            {
                using var titleFont = new Font(Font.FontFamily, 9.5f, FontStyle.Bold);
                using var titleBrush = new SolidBrush(textColor);
                g.DrawString(_title, titleFont, titleBrush, 10, 8);
            }

            var plotRect = new Rectangle(50, titleHeight + 10, Width - 100, Height - titleHeight - 30);
            if (plotRect.Width <= 50 || plotRect.Height <= 50 || _nodes.Count == 0)
            {
                using var muted = new SolidBrush(Color.Gray);
                g.DrawString("No sankey flow data available.", Font, muted, 12, titleHeight + 10);
                return;
            }

            // Compute layout via Core SankeyLayoutEngine
            _lastLayout = SankeyLayoutEngine.Compute(_nodes, _links);
            if (_lastLayout.Nodes.Count == 0) return;

            // Map computed nodes to screen rectangles
            var nodeRectMap = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
            foreach (var cn in _lastLayout.Nodes)
            {
                float nx = (float)(plotRect.Left + cn.NormalizedX * (plotRect.Width - _nodeWidth));
                float ny = (float)(plotRect.Top + cn.NormalizedY * plotRect.Height);
                float nh = (float)(cn.NormalizedHeight * plotRect.Height);
                nodeRectMap[cn.Node.Name] = new RectangleF(nx, ny, _nodeWidth, Math.Max(10f, nh));
            }

            // 1. Draw Flow Ribbons
            for (int i = 0; i < _lastLayout.Ribbons.Count; i++)
            {
                var ribbon = _lastLayout.Ribbons[i];
                if (!nodeRectMap.TryGetValue(ribbon.SourceNode.Node.Name, out var srcRect) ||
                    !nodeRectMap.TryGetValue(ribbon.TargetNode.Node.Name, out var dstRect))
                {
                    continue;
                }

                float x1 = srcRect.Right;
                float x2 = dstRect.Left;
                float dx = (x2 - x1) * 0.5f;

                float y1_top = (float)(plotRect.Top + ribbon.SourceY0 * plotRect.Height);
                float y1_bot = (float)(plotRect.Top + ribbon.SourceY1 * plotRect.Height);
                float y2_top = (float)(plotRect.Top + ribbon.TargetY0 * plotRect.Height);
                float y2_bot = (float)(plotRect.Top + ribbon.TargetY1 * plotRect.Height);

                bool isHovered = (i == _hoverLinkIndex) ||
                                 (_hoverNodeName != null && (_hoverNodeName == ribbon.SourceNode.Node.Name || _hoverNodeName == ribbon.TargetNode.Node.Name));

                Color c1 = ribbon.Link.ColorRgba.ToColor(ribbon.SourceNode.Node.ColorRgba.ToColor(Color.FromArgb(79, 70, 229)));
                Color c2 = ribbon.Link.ColorRgba.ToColor(ribbon.TargetNode.Node.ColorRgba.ToColor(Color.FromArgb(16, 185, 129)));
                int alpha = isHovered ? 180 : 85;

                using (var path = new GraphicsPath())
                {
                    path.AddBezier(x1, y1_top, x1 + dx, y1_top, x2 - dx, y2_top, x2, y2_top);
                    path.AddLine(x2, y2_top, x2, y2_bot);
                    path.AddBezier(x2, y2_bot, x2 - dx, y2_bot, x1 + dx, y1_bot, x1, y1_bot);
                    path.CloseFigure();

                    using var ribbonBrush = new LinearGradientBrush(
                        new PointF(x1, y1_top), new PointF(x2, y2_top),
                        Color.FromArgb(alpha, c1), Color.FromArgb(alpha, c2));
                    g.FillPath(ribbonBrush, path);

                    if (isHovered)
                    {
                        using var borderPen = new Pen(Color.White, 1.2f);
                        g.DrawPath(borderPen, path);
                    }
                }
            }

            // 2. Draw Nodes
            using var font = new Font(Font.FontFamily, 8f, FontStyle.Bold);
            using var fontSub = new Font(Font.FontFamily, 7.5f, FontStyle.Regular);
            using var textBrush = new SolidBrush(textColor);
            using var mutedBrush = new SolidBrush(mutedColor);

            int totalCols = _lastLayout.ColumnCount;

            foreach (var cn in _lastLayout.Nodes)
            {
                if (!nodeRectMap.TryGetValue(cn.Node.Name, out var nr)) continue;

                bool isHighlighted = (_hoverNodeName == cn.Node.Name);
                Color nodeColor = cn.Node.ColorRgba.ToColor(Color.FromArgb(79, 70, 229));

                using (var nb = new SolidBrush(nodeColor))
                {
                    g.FillRectangle(nb, nr);
                }

                using (var np = new Pen(isHighlighted ? Color.White : Color.FromArgb(50, 0, 0, 0), isHighlighted ? 2f : 1f))
                {
                    g.DrawRectangle(np, nr.X, nr.Y, nr.Width, nr.Height);
                }

                // Node text label
                bool isRightSide = totalCols > 1 && cn.Node.Column >= totalCols / 2;
                float tx = isRightSide ? nr.Right + 6 : nr.Left - 6;
                var sf = new StringFormat
                {
                    Alignment = isRightSide ? StringAlignment.Near : StringAlignment.Far,
                    LineAlignment = StringAlignment.Center
                };

                string label = cn.Node.Name;
                string val = $"{cn.EffectiveValue:N0}{_valueSuffix}";

                g.DrawString(label, font, textBrush, tx, nr.Top + nr.Height / 2 - 6, sf);
                g.DrawString(val, fontSub, mutedBrush, tx, nr.Top + nr.Height / 2 + 7, sf);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_lastLayout == null) return;

            string? foundNode = null;
            int titleHeight = string.IsNullOrEmpty(_title) ? 6 : 30;
            var plotRect = new Rectangle(50, titleHeight + 10, Width - 100, Height - titleHeight - 30);

            foreach (var cn in _lastLayout.Nodes)
            {
                float nx = (float)(plotRect.Left + cn.NormalizedX * (plotRect.Width - _nodeWidth));
                float ny = (float)(plotRect.Top + cn.NormalizedY * plotRect.Height);
                float nh = (float)(cn.NormalizedHeight * plotRect.Height);
                var rect = new RectangleF(nx, ny, _nodeWidth, Math.Max(10f, nh));

                if (rect.Contains(e.Location))
                {
                    foundNode = cn.Node.Name;
                    break;
                }
            }

            if (foundNode != _hoverNodeName)
            {
                _hoverNodeName = foundNode;
                Invalidate();

                if (_hoverNodeName != null)
                {
                    var cn = _lastLayout.Nodes.FirstOrDefault(n => n.Node.Name == _hoverNodeName);
                    if (cn != null)
                    {
                        string tip = $"{cn.Node.Name}\nInflow: {cn.InValue:N0}{_valueSuffix}\nOutflow: {cn.OutValue:N0}{_valueSuffix}";
                        _toolTip.SetToolTip(this, tip);
                    }
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                }
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverNodeName != null)
            {
                _hoverNodeName = null;
                _toolTip.SetToolTip(this, null);
                Invalidate();
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

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSankeyChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SankeyChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSankeyChart instead.")]
    [ToolboxItem(false)]
    public class SankeyChart : ZSankeyChart
    {
    }

    #endregion
}
