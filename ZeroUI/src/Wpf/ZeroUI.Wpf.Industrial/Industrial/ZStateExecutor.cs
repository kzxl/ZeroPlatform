using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Automation;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// High-performance Single-Visual animated State Machine and PLC logic execution canvas for WPF.
    /// Provides live token pulse photon animation across transition paths, active state timers,
    /// and interactive node repositioning and transition triggering.
    /// </summary>
    public class ZStateExecutor : FrameworkElement
    {
        private readonly StateMachineEngine _engine = new StateMachineEngine();
        private Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _lastTicks = 0;
        private bool _isAnimating = true;

        private MachineStateNode? _draggedNode;
        private Point _dragStartMouse;
        private Point _dragStartPos;

        public StateMachineEngine Engine => _engine;

        public bool IsAnimating
        {
            get => _isAnimating;
            set
            {
                _isAnimating = value;
                _engine.IsRunning = value;
            }
        }

        public ZStateExecutor()
        {
            ClipToBounds = true;
            Focusable = true;
            Cursor = Cursors.Arrow;

            _lastTicks = _stopwatch.ElapsedTicks;
            CompositionTarget.Rendering += OnRendering;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isAnimating) return;

            long currentTicks = _stopwatch.ElapsedTicks;
            double deltaSec = (double)(currentTicks - _lastTicks) / Stopwatch.Frequency;
            _lastTicks = currentTicks;

            if (deltaSec > 0.1) deltaSec = 0.1; // Cap on background tab resume

            _engine.Update(deltaSec);
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // 1. Background
            dc.DrawRectangle(ZeroWpfTheme.BgPrimary, null, new Rect(0, 0, w, h));

            // 2. Draw Subtle Grid Pattern
            DrawGrid(dc, w, h);

            // 3. Draw Transitions
            DrawTransitions(dc);

            // 4. Draw Active Glowing Photon Pulses
            DrawPulses(dc);

            // 5. Draw State Nodes
            DrawNodes(dc);
        }

        private void DrawGrid(DrawingContext dc, double w, double h)
        {
            var dotBrush = new SolidColorBrush(Color.FromArgb(20, 148, 163, 184));
            dotBrush.Freeze();
            double step = 28;

            for (double x = 14; x < w; x += step)
            {
                for (double y = 14; y < h; y += step)
                {
                    dc.DrawRectangle(dotBrush, null, new Rect(x - 1, y - 1, 2, 2));
                }
            }
        }

        private void DrawTransitions(DrawingContext dc)
        {
            var linePen = new Pen(ZeroWpfTheme.BorderDefault, 1.8);
            linePen.Freeze();

            foreach (var trans in _engine.Transitions)
            {
                var src = _engine.Nodes.Find(n => n.Id == trans.SourceId);
                var tgt = _engine.Nodes.Find(n => n.Id == trans.TargetId);
                if (src == null || tgt == null) continue;

                Point p1 = new Point(src.X, src.Y);
                Point p2 = new Point(tgt.X, tgt.Y);

                // Draw connecting line
                dc.DrawLine(linePen, p1, p2);

                // Arrow head at target
                Vector dir = p2 - p1;
                if (dir.Length > src.Radius + tgt.Radius)
                {
                    dir.Normalize();
                    Point arrowTip = p2 - dir * tgt.Radius;
                    Vector norm = new Vector(-dir.Y, dir.X);
                    Point a1 = arrowTip - dir * 10 + norm * 5;
                    Point a2 = arrowTip - dir * 10 - norm * 5;

                    var arrowGeo = new StreamGeometry();
                    using (var ctx = arrowGeo.Open())
                    {
                        ctx.BeginFigure(arrowTip, true, true);
                        ctx.LineTo(a1, true, false);
                        ctx.LineTo(a2, true, false);
                    }
                    arrowGeo.Freeze();
                    dc.DrawGeometry(ZeroWpfTheme.BorderDefault, null, arrowGeo);
                }

                // Condition Label pill
                if (!string.IsNullOrEmpty(trans.ConditionText))
                {
                    Point mid = new Point((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0 - 12);
                    var ft = new FormattedText(trans.ConditionText, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface, 10, ZeroWpfTheme.TextMuted, 1.0);

                    Rect pillRect = new Rect(mid.X - ft.Width / 2.0 - 4, mid.Y - 2, ft.Width + 8, ft.Height + 4);
                    dc.DrawRoundedRectangle(ZeroWpfTheme.BgCard, new Pen(ZeroWpfTheme.BorderDefault, 0.8), pillRect, 3, 3);
                    dc.DrawText(ft, new Point(pillRect.Left + 4, pillRect.Top + 2));
                }
            }
        }

        private void DrawPulses(DrawingContext dc)
        {
            foreach (var pulse in _engine.ActivePulses)
            {
                var src = _engine.Nodes.Find(n => n.Id == pulse.SourceId);
                var tgt = _engine.Nodes.Find(n => n.Id == pulse.TargetId);
                if (src == null || tgt == null) continue;

                double px = src.X + (tgt.X - src.X) * pulse.Progress;
                double py = src.Y + (tgt.Y - src.Y) * pulse.Progress;

                // Glowing Photon halo
                var pulseColor = Color.FromArgb(
                    (byte)((pulse.ColorArgb >> 24) & 0xFF),
                    (byte)((pulse.ColorArgb >> 16) & 0xFF),
                    (byte)((pulse.ColorArgb >> 8) & 0xFF),
                    (byte)(pulse.ColorArgb & 0xFF));

                var glowBrush = new RadialGradientBrush(pulseColor, Color.FromArgb(0, pulseColor.R, pulseColor.G, pulseColor.B));
                glowBrush.Freeze();
                dc.DrawEllipse(glowBrush, null, new Point(px, py), 14, 14);

                // Intense core
                var coreBrush = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                coreBrush.Freeze();
                dc.DrawEllipse(coreBrush, null, new Point(px, py), 4.5, 4.5);
            }
        }

        private void DrawNodes(DrawingContext dc, double nodeRadius = 38)
        {
            foreach (var node in _engine.Nodes)
            {
                Point center = new Point(node.X, node.Y);
                bool isActive = (node.Status == MachineStateStatus.Active);
                bool isCompleted = (node.Status == MachineStateStatus.Completed);

                var baseColor = Color.FromArgb(
                    (byte)((node.ColorArgb >> 24) & 0xFF),
                    (byte)((node.ColorArgb >> 16) & 0xFF),
                    (byte)((node.ColorArgb >> 8) & 0xFF),
                    (byte)(node.ColorArgb & 0xFF));

                // Active Glow
                if (isActive)
                {
                    var glowBrush = new RadialGradientBrush(Color.FromArgb(110, baseColor.R, baseColor.G, baseColor.B), Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B));
                    glowBrush.Freeze();
                    dc.DrawEllipse(glowBrush, null, center, nodeRadius + 14, nodeRadius + 14);
                }

                // Node Body
                var bodyBrush = isActive ? new SolidColorBrush(Color.FromArgb(40, baseColor.R, baseColor.G, baseColor.B)) : ZeroWpfTheme.BgCard;
                var borderPen = isActive
                    ? new Pen(new SolidColorBrush(baseColor), 2.5)
                    : (isCompleted ? new Pen(new SolidColorBrush(Color.FromRgb(16, 185, 129)), 1.8) : new Pen(ZeroWpfTheme.BorderDefault, 1.2));
                borderPen.Freeze();

                dc.DrawEllipse(bodyBrush, borderPen, center, nodeRadius, nodeRadius);

                // Progress ring for active state
                if (isActive && node.DurationSeconds > 0)
                {
                    double progressAngle = node.Progress * 360.0;
                    DrawProgressArc(dc, center, nodeRadius - 2, progressAngle, baseColor);
                }

                // Node Title
                var titleFt = new FormattedText(node.Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface, 11, ZeroWpfTheme.TextPrimary, 1.0);
                titleFt.MaxTextWidth = nodeRadius * 2 - 8;
                titleFt.TextAlignment = TextAlignment.Center;
                dc.DrawText(titleFt, new Point(center.X - titleFt.Width / 2.0, center.Y - 14));

                // Timer / Status Subtext
                string subtext = isActive
                    ? $"{node.ElapsedSeconds:0.0}s / {node.DurationSeconds:0.#}s"
                    : (isCompleted ? "DONE" : "IDLE");

                var subFt = new FormattedText(subtext, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface, 9.5, isActive ? new SolidColorBrush(baseColor) : ZeroWpfTheme.TextMuted, 1.0);
                subFt.TextAlignment = TextAlignment.Center;
                dc.DrawText(subFt, new Point(center.X - subFt.Width / 2.0, center.Y + 4));
            }
        }

        private void DrawProgressArc(DrawingContext dc, Point center, double radius, double angleDeg, Color color)
        {
            if (angleDeg <= 1) return;
            if (angleDeg >= 360) angleDeg = 359.9;

            double rad = (angleDeg - 90) * Math.PI / 180.0;
            Point startPoint = new Point(center.X, center.Y - radius);
            Point endPoint = new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
            bool isLargeArc = angleDeg > 180.0;

            var pathGeo = new StreamGeometry();
            using (var ctx = pathGeo.Open())
            {
                ctx.BeginFigure(startPoint, false, false);
                ctx.ArcTo(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true, false);
            }
            pathGeo.Freeze();

            var arcPen = new Pen(new SolidColorBrush(color), 2.5);
            arcPen.Freeze();
            dc.DrawGeometry(null, arcPen, pathGeo);
        }

        #region Node Dragging & Transition Click

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            Point pt = e.GetPosition(this);

            // Find clicked node
            MachineStateNode? hit = null;
            for (int i = _engine.Nodes.Count - 1; i >= 0; i--)
            {
                var n = _engine.Nodes[i];
                if ((new Point(n.X, n.Y) - pt).Length <= n.Radius)
                {
                    hit = n;
                    break;
                }
            }

            if (hit != null)
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    _draggedNode = hit;
                    _dragStartMouse = pt;
                    _dragStartPos = new Point(hit.X, hit.Y);
                    CaptureMouse();
                }
                else if (e.ChangedButton == MouseButton.Right)
                {
                    // Right-click triggers transition to this state
                    _engine.TriggerTransition(hit.Id);
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_draggedNode != null)
            {
                Point pt = e.GetPosition(this);
                _draggedNode.X = _dragStartPos.X + (pt.X - _dragStartMouse.X);
                _draggedNode.Y = _dragStartPos.Y + (pt.Y - _dragStartMouse.Y);
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_draggedNode != null)
            {
                _draggedNode = null;
                ReleaseMouseCapture();
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
    /// Legacy alias for <see cref="ZStateExecutor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("StateExecutor is deprecated and will be removed in 5 release cycles. Please migrate to ZStateExecutor instead.")]
    public class StateExecutor : ZStateExecutor { }

    /// <summary>
    /// Legacy alias for <see cref="ZStateExecutor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroStateExecutor is deprecated and will be removed in 5 release cycles. Please migrate to ZStateExecutor instead.")]
    public class ZeroStateExecutor : ZStateExecutor { }

    #endregion

}
