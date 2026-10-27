using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Multi-segment industrial polyline piping component with animated fluid flow pulses.
    /// Renders complex P&ID pipeline networks with automatic elbow transitions on a single control.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroPipeFlow.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Continuous multi-segment polyline pipeline with subpixel animated fluid dynamics")]
    public class ZPolylinePipeFlow : Control, IScadaBindable
    {
        private readonly List<Point> _points = new List<Point>();
        private ZeroFluidType _fluidType = ZeroFluidType.Water;
        private double _flowVelocity = 2.0;
        private bool _isFlowing = true;
        private bool _reverseFlow = false;
        private int _pipeDiameter = 16;
        private bool _useSharedClockPhase = true;
        private float _pulseOffset = 0f;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<Point> Points => _points;

        [Category("Appearance")]
        [DefaultValue(ZeroFluidType.Water)]
        public ZeroFluidType FluidType
        {
            get => _fluidType;
            set { _fluidType = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(2.0)]
        public double FlowVelocity
        {
            get => _flowVelocity;
            set { _flowVelocity = Math.Max(0, value); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(true)]
        public bool IsFlowing
        {
            get => _isFlowing;
            set { _isFlowing = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(false)]
        public bool ReverseFlow
        {
            get => _reverseFlow;
            set { _reverseFlow = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(true)]
        public bool UseSharedClockPhase
        {
            get => _useSharedClockPhase;
            set { _useSharedClockPhase = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(16)]
        public int PipeDiameter
        {
            get => _pipeDiameter;
            set { _pipeDiameter = Math.Max(6, Math.Min(60, value)); Invalidate(); }
        }

        public ZPolylinePipeFlow()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(200, 150);

            // Default route
            _points.Add(new Point(20, 30));
            _points.Add(new Point(120, 30));
            _points.Add(new Point(120, 110));
            _points.Add(new Point(180, 110));

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void SetPoints(IEnumerable<Point> points)
        {
            _points.Clear();
            if (points != null)
            {
                _points.AddRange(points);
            }
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                _clockToken = ZeroAnimationClock.Subscribe(OnAnimationFrameTick);
                ZeroTagEngine.RegisterBindable(this);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            _clockToken?.Dispose();
            _clockToken = null;
            ZeroTagEngine.UnregisterBindable(this);
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            if (_isFlowing && _flowVelocity > 0 && IsHandleCreated && Visible)
            {
                if (!_useSharedClockPhase)
                {
                    float step = (float)(_flowVelocity * 45.0 * deltaSeconds);
                    _pulseOffset = _reverseFlow ? (_pulseOffset - step) : (_pulseOffset + step);
                    if (_pulseOffset > 1000f || _pulseOffset < -1000f) _pulseOffset = 0f;
                }
                Invalidate();
            }
        }

        private void OnThemeChanged(object? sender, EventArgs e) => Invalidate();

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                return;
            }

            if (tag.Value is bool b)
            {
                IsFlowing = b;
            }
            else if (tag.Value is double d)
            {
                FlowVelocity = d;
                IsFlowing = d > 0.05;
            }
            else if (tag.Value is int i)
            {
                FlowVelocity = i;
                IsFlowing = i > 0;
            }
        }

        public Color GetFluidColor()
        {
            return _fluidType switch
            {
                ZeroFluidType.Water => Color.FromArgb(6, 182, 212),     // Cyan
                ZeroFluidType.Gas => Color.FromArgb(99, 102, 241),      // Indigo
                ZeroFluidType.Oil => Color.FromArgb(245, 158, 11),      // Amber
                ZeroFluidType.Steam => Color.FromArgb(226, 232, 240),   // Light Gray
                ZeroFluidType.Chemical => Color.FromArgb(236, 72, 153), // Magenta
                ZeroFluidType.Acid => Color.FromArgb(234, 179, 8),      // Yellow
                ZeroFluidType.Slurry => Color.FromArgb(180, 83, 9),     // Earth Brown
                ZeroFluidType.CoolingWater => Color.FromArgb(16, 185, 129), // Emerald
                _ => Color.FromArgb(6, 182, 212)
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_points.Count < 2) return;

            bool isDark = ZeroTheme.IsDark;
            Color pipeWallColor = isDark ? Color.FromArgb(46, 52, 78) : Color.FromArgb(148, 163, 184);
            Color fluidBase = GetFluidColor();
            Color coreFluid = _isFlowing ? fluidBase : Color.FromArgb(50, fluidBase);

            int d = _pipeDiameter;
            int fluidD = Math.Max(3, d - 6);

            float effectiveOffset = _useSharedClockPhase
                ? (float)(_reverseFlow ? -(ZeroAnimationClock.TotalElapsedTime * _flowVelocity * 30.0 % 1000.0) : (ZeroAnimationClock.TotalElapsedTime * _flowVelocity * 30.0 % 1000.0))
                : _pulseOffset;

            Point[] pts = _points.ToArray();

            // 1. Pipe Outer Wall
            using (var penOuter = new Pen(pipeWallColor, d) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(penOuter, pts);
            }

            // 2. Inner Fluid Channel
            using (var penFluid = new Pen(coreFluid, fluidD) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                g.DrawLines(penFluid, pts);
            }

            // 3. Animated Fluid Pulses
            if (_isFlowing && _flowVelocity > 0)
            {
                using var penPulse = new Pen(Color.FromArgb(220, Color.White), 2f)
                {
                    DashStyle = DashStyle.Dash,
                    DashPattern = new[] { 6f, 8f },
                    DashOffset = effectiveOffset,
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round,
                    LineJoin = LineJoin.Round
                };
                g.DrawLines(penPulse, pts);
            }

            // 4. Flanges at start and end
            DrawFlangeDot(g, pts[0], d, isDark);
            DrawFlangeDot(g, pts[pts.Length - 1], d, isDark);
        }

        private static void DrawFlangeDot(Graphics g, Point p, int d, bool isDark)
        {
            Color flangeCol = isDark ? Color.FromArgb(70, 78, 110) : Color.FromArgb(100, 116, 139);
            using var brush = new SolidBrush(flangeCol);
            using var pen = new Pen(isDark ? Color.FromArgb(30, 36, 56) : Color.White, 1.5f);
            int r = (d / 2) + 2;
            g.FillEllipse(brush, p.X - r, p.Y - r, r * 2, r * 2);
            g.DrawEllipse(pen, p.X - r, p.Y - r, r * 2, r * 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _clockToken?.Dispose();
                _clockToken = null;
                ZeroTheme.ThemeChanged -= OnThemeChanged;
                ZeroTagEngine.UnregisterBindable(this);
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZPolylinePipeFlow"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPolylinePipeFlow is deprecated and will be removed in 5 release cycles. Please migrate to ZPolylinePipeFlow instead.")]
    [ToolboxItem(false)]
    public class ZeroPolylinePipeFlow : ZPolylinePipeFlow
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPolylinePipeFlow"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PolylinePipeFlow is deprecated and will be removed in 5 release cycles. Please migrate to ZPolylinePipeFlow instead.")]
    [ToolboxItem(false)]
    public class PolylinePipeFlow : ZPolylinePipeFlow
    {
    }

    #endregion
}
