using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Runtime;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Diagram
{
    public class DiagramPort
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Port";
        public bool IsInput { get; set; } = false;
        public Point RelativeOffset { get; set; } = new Point(1.0, 0.5);

        public Point GetAbsolutePosition(DiagramNode node)
        {
            return new Point(node.X + node.Width * RelativeOffset.X, node.Y + node.Height * RelativeOffset.Y);
        }
    }

    public class DiagramNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "Node";
        public string Subtitle { get; set; } = "Process";
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;
        public double Width { get; set; } = 150;
        public double Height { get; set; } = 75;
        public Color HeaderColor { get; set; } = Color.FromRgb(56, 189, 248); // Sky blue
        public string IconGlyph { get; set; } = "⚙";
        public List<DiagramPort> Ports { get; } = new List<DiagramPort>();
        public object? Tag { get; set; }

        public Rect Bounds => new Rect(X, Y, Width, Height);

        public DiagramNode()
        {
        }

        public DiagramNode(string id, string title, string subtitle, double x, double y, Color headerColor, string icon = "⚙")
        {
            Id = id;
            Title = title;
            Subtitle = subtitle;
            X = x;
            Y = y;
            HeaderColor = headerColor;
            IconGlyph = icon;

            // Default: 1 input port on left, 1 output port on right
            Ports.Add(new DiagramPort { Id = "in", Name = "In", IsInput = true, RelativeOffset = new Point(0, 0.5) });
            Ports.Add(new DiagramPort { Id = "out", Name = "Out", IsInput = false, RelativeOffset = new Point(1.0, 0.5) });
        }
    }

    public class DiagramConnection
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string SourceNodeId { get; set; } = string.Empty;
        public string SourcePortId { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetPortId { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public Color StrokeColor { get; set; } = Color.FromRgb(14, 165, 233);
        public double Thickness { get; set; } = 2.5;

        public DiagramConnection() { }

        public DiagramConnection(string srcNode, string srcPort, string tgtNode, string tgtPort, string label = "", Color? color = null)
        {
            SourceNodeId = srcNode;
            SourcePortId = srcPort;
            TargetNodeId = tgtNode;
            TargetPortId = tgtPort;
            Label = label;
            if (color.HasValue) StrokeColor = color.Value;
        }
    }

    /// <summary>
    /// Single-Visual high-performance interactive diagram and P&amp;ID process canvas for WPF.
    /// Provides zero-visual-tree node graph rendering, Bezier connection wires, port magnetics,
    /// smooth zoom &amp; pan, and interactive node drag &amp; drop.
    /// </summary>
    public class ZDiagramCanvas : FrameworkElement
    {
        private readonly ObservableCollection<DiagramNode> _nodes = new ObservableCollection<DiagramNode>();
        private readonly ObservableCollection<DiagramConnection> _connections = new ObservableCollection<DiagramConnection>();

        private double _zoomFactor = 1.0;
        private Point _panOffset = new Point(0, 0);

        private bool _isPanning = false;
        private Point _panStartMouse;
        private Point _panStartOffset;

        private DiagramNode? _draggedNode;
        private Point _nodeDragStartMouse;
        private Point _nodeDragStartPos;

        private DiagramNode? _selectedNode;
        private (DiagramNode Node, DiagramPort Port)? _connectingSource;
        private Point _connectingCurrentMouse;

        private (DiagramNode Node, DiagramPort Port)? _hoveredPort;

        public ObservableCollection<DiagramNode> Nodes => _nodes;
        public ObservableCollection<DiagramConnection> Connections => _connections;

        public DiagramNode? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    _selectedNode = value;
                    NodeSelected?.Invoke(this, _selectedNode);
                    InvalidateVisual();
                }
            }
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                _zoomFactor = Math.Max(0.2, Math.Min(3.0, value));
                InvalidateVisual();
            }
        }

        public Point PanOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                InvalidateVisual();
            }
        }

        public event EventHandler<DiagramNode?>? NodeSelected;
        public event EventHandler<DiagramConnection>? ConnectionCreated;

        public ZDiagramCanvas()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = Cursors.Arrow;

            _nodes.CollectionChanged += (s, e) => InvalidateVisual();
            _connections.CollectionChanged += (s, e) => InvalidateVisual();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public Point ScreenToWorld(Point screen)
        {
            return new Point((screen.X - _panOffset.X) / _zoomFactor, (screen.Y - _panOffset.Y) / _zoomFactor);
        }

        public Point WorldToScreen(Point world)
        {
            return new Point(world.X * _zoomFactor + _panOffset.X, world.Y * _zoomFactor + _panOffset.Y);
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Draw Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // 2. Draw Grid Dots / Lines
            DrawGrid(dc, w, h);

            // Apply Transform for World Coordinates
            dc.PushTransform(new MatrixTransform(_zoomFactor, 0, 0, _zoomFactor, _panOffset.X, _panOffset.Y));

            // 3. Draw Connections
            DrawConnections(dc);

            // 4. Draw Connecting Wire (rubber band)
            if (_connectingSource.HasValue)
            {
                Point srcPt = _connectingSource.Value.Port.GetAbsolutePosition(_connectingSource.Value.Node);
                Point tgtPt = ScreenToWorld(_connectingCurrentMouse);
                DrawBezierWire(dc, srcPt, tgtPt, Color.FromRgb(245, 158, 11), 2.0, true);
            }

            // 5. Draw Nodes
            foreach (var node in _nodes)
            {
                DrawNode(dc, node, node == _selectedNode);
            }

            dc.Pop(); // Restore Transform
        }

        private void DrawGrid(DrawingContext dc, double w, double h)
        {
            double step = 30 * _zoomFactor;
            if (step < 10) return;

            double startX = _panOffset.X % step;
            if (startX < 0) startX += step;
            double startY = _panOffset.Y % step;
            if (startY < 0) startY += step;

            var dotBrush = new SolidColorBrush(Color.FromArgb(28, 148, 163, 184));
            dotBrush.Freeze();

            for (double x = startX; x < w; x += step)
            {
                for (double y = startY; y < h; y += step)
                {
                    dc.DrawRectangle(dotBrush, null, new Rect(x - 1, y - 1, 2, 2));
                }
            }
        }

        private void DrawConnections(DrawingContext dc)
        {
            foreach (var conn in _connections)
            {
                var srcNode = _nodes.FirstOrDefault(n => n.Id == conn.SourceNodeId);
                var tgtNode = _nodes.FirstOrDefault(n => n.Id == conn.TargetNodeId);
                if (srcNode == null || tgtNode == null) continue;

                var srcPort = srcNode.Ports.FirstOrDefault(p => p.Id == conn.SourcePortId) ?? srcNode.Ports.FirstOrDefault(p => !p.IsInput) ?? srcNode.Ports.FirstOrDefault();
                var tgtPort = tgtNode.Ports.FirstOrDefault(p => p.Id == conn.TargetPortId) ?? tgtNode.Ports.FirstOrDefault(p => p.IsInput) ?? tgtNode.Ports.FirstOrDefault();

                Point p1 = srcPort != null ? srcPort.GetAbsolutePosition(srcNode) : new Point(srcNode.X + srcNode.Width, srcNode.Y + srcNode.Height / 2);
                Point p2 = tgtPort != null ? tgtPort.GetAbsolutePosition(tgtNode) : new Point(tgtNode.X, tgtNode.Y + tgtNode.Height / 2);

                DrawBezierWire(dc, p1, p2, conn.StrokeColor, conn.Thickness, false);

                // Label pill
                if (!string.IsNullOrEmpty(conn.Label))
                {
                    Point mid = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                    var ft = new FormattedText(conn.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        ZeroWpfTheme.MediumTypeface,
                        10, ZeroWpfTheme.TextPrimary, 1.0);

                    double pillW = ft.Width + 10;
                    double pillH = ft.Height + 4;
                    Rect pillRect = new Rect(mid.X - pillW / 2, mid.Y - pillH / 2, pillW, pillH);

                    dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, new Pen(ZeroWpfTheme.BorderDefault, 1), pillRect, 4, 4);
                    dc.DrawText(ft, new Point(pillRect.Left + 5, pillRect.Top + 2));
                }
            }
        }

        private void DrawBezierWire(DrawingContext dc, Point p1, Point p2, Color color, double thickness, bool dashed)
        {
            double dx = Math.Abs(p2.X - p1.X) * 0.5;
            if (dx < 40) dx = 40;

            Point cp1 = new Point(p1.X + dx, p1.Y);
            Point cp2 = new Point(p2.X - dx, p2.Y);

            var pathGeo = new StreamGeometry();
            using (var ctx = pathGeo.Open())
            {
                ctx.BeginFigure(p1, false, false);
                ctx.BezierTo(cp1, cp2, p2, true, true);
            }
            pathGeo.Freeze();

            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var pen = new Pen(brush, thickness);
            if (dashed)
            {
                pen.DashStyle = DashStyles.Dash;
            }
            pen.Freeze();

            dc.DrawGeometry(null, pen, pathGeo);

            // Draw arrow at p2
            Vector dir = p2 - cp2;
            if (dir.Length > 0.1)
            {
                dir.Normalize();
                Vector normal = new Vector(-dir.Y, dir.X);
                Point a1 = p2 - dir * 8 + normal * 4;
                Point a2 = p2 - dir * 8 - normal * 4;

                var arrowGeo = new StreamGeometry();
                using (var ctx = arrowGeo.Open())
                {
                    ctx.BeginFigure(p2, true, true);
                    ctx.LineTo(a1, true, false);
                    ctx.LineTo(a2, true, false);
                }
                arrowGeo.Freeze();
                dc.DrawGeometry(brush, null, arrowGeo);
            }
        }

        private void DrawNode(DrawingContext dc, DiagramNode node, bool isSelected)
        {
            Rect rect = node.Bounds;

            // Shadow / Selection Glow
            if (isSelected)
            {
                var glowPen = new Pen(ZeroWpfTheme.PrimaryAccent, 2.5);
                glowPen.Freeze();
                dc.DrawRoundedRectangle(null, glowPen, new Rect(rect.X - 3, rect.Y - 3, rect.Width + 6, rect.Height + 6), 8, 8);
            }

            // Body Card
            var borderPen = isSelected ? new Pen(ZeroWpfTheme.PrimaryAccent, 1.5) : new Pen(ZeroWpfTheme.BorderDefault, 1);
            borderPen.Freeze();
            dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, borderPen, rect, 6, 6);

            // Top Header Bar
            Rect headerRect = new Rect(rect.X, rect.Y, rect.Width, 24);
            var headerClip = new RectangleGeometry(headerRect, 6, 6);
            headerClip.Freeze();
            dc.PushClip(headerClip);
            var headerBrush = new SolidColorBrush(Color.FromArgb(40, node.HeaderColor.R, node.HeaderColor.G, node.HeaderColor.B));
            headerBrush.Freeze();
            dc.DrawRectangle(headerBrush, null, headerRect);
            dc.Pop();

            // Accent Line under header
            var lineBrush = new SolidColorBrush(node.HeaderColor);
            lineBrush.Freeze();
            dc.DrawLine(new Pen(lineBrush, 2), new Point(rect.X, rect.Y + 24), new Point(rect.Right, rect.Y + 24));

            // Icon + Title Text
            string fullTitle = string.IsNullOrEmpty(node.IconGlyph) ? node.Title : $"{node.IconGlyph}  {node.Title}";
            var titleFt = new FormattedText(fullTitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                ZeroWpfTheme.BoldTypeface,
                11, ZeroWpfTheme.TextPrimary, 1.0);
            titleFt.MaxTextWidth = rect.Width - 16;
            titleFt.MaxLineCount = 1;
            dc.DrawText(titleFt, new Point(rect.X + 8, rect.Y + 4));

            // Subtitle text
            if (!string.IsNullOrEmpty(node.Subtitle))
            {
                var subFt = new FormattedText(node.Subtitle, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    10, ZeroWpfTheme.TextMuted, 1.0);
                subFt.MaxTextWidth = rect.Width - 16;
                subFt.MaxLineCount = 2;
                dc.DrawText(subFt, new Point(rect.X + 8, rect.Y + 30));
            }

            // Draw Ports
            foreach (var port in node.Ports)
            {
                Point portPos = port.GetAbsolutePosition(node);
                bool isHovered = _hoveredPort.HasValue && _hoveredPort.Value.Node == node && _hoveredPort.Value.Port == port;
                double radius = isHovered ? 5.5 : 4.0;

                var portBrush = isHovered ? ZeroWpfTheme.PrimaryAccent : (port.IsInput ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(59, 130, 246)));
                var portPen = new Pen(ZeroWpfTheme.BgCard, 1.5);
                portPen.Freeze();

                dc.DrawEllipse(portBrush, portPen, portPos, radius, radius);
            }
        }

        #endregion

        #region Hit-Testing & Interaction

        private (DiagramNode Node, DiagramPort Port)? HitTestPort(Point worldPt, double tolerance = 10)
        {
            foreach (var node in _nodes)
            {
                foreach (var port in node.Ports)
                {
                    Point pt = port.GetAbsolutePosition(node);
                    if ((pt - worldPt).Length <= tolerance)
                    {
                        return (node, port);
                    }
                }
            }
            return null;
        }

        private DiagramNode? HitTestNode(Point worldPt)
        {
            for (int i = _nodes.Count - 1; i >= 0; i--)
            {
                if (_nodes[i].Bounds.Contains(worldPt))
                {
                    return _nodes[i];
                }
            }
            return null;
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point screenPt = e.GetPosition(this);
            Point worldPt = ScreenToWorld(screenPt);

            if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Right && Keyboard.Modifiers != ModifierKeys.Control))
            {
                // Start Panning
                _isPanning = true;
                _panStartMouse = screenPt;
                _panStartOffset = _panOffset;
                CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                // 1. Check Port click (for wire creation)
                var portHit = HitTestPort(worldPt);
                if (portHit.HasValue)
                {
                    _connectingSource = portHit;
                    _connectingCurrentMouse = screenPt;
                    CaptureMouse();
                    e.Handled = true;
                    InvalidateVisual();
                    return;
                }

                // 2. Check Node click (for drag / selection)
                var nodeHit = HitTestNode(worldPt);
                if (nodeHit != null)
                {
                    SelectedNode = nodeHit;
                    _draggedNode = nodeHit;
                    _nodeDragStartMouse = screenPt;
                    _nodeDragStartPos = new Point(nodeHit.X, nodeHit.Y);
                    CaptureMouse();
                    e.Handled = true;
                    return;
                }

                // 3. Clicked empty background: start pan or clear selection
                SelectedNode = null;
                _isPanning = true;
                _panStartMouse = screenPt;
                _panStartOffset = _panOffset;
                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point screenPt = e.GetPosition(this);
            Point worldPt = ScreenToWorld(screenPt);

            if (_isPanning)
            {
                _panOffset = new Point(
                    _panStartOffset.X + (screenPt.X - _panStartMouse.X),
                    _panStartOffset.Y + (screenPt.Y - _panStartMouse.Y)
                );
                InvalidateVisual();
                return;
            }

            if (_connectingSource.HasValue)
            {
                _connectingCurrentMouse = screenPt;
                _hoveredPort = HitTestPort(worldPt);
                InvalidateVisual();
                return;
            }

            if (_draggedNode != null)
            {
                double dx = (screenPt.X - _nodeDragStartMouse.X) / _zoomFactor;
                double dy = (screenPt.Y - _nodeDragStartMouse.Y) / _zoomFactor;
                _draggedNode.X = _nodeDragStartPos.X + dx;
                _draggedNode.Y = _nodeDragStartPos.Y + dy;
                InvalidateVisual();
                return;
            }

            // Hover check
            var port = HitTestPort(worldPt);
            if (port != _hoveredPort)
            {
                _hoveredPort = port;
                Cursor = _hoveredPort.HasValue ? Cursors.Cross : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            Point screenPt = e.GetPosition(this);
            Point worldPt = ScreenToWorld(screenPt);

            if (_isPanning)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                return;
            }

            if (_connectingSource.HasValue)
            {
                var targetPort = HitTestPort(worldPt);
                if (targetPort.HasValue && targetPort.Value.Node != _connectingSource.Value.Node)
                {
                    // Create connection
                    var src = _connectingSource.Value;
                    var tgt = targetPort.Value;

                    // Swap if src was input and tgt was output
                    if (src.Port.IsInput && !tgt.Port.IsInput)
                    {
                        var temp = src;
                        src = tgt;
                        tgt = temp;
                    }

                    var newConn = new DiagramConnection(src.Node.Id, src.Port.Id, tgt.Node.Id, tgt.Port.Id, "");
                    _connections.Add(newConn);
                    ConnectionCreated?.Invoke(this, newConn);
                }

                _connectingSource = null;
                ReleaseMouseCapture();
                InvalidateVisual();
                return;
            }

            if (_draggedNode != null)
            {
                _draggedNode = null;
                ReleaseMouseCapture();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            Point mousePos = e.GetPosition(this);
            Point worldBefore = ScreenToWorld(mousePos);

            double zoomDelta = e.Delta > 0 ? 1.12 : (1.0 / 1.12);
            double newZoom = Math.Max(0.2, Math.Min(3.0, _zoomFactor * zoomDelta));

            _zoomFactor = newZoom;
            Point worldAfter = ScreenToWorld(mousePos);

            _panOffset = new Point(
                _panOffset.X + (worldAfter.X - worldBefore.X) * _zoomFactor,
                _panOffset.Y + (worldAfter.Y - worldBefore.Y) * _zoomFactor
            );

            InvalidateVisual();
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Delete && _selectedNode != null)
            {
                // Delete selected node and all its connections
                string nodeId = _selectedNode.Id;
                for (int i = _connections.Count - 1; i >= 0; i--)
                {
                    if (_connections[i].SourceNodeId == nodeId || _connections[i].TargetNodeId == nodeId)
                    {
                        _connections.RemoveAt(i);
                    }
                }
                _nodes.Remove(_selectedNode);
                SelectedNode = null;
                InvalidateVisual();
                e.Handled = true;
            }
        }

        #endregion
    
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
    /// Legacy alias for <see cref="ZDiagramCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DiagramCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZDiagramCanvas instead.")]
    public class DiagramCanvas : ZDiagramCanvas { }

    /// <summary>
    /// Legacy alias for <see cref="ZDiagramCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDiagramCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZDiagramCanvas instead.")]
    public class ZeroDiagramCanvas : ZDiagramCanvas { }

    #endregion

}
