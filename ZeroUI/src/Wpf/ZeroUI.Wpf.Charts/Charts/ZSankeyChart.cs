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
    /// Interactive Sankey diagram for visualizing energy, cost, material, and process flows in WPF.
    /// Thin WPF View layer rendering topology and Bézier flow ribbons computed by ZeroUI.Core.Analytics.SankeyLayoutEngine.
    /// </summary>
    public class ZSankeyChart : FrameworkElement
    {
        private readonly List<SankeyNode> _nodes = new List<SankeyNode>();
        private readonly List<SankeyLink> _links = new List<SankeyLink>();

        private string _title = "Process & Material Flow Distribution (Sankey)";
        private string _valueSuffix = " MWh";
        private double _nodeWidth = 18.0;
        private string? _hoverNodeName = null;
        private SankeyLayoutResult? _lastLayout = null;

        public List<SankeyNode> Nodes => _nodes;
        public List<SankeyLink> Links => _links;

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? string.Empty; InvalidateVisual(); }
        }

        public ZSankeyChart()
        {
            ClipToBounds = true;
            MinHeight = 240;
            MinWidth = 320;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public SankeyNode AddNode(string name, Color color, int column = 0)
        {
            uint rgba = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | (uint)color.B;
            var node = new SankeyNode(name, column, rgba);
            _nodes.Add(node);
            InvalidateVisual();
            return node;
        }

        public SankeyNode AddNode(string name, int column = 0)
        {
            var node = new SankeyNode(name, column);
            _nodes.Add(node);
            InvalidateVisual();
            return node;
        }

        public SankeyLink AddLink(string source, string target, double value, Color? color = null)
        {
            uint? rgba = color.HasValue
                ? (((uint)color.Value.A << 24) | ((uint)color.Value.R << 16) | ((uint)color.Value.G << 8) | (uint)color.Value.B)
                : (uint?)null;
            var link = new SankeyLink(source, target, value, rgba);
            _links.Add(link);
            InvalidateVisual();
            return link;
        }

        public void Clear()
        {
            _nodes.Clear();
            _links.Clear();
            _lastLayout = null;
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

            double titleH = string.IsNullOrEmpty(_title) ? 6 : 30;
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
                dc.DrawText(titleText, new Point(16, 8));
            }

            double plotX = 50;
            double plotY = titleH + 10;
            double plotW = Math.Max(0, w - 100);
            double plotH = Math.Max(0, h - titleH - 30);
            if (plotW <= 50 || plotH <= 50 || _nodes.Count == 0) return;

            _lastLayout = SankeyLayoutEngine.Compute(_nodes, _links);
            if (_lastLayout.Nodes.Count == 0) return;

            var nodeRectMap = new Dictionary<string, Rect>(StringComparer.OrdinalIgnoreCase);
            foreach (var cn in _lastLayout.Nodes)
            {
                double nx = plotX + cn.NormalizedX * (plotW - _nodeWidth);
                double ny = plotY + cn.NormalizedY * plotH;
                double nh = Math.Max(10.0, cn.NormalizedHeight * plotH);
                nodeRectMap[cn.Node.Name] = new Rect(nx, ny, _nodeWidth, nh);
            }

            // 1. Draw Flow Ribbons with StreamGeometry
            for (int i = 0; i < _lastLayout.Ribbons.Count; i++)
            {
                var ribbon = _lastLayout.Ribbons[i];
                if (!nodeRectMap.TryGetValue(ribbon.SourceNode.Node.Name, out var srcRect) ||
                    !nodeRectMap.TryGetValue(ribbon.TargetNode.Node.Name, out var dstRect))
                {
                    continue;
                }

                double x1 = srcRect.Right;
                double x2 = dstRect.Left;
                double dx = (x2 - x1) * 0.5;

                double y1_top = plotY + ribbon.SourceY0 * plotH;
                double y1_bot = plotY + ribbon.SourceY1 * plotH;
                double y2_top = plotY + ribbon.TargetY0 * plotH;
                double y2_bot = plotY + ribbon.TargetY1 * plotH;

                bool isHovered = (_hoverNodeName != null && (_hoverNodeName == ribbon.SourceNode.Node.Name || _hoverNodeName == ribbon.TargetNode.Node.Name));

                Color c1 = ribbon.Link.ColorRgba.ToColor(ribbon.SourceNode.Node.ColorRgba.ToColor(Color.FromRgb(79, 70, 229)));
                Color c2 = ribbon.Link.ColorRgba.ToColor(ribbon.TargetNode.Node.ColorRgba.ToColor(Color.FromRgb(16, 185, 129)));
                byte alpha = isHovered ? (byte)190 : (byte)95;

                var grad = new LinearGradientBrush(
                    Color.FromArgb(alpha, c1.R, c1.G, c1.B),
                    Color.FromArgb(alpha, c2.R, c2.G, c2.B),
                    new Point(0, 0), new Point(1, 0));
                grad.Freeze();

                var pen = isHovered ? new Pen(Brushes.White, 1.2) : null;
                if (pen != null && pen.CanFreeze) pen.Freeze();

                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(new Point(x1, y1_top), isFilled: true, isClosed: true);
                    ctx.BezierTo(new Point(x1 + dx, y1_top), new Point(x2 - dx, y2_top), new Point(x2, y2_top), isStroked: false, isSmoothJoin: true);
                    ctx.LineTo(new Point(x2, y2_bot), isStroked: false, isSmoothJoin: true);
                    ctx.BezierTo(new Point(x2 - dx, y2_bot), new Point(x1 + dx, y1_bot), new Point(x1, y1_bot), isStroked: false, isSmoothJoin: true);
                }
                geom.Freeze();

                dc.DrawGeometry(grad, pen, geom);
            }

            // 2. Draw Nodes
            int totalCols = _lastLayout.ColumnCount;

            foreach (var cn in _lastLayout.Nodes)
            {
                if (!nodeRectMap.TryGetValue(cn.Node.Name, out var nr)) continue;

                bool isHighlighted = (_hoverNodeName == cn.Node.Name);
                Color nodeColor = cn.Node.ColorRgba.ToColor(Color.FromRgb(79, 70, 229));
                var nodeBrush = new SolidColorBrush(nodeColor);
                nodeBrush.Freeze();

                var borderPen = isHighlighted ? new Pen(Brushes.White, 2.0) : new Pen(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), 1.0);
                if (borderPen.CanFreeze) borderPen.Freeze();

                dc.DrawRectangle(nodeBrush, borderPen, nr);

                // Node Labels
                bool isRightSide = totalCols > 1 && cn.Node.Column >= totalCols / 2;
                string label = cn.Node.Name;
                string val = $"{cn.EffectiveValue:N0}{_valueSuffix}";

                var labelText = new FormattedText(
                    label,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    10.5,
                    ZeroWpfTheme.TextPrimary,
                    1.0);

                var valText = new FormattedText(
                    val,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    9.5,
                    ZeroWpfTheme.TextSecondary,
                    1.0);

                double tx = isRightSide ? nr.Right + 6 : nr.Left - Math.Max(labelText.Width, valText.Width) - 6;
                dc.DrawText(labelText, new Point(tx, nr.Top + nr.Height / 2 - 12));
                dc.DrawText(valText, new Point(tx, nr.Top + nr.Height / 2 + 1));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_lastLayout == null) return;

            var pt = e.GetPosition(this);
            string? foundNode = null;
            double titleH = string.IsNullOrEmpty(_title) ? 6 : 30;
            double plotX = 50;
            double plotY = titleH + 10;
            double plotW = Math.Max(0, ActualWidth - 100);
            double plotH = Math.Max(0, ActualHeight - titleH - 30);

            foreach (var cn in _lastLayout.Nodes)
            {
                double nx = plotX + cn.NormalizedX * (plotW - _nodeWidth);
                double ny = plotY + cn.NormalizedY * plotH;
                double nh = Math.Max(10.0, cn.NormalizedHeight * plotH);
                var rect = new Rect(nx, ny, _nodeWidth, nh);

                if (rect.Contains(pt))
                {
                    foundNode = cn.Node.Name;
                    break;
                }
            }

            if (foundNode != _hoverNodeName)
            {
                _hoverNodeName = foundNode;
                InvalidateVisual();

                if (_hoverNodeName != null)
                {
                    var cn = _lastLayout.Nodes.FirstOrDefault(n => n.Node.Name == _hoverNodeName);
                    if (cn != null)
                    {
                        ToolTip = $"{cn.Node.Name}\nInflow: {cn.InValue:N0}{_valueSuffix}\nOutflow: {cn.OutValue:N0}{_valueSuffix}";
                        Cursor = Cursors.Hand;
                    }
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
            if (_hoverNodeName != null)
            {
                _hoverNodeName = null;
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
    /// Legacy alias for <see cref="ZSankeyChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SankeyChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSankeyChart instead.")]
    public class SankeyChart : ZSankeyChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZSankeyChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSankeyChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSankeyChart instead.")]
    public class ZeroSankeyChart : ZSankeyChart { }

    #endregion

}
