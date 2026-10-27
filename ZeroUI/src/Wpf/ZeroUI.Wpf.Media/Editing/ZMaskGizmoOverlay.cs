using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    public enum MaskGizmoType
    {
        None,
        LinearGradient,
        RadialGradient
    }

    /// <summary>
    /// Interactive visual gizmo overlay for Linear Gradient and Radial Gradient masks.
    /// Provides center pin positioning, directional rotation, falloff feather ellipses,
    /// and cardinal handles. Renders directly using <see cref="DrawingContext"/>.
    /// </summary>
    public class ZMaskGizmoOverlay : FrameworkElement, IZeroEditor
    {
        private enum HandleKind { None, Center, Start, End, Top, Bottom, Left, Right }
        private HandleKind _activeHandle = HandleKind.None;
        private Point _dragStartMouse;
        private Point _dragStartCenter;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty GizmoTypeProperty =
            DependencyProperty.Register(nameof(GizmoType), typeof(MaskGizmoType), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(MaskGizmoType.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CenterPointProperty =
            DependencyProperty.Register(nameof(CenterPoint), typeof(Point), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(new Point(0.5, 0.5), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty EndPointProperty =
            DependencyProperty.Register(nameof(EndPoint), typeof(Point), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(new Point(0.5, 0.8), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RadiusXProperty =
            DependencyProperty.Register(nameof(RadiusX), typeof(double), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(0.25, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RadiusYProperty =
            DependencyProperty.Register(nameof(RadiusY), typeof(double), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(0.20, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty FeatherProperty =
            DependencyProperty.Register(nameof(Feather), typeof(double), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(0.5, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GizmoBrushProperty =
            DependencyProperty.Register(nameof(GizmoBrush), typeof(Brush), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZMaskGizmoOverlay),
                new FrameworkPropertyMetadata(false));

        #endregion

        #region Properties & Events

        public MaskGizmoType GizmoType
        {
            get => (MaskGizmoType)GetValue(GizmoTypeProperty);
            set => SetValue(GizmoTypeProperty, value);
        }

        public Point CenterPoint
        {
            get => (Point)GetValue(CenterPointProperty);
            set => SetValue(CenterPointProperty, value);
        }

        public Point EndPoint
        {
            get => (Point)GetValue(EndPointProperty);
            set => SetValue(EndPointProperty, value);
        }

        public double RadiusX
        {
            get => (double)GetValue(RadiusXProperty);
            set => SetValue(RadiusXProperty, Math.Max(0.01, value));
        }

        public double RadiusY
        {
            get => (double)GetValue(RadiusYProperty);
            set => SetValue(RadiusYProperty, Math.Max(0.01, value));
        }

        public double Feather
        {
            get => (double)GetValue(FeatherProperty);
            set => SetValue(FeatherProperty, Clamp(value, 0.0, 1.0));
        }

        public Brush? GizmoBrush
        {
            get => (Brush?)GetValue(GizmoBrushProperty);
            set => SetValue(GizmoBrushProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public event EventHandler? GizmoChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => (GizmoType, CenterPoint, EndPoint, RadiusX, RadiusY, Feather);
            set
            {
                if (value is ValueTuple<MaskGizmoType, Point, Point, double, double, double> t)
                {
                    GizmoType = t.Item1;
                    CenterPoint = t.Item2;
                    EndPoint = t.Item3;
                    RadiusX = t.Item4;
                    RadiusY = t.Item5;
                    Feather = t.Item6;
                }
            }
        }

        public event EventHandler? EditValueChanged;

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public void Reset()
        {
            CenterPoint = new Point(0.5, 0.5);
            EndPoint = new Point(0.5, 0.8);
            RadiusX = 0.25;
            RadiusY = 0.20;
            Feather = 0.5;
            _isModified = false;
            InvalidateVisual();
        }

        public void Clear()
        {
            GizmoType = MaskGizmoType.None;
            Reset();
        }

        #endregion

        public ZMaskGizmoOverlay()
        {
            Focusable = true;
            ClipToBounds = true;
            Cursor = Cursors.Arrow;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #region Rendering

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0 || GizmoType == MaskGizmoType.None) return;

            var stroke = GizmoBrush ?? ZeroWpfTheme.PrimaryAccent;
            var pen = new Pen(stroke, 1.5);
            pen.Freeze();

            var subtlePen = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1.0)
            {
                DashStyle = DashStyles.Dash
            };
            subtlePen.Freeze();

            if (GizmoType == MaskGizmoType.LinearGradient)
            {
                RenderLinearGizmo(dc, w, h, pen, subtlePen);
            }
            else if (GizmoType == MaskGizmoType.RadialGradient)
            {
                RenderRadialGizmo(dc, w, h, pen, subtlePen);
            }
        }

        private void RenderLinearGizmo(DrawingContext dc, double w, double h, Pen mainPen, Pen dashPen)
        {
            Point p0 = new Point(CenterPoint.X * w, CenterPoint.Y * h);
            Point p1 = new Point(EndPoint.X * w, EndPoint.Y * h);

            Vector dir = p1 - p0;
            double len = dir.Length;
            Vector normal = len > 1e-4 ? new Vector(-dir.Y / len, dir.X / len) : new Vector(1, 0);
            double barLen = Math.Max(200.0, len * 1.5);

            Point startA = p0 - normal * (barLen / 2.0);
            Point startB = p0 + normal * (barLen / 2.0);

            Point endA = p1 - normal * (barLen / 2.0);
            Point endB = p1 + normal * (barLen / 2.0);

            Point mid = new Point((p0.X + p1.X) / 2.0, (p0.Y + p1.Y) / 2.0);
            Point midA = mid - normal * (barLen / 2.0);
            Point midB = mid + normal * (barLen / 2.0);

            // Direction axis
            dc.DrawLine(dashPen, p0, p1);

            // Start Bar (Solid 100%)
            dc.DrawLine(mainPen, startA, startB);

            // Midline (50% falloff)
            dc.DrawLine(dashPen, midA, midB);

            // End Bar (0% falloff)
            dc.DrawLine(mainPen, endA, endB);

            // Center Pin at p0
            DrawPin(dc, p0, Brushes.White, mainPen.Brush);

            // End Pin at p1
            DrawPin(dc, p1, mainPen.Brush, Brushes.White);
        }

        private void RenderRadialGizmo(DrawingContext dc, double w, double h, Pen mainPen, Pen dashPen)
        {
            Point c = new Point(CenterPoint.X * w, CenterPoint.Y * h);
            double rx = RadiusX * w;
            double ry = RadiusY * h;

            // Outer feather ellipse
            dc.DrawEllipse(null, mainPen, c, rx, ry);

            // Inner solid ellipse
            double innerFactor = Math.Max(0.05, 1.0 - Feather);
            double innerRx = rx * innerFactor;
            double innerRy = ry * innerFactor;
            dc.DrawEllipse(null, dashPen, c, innerRx, innerRy);

            // Center Pin
            DrawPin(dc, c, Brushes.White, mainPen.Brush);

            // 4 Cardinal resize handles on outer ellipse
            DrawHandle(dc, new Point(c.X, c.Y - ry)); // Top
            DrawHandle(dc, new Point(c.X, c.Y + ry)); // Bottom
            DrawHandle(dc, new Point(c.X - rx, c.Y)); // Left
            DrawHandle(dc, new Point(c.X + rx, c.Y)); // Right
        }

        private static void DrawPin(DrawingContext dc, Point p, Brush fill, Brush stroke)
        {
            var pen = new Pen(stroke, 1.5);
            pen.Freeze();
            dc.DrawEllipse(fill, pen, p, 5.0, 5.0);
        }

        private static void DrawHandle(DrawingContext dc, Point p)
        {
            var pen = new Pen(Brushes.White, 1.2);
            pen.Freeze();
            dc.DrawEllipse(Brushes.DodgerBlue, pen, p, 4.5, 4.5);
        }

        #endregion

        #region User Interaction

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            if (ReadOnly || GizmoType == MaskGizmoType.None) return;

            Point p = e.GetPosition(this);
            double w = ActualWidth;
            double h = ActualHeight;

            Point cp = new Point(CenterPoint.X * w, CenterPoint.Y * h);
            Point ep = new Point(EndPoint.X * w, EndPoint.Y * h);

            if (Near(p, cp, 12.0))
            {
                _activeHandle = HandleKind.Center;
                _dragStartMouse = p;
                _dragStartCenter = CenterPoint;
                CaptureMouse();
                Cursor = Cursors.SizeAll;
                e.Handled = true;
                return;
            }

            if (GizmoType == MaskGizmoType.LinearGradient && Near(p, ep, 12.0))
            {
                _activeHandle = HandleKind.End;
                _dragStartMouse = p;
                CaptureMouse();
                Cursor = Cursors.Hand;
                e.Handled = true;
                return;
            }

            if (GizmoType == MaskGizmoType.RadialGradient)
            {
                double rx = RadiusX * w;
                double ry = RadiusY * h;
                if (Near(p, new Point(cp.X, cp.Y - ry), 10.0)) { _activeHandle = HandleKind.Top; }
                else if (Near(p, new Point(cp.X, cp.Y + ry), 10.0)) { _activeHandle = HandleKind.Bottom; }
                else if (Near(p, new Point(cp.X - rx, cp.Y), 10.0)) { _activeHandle = HandleKind.Left; }
                else if (Near(p, new Point(cp.X + rx, cp.Y), 10.0)) { _activeHandle = HandleKind.Right; }

                if (_activeHandle != HandleKind.None)
                {
                    _dragStartMouse = p;
                    CaptureMouse();
                    Cursor = Cursors.Hand;
                    e.Handled = true;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_activeHandle != HandleKind.None && !ReadOnly)
            {
                Point p = e.GetPosition(this);
                double w = Math.Max(1.0, ActualWidth);
                double h = Math.Max(1.0, ActualHeight);

                double dx = (p.X - _dragStartMouse.X) / w;
                double dy = (p.Y - _dragStartMouse.Y) / h;

                if (_activeHandle == HandleKind.Center)
                {
                    CenterPoint = new Point(Clamp(_dragStartCenter.X + dx, 0.0, 1.0), Clamp(_dragStartCenter.Y + dy, 0.0, 1.0));
                }
                else if (_activeHandle == HandleKind.End)
                {
                    EndPoint = new Point(Clamp(p.X / w, 0.0, 1.0), Clamp(p.Y / h, 0.0, 1.0));
                }
                else if (_activeHandle == HandleKind.Right || _activeHandle == HandleKind.Left)
                {
                    RadiusX = Math.Abs(p.X / w - CenterPoint.X);
                }
                else if (_activeHandle == HandleKind.Top || _activeHandle == HandleKind.Bottom)
                {
                    RadiusY = Math.Abs(p.Y / h - CenterPoint.Y);
                }

                _isModified = true;
                InvalidateVisual();
                GizmoChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_activeHandle != HandleKind.None)
            {
                _activeHandle = HandleKind.None;
                ReleaseMouseCapture();
                Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private static bool Near(Point a, Point b, double dist)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return dx * dx + dy * dy <= dist * dist;
        }

        private static double Clamp(double val, double min, double max)
        {
            if (val < min) return min;
            if (val > max) return max;
            return val;
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
    /// Legacy alias for <see cref="ZMaskGizmoOverlay"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MaskGizmoOverlay is deprecated. Use ZMaskGizmoOverlay instead.")]
    public class MaskGizmoOverlay : ZMaskGizmoOverlay
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZMaskGizmoOverlay"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroMaskGizmoOverlay is deprecated. Use ZMaskGizmoOverlay instead.")]
    public class ZeroMaskGizmoOverlay : ZMaskGizmoOverlay
    {
    }
    #endregion
}
