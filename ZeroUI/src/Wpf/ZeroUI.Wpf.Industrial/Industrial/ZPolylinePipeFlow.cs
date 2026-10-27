using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Industrial
{
    /// <summary>
    /// Multi-segment industrial polyline piping component with animated fluid flow pulses for ZeroUI WPF.
    /// Renders complex P&ID pipeline networks with automatic elbow transitions on a single control.
    /// </summary>
    public class ZPolylinePipeFlow : FrameworkElement
    {
        private readonly List<Point> _points = new List<Point>();
        private ZeroFluidType _fluidType = ZeroFluidType.Water;
        private double _flowVelocity = 2.0;
        private bool _isFlowing = true;
        private bool _reverseFlow = false;
        private double _pipeDiameter = 16.0;
        private double _pulseOffset = 0.0;
        private bool _isSubscribed = false;

        public List<Point> Points => _points;

        public ZeroFluidType FluidType
        {
            get => _fluidType;
            set
            {
                if (_fluidType != value)
                {
                    _fluidType = value;
                    InvalidateVisual();
                }
            }
        }

        public double FlowVelocity
        {
            get => _flowVelocity;
            set => _flowVelocity = Math.Max(0, value);
        }

        public bool IsFlowing
        {
            get => _isFlowing;
            set
            {
                if (_isFlowing != value)
                {
                    _isFlowing = value;
                    InvalidateVisual();
                }
            }
        }

        public bool ReverseFlow
        {
            get => _reverseFlow;
            set
            {
                if (_reverseFlow != value)
                {
                    _reverseFlow = value;
                    InvalidateVisual();
                }
            }
        }

        public double PipeDiameter
        {
            get => _pipeDiameter;
            set
            {
                _pipeDiameter = Math.Max(6, Math.Min(60, value));
                InvalidateVisual();
            }
        }

        public ZPolylinePipeFlow()
        {
            // Default route
            _points.Add(new Point(20, 30));
            _points.Add(new Point(120, 30));
            _points.Add(new Point(120, 110));
            _points.Add(new Point(180, 110));

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void SetPoints(IEnumerable<Point> points)
        {
            _points.Clear();
            if (points != null)
            {
                _points.AddRange(points);
            }
            InvalidateVisual();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            if (!_isSubscribed)
            {
                CompositionTarget.Rendering += OnRendering;
                _isSubscribed = true;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
            if (_isSubscribed)
            {
                CompositionTarget.Rendering -= OnRendering;
                _isSubscribed = false;
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (_isFlowing && _flowVelocity > 0 && IsVisible)
            {
                double step = _flowVelocity * 0.8;
                _pulseOffset = _reverseFlow ? (_pulseOffset - step) : (_pulseOffset + step);
                if (_pulseOffset > 1000.0 || _pulseOffset < -1000.0) _pulseOffset = 0.0;
                InvalidateVisual();
            }
        }

        public Color GetFluidColor()
        {
            return _fluidType switch
            {
                ZeroFluidType.Water => Color.FromRgb(6, 182, 212),     // Cyan
                ZeroFluidType.Gas => Color.FromRgb(99, 102, 241),      // Indigo
                ZeroFluidType.Oil => Color.FromRgb(245, 158, 11),      // Amber
                ZeroFluidType.Steam => Color.FromRgb(226, 232, 240),   // Light Gray
                ZeroFluidType.Chemical => Color.FromRgb(236, 72, 153), // Magenta
                ZeroFluidType.Acid => Color.FromRgb(234, 179, 8),      // Yellow
                ZeroFluidType.Slurry => Color.FromRgb(180, 83, 9),     // Earth Brown
                ZeroFluidType.CoolingWater => Color.FromRgb(16, 185, 129), // Emerald
                _ => Color.FromRgb(6, 182, 212)
            };
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            if (_points.Count < 2) return;

            bool isDark = ZeroWpfTheme.IsDark;
            Color pipeWallColor = isDark ? Color.FromRgb(46, 52, 78) : Color.FromRgb(148, 163, 184);
            Color fluidBase = GetFluidColor();
            Color coreFluid = _isFlowing ? fluidBase : Color.FromArgb(60, fluidBase.R, fluidBase.G, fluidBase.B);

            double d = _pipeDiameter;
            double fluidD = Math.Max(3, d - 6);

            // Build StreamGeometry for polyline path
            StreamGeometry geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(_points[0], false, false);
                for (int i = 1; i < _points.Count; i++)
                {
                    ctx.LineTo(_points[i], true, false);
                }
            }
            geom.Freeze();

            // 1. Pipe Outer Wall
            var outerPen = new Pen(new SolidColorBrush(pipeWallColor), d)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            outerPen.Freeze();
            dc.DrawGeometry(null, outerPen, geom);

            // 2. Inner Fluid Channel
            var fluidPen = new Pen(new SolidColorBrush(coreFluid), fluidD)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            fluidPen.Freeze();
            dc.DrawGeometry(null, fluidPen, geom);

            // 3. Animated Fluid Pulses (Dashed chevrons / white pulses)
            if (_isFlowing && _flowVelocity > 0)
            {
                double dashOffset = (_pulseOffset / 8.0) % 100.0;
                var pulsePen = new Pen(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), 2.5)
                {
                    DashStyle = new DashStyle(new double[] { 3.0, 4.0 }, dashOffset),
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };
                pulsePen.Freeze();
                dc.DrawGeometry(null, pulsePen, geom);
            }

            // 4. Flanges at start and end
            DrawFlangeDot(dc, _points[0], d, isDark);
            DrawFlangeDot(dc, _points[_points.Count - 1], d, isDark);
        }

        private static void DrawFlangeDot(DrawingContext dc, Point p, double d, bool isDark)
        {
            Color flangeCol = isDark ? Color.FromRgb(70, 78, 110) : Color.FromRgb(100, 116, 139);
            Brush flangeBrush = new SolidColorBrush(flangeCol);
            flangeBrush.Freeze();
            dc.DrawEllipse(flangeBrush, null, p, d * 0.65, d * 0.65);

            Brush centerBrush = new SolidColorBrush(isDark ? Color.FromRgb(15, 23, 42) : Colors.White);
            centerBrush.Freeze();
            dc.DrawEllipse(centerBrush, null, p, d * 0.25, d * 0.25);
        }
        private void OnThemeChanged() => InvalidateVisual();
}
    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPolylinePipeFlow"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PolylinePipeFlow is deprecated and will be removed in 5 release cycles. Please migrate to ZPolylinePipeFlow instead.")]
    public class PolylinePipeFlow : ZPolylinePipeFlow { }

    /// <summary>
    /// Legacy alias for <see cref="ZPolylinePipeFlow"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPolylinePipeFlow is deprecated and will be removed in 5 release cycles. Please migrate to ZPolylinePipeFlow instead.")]
    public class ZeroPolylinePipeFlow : ZPolylinePipeFlow { }

    #endregion

}
