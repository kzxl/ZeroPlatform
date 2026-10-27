using System;
using System.Collections.Generic;
using ZeroUI.Core.Common;
using ZeroUI.Core.Scene.Routing;
using ZeroUI.Core.Scene.Svg;
using ZeroUI.WinForms.Icons;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scene;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Single-HWND high-performance SCADA plant mimic canvas powered by ZeroScene graph engine.
    /// Solves the Windows 10,000 HWND limit trap by rendering thousands of process elements
    /// (Tanks, Valves, Pumps, Pipes, Sensors) on a single virtual viewport with Pan, Zoom, and Spatial Grid Culling.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Single-HWND high-performance P&ID plant mimic canvas with ZeroScene graph engine and spatial culling")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroPlantMimicCanvas.bmp")]
    public class ZPlantMimicCanvas : Control
    {
        private ZeroScene _scene;
        private readonly List<SceneNode> _visibleNodesBuffer = new List<SceneNode>(256);
        private readonly List<IScadaDrawable> _elements = new List<IScadaDrawable>();
        private readonly IDisposable? _animationSub;
        private long _lastTick = MonotonicClock.ElapsedMilliseconds;
        private float _zoomFactor = 1.0f;
        private float _panOffsetX = 0f;
        private float _panOffsetY = 0f;
        private bool _isPanning = false;
        private Point _lastMousePos;
        private IScadaDrawable? _hoveredElement;
        private IScadaDrawable? _selectedElement;

        public event EventHandler<IScadaDrawable>? ElementClicked;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ZeroScene Scene
        {
            get => _scene;
            set
            {
                if (_scene != value)
                {
                    if (_scene != null) _scene.SceneDirty -= OnSceneDirty;
                    _scene = value ?? new ZeroScene();
                    _scene.SceneDirty += OnSceneDirty;
                    Invalidate();
                }
            }
        }

        [Category("Viewport")]
        [DefaultValue(1.0f)]
        public float ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                _zoomFactor = Math.Max(0.25f, Math.Min(4.0f, value));
                Invalidate();
            }
        }

        [Category("Viewport")]
        [DefaultValue(0f)]
        public float PanOffsetX
        {
            get => _panOffsetX;
            set { _panOffsetX = value; Invalidate(); }
        }

        [Category("Viewport")]
        [DefaultValue(0f)]
        public float PanOffsetY
        {
            get => _panOffsetY;
            set { _panOffsetY = value; Invalidate(); }
        }

        [Browsable(false)]
        public List<IScadaDrawable> Elements => _elements;

        public ZPlantMimicCanvas()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor |
                ControlStyles.Selectable, true);

            Size = new Size(800, 500);
            BackColor = Color.FromArgb(15, 23, 42); // Industrial slate dark

            _scene = new ZeroScene();
            _scene.SceneDirty += OnSceneDirty;

            // Centralized deterministic animation scheduling via ZeroRuntime
            _animationSub = ZeroRuntime.Shared.Register(RuntimeCycle.Animation, (delta, count) => OnAnimationTick());
            if (!ZeroRuntime.Shared.IsRunning)
            {
                ZeroRuntime.Shared.Start();
            }
        }

        private void OnSceneDirty(object? sender, EventArgs e)
        {
            Invalidate();
        }

        private void OnAnimationTick()
        {
            long now = MonotonicClock.ElapsedMilliseconds;
            long elapsed = Math.Max(1, now - _lastTick);
            _lastTick = now;

            // Slow Tier (30-60 Hz): Flush coalesced dirty tags to visual controls
            if (ZeroTagEngine.IsDecoupledUiMode)
            {
                ZeroTagEngine.FlushUiBatch(1024);
            }

            for (int i = 0; i < _scene.RootNodes.Count; i++)
            {
                _scene.RootNodes[i].UpdateAnimation(elapsed);
            }
        }

        public void AddNode(SceneNode node)
        {
            _scene.AddNode(node);
        }

        public bool RemoveNode(SceneNode node)
        {
            return _scene.RemoveNode(node);
        }

        public void AddElement(IScadaDrawable element)
        {
            if (element == null) return;
            if (element is SceneNode sn)
            {
                AddNode(sn);
                return;
            }

            if (!_elements.Contains(element))
            {
                _elements.Add(element);
                Invalidate();
            }
        }

        public void RemoveElement(IScadaDrawable element)
        {
            if (element == null) return;
            if (element is SceneNode sn)
            {
                RemoveNode(sn);
                return;
            }

            if (_elements.Remove(element))
            {
                Invalidate();
            }
        }

        public void ClearElements()
        {
            _scene.Clear();
            _elements.Clear();
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && ModifierKeys == Keys.Space))
            {
                _isPanning = true;
                _lastMousePos = e.Location;
                Cursor = Cursors.SizeAll;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                var worldPt = ScreenToWorld(e.Location);
                IScadaDrawable? hit = _scene.HitTest(worldPt.X, worldPt.Y) ?? FindElementAt(worldPt.X, worldPt.Y);

                if (_selectedElement != null) _selectedElement.IsSelected = false;
                _selectedElement = hit;
                _scene.SelectedNode = hit as SceneNode;

                if (_selectedElement != null)
                {
                    _selectedElement.IsSelected = true;
                    ElementClicked?.Invoke(this, _selectedElement);
                }

                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isPanning)
            {
                float dx = e.X - _lastMousePos.X;
                float dy = e.Y - _lastMousePos.Y;
                _panOffsetX += dx;
                _panOffsetY += dy;
                _lastMousePos = e.Location;
                Invalidate();
                return;
            }

            var worldPt = ScreenToWorld(e.Location);
            var hit = (IScadaDrawable?)_scene.HitTest(worldPt.X, worldPt.Y) ?? FindElementAt(worldPt.X, worldPt.Y);

            if (hit != _hoveredElement)
            {
                if (_hoveredElement != null) _hoveredElement.IsHovered = false;
                _hoveredElement = hit;
                _scene.HoveredNode = hit as SceneNode;

                if (_hoveredElement != null) _hoveredElement.IsHovered = true;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPanning)
            {
                _isPanning = false;
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float zoomDelta = e.Delta > 0 ? 1.15f : 0.85f;
            float newZoom = Math.Max(0.25f, Math.Min(4.0f, _zoomFactor * zoomDelta));

            // Zoom centered on mouse pointer
            float mouseWorldX = (e.X - _panOffsetX) / _zoomFactor;
            float mouseWorldY = (e.Y - _panOffsetY) / _zoomFactor;

            _panOffsetX = e.X - mouseWorldX * newZoom;
            _panOffsetY = e.Y - mouseWorldY * newZoom;
            _zoomFactor = newZoom;

            Invalidate();
        }

        private PointF ScreenToWorld(Point screenPt)
        {
            return new PointF(
                (screenPt.X - _panOffsetX) / _zoomFactor,
                (screenPt.Y - _panOffsetY) / _zoomFactor
            );
        }

        private IScadaDrawable? FindElementAt(float wx, float wy)
        {
            for (int i = _elements.Count - 1; i >= 0; i--)
            {
                var el = _elements[i];
                if (!el.IsVisible) continue;
                if (el.HitTest(wx, wy))
                {
                    return el;
                }
            }
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isDark = ZeroTheme.IsDark;
            Color canvasBg = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(241, 245, 249);
            g.Clear(canvasBg);

            // 1. Draw Grid Lines (transformed)
            DrawGrid(g, isDark);

            // 2. Setup Viewport Matrix
            var matrix = new Matrix();
            matrix.Translate(_panOffsetX, _panOffsetY);
            matrix.Scale(_zoomFactor, _zoomFactor);
            g.Transform = matrix;

            // Viewport bounds in world coordinates for spatial culling
            float vwLeft = -_panOffsetX / _zoomFactor;
            float vwTop = -_panOffsetY / _zoomFactor;
            float vwRight = vwLeft + (Width / _zoomFactor);
            float vwBottom = vwTop + (Height / _zoomFactor);
            var viewportRect = new SceneRect(vwLeft, vwTop, vwRight - vwLeft, vwBottom - vwTop);

            // 3. Render ZeroScene Nodes with Spatial Culling
            _scene.QueryVisibleNodes(viewportRect, _visibleNodesBuffer);
            var renderContext = new RenderContext(viewportRect, _zoomFactor, isDark, MonotonicClock.ElapsedMilliseconds);

            for (int i = 0; i < _visibleNodesBuffer.Count; i++)
            {
                var node = _visibleNodesBuffer[i];
                if (node is ZeroSceneNode zsn)
                {
                    RenderZeroSceneNode(g, zsn, renderContext);
                }
                else
                {
                    node.Render(g, renderContext);
                }
            }

            // 4. Render Legacy/Fallback Elements (if any)
            if (_elements.Count > 0)
            {
                var gdiViewport = new RectangleF(vwLeft, vwTop, vwRight - vwLeft, vwBottom - vwTop);
                using (var elemBorderPen = new Pen(Color.FromArgb(59, 130, 246), 1.5f))
                using (var selPen = new Pen(Color.FromArgb(245, 158, 11), 2.5f) { DashStyle = DashStyle.Dash })
                using (var textBrush = new SolidBrush(isDark ? Color.White : Color.Black))
                using (var font = new Font("Segoe UI", 8f, FontStyle.Bold))
                {
                    for (int i = 0; i < _elements.Count; i++)
                    {
                        var el = _elements[i];
                        if (!el.IsVisible) continue;

                        var elBounds = new RectangleF(el.X, el.Y, el.Width, el.Height);
                        if (!gdiViewport.IntersectsWith(elBounds))
                            continue;

                        using (var bodyBrush = new SolidBrush(isDark ? Color.FromArgb(30, 41, 59) : Color.White))
                        {
                            g.FillRectangle(bodyBrush, elBounds);
                        }

                        if (el.IsSelected)
                        {
                            g.DrawRectangle(selPen, elBounds.X - 2, elBounds.Y - 2, elBounds.Width + 4, elBounds.Height + 4);
                        }
                        else
                        {
                            g.DrawRectangle(elemBorderPen, elBounds.X, elBounds.Y, elBounds.Width, elBounds.Height);
                        }

                        g.DrawString(el.Label, font, textBrush, el.X + 4, el.Y + 4);
                    }
                }
            }

            g.ResetTransform();

            // 5. Viewport HUD (Zoom & Spatial node counts)
            using (var hudFont = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var hudBrush = new SolidBrush(Color.FromArgb(148, 163, 184)))
            {
                int totalCount = _scene.RootNodes.Count + _elements.Count;
                string hud = $"ZOOM: {_zoomFactor * 100:0}% | NODES: {totalCount} | VISIBLE: {_visibleNodesBuffer.Count} | PAN: [SPACE+DRAG]";
                g.DrawString(hud, hudFont, hudBrush, 10, Height - 22);
            }
        }

        private void RenderZeroSceneNode(Graphics g, ZeroSceneNode node, in RenderContext context)
        {
            if (!node.IsVisible) return;

            var bounds = node.WorldBounds;
            float x = bounds.X;
            float y = bounds.Y;
            float w = bounds.Width;
            float h = bounds.Height;
            bool isDark = context.IsDarkTheme;

            Color borderColor = isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225);
            Color bodyColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(248, 250, 252);
            Color textColor = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(15, 23, 42);
            Color subTextColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            switch (node.NodeType)
            {
                case IndustrialNodeType.Tank:
                    using (var shellBrush = new SolidBrush(bodyColor))
                    using (var shellPen = new Pen(borderColor, 1.5f))
                    using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    using (var subBrush = new SolidBrush(subTextColor))
                    {
                        g.FillRectangle(shellBrush, x, y, w, h);

                        // Liquid Fill
                        double pct = Math.Max(0.0, Math.Min(100.0, node.Value));
                        float fillH = (float)(h * (pct / 100.0));
                        float fillY = y + h - fillH;
                        if (fillH > 0)
                        {
                            Color fluidColor = node.State == ScadaNodeState.Fault ? Color.FromArgb(239, 68, 68) : Color.FromArgb(14, 165, 233);
                            using (var fluidBrush = new SolidBrush(Color.FromArgb(180, fluidColor)))
                            {
                                g.FillRectangle(fluidBrush, x + 1, fillY, w - 2, fillH);
                            }
                        }

                        g.DrawRectangle(shellPen, x, y, w, h);
                        g.DrawString(node.Label, font, textBrush, x + 4, y + 4);
                        g.DrawString($"{pct:0.0} %", font, subBrush, x + 4, y + 20);
                    }
                    break;

                case IndustrialNodeType.Pump:
                    using (var pumpBrush = new SolidBrush(bodyColor))
                    using (var pumpPen = new Pen(borderColor, 1.5f))
                    using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    using (var subBrush = new SolidBrush(subTextColor))
                    {
                        g.FillEllipse(pumpBrush, x, y, w, h);

                        // Running state indicator
                        Color ringColor = node.State == ScadaNodeState.Running ? Color.FromArgb(16, 185, 129) :
                                          node.State == ScadaNodeState.Fault ? Color.FromArgb(239, 68, 68) : borderColor;
                        using (var statePen = new Pen(ringColor, 2.5f))
                        {
                            g.DrawEllipse(statePen, x + 2, y + 2, w - 4, h - 4);
                        }

                        g.DrawEllipse(pumpPen, x, y, w, h);

                        // Impeller cross
                        float cx = x + w / 2f;
                        float cy = y + h / 2f;
                        float r = Math.Min(w, h) * 0.3f;
                        using (var impellerPen = new Pen(ringColor, 1.5f))
                        {
                            g.DrawLine(impellerPen, cx - r, cy, cx + r, cy);
                            g.DrawLine(impellerPen, cx, cy - r, cx, cy + r);
                        }

                        g.DrawString(node.Label, font, textBrush, x, y + h + 2);
                        if (node.Value > 0)
                        {
                            g.DrawString($"{node.Value:0} RPM", font, subBrush, x, y + h + 15);
                        }
                    }
                    break;

                case IndustrialNodeType.Sensor:
                    using (var pillBrush = new SolidBrush(bodyColor))
                    using (var pillPen = new Pen(borderColor, 1.2f))
                    using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    using (var subBrush = new SolidBrush(subTextColor))
                    {
                        g.FillRectangle(pillBrush, x, y, w, h);
                        g.DrawRectangle(pillPen, x, y, w, h);

                        Color statusDot = node.State == ScadaNodeState.Fault ? Color.FromArgb(239, 68, 68) :
                                          node.State == ScadaNodeState.Warning ? Color.FromArgb(245, 158, 11) : Color.FromArgb(16, 185, 129);
                        using (var dotBrush = new SolidBrush(statusDot))
                        {
                            g.FillEllipse(dotBrush, x + 4, y + 6, 6, 6);
                        }

                        g.DrawString(node.Label, font, subBrush, x + 14, y + 3);
                        string valText = $"{node.Value:0.0} {node.EngineeringUnit}".Trim();
                        g.DrawString(valText, font, textBrush, x + 4, y + 17);
                    }
                    break;

                case IndustrialNodeType.Valve:
                    using (var bodyBrush = new SolidBrush(bodyColor))
                    using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        Color valveColor = node.State == ScadaNodeState.Running ? Color.FromArgb(16, 185, 129) :
                                           node.State == ScadaNodeState.Fault ? Color.FromArgb(239, 68, 68) : borderColor;
                        using (var vBrush = new SolidBrush(valveColor))
                        using (var vPen = new Pen(valveColor, 1.5f))
                        {
                            // Bowtie: 2 triangles meeting at center
                            var ptsLeft = new PointF[] { new PointF(x, y), new PointF(x, y + h), new PointF(x + w / 2f, y + h / 2f) };
                            var ptsRight = new PointF[] { new PointF(x + w, y), new PointF(x + w, y + h), new PointF(x + w / 2f, y + h / 2f) };
                            g.FillPolygon(vBrush, ptsLeft);
                            g.FillPolygon(vBrush, ptsRight);
                            g.DrawPolygon(vPen, ptsLeft);
                            g.DrawPolygon(vPen, ptsRight);

                            // Actuator Stem
                            g.DrawLine(vPen, x + w / 2f, y + h / 2f, x + w / 2f, y - 6);
                            g.DrawRectangle(vPen, x + w / 2f - 6, y - 12, 12, 6);
                        }

                        g.DrawString(node.Label, font, textBrush, x, y + h + 2);
                    }
                    break;

                case IndustrialNodeType.Motor:
                    using (var motorBrush = new SolidBrush(bodyColor))
                    using (var motorPen = new Pen(borderColor, 1.5f))
                    using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    using (var subBrush = new SolidBrush(subTextColor))
                    {
                        g.FillRectangle(motorBrush, x, y, w, h);
                        g.DrawRectangle(motorPen, x, y, w, h);

                        // Cooling fins
                        using (var finPen = new Pen(borderColor, 1f))
                        {
                            for (float fy = y + 8; fy < y + h - 6; fy += 6)
                            {
                                g.DrawLine(finPen, x + 4, fy, x + w - 4, fy);
                            }
                        }

                        g.DrawString(node.Label, font, textBrush, x + 4, y + 3);
                        g.DrawString($"{node.Value:0} RPM", font, subBrush, x + 4, y + h - 16);
                    }
                    break;

                default:
                    using (var genBrush = new SolidBrush(bodyColor))
                    using (var genPen = new Pen(borderColor, 1f))
                    using (var font = new Font("Segoe UI", 7.5f, FontStyle.Bold))
                    using (var textBrush = new SolidBrush(textColor))
                    {
                        g.FillRectangle(genBrush, x, y, w, h);
                        g.DrawRectangle(genPen, x, y, w, h);
                        g.DrawString(node.Label, font, textBrush, x + 4, y + 4);
                    }
                    break;
            }

            // Selection & Hover highlights
            if (node.IsSelected)
            {
                using (var selPen = new Pen(Color.FromArgb(245, 158, 11), 2f) { DashStyle = DashStyle.Dash })
                {
                    g.DrawRectangle(selPen, x - 3, y - 3, w + 6, h + 6);
                }
            }
            else if (node.IsHovered)
            {
                using var hovPen = new Pen(Color.FromArgb(59, 130, 246), 1.5f);
                g.DrawRectangle(hovPen, x - 2, y - 2, w + 4, h + 4);
            }

            // Status & Safety Badges (LOTO, Tagout, Interlock, Fault)
            if (node.StatusFlags != ZeroUI.Core.Scada.Safety.DeviceStatusFlags.None)
            {
                var nodeRect = new Rectangle((int)x, (int)y, (int)w, (int)h);
                ZDeviceBadgeRenderer.DrawBadges(g, nodeRect, node.StatusFlags, isDark);
            }
        }


        private void DrawGrid(Graphics g, bool isDark)
        {
            Color gridColor = isDark ? Color.FromArgb(25, 35, 55) : Color.FromArgb(226, 232, 240);
            using (var gridPen = new Pen(gridColor, 1f))
            {
                float gridSpacing = 40f * _zoomFactor;
                float startX = _panOffsetX % gridSpacing;
                float startY = _panOffsetY % gridSpacing;

                for (float x = startX; x < Width; x += gridSpacing)
                {
                    g.DrawLine(gridPen, x, 0, x, Height);
                }

                for (float y = startY; y < Height; y += gridSpacing)
                {
                    g.DrawLine(gridPen, 0, y, Width, y);
                }
            }
        }

        /// <summary>
        /// Imports an SVG diagram string into the scene at the specified coordinates.
        /// </summary>
        public SvgVectorNode LoadSvg(string svgXml, float x = 0f, float y = 0f)
        {
            var node = SvgSceneImporter.ImportFromString(svgXml);
            node.X = x;
            node.Y = y;
            _scene.AddNode(node);
            Invalidate();
            return node;
        }

        /// <summary>
        /// Computes an optimal 90-degree orthogonal Manhattan pipe route between two scene points.
        /// </summary>
        public List<ScenePoint> RoutePipeManhattan(ScenePoint source, ScenePoint target, PortDirection srcDir = PortDirection.Auto, PortDirection tgtDir = PortDirection.Auto)
        {
            return PipeRouter.RouteManhattan(source, target, srcDir, tgtDir);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationSub?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZPlantMimicCanvas"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPlantMimicCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZPlantMimicCanvas instead.")]
    [ToolboxItem(false)]
    public class ZeroPlantMimicCanvas : ZPlantMimicCanvas
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPlantMimicCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PlantMimicCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZPlantMimicCanvas instead.")]
    [ToolboxItem(false)]
    public class PlantMimicCanvas : ZPlantMimicCanvas
    {
    }

    #endregion
}
