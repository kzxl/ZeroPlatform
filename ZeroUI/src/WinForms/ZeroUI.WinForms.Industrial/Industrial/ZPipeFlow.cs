using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum ZeroPipeShape
    {
        Horizontal,
        Vertical,
        ElbowTopRight,
        ElbowTopLeft,
        ElbowBottomRight,
        ElbowBottomLeft,
        TeeJunction,
        FourWayCross
    }


    /// <summary>
    /// High-performance industrial vector piping component with subpixel animated fluid flow pulses.
    /// Implements IScadaBindable for real-time SCADA telemetry synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroPipeFlow.bmp")]
    public class ZPipeFlow : Control, IScadaBindable
    {
        private ZeroPipeShape _shape = ZeroPipeShape.Horizontal;
        private ZeroFluidType _fluidType = ZeroFluidType.Water;
        private double _flowVelocity = 2.0; // m/s
        private bool _isFlowing = true;
        private bool _reverseFlow = false;
        private int _pipeDiameter = 18;
        private float _pulseOffset = 0f;
        private bool _useSharedClockPhase = true;
        private bool _showFlowArrows = false;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Appearance")]
        [DefaultValue(ZeroPipeShape.Horizontal)]
        public ZeroPipeShape Shape
        {
            get => _shape;
            set { _shape = value; Invalidate(); }
        }

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
        [Description("Uses centralized ZeroAnimationClock phase directly for zero-alloc multi-pipe synchronization")]
        public bool UseSharedClockPhase
        {
            get => _useSharedClockPhase;
            set { _useSharedClockPhase = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(false)]
        [Description("Draws directional flow chevron arrows along the fluid channel")]
        public bool ShowFlowArrows
        {
            get => _showFlowArrows;
            set { _showFlowArrows = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(18)]
        public int PipeDiameter
        {
            get => _pipeDiameter;
            set { _pipeDiameter = Math.Max(8, Math.Min(60, value)); Invalidate(); }
        }


        public ZPipeFlow()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Size = new Size(160, 24);

            ZeroTheme.ThemeChanged += OnThemeChanged;
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
            if (_isFlowing && _flowVelocity > 0)
            {
                float step = (float)(_flowVelocity * 45.0 * deltaSeconds);
                _pulseOffset = _reverseFlow ? (_pulseOffset - step) : (_pulseOffset + step);
                if (_pulseOffset > 1000f || _pulseOffset < -1000f) _pulseOffset = 0f;
                if (IsHandleCreated && Visible)
                {
                    Invalidate();
                }
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

            var palette = ZeroTheme.Colors;
            bool isDark = ZeroTheme.IsDark;

            Color pipeWallColor = isDark ? Color.FromArgb(46, 52, 78) : Color.FromArgb(148, 163, 184);
            Color pipeCenterColor = isDark ? Color.FromArgb(30, 36, 56) : Color.FromArgb(203, 213, 225);
            Color fluidBase = GetFluidColor();

            int d = _pipeDiameter;
            int halfD = d / 2;

            float effectiveOffset = _useSharedClockPhase
                ? (float)(_reverseFlow ? -(ZeroAnimationClock.TotalElapsedTime * _flowVelocity * 30.0 % 1000.0) : (ZeroAnimationClock.TotalElapsedTime * _flowVelocity * 30.0 % 1000.0))
                : _pulseOffset;

            if (_shape == ZeroPipeShape.Horizontal)
            {
                int y = (Height - d) / 2;
                var rect = new Rectangle(0, y, Width, d);

                // Pipe Metallic Base
                using (var brushPipe = new LinearGradientBrush(rect, pipeWallColor, pipeCenterColor, LinearGradientMode.Vertical))
                {
                    g.FillRectangle(brushPipe, rect);
                }

                // Inner Fluid Channel
                int fluidH = Math.Max(4, d - 6);
                int fluidY = y + 3;
                var fluidRect = new Rectangle(0, fluidY, Width, fluidH);

                Color coreFluid = _isFlowing ? fluidBase : Color.FromArgb(50, fluidBase);
                using (var brushFluid = new SolidBrush(coreFluid))
                {
                    g.FillRectangle(brushFluid, fluidRect);
                }

                // Animated Fluid Pulse Particles
                if (_isFlowing && _flowVelocity > 0)
                {
                    using var penPulse = new Pen(Color.FromArgb(220, Color.White), 2f);
                    penPulse.DashStyle = DashStyle.Dash;
                    penPulse.DashPattern = new[] { 6f, 8f };
                    penPulse.DashOffset = effectiveOffset;
                    g.DrawLine(penPulse, 0, fluidY + fluidH / 2, Width, fluidY + fluidH / 2);
                }

                // Directional Flow Arrows
                if (_showFlowArrows && _isFlowing)
                {
                    using var arrowBrush = new SolidBrush(Color.FromArgb(190, Color.White));
                    int arrowY = fluidY + fluidH / 2;
                    int dir = _reverseFlow ? -1 : 1;
                    for (int ax = 24; ax < Width - 24; ax += 36)
                    {
                        PointF[] arrowPts = new PointF[]
                        {
                            new PointF(ax - (4 * dir), arrowY - 3),
                            new PointF(ax + (4 * dir), arrowY),
                            new PointF(ax - (4 * dir), arrowY + 3)
                        };
                        g.FillPolygon(arrowBrush, arrowPts);
                    }
                }

                // Flange Joints
                DrawFlange(g, 0, y, d, true, isDark);
                DrawFlange(g, Width - 6, y, d, true, isDark);
            }
            else if (_shape == ZeroPipeShape.Vertical)
            {
                int x = (Width - d) / 2;
                var rect = new Rectangle(x, 0, d, Height);

                using (var brushPipe = new LinearGradientBrush(rect, pipeWallColor, pipeCenterColor, LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(brushPipe, rect);
                }

                int fluidW = Math.Max(4, d - 6);
                int fluidX = x + 3;
                var fluidRect = new Rectangle(fluidX, 0, fluidW, Height);

                Color coreFluid = _isFlowing ? fluidBase : Color.FromArgb(50, fluidBase);
                using (var brushFluid = new SolidBrush(coreFluid))
                {
                    g.FillRectangle(brushFluid, fluidRect);
                }

                if (_isFlowing && _flowVelocity > 0)
                {
                    using var penPulse = new Pen(Color.FromArgb(220, Color.White), 2f);
                    penPulse.DashStyle = DashStyle.Dash;
                    penPulse.DashPattern = new[] { 6f, 8f };
                    penPulse.DashOffset = effectiveOffset;
                    g.DrawLine(penPulse, fluidX + fluidW / 2, 0, fluidX + fluidW / 2, Height);
                }

                // Directional Flow Arrows
                if (_showFlowArrows && _isFlowing)
                {
                    using var arrowBrush = new SolidBrush(Color.FromArgb(190, Color.White));
                    int arrowX = fluidX + fluidW / 2;
                    int dir = _reverseFlow ? -1 : 1;
                    for (int ay = 24; ay < Height - 24; ay += 36)
                    {
                        PointF[] arrowPts = new PointF[]
                        {
                            new PointF(arrowX - 3, ay - (4 * dir)),
                            new PointF(arrowX, ay + (4 * dir)),
                            new PointF(arrowX + 3, ay - (4 * dir))
                        };
                        g.FillPolygon(arrowBrush, arrowPts);
                    }
                }

                DrawFlange(g, x, 0, d, false, isDark);
                DrawFlange(g, x, Height - 6, d, false, isDark);
            }

            else
            {
                // Curved Elbow / Tee fallback rendering
                DrawElbow(g, d, pipeWallColor, fluidBase, isDark);
            }
        }

        private void DrawElbow(Graphics g, int d, Color pipeWall, Color fluidBase, bool isDark)
        {
            int cx = Width / 2;
            int cy = Height / 2;

            using var penPipe = new Pen(pipeWall, d);
            using var penFluid = new Pen(_isFlowing ? fluidBase : Color.FromArgb(50, fluidBase), d - 6);

            // Connect center to edges
            Point p1, p2;
            switch (_shape)
            {
                case ZeroPipeShape.ElbowTopRight:
                    p1 = new Point(cx, 0); p2 = new Point(Width, cy);
                    break;
                case ZeroPipeShape.ElbowTopLeft:
                    p1 = new Point(cx, 0); p2 = new Point(0, cy);
                    break;
                case ZeroPipeShape.ElbowBottomRight:
                    p1 = new Point(cx, Height); p2 = new Point(Width, cy);
                    break;
                case ZeroPipeShape.ElbowBottomLeft:
                default:
                    p1 = new Point(cx, Height); p2 = new Point(0, cy);
                    break;
            }

            using var path = new GraphicsPath();
            path.AddLine(p1, new Point(cx, cy));
            path.AddLine(new Point(cx, cy), p2);

            g.DrawPath(penPipe, path);
            g.DrawPath(penFluid, path);
        }

        private static void DrawFlange(Graphics g, int x, int y, int d, bool isHorizontal, bool isDark)
        {
            Color flangeCol = isDark ? Color.FromArgb(70, 78, 110) : Color.FromArgb(100, 116, 139);
            using var brush = new SolidBrush(flangeCol);
            if (isHorizontal)
            {
                g.FillRectangle(brush, x, y - 2, 6, d + 4);
            }
            else
            {
                g.FillRectangle(brush, x - 2, y, d + 4, 6);
            }
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
    /// Legacy alias for <see cref="ZPipeFlow"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPipeFlow is deprecated and will be removed in 5 release cycles. Please migrate to ZPipeFlow instead.")]
    [ToolboxItem(false)]
    public class ZeroPipeFlow : ZPipeFlow
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPipeFlow"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PipeFlow is deprecated and will be removed in 5 release cycles. Please migrate to ZPipeFlow instead.")]
    [ToolboxItem(false)]
    public class PipeFlow : ZPipeFlow
    {
    }

    #endregion
}
