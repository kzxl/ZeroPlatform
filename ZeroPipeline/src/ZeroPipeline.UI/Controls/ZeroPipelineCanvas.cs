using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.UI.Models;
using ZeroUI.WinForms.Theme;

namespace ZeroPipeline.UI.Controls
{
    /// <summary>
    /// Interactive visual canvas for creating, viewing, and orchestrating pipeline DAGs.
    /// Provides infinite pan/zoom, grid snapping, cubic Bezier connection noodles, and live execution status.
    /// </summary>
    public class ZeroPipelineCanvas : Control
    {
        public CanvasTransform Transform { get; } = new CanvasTransform();
        public List<CanvasNode> Nodes { get; } = new List<CanvasNode>();
        public List<CanvasConnection> Connections { get; } = new List<CanvasConnection>();

        public CanvasNode? SelectedNode { get; private set; }
        public CanvasConnection? SelectedConnection { get; private set; }

        public bool SnapToGrid { get; set; } = true;
        public float GridSpacing { get; set; } = 20.0f;

        public event EventHandler<CanvasNode?>? NodeSelected;
        public event EventHandler<CanvasConnection?>? ConnectionSelected;
        public event EventHandler<CanvasConnection>? ConnectionCreated;
        public event EventHandler<CanvasConnection>? ConnectionRemoved;
        public event EventHandler<CanvasNode>? NodeRemoved;

        // Mouse interaction state
        private bool _isPanning;
        private Point _panStartScreen;

        private bool _isDraggingNode;
        private PointF _dragNodeStartPos;
        private PointF _dragStartWorld;

        private bool _isConnecting;
        private CanvasPortPin? _connectingSourcePin;
        private PointF _currentMouseWorld;

        private CanvasPortPin? _hoveredPin;
        private CanvasNode? _hoveredNode;
        private CanvasConnection? _hoveredConnection;

        // Theme palette
        private static readonly Color BgColor = Color.FromArgb(18, 21, 28);
        private static readonly Color MinorGridColor = Color.FromArgb(28, 34, 46);
        private static readonly Color MajorGridColor = Color.FromArgb(38, 46, 62);
        private static readonly Color NodeBgColor = Color.FromArgb(30, 35, 48);
        private static readonly Color NodeHeaderColor = Color.FromArgb(42, 49, 66);
        private static readonly Color NodeHeaderSelectedColor = Color.FromArgb(30, 58, 95);
        private static readonly Color NodeBorderColor = Color.FromArgb(51, 60, 78);
        private static readonly Color NodeBorderSelectedColor = Color.FromArgb(0, 229, 255);
        private static readonly Color NodeBorderHoverColor = Color.FromArgb(90, 110, 140);
        private static readonly Color TextPrimaryColor = Color.FromArgb(240, 245, 255);
        private static readonly Color TextSecondaryColor = Color.FromArgb(144, 164, 174);
        private static readonly Color WireSelectedColor = Color.FromArgb(0, 229, 255);
        private static readonly Color WireHoverColor = Color.FromArgb(128, 216, 255);

        private readonly Font _titleFont = new Font("Segoe UI", 8.75f, FontStyle.Bold);
        private readonly Font _subtitleFont = new Font("Segoe UI", 7.25f, FontStyle.Regular);
        private readonly Font _pinFont = new Font("Segoe UI", 7.75f, FontStyle.Regular);
        private readonly Font _badgeFont = new Font("Segoe UI", 7.0f, FontStyle.Bold);

        public ZeroPipelineCanvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);

