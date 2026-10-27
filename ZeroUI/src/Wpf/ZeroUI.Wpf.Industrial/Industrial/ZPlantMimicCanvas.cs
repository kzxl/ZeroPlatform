using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Common;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scene;
using ZeroUI.Core.Scene.Routing;
using ZeroUI.Core.Scene.Svg;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Single-Visual high-performance SCADA plant mimic canvas for WPF powered by ZeroScene graph engine.
    /// Provides zero-visual-tree overhead, spatial culling, interactive pan/zoom (25% to 400%),
    /// and full vector rendering for all industrial node archetypes (Tanks, Pumps, Sensors, Valves, Pipes, Motors).
    /// </summary>
    public class ZPlantMimicCanvas : FrameworkElement
    {
        private ZeroScene _scene;
        private readonly List<SceneNode> _visibleNodesBuffer = new List<SceneNode>(256);
        private double _zoomFactor = 1.0;
        private double _panOffsetX = 0.0;
        private double _panOffsetY = 0.0;
        private bool _isPanning = false;
        private Point _lastMousePos;
        private SceneNode? _hoveredNode;
        private SceneNode? _selectedNode;

        public event EventHandler<SceneNode>? NodeClicked;

        #region Dependency Properties

        public static readonly DependencyProperty SceneProperty =
            DependencyProperty.Register(nameof(Scene), typeof(ZeroScene), typeof(ZPlantMimicCanvas),
                new FrameworkPropertyMetadata(null, OnSceneChanged));

        public static readonly DependencyProperty ZoomFactorProperty =
            DependencyProperty.Register(nameof(ZoomFactor), typeof(double), typeof(ZPlantMimicCanvas),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnZoomFactorChanged));

        #endregion

        #region Properties

        public ZeroScene Scene
        {
            get => _scene;
            set
            {
                SetValue(SceneProperty, value);
                SetSceneInternal(value);
            }
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                double clamped = Math.Max(0.25, Math.Min(4.0, value));
                SetValue(ZoomFactorProperty, clamped);
                _zoomFactor = clamped;
                InvalidateVisual();
            }
        }

        public SceneNode? SelectedNode => _selectedNode;

        #endregion

        public ZPlantMimicCanvas()
        {
            ClipToBounds = true;
            Focusable = true;
            _scene = new ZeroScene();
            _scene.SceneDirty += OnSceneDirty;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnSceneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPlantMimicCanvas canvas)
            {
                canvas.SetSceneInternal((ZeroScene)e.NewValue);
            }
        }

        private static void OnZoomFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPlantMimicCanvas canvas)
            {
                canvas._zoomFactor = (double)e.NewValue;
            }
        }

        private void SetSceneInternal(ZeroScene scene)
        {
            if (_scene != null)
            {
                _scene.SceneDirty -= OnSceneDirty;
            }
            _scene = scene ?? new ZeroScene();
            _scene.SceneDirty += OnSceneDirty;
            InvalidateVisual();
        }

        private void OnSceneDirty(object? sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(InvalidateVisual, System.Windows.Threading.DispatcherPriority.Render);
        }

        #region Interactive Pan & Zoom

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed ||
                (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space)))
            {
                _isPanning = true;
                _lastMousePos = e.GetPosition(this);
                Cursor = Cursors.SizeAll;
                CaptureMouse();
                e.Handled = true;
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePt = e.GetPosition(this);
                Point worldPt = ScreenToWorld(mousePt);

                var hit = FindNodeAt((float)worldPt.X, (float)worldPt.Y);
                _selectedNode = hit;
                NodeClicked?.Invoke(this, hit!);
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point currentPos = e.GetPosition(this);

            if (_isPanning && IsMouseCaptured)
            {
                double dx = currentPos.X - _lastMousePos.X;
                double dy = currentPos.Y - _lastMousePos.Y;
                _panOffsetX += dx;
                _panOffsetY += dy;
                _lastMousePos = currentPos;
                InvalidateVisual();
                return;
            }

            Point worldPt = ScreenToWorld(currentPos);
            var hit = FindNodeAt((float)worldPt.X, (float)worldPt.Y);
            if (_hoveredNode != hit)
            {
                _hoveredNode = hit;
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isPanning && IsMouseCaptured)
            {
                _isPanning = false;
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            Point mousePos = e.GetPosition(this);

            double zoomDelta = e.Delta > 0 ? 1.15 : 0.87;
            double newZoom = Math.Max(0.25, Math.Min(4.0, _zoomFactor * zoomDelta));

            double mouseWorldX = (mousePos.X - _panOffsetX) / _zoomFactor;
            double mouseWorldY = (mousePos.Y - _panOffsetY) / _zoomFactor;

            _panOffsetX = mousePos.X - mouseWorldX * newZoom;
            _panOffsetY = mousePos.Y - mouseWorldY * newZoom;
            ZoomFactor = newZoom;
            e.Handled = true;
        }

        public void ResetView()
        {
            _panOffsetX = 0.0;
            _panOffsetY = 0.0;
            ZoomFactor = 1.0;
            InvalidateVisual();
        }

        private Point ScreenToWorld(Point screenPt)
        {
            return new Point(
                (screenPt.X - _panOffsetX) / _zoomFactor,
                (screenPt.Y - _panOffsetY) / _zoomFactor
            );
        }

        private SceneNode? FindNodeAt(float wx, float wy)
        {
            for (int i = _visibleNodesBuffer.Count - 1; i >= 0; i--)
            {
                var node = _visibleNodesBuffer[i];
                if (!node.IsVisible) continue;
                var b = node.WorldBounds;
                if (wx >= b.X && wx <= b.Right && wy >= b.Y && wy <= b.Bottom)
                {
                    return node;
                }
            }
            return null;
        }

        #endregion

        #region Cached Frozen Drawing Resources

        private static readonly Brush DarkCanvasBg;
        private static readonly Brush LightCanvasBg;
        private static readonly Brush DarkBodyBrush;
        private static readonly Brush LightBodyBrush;
        private static readonly Pen DarkBorderPen;
        private static readonly Pen LightBorderPen;
        private static readonly Brush DarkTextBrush;
        private static readonly Brush LightTextBrush;
        private static readonly Brush DarkSubTextBrush;
        private static readonly Brush LightSubTextBrush;
        private static readonly Pen SelectionPen;
        private static readonly Pen HoverPen;
        private static readonly Pen RunningRingPen;
        private static readonly Pen WarningRingPen;
        private static readonly Pen FaultRingPen;
        private static readonly Pen StoppedRingPen;
        private static readonly Pen RunningImpellerPen;
        private static readonly Pen WarningImpellerPen;
        private static readonly Pen FaultImpellerPen;
        private static readonly Pen StoppedImpellerPen;
        private static readonly Brush NormalFluidBrush;
        private static readonly Brush FaultFluidBrush;
        private static readonly Brush NormalStatusDotBrush;
        private static readonly Brush WarningStatusDotBrush;
        private static readonly Brush FaultStatusDotBrush;
        private static readonly Brush RunningValveBrush;
        private static readonly Brush FaultValveBrush;
        private static readonly Brush StoppedValveBrush;

        static ZPlantMimicCanvas()
        {
            DarkCanvasBg = new SolidColorBrush(Color.FromRgb(15, 23, 42)); DarkCanvasBg.Freeze();
            LightCanvasBg = new SolidColorBrush(Color.FromRgb(241, 245, 249)); LightCanvasBg.Freeze();

            DarkBodyBrush = new SolidColorBrush(Color.FromRgb(30, 41, 59)); DarkBodyBrush.Freeze();
            LightBodyBrush = new SolidColorBrush(Color.FromRgb(248, 250, 252)); LightBodyBrush.Freeze();

            DarkBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(71, 85, 105)), 1.5); DarkBorderPen.Freeze();
            LightBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(203, 213, 225)), 1.5); LightBorderPen.Freeze();

            DarkTextBrush = new SolidColorBrush(Color.FromRgb(241, 245, 249)); DarkTextBrush.Freeze();
            LightTextBrush = new SolidColorBrush(Color.FromRgb(15, 23, 42)); LightTextBrush.Freeze();

            DarkSubTextBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)); DarkSubTextBrush.Freeze();
            LightSubTextBrush = new SolidColorBrush(Color.FromRgb(100, 116, 139)); LightSubTextBrush.Freeze();

            SelectionPen = new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 2.0); SelectionPen.Freeze();
            HoverPen = new Pen(new SolidColorBrush(Color.FromArgb(120, 59, 130, 246)), 1.5); HoverPen.Freeze();

            RunningRingPen = new Pen(new SolidColorBrush(Color.FromRgb(16, 185, 129)), 2.5); RunningRingPen.Freeze();
            WarningRingPen = new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 2.5); WarningRingPen.Freeze();
            FaultRingPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 2.5); FaultRingPen.Freeze();
            StoppedRingPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 2.5); StoppedRingPen.Freeze();

            RunningImpellerPen = new Pen(new SolidColorBrush(Color.FromRgb(16, 185, 129)), 1.5); RunningImpellerPen.Freeze();
            WarningImpellerPen = new Pen(new SolidColorBrush(Color.FromRgb(245, 158, 11)), 1.5); WarningImpellerPen.Freeze();
            FaultImpellerPen = new Pen(new SolidColorBrush(Color.FromRgb(239, 68, 68)), 1.5); FaultImpellerPen.Freeze();
            StoppedImpellerPen = new Pen(new SolidColorBrush(Color.FromRgb(100, 116, 139)), 1.5); StoppedImpellerPen.Freeze();

            NormalFluidBrush = new SolidColorBrush(Color.FromArgb(180, 14, 165, 233)); NormalFluidBrush.Freeze();
            FaultFluidBrush = new SolidColorBrush(Color.FromArgb(180, 239, 68, 68)); FaultFluidBrush.Freeze();

            NormalStatusDotBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129)); NormalStatusDotBrush.Freeze();
            WarningStatusDotBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)); WarningStatusDotBrush.Freeze();
            FaultStatusDotBrush = new SolidColorBrush(Color.FromRgb(239, 68, 68)); FaultStatusDotBrush.Freeze();

            RunningValveBrush = new SolidColorBrush(Color.FromArgb(140, 16, 185, 129)); RunningValveBrush.Freeze();
            FaultValveBrush = new SolidColorBrush(Color.FromArgb(140, 239, 68, 68)); FaultValveBrush.Freeze();
            StoppedValveBrush = new SolidColorBrush(Color.FromArgb(140, 148, 163, 184)); StoppedValveBrush.Freeze();
        }

        #endregion

        #region Render Pipeline

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;

            // 1. Fill Canvas Background
            Brush canvasBg = isDark ? DarkCanvasBg : LightCanvasBg;
            dc.DrawRectangle(canvasBg, null, new Rect(0, 0, w, h));

            // 2. Draw Transformed Grid Lines
            DrawGridLines(dc, w, h, isDark);

            // 3. Setup Transform
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(_zoomFactor, _zoomFactor));
            transformGroup.Children.Add(new TranslateTransform(_panOffsetX, _panOffsetY));
            dc.PushTransform(transformGroup);

            // 4. Query Visible Nodes via Spatial Culling
            float vwLeft = (float)(-_panOffsetX / _zoomFactor);
            float vwTop = (float)(-_panOffsetY / _zoomFactor);
            float vwWidth = (float)(w / _zoomFactor);
            float vwHeight = (float)(h / _zoomFactor);
            var viewportRect = new SceneRect(vwLeft, vwTop, vwWidth, vwHeight);

            _scene.QueryVisibleNodes(viewportRect, _visibleNodesBuffer);
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            var renderContext = new RenderContext(viewportRect, (float)_zoomFactor, isDark, MonotonicClock.ElapsedMilliseconds);

            // 5. Render Scene Nodes (both ZeroSceneNode and polymorphic SceneNode subclasses)
            for (int i = 0; i < _visibleNodesBuffer.Count; i++)
            {
                var node = _visibleNodesBuffer[i];
                if (node is ZeroSceneNode zsn)
                {
                    RenderZeroSceneNode(dc, zsn, isDark, dpi);
                }
                else if (node is SvgVectorNode svgNode)
                {
                    RenderSvgVectorNode(dc, svgNode, isDark);
                }
                else
                {
                    node.Render(dc, renderContext);
                }
            }

            dc.Pop(); // Restore transform

            // 6. HUD Diagnostics Overlay
            string hud = $"ZOOM: {_zoomFactor * 100:0}% | NODES: {_scene.RootNodes.Count} | VISIBLE: {_visibleNodesBuffer.Count} | PAN: [SPACE+DRAG / RIGHT-CLICK]";
            var hudFt = new FormattedText(
                hud,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.MediumTypeface,
                10.5,
                ZeroWpfTheme.TextMuted,
                dpi);

            dc.DrawText(hudFt, new Point(12, h - 22));
        }

        private void DrawGridLines(DrawingContext dc, double w, double h, bool isDark)
        {
            double gridSize = 40.0 * _zoomFactor;
            if (gridSize < 12.0) return;

            Color gridColor = isDark ? Color.FromArgb(18, 255, 255, 255) : Color.FromArgb(18, 0, 0, 0);
            Pen gridPen = new Pen(new SolidColorBrush(gridColor), 1.0);
            gridPen.Freeze();

            double startX = _panOffsetX % gridSize;
            for (double x = startX; x < w; x += gridSize)
            {
                dc.DrawLine(gridPen, new Point(x, 0), new Point(x, h));
            }

            double startY = _panOffsetY % gridSize;
            for (double y = startY; y < h; y += gridSize)
            {
                dc.DrawLine(gridPen, new Point(0, y), new Point(w, y));
            }
        }

        private void RenderZeroSceneNode(DrawingContext dc, ZeroSceneNode node, bool isDark, double dpi)
        {
            if (!node.IsVisible) return;

            var b = node.WorldBounds;
            double x = b.X;
            double y = b.Y;
            double nw = b.Width;
            double nh = b.Height;

            Brush bodyBrush = isDark ? DarkBodyBrush : LightBodyBrush;
            Pen borderPen = isDark ? DarkBorderPen : LightBorderPen;
            Brush textBrush = isDark ? DarkTextBrush : LightTextBrush;
            Brush subTextBrush = isDark ? DarkSubTextBrush : LightSubTextBrush;

            Rect nodeRect = new Rect(x, y, nw, nh);

            // Selection Glow
            if (node == _selectedNode)
            {
                dc.DrawRoundedRectangle(null, SelectionPen, new Rect(x - 3, y - 3, nw + 6, nh + 6), 6, 6);
            }
            else if (node == _hoveredNode)
            {
                dc.DrawRoundedRectangle(null, HoverPen, new Rect(x - 2, y - 2, nw + 4, nh + 4), 5, 5);
            }

            switch (node.NodeType)
            {
                case IndustrialNodeType.Tank:
                    // Cylindrical Tank Shell
                    dc.DrawRoundedRectangle(bodyBrush, borderPen, nodeRect, 6, 6);

                    // Liquid Fill
                    double pct = Math.Max(0.0, Math.Min(100.0, node.Value));
                    double fillH = nh * (pct / 100.0);
                    double fillY = y + nh - fillH;
                    if (fillH > 0)
                    {
                        Brush fluidBrush = node.State == ScadaNodeState.Fault ? FaultFluidBrush : NormalFluidBrush;
                        dc.DrawRectangle(fluidBrush, null, new Rect(x + 1.5, fillY, nw - 3, fillH));
                    }

                    // Labels
                    var tankTitle = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 9.5, textBrush, dpi);
                    dc.DrawText(tankTitle, new Point(x + 6, y + 6));

                    var tankVal = new FormattedText($"{pct:0.0} %", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.MediumTypeface, 9.0, subTextBrush, dpi);
                    dc.DrawText(tankVal, new Point(x + 6, y + 20));
                    break;

                case IndustrialNodeType.Pump:
                    double cx = x + nw / 2.0;
                    double cy = y + nh / 2.0;
                    double r = Math.Min(nw, nh) / 2.0;

                    // Pump Body Casing
                    dc.DrawEllipse(bodyBrush, borderPen, new Point(cx, cy), r, r);

                    // State Indicator Ring
                    Pen ringPen = node.State == ScadaNodeState.Running ? RunningRingPen :
                                  node.State == ScadaNodeState.Fault ? FaultRingPen :
                                  node.State == ScadaNodeState.Warning ? WarningRingPen : StoppedRingPen;
                    dc.DrawEllipse(null, ringPen, new Point(cx, cy), r - 3, r - 3);

                    // Impeller Cross
                    double ir = r * 0.45;
                    Pen impellerPen = node.State == ScadaNodeState.Running ? RunningImpellerPen :
                                      node.State == ScadaNodeState.Fault ? FaultImpellerPen :
                                      node.State == ScadaNodeState.Warning ? WarningImpellerPen : StoppedImpellerPen;
                    dc.DrawLine(impellerPen, new Point(cx - ir, cy), new Point(cx + ir, cy));
                    dc.DrawLine(impellerPen, new Point(cx, cy - ir), new Point(cx, cy + ir));

                    // Pump Label
                    var pumpLabel = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 9.0, textBrush, dpi);
                    dc.DrawText(pumpLabel, new Point(x, y + nh + 3));

                    if (node.Value > 0)
                    {
                        var pumpRpm = new FormattedText($"{node.Value:0} RPM", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.MediumTypeface, 8.5, subTextBrush, dpi);
                        dc.DrawText(pumpRpm, new Point(x, y + nh + 16));
                    }
                    break;

                case IndustrialNodeType.Sensor:
                    // Capsule Transmitter
                    dc.DrawRoundedRectangle(bodyBrush, borderPen, nodeRect, 4, 4);

                    Brush statusDotBrush = node.State == ScadaNodeState.Fault ? FaultStatusDotBrush :
                                           node.State == ScadaNodeState.Warning ? WarningStatusDotBrush : NormalStatusDotBrush;
                    dc.DrawEllipse(statusDotBrush, null, new Point(x + 8, y + 10), 3.5, 3.5);

                    var sensorLabel = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.MediumTypeface, 8.5, subTextBrush, dpi);
                    dc.DrawText(sensorLabel, new Point(x + 16, y + 4));

                    string valStr = $"{node.Value:0.0} {node.EngineeringUnit}".Trim();
                    var sensorVal = new FormattedText(valStr, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 10.0, textBrush, dpi);
                    dc.DrawText(sensorVal, new Point(x + 6, y + 17));
                    break;

                case IndustrialNodeType.Valve:
                    // Two opposing triangles
                    StreamGeometry valveGeom = new StreamGeometry();
                    using (var ctx = valveGeom.Open())
                    {
                        ctx.BeginFigure(new Point(x, y), true, true);
                        ctx.LineTo(new Point(x + nw, y + nh), true, false);
                        ctx.LineTo(new Point(x + nw, y), true, false);
                        ctx.LineTo(new Point(x, y + nh), true, false);
                    }
                    valveGeom.Freeze();

                    Brush valveBrush = node.State == ScadaNodeState.Running ? RunningValveBrush :
                                       node.State == ScadaNodeState.Fault ? FaultValveBrush : StoppedValveBrush;
                    dc.DrawGeometry(valveBrush, borderPen, valveGeom);

                    var valveLabel = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 8.5, textBrush, dpi);
                    dc.DrawText(valveLabel, new Point(x, y + nh + 2));
                    break;

                default:
                    // Generic Archetype
                    dc.DrawRoundedRectangle(bodyBrush, borderPen, nodeRect, 4, 4);
                    var genLabel = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, ZeroWpfTheme.BoldTypeface, 9.0, textBrush, dpi);
                    dc.DrawText(genLabel, new Point(x + 6, y + 6));
                    break;
            }

            // Status & Safety Badges (LOTO, Tagout, Interlock, Fault)
            if (node.StatusFlags != ZeroUI.Core.Scada.Safety.DeviceStatusFlags.None)
            {
                DeviceBadgeRenderer.DrawBadges(dc, nodeRect, node.StatusFlags, dpi);
            }
        }

        private void RenderSvgVectorNode(DrawingContext dc, SvgVectorNode node, bool isDark)
        {
            if (!node.IsVisible) return;
            var b = node.WorldBounds;
            dc.PushTransform(new TranslateTransform(b.X, b.Y));

            foreach (var elem in node.Elements)
            {
                Brush? fill = ParseBrush(elem.FillColor);
                Pen? stroke = ParsePen(elem.StrokeColor, elem.StrokeWidth);

                switch (elem)
                {
                    case SvgRectElement r:
                        dc.DrawRoundedRectangle(fill, stroke, new Rect(r.X, r.Y, r.Width, r.Height), r.RadiusX, r.RadiusY);
                        break;
                    case SvgCircleElement c:
                        dc.DrawEllipse(fill, stroke, new Point(c.Cx, c.Cy), c.R, c.R);
                        break;
                    case SvgEllipseElement el:
                        dc.DrawEllipse(fill, stroke, new Point(el.Cx, el.Cy), el.Rx, el.Ry);
                        break;
                    case SvgLineElement l:
                        if (stroke != null) dc.DrawLine(stroke, new Point(l.X1, l.Y1), new Point(l.X2, l.Y2));
                        break;
                    case SvgPolylineElement pl:
                        if (pl.Points.Count > 1)
                        {
                            var sg = new StreamGeometry();
                            using (var ctx = sg.Open())
                            {
                                ctx.BeginFigure(new Point(pl.Points[0].X, pl.Points[0].Y), pl.IsClosed, pl.IsClosed);
                                for (int pi = 1; pi < pl.Points.Count; pi++)
                                {
                                    ctx.LineTo(new Point(pl.Points[pi].X, pl.Points[pi].Y), true, false);
                                }
                            }
                            sg.Freeze();
                            dc.DrawGeometry(fill, stroke, sg);
                        }
                        break;
                    case SvgPathElement p:
                        if (!string.IsNullOrWhiteSpace(p.PathData))
                        {
                            try
                            {
                                var geom = Geometry.Parse(p.PathData);
                                dc.DrawGeometry(fill, stroke, geom);
                            }
                            catch { }
                        }
                        break;
                }
            }

            dc.Pop();
        }

        private static Brush? ParseBrush(string? colorStr)
        {
            if (string.IsNullOrWhiteSpace(colorStr) || colorStr!.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                var converted = new BrushConverter().ConvertFromString(colorStr!);
                if (converted is Brush brush)
                {
                    if (brush.CanFreeze) brush.Freeze();
                    return brush;
                }
            }
            catch { }
            return Brushes.Gray;
        }

        private static Pen? ParsePen(string? colorStr, float width)
        {
            if (string.IsNullOrWhiteSpace(colorStr) || colorStr!.Equals("none", StringComparison.OrdinalIgnoreCase)) return null;
            try
            {
                var converted = new BrushConverter().ConvertFromString(colorStr!);
                if (converted is Brush brush)
                {
                    if (brush.CanFreeze) brush.Freeze();
                    var pen = new Pen(brush, Math.Max(0.5, width));
                    pen.Freeze();
                    return pen;
                }
            }
            catch { }
            return new Pen(Brushes.DarkGray, 1.0);
        }

        /// <summary>
        /// Imports an SVG diagram string into the scene at the specified world coordinates.
        /// </summary>
        public SvgVectorNode LoadSvg(string svgXml, float x = 0f, float y = 0f)
        {
            var node = SvgSceneImporter.ImportFromString(svgXml);
            node.X = x;
            node.Y = y;
            _scene.AddNode(node);
            InvalidateVisual();
            return node;
        }

        /// <summary>
        /// Computes an optimal 90-degree orthogonal Manhattan pipe route between two scene points.
        /// </summary>
        public List<ScenePoint> RoutePipeManhattan(ScenePoint source, ScenePoint target, PortDirection srcDir = PortDirection.Auto, PortDirection tgtDir = PortDirection.Auto)
        {
            return PipeRouter.RouteManhattan(source, target, srcDir, tgtDir);
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
    /// Legacy alias for <see cref="ZPlantMimicCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PlantMimicCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZPlantMimicCanvas instead.")]
    public class PlantMimicCanvas : ZPlantMimicCanvas { }

    /// <summary>
    /// Legacy alias for <see cref="ZPlantMimicCanvas"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPlantMimicCanvas is deprecated and will be removed in 5 release cycles. Please migrate to ZPlantMimicCanvas instead.")]
    public class ZeroPlantMimicCanvas : ZPlantMimicCanvas { }

    #endregion

}