            BackColor = BgColor;
            Cursor = Cursors.Default;
        }

        public void AddNode(CanvasNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (!Nodes.Contains(node))
            {
                Nodes.Add(node);
                Invalidate();
            }
        }

        public void RemoveNode(CanvasNode node)
        {
            if (node == null) return;

            // Remove associated connections
            var connsToRemove = Connections.FindAll(c =>
                c.SourcePin.OwnerNode == node || c.TargetPin.OwnerNode == node);

            foreach (var conn in connsToRemove)
            {
                Connections.Remove(conn);
                ConnectionRemoved?.Invoke(this, conn);
            }

            Nodes.Remove(node);
            if (SelectedNode == node)
            {
                SelectedNode = null;
                NodeSelected?.Invoke(this, null);
            }

            NodeRemoved?.Invoke(this, node);
            Invalidate();
        }

        public CanvasConnection? Connect(CanvasPortPin sourcePin, CanvasPortPin targetPin)
        {
            if (sourcePin == null || targetPin == null) return null;
            if (sourcePin.Direction != PortDirection.Output || targetPin.Direction != PortDirection.Input)
                return null;
            if (sourcePin.OwnerNode == targetPin.OwnerNode)
                return null;

            // Type compatibility check
            if (!targetPin.DataType.IsAssignableFrom(sourcePin.DataType) &&
                sourcePin.DataType != typeof(object) &&
                targetPin.DataType != typeof(object))
            {
                return null;
            }

            // Remove existing input connection if input only supports single driver
            Connections.RemoveAll(c => c.TargetPin == targetPin);

            var conn = new CanvasConnection(sourcePin, targetPin);
            Connections.Add(conn);
            ConnectionCreated?.Invoke(this, conn);
            Invalidate();
            return conn;
        }

        public void RemoveConnection(CanvasConnection conn)
        {
            if (conn == null) return;
            if (Connections.Remove(conn))
            {
                if (SelectedConnection == conn)
                {
                    SelectedConnection = null;
                    ConnectionSelected?.Invoke(this, null);
                }
                ConnectionRemoved?.Invoke(this, conn);
                Invalidate();
            }
        }

        public void Clear()
        {
            Nodes.Clear();
            Connections.Clear();
            SelectedNode = null;
            SelectedConnection = null;
            Invalidate();
        }

        public void ZoomToFit()
        {
            if (Nodes.Count == 0)
            {
                Transform.Reset();
                Invalidate();
                return;
            }

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var node in Nodes)
            {
                if (node.X < minX) minX = node.X;
                if (node.Y < minY) minY = node.Y;
                if (node.X + node.Width > maxX) maxX = node.X + node.Width;
                if (node.Y + node.Height > maxY) maxY = node.Y + node.Height;
            }

            float contentW = Math.Max(100f, maxX - minX);
            float contentH = Math.Max(100f, maxY - minY);
            float margin = 80f;

            float availW = Math.Max(200f, Width - margin * 2f);
            float availH = Math.Max(200f, Height - margin * 2f);

            float zoomX = availW / contentW;
            float zoomY = availH / contentH;
            float targetZoom = Math.Max(0.25f, Math.Min(1.5f, Math.Min(zoomX, zoomY)));

            Transform.Zoom = targetZoom;
            Transform.PanX = (Width - contentW * targetZoom) * 0.5f - minX * targetZoom;
            Transform.PanY = (Height - contentH * targetZoom) * 0.5f - minY * targetZoom;

            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            PointF worldPt = Transform.ScreenToWorld(e.Location);

            if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Right)
            {
                _isPanning = true;
                _panStartScreen = e.Location;
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                // 1. Hit test pins
                CanvasPortPin? hitPin = FindPinAt(worldPt);
                if (hitPin != null)
                {
                    if (hitPin.Direction == PortDirection.Output)
                    {
                        _isConnecting = true;
                        _connectingSourcePin = hitPin;
                        _currentMouseWorld = worldPt;
                        Invalidate();
                        return;
                    }
                }

                // 2. Hit test nodes
                CanvasNode? hitNode = FindNodeAt(worldPt);
                if (hitNode != null)
                {
                    SelectNode(hitNode);
                    _isDraggingNode = true;
                    _dragStartWorld = worldPt;
                    _dragNodeStartPos = new PointF(hitNode.X, hitNode.Y);
                    Invalidate();
                    return;
                }

                // 3. Hit test connections
                CanvasConnection? hitConn = FindConnectionAt(worldPt);
                if (hitConn != null)
                {
                    SelectConnection(hitConn);
                    Invalidate();
                    return;
                }

                // Clicked empty space
                SelectNode(null);
                SelectConnection(null);
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            PointF worldPt = Transform.ScreenToWorld(e.Location);

            if (_isPanning)
            {
                float dx = e.X - _panStartScreen.X;
                float dy = e.Y - _panStartScreen.Y;
                Transform.Pan(dx, dy);
                _panStartScreen = e.Location;
                Invalidate();
                return;
            }

            if (_isDraggingNode && SelectedNode != null)
            {
                float dx = worldPt.X - _dragStartWorld.X;
                float dy = worldPt.Y - _dragStartWorld.Y;

                float newX = _dragNodeStartPos.X + dx;
                float newY = _dragNodeStartPos.Y + dy;

                if (SnapToGrid)
                {
                    newX = (float)Math.Round(newX / GridSpacing) * GridSpacing;
                    newY = (float)Math.Round(newY / GridSpacing) * GridSpacing;
                }

                SelectedNode.X = newX;
                SelectedNode.Y = newY;
                Invalidate();
                return;
            }

            if (_isConnecting)
            {
                _currentMouseWorld = worldPt;
                _hoveredPin = FindPinAt(worldPt);
                Invalidate();
                return;
            }

            // Hover tracking
            CanvasPortPin? newHoverPin = FindPinAt(worldPt);
            CanvasNode? newHoverNode = FindNodeAt(worldPt);
            CanvasConnection? newHoverConn = newHoverNode == null ? FindConnectionAt(worldPt) : null;

            bool needRedraw = false;
            if (_hoveredPin != newHoverPin) { _hoveredPin = newHoverPin; needRedraw = true; }
            if (_hoveredNode != newHoverNode)
            {
                if (_hoveredNode != null) _hoveredNode.IsHovered = false;
                _hoveredNode = newHoverNode;
                if (_hoveredNode != null) _hoveredNode.IsHovered = true;
                needRedraw = true;
            }
            if (_hoveredConnection != newHoverConn)
            {
                if (_hoveredConnection != null) _hoveredConnection.IsHovered = false;
                _hoveredConnection = newHoverConn;
                if (_hoveredConnection != null) _hoveredConnection.IsHovered = true;
                needRedraw = true;
            }

            if (needRedraw) Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_isPanning)
            {
                _isPanning = false;
                Cursor = Cursors.Default;
            }

            if (_isDraggingNode)
            {
                _isDraggingNode = false;
            }

            if (_isConnecting && _connectingSourcePin != null)
            {
                PointF worldPt = Transform.ScreenToWorld(e.Location);
                CanvasPortPin? targetPin = FindPinAt(worldPt);

                if (targetPin != null && targetPin.Direction == PortDirection.Input)
                {
                    Connect(_connectingSourcePin, targetPin);
                }

                _isConnecting = false;
                _connectingSourcePin = null;
                Invalidate();
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float factor = e.Delta > 0 ? 1.15f : (1.0f / 1.15f);
            Transform.ZoomAt(e.Location, factor);
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                if (SelectedNode != null)
                {
                    RemoveNode(SelectedNode);
                }
                else if (SelectedConnection != null)
                {
                    RemoveConnection(SelectedConnection);
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                if (_isConnecting)
                {
                    _isConnecting = false;
                    _connectingSourcePin = null;
                    Invalidate();
                }
            }
            else if (e.KeyCode == Keys.Home)
            {
                ZoomToFit();
            }
        }

        private void SelectNode(CanvasNode? node)
        {
            if (SelectedNode == node) return;

            if (SelectedNode != null) SelectedNode.IsSelected = false;
            SelectedNode = node;
            if (SelectedNode != null)
            {
                SelectedNode.IsSelected = true;
                SelectConnection(null);
            }

            NodeSelected?.Invoke(this, SelectedNode);
        }

        private void SelectConnection(CanvasConnection? conn)
        {
            if (SelectedConnection == conn) return;

            if (SelectedConnection != null) SelectedConnection.IsSelected = false;
            SelectedConnection = conn;
            if (SelectedConnection != null)
            {
                SelectedConnection.IsSelected = true;
                if (SelectedNode != null)
                {
                    SelectedNode.IsSelected = false;
                    SelectedNode = null;
                    NodeSelected?.Invoke(this, null);
                }
            }

            ConnectionSelected?.Invoke(this, SelectedConnection);
        }

        private CanvasPortPin? FindPinAt(PointF worldPt)
        {
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                var pin = Nodes[i].FindPinAt(worldPt);
                if (pin != null) return pin;
            }
            return null;
        }

        private CanvasNode? FindNodeAt(PointF worldPt)
        {
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                if (Nodes[i].ContainsPoint(worldPt))
                    return Nodes[i];
            }
            return null;
        }

        private CanvasConnection? FindConnectionAt(PointF worldPt)
        {
            for (int i = Connections.Count - 1; i >= 0; i--)
            {
                if (Connections[i].Hits(worldPt))
                    return Connections[i];
            }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // 1. Draw dual-level grid
            DrawGrid(g);

            // 2. Draw connections (noodles)
            DrawConnections(g);

            // 3. Draw connecting wire in progress
            if (_isConnecting && _connectingSourcePin != null)
            {
                DrawConnectingWire(g);
            }

            // 4. Draw nodes
            DrawNodes(g);

            // 5. Draw overlay metrics badge (bottom right)
            DrawOverlayMetrics(g);
        }

        private void DrawGrid(Graphics g)
        {
            float zoom = Transform.Zoom;
            float step = GridSpacing * zoom;

            if (step < 8f) return; // skip if too dense to avoid visual artifact

            float startX = (Transform.PanX % step);
            if (startX > 0) startX -= step;

            float startY = (Transform.PanY % step);
            if (startY > 0) startY -= step;

            using (var minorPen = new Pen(MinorGridColor, 1.0f))
            using (var majorPen = new Pen(MajorGridColor, 1.25f))
            {
                int indexX = 0;
                for (float x = startX; x <= Width; x += step, indexX++)
                {
                    bool isMajor = Math.Abs(((x - Transform.PanX) / zoom) % (GridSpacing * 5.0f)) < 0.5f;
                    g.DrawLine(isMajor ? majorPen : minorPen, x, 0, x, Height);
                }

                int indexY = 0;
                for (float y = startY; y <= Height; y += step, indexY++)
                {
                    bool isMajor = Math.Abs(((y - Transform.PanY) / zoom) % (GridSpacing * 5.0f)) < 0.5f;
                    g.DrawLine(isMajor ? majorPen : minorPen, 0, y, Width, y);
                }
            }
        }

        private void DrawConnections(Graphics g)
        {
            foreach (var conn in Connections)
            {
                conn.GetControlPoints(out var p0W, out var p1W, out var p2W, out var p3W);

                var p0 = Transform.WorldToScreen(p0W);
                var p1 = Transform.WorldToScreen(p1W);
                var p2 = Transform.WorldToScreen(p2W);
                var p3 = Transform.WorldToScreen(p3W);

                using (var path = new GraphicsPath())
                {
                    path.AddBezier(p0, p1, p2, p3);

                    // Shadow / Outer Stroke
                    using (var shadowPen = new Pen(Color.FromArgb(120, 0, 0, 0), 5.0f * Transform.Zoom))
                    {
                        g.DrawPath(shadowPen, path);
                    }

                    // Main Stroke
                    Color strokeColor = conn.IsSelected ? WireSelectedColor :
                                       conn.IsHovered ? WireHoverColor :
                                       conn.SourcePin.PinColor;

                    float strokeWidth = (conn.IsSelected ? 3.5f : conn.IsHovered ? 2.75f : 2.0f) * Transform.Zoom;
                    using (var wirePen = new Pen(strokeColor, Math.Max(1.5f, strokeWidth)))
                    {
                        g.DrawPath(wirePen, path);
                    }

                    // Flow Direction Indicator Dot at t=0.5
                    PointF mid = CanvasConnection.EvaluateBezier(p0, p1, p2, p3, 0.5f);
                    float dotR = 3.0f * Transform.Zoom;
                    using (var dotBrush = new SolidBrush(strokeColor))
                    {
                        g.FillEllipse(dotBrush, mid.X - dotR, mid.Y - dotR, dotR * 2, dotR * 2);
                    }
                }
            }
        }

        private void DrawConnectingWire(Graphics g)
        {
            if (_connectingSourcePin == null) return;

            PointF p0W = _connectingSourcePin.GetWorldCenter();
            PointF p3W = _currentMouseWorld;
            CanvasConnection.ComputeBezierPoints(p0W, p3W, out var p1W, out var p2W);

            var p0 = Transform.WorldToScreen(p0W);
            var p1 = Transform.WorldToScreen(p1W);
            var p2 = Transform.WorldToScreen(p2W);
            var p3 = Transform.WorldToScreen(p3W);

            using (var path = new GraphicsPath())
            {
                path.AddBezier(p0, p1, p2, p3);

                using (var pen = new Pen(Color.FromArgb(255, 214, 0), 2.5f * Transform.Zoom))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawPath(pen, path);
                }
            }

            // Target Pin Halo if hovering compatible input pin
            if (_hoveredPin != null && _hoveredPin.Direction == PortDirection.Input)
            {
                bool isCompatible = (_hoveredPin.OwnerNode != _connectingSourcePin.OwnerNode) &&
                                    (_hoveredPin.DataType.IsAssignableFrom(_connectingSourcePin.DataType) ||
                                     _connectingSourcePin.DataType == typeof(object) ||
                                     _hoveredPin.DataType == typeof(object));

                Color haloColor = isCompatible ? Color.FromArgb(0, 255, 136) : Color.FromArgb(255, 23, 68);
                PointF screenCenter = Transform.WorldToScreen(_hoveredPin.GetWorldCenter());
                float radius = 12.0f * Transform.Zoom;

                using (var haloPen = new Pen(haloColor, 2.5f))
                {
                    g.DrawEllipse(haloPen, screenCenter.X - radius, screenCenter.Y - radius, radius * 2, radius * 2);
                }
            }
        }

        private void DrawNodes(Graphics g)
        {
            foreach (var node in Nodes)
            {
                var boundsW = node.GetBounds();
                var boundsS = Transform.WorldToScreen(boundsW);

                float cornerRadius = CanvasNode.CornerRadius * Transform.Zoom;
                using (var cardPath = ZeroUIConfig.CreateRoundedRectangleF(boundsS, Math.Max(2f, cornerRadius)))
                {
                    // 1. Node Card Background
                    using (var bgBrush = new SolidBrush(NodeBgColor))
                    {
                        g.FillPath(bgBrush, cardPath);
                    }

                    // 2. Header Area
                    var headerW = node.GetHeaderBounds();
                    var headerS = Transform.WorldToScreen(headerW);
                    using (var headerPath = ZeroUIConfig.CreateRoundedRectangleF(headerS, Math.Max(2f, cornerRadius)))
                    {
                        var oldClip = g.Clip;
                        g.SetClip(cardPath, CombineMode.Intersect);

                        Color hColor = node.IsSelected ? NodeHeaderSelectedColor : NodeHeaderColor;
                        using (var headerBrush = new SolidBrush(hColor))
                        {
                            g.FillRectangle(headerBrush, headerS);
                        }

                        // Header Divider Line
                        using (var linePen = new Pen(NodeBorderColor, 1f))
                        {
                            g.DrawLine(linePen, headerS.Left, headerS.Bottom, headerS.Right, headerS.Bottom);
                        }

                        g.Clip = oldClip;
                    }

                    // 3. Node Border
                    Color borderColor = node.IsSelected ? NodeBorderSelectedColor :
                                        node.IsHovered ? NodeBorderHoverColor :
                                        NodeBorderColor;

                    float borderWidth = (node.IsSelected ? 2.5f : 1.25f) * Math.Min(1.5f, Transform.Zoom);
                    using (var borderPen = new Pen(borderColor, Math.Max(1f, borderWidth)))
                    {
                        g.DrawPath(borderPen, cardPath);
                    }
                }

                // 4. Header Labels & Execution Status Badge
                var headerScreen = Transform.WorldToScreen(node.GetHeaderBounds());
                float titlePaddingLeft = 10f * Transform.Zoom;
                float titleX = headerScreen.X + titlePaddingLeft;
                float titleY = headerScreen.Y + 4f * Transform.Zoom;

                // Status Indicator Dot (Right side of header)
                float badgeSize = 8f * Transform.Zoom;
                float badgeX = headerScreen.Right - 14f * Transform.Zoom;
                float badgeY = headerScreen.Y + (headerScreen.Height - badgeSize) * 0.5f;
                Color statusColor = node.State switch
                {
                    NodeState.Running => Color.FromArgb(0, 229, 255),
                    NodeState.Completed => Color.FromArgb(0, 230, 118),
                    NodeState.Faulted => Color.FromArgb(255, 23, 68),
                    _ => Color.FromArgb(120, 144, 156)
                };

                using (var badgeBrush = new SolidBrush(statusColor))
                {
                    g.FillEllipse(badgeBrush, badgeX, badgeY, badgeSize, badgeSize);
                }

                // Node Title & Subtitle
                using (var titleBrush = new SolidBrush(TextPrimaryColor))
                using (var subBrush = new SolidBrush(TextSecondaryColor))
                {
                    g.DrawString(node.Name, _titleFont, titleBrush, titleX, titleY);

                    string subText = $"{node.NodeType} [{node.Category}]";
                    if (node.LastExecutionDurationMs > 0)
                    {
                        subText += $" • {node.LastExecutionDurationMs:F2} ms";
                    }
                    g.DrawString(subText, _subtitleFont, subBrush, titleX, titleY + 14f * Transform.Zoom);
                }

                // 5. Draw Pins
                DrawNodePins(g, node);
            }
        }

        private void DrawNodePins(Graphics g, CanvasNode node)
        {
            float pinRadius = CanvasPortPin.PinRadius * Transform.Zoom;

            // Draw Inputs
            foreach (var pin in node.Inputs)
            {
                PointF centerS = Transform.WorldToScreen(pin.GetWorldCenter());
                bool isHovered = (pin == _hoveredPin);

                using (var pinBrush = new SolidBrush(pin.PinColor))
                using (var strokePen = new Pen(isHovered ? Color.White : Color.FromArgb(18, 21, 28), 1.5f))
                {
                    g.FillEllipse(pinBrush, centerS.X - pinRadius, centerS.Y - pinRadius, pinRadius * 2, pinRadius * 2);
                    g.DrawEllipse(strokePen, centerS.X - pinRadius, centerS.Y - pinRadius, pinRadius * 2, pinRadius * 2);
                }

                // Pin label
                using (var textBrush = new SolidBrush(TextPrimaryColor))
                {
                    g.DrawString(pin.Name, _pinFont, textBrush, centerS.X + pinRadius + 4f, centerS.Y - 6f);
                }
            }

            // Draw Outputs
            foreach (var pin in node.Outputs)
            {
                PointF centerS = Transform.WorldToScreen(pin.GetWorldCenter());
                bool isHovered = (pin == _hoveredPin);

                using (var pinBrush = new SolidBrush(pin.PinColor))
                using (var strokePen = new Pen(isHovered ? Color.White : Color.FromArgb(18, 21, 28), 1.5f))
                {
                    g.FillEllipse(pinBrush, centerS.X - pinRadius, centerS.Y - pinRadius, pinRadius * 2, pinRadius * 2);
                    g.DrawEllipse(strokePen, centerS.X - pinRadius, centerS.Y - pinRadius, pinRadius * 2, pinRadius * 2);
                }

                // Pin label (right aligned towards left)
                var size = g.MeasureString(pin.Name, _pinFont);
                using (var textBrush = new SolidBrush(TextPrimaryColor))
                {
                    g.DrawString(pin.Name, _pinFont, textBrush, centerS.X - pinRadius - size.Width - 4f, centerS.Y - 6f);
                }
            }
        }

        private void DrawOverlayMetrics(Graphics g)
        {
            string overlay = $"Nodes: {Nodes.Count} | Wires: {Connections.Count} | Zoom: {(int)(Transform.Zoom * 100)}%";
            var size = g.MeasureString(overlay, _badgeFont);

            float pillW = size.Width + 16f;
            float pillH = size.Height + 8f;
            var pillRect = new RectangleF(Width - pillW - 12f, Height - pillH - 12f, pillW, pillH);

            using (var path = ZeroUIConfig.CreateRoundedRectangleF(pillRect, 4f))
            using (var bgBrush = new SolidBrush(Color.FromArgb(180, 24, 29, 40)))
            using (var borderPen = new Pen(Color.FromArgb(60, 75, 100), 1f))
            using (var textBrush = new SolidBrush(Color.FromArgb(180, 200, 225)))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
                g.DrawString(overlay, _badgeFont, textBrush, pillRect.X + 8f, pillRect.Y + 4f);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _subtitleFont.Dispose();
                _pinFont.Dispose();
                _badgeFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
