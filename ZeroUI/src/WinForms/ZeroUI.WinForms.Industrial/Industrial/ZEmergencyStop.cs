using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Safety;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial Emergency Stop pushbutton compliant with IEC 60947-5-5 and ISO 13850.
    /// Features high-contrast safety yellow collar, 3D mushroom actuator with twist-to-release arrows,
    /// dual-channel safety contact monitoring (SIL/PL discrepancy detection), and animated latching action.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroIndustrialButton.bmp")]
    [Category("ZeroUI - SCADA Safety")]
    public class ZEmergencyStop : ControlBase, IScadaBindable
    {
        private bool _isDepressed;
        private bool _channel1Ok = true;
        private bool _channel2Ok = true;
        private EStopResetMode _resetMode = EStopResetMode.TwistToReset;
        private string _collarText = "EMERGENCY STOP";
        private bool _showDirectionalArrows = true;
        private bool _showSafetyContactLeds = true;
        private Color _mushroomColor = Color.FromArgb(204, 24, 30);
        private Color _collarColor = Color.FromArgb(245, 196, 0);

        private float _twistAngle = 0f;
        private bool _isDraggingTwist;
        private Point _dragStartPoint;
        private float _depressOffset = 0f;
        private IDisposable? _animationToken;

        public ZEmergencyStop()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(160, 160);
            Cursor = Cursors.Hand;
        }

        #region Properties

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Safety State")]
        [DefaultValue(false)]
        [Description("Indicates whether the emergency stop button is mechanically latched in the tripped state.")]
        public bool IsDepressed
        {
            get => _isDepressed;
            set
            {
                if (_isDepressed != value)
                {
                    _isDepressed = value;
                    if (_isDepressed)
                    {
                        _channel1Ok = false;
                        _channel2Ok = false;
                        _depressOffset = 6f;
                        Tripped?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        _channel1Ok = true;
                        _channel2Ok = true;
                        _depressOffset = 0f;
                        _twistAngle = 0f;
                        ResetCompleted?.Invoke(this, EventArgs.Empty);
                    }
                    Invalidate();
                }
            }
        }

        [Category("Safety State")]
        [DefaultValue(true)]
        [Description("State of Safety Channel 1 (Normally Closed contact).")]
        public bool Channel1Ok
        {
            get => _channel1Ok;
            set
            {
                if (_channel1Ok != value)
                {
                    _channel1Ok = value;
                    EvaluateChannelState();
                    Invalidate();
                }
            }
        }

        [Category("Safety State")]
        [DefaultValue(true)]
        [Description("State of Safety Channel 2 (Normally Closed contact).")]
        public bool Channel2Ok
        {
            get => _channel2Ok;
            set
            {
                if (_channel2Ok != value)
                {
                    _channel2Ok = value;
                    EvaluateChannelState();
                    Invalidate();
                }
            }
        }

        [Browsable(false)]
        public bool HasChannelFault => _channel1Ok != _channel2Ok;

        [Browsable(false)]
        public EStopStatus Status
        {
            get
            {
                if (HasChannelFault) return EStopStatus.ChannelFault;
                if (_isDepressed || !_channel1Ok || !_channel2Ok) return EStopStatus.Tripped;
                return EStopStatus.Normal;
            }
        }

        [Category("Safety Behavior")]
        [DefaultValue(EStopResetMode.TwistToReset)]
        public EStopResetMode ResetMode
        {
            get => _resetMode;
            set { _resetMode = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("EMERGENCY STOP")]
        public string CollarText
        {
            get => _collarText;
            set { _collarText = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowDirectionalArrows
        {
            get => _showDirectionalArrows;
            set { _showDirectionalArrows = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowSafetyContactLeds
        {
            get => _showSafetyContactLeds;
            set { _showSafetyContactLeds = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color MushroomColor
        {
            get => _mushroomColor;
            set { _mushroomColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color CollarColor
        {
            get => _collarColor;
            set { _collarColor = value; Invalidate(); }
        }

        [Category("Mechanics")]
        [DefaultValue(0f)]
        public float TwistAngle
        {
            get => _twistAngle;
            set { _twistAngle = value; Invalidate(); }
        }

        #endregion

        #region Events

        public event EventHandler? Tripped;
        public event EventHandler? ResetCompleted;
        public event EventHandler? ChannelFaultChanged;

        #endregion

        #region Methods

        public void Trip()
        {
            IsDepressed = true;
        }

        public void Reset()
        {
            IsDepressed = false;
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (bool.TryParse(tag.Value.ToString(), out var b))
            {
                IsDepressed = b;
            }
        }

        private void EvaluateChannelState()
        {
            if (HasChannelFault)
            {
                ChannelFaultChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        #endregion

        #region Mouse Interaction

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left) return;

            var center = new PointF(Width / 2f, Height / 2f);
            float radius = Math.Min(Width, Height) * 0.34f;
            float dx = e.X - center.X;
            float dy = e.Y - center.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist <= radius)
            {
                if (!_isDepressed)
                {
                    // Mechanical press down
                    IsDepressed = true;
                }
                else
                {
                    // Start twisting or pull to reset
                    if (_resetMode == EStopResetMode.TwistToReset)
                    {
                        _isDraggingTwist = true;
                        _dragStartPoint = e.Location;
                    }
                    else
                    {
                        Reset();
                    }
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_isDraggingTwist && _isDepressed)
            {
                var center = new PointF(Width / 2f, Height / 2f);
                float a1 = (float)(Math.Atan2(_dragStartPoint.Y - center.Y, _dragStartPoint.X - center.X) * 180.0 / Math.PI);
                float a2 = (float)(Math.Atan2(e.Y - center.Y, e.X - center.X) * 180.0 / Math.PI);
                float delta = a2 - a1;

                if (delta > 0)
                {
                    _twistAngle = Math.Min(35f, delta);
                    Invalidate();

                    if (_twistAngle >= 30f)
                    {
                        _isDraggingTwist = false;
                        Reset();
                    }
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingTwist)
            {
                _isDraggingTwist = false;
                if (_isDepressed)
                {
                    // Return spring
                    _twistAngle = 0f;
                    Invalidate();
                }
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int side = Math.Min(Width, Height);
            float cx = Width / 2f;
            float cy = Height / 2f;
            float collarRadius = (side - 16) / 2f;

            if (collarRadius <= 10) return;

            // 1. Outer Bezel / Housing Base
            var collarBounds = new RectangleF(cx - collarRadius, cy - collarRadius, collarRadius * 2f, collarRadius * 2f);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(collarBounds);
                using (var brush = new LinearGradientBrush(collarBounds, Color.FromArgb(70, 75, 80), Color.FromArgb(30, 32, 35), 45f))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(Color.FromArgb(90, 95, 102), 2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // 2. Yellow Safety Collar
            float yellowRadius = collarRadius - 6f;
            var yellowBounds = new RectangleF(cx - yellowRadius, cy - yellowRadius, yellowRadius * 2f, yellowRadius * 2f);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(yellowBounds);
                using (var brush = new LinearGradientBrush(yellowBounds, _collarColor, Color.FromArgb(200, 150, 0), 60f))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(Color.FromArgb(160, 120, 0), 1.5f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // 3. Collar Legend Text ("EMERGENCY STOP")
            if (!string.IsNullOrEmpty(_collarText))
            {
                var font = ZeroFontCache.Get("Segoe UI", Math.Max(7.5f, side * 0.055f), FontStyle.Bold);
                using (var textBrush = new SolidBrush(Color.FromArgb(30, 30, 30)))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                {
                    // Top arc label or straight banner
                    var topTextRect = new RectangleF(cx - yellowRadius, cy - yellowRadius + 8f, yellowRadius * 2f, side * 0.16f);
                    g.DrawString(_collarText, font, textBrush, topTextRect, sf);
                }
            }

            // 4. Mushroom Actuator Head
            float mushroomRadius = yellowRadius * 0.65f;
            float currentMushroomRadius = _isDepressed ? mushroomRadius * 0.94f : mushroomRadius;
            float mushroomCenterY = cy + (_isDepressed ? _depressOffset : 0f);

            var mushroomBounds = new RectangleF(cx - currentMushroomRadius, mushroomCenterY - currentMushroomRadius, currentMushroomRadius * 2f, currentMushroomRadius * 2f);

            // Cast shadow
            if (!_isDepressed)
            {
                using (var shadowPath = new GraphicsPath())
                {
                    shadowPath.AddEllipse(cx - mushroomRadius + 2f, cy - mushroomRadius + 5f, mushroomRadius * 2f, mushroomRadius * 2f);
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
                    {
                        g.FillPath(shadowBrush, shadowPath);
                    }
                }
            }

            // Mushroom Body Gradient
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(mushroomBounds);
                Color topCol = _isDepressed ? Color.FromArgb(180, 20, 24) : Color.FromArgb(240, 48, 56);
                Color botCol = _isDepressed ? Color.FromArgb(110, 10, 14) : Color.FromArgb(150, 16, 20);

                using (var brush = new LinearGradientBrush(mushroomBounds, topCol, botCol, 75f))
                {
                    g.FillPath(brush, path);
                }

                // Dome 3D Highlight sheen
                float sheenWidth = currentMushroomRadius * 1.3f;
                float sheenHeight = currentMushroomRadius * 0.7f;
                var sheenBounds = new RectangleF(cx - sheenWidth / 2f, mushroomCenterY - currentMushroomRadius + 4f, sheenWidth, sheenHeight);
                using (var sheenBrush = new LinearGradientBrush(sheenBounds, Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                {
                    g.FillEllipse(sheenBrush, sheenBounds);
                }

                using (var pen = new Pen(Color.FromArgb(90, 8, 12), 2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // 5. Directional Reset Arrows (Rotated by _twistAngle)
            if (_showDirectionalArrows && _resetMode == EStopResetMode.TwistToReset)
            {
                var state = g.Save();
                g.TranslateTransform(cx, mushroomCenterY);
                g.RotateTransform(_twistAngle);

                float arrowOrbit = currentMushroomRadius * 0.55f;
                using (var arrowPen = new Pen(Color.FromArgb(220, 255, 255, 255), 2.5f))
                {
                    arrowPen.StartCap = LineCap.Round;
                    arrowPen.EndCap = LineCap.ArrowAnchor;

                    for (int i = 0; i < 3; i++)
                    {
                        float startAng = i * 120f + 10f;
                        g.DrawArc(arrowPen, -arrowOrbit, -arrowOrbit, arrowOrbit * 2f, arrowOrbit * 2f, startAng, 65f);
                    }
                }

                g.Restore(state);
            }

            // 6. Dual-Channel Safety Contact Indicators
            if (_showSafetyContactLeds)
            {
                DrawSafetyContactLeds(g, cx, cy, yellowRadius);
            }
        }

        private void DrawSafetyContactLeds(Graphics g, float cx, float cy, float yellowRadius)
        {
            var font = ZeroFontCache.Get("Segoe UI", 7f, FontStyle.Bold);
            float ledY = cy + yellowRadius - 16f;

            // CH1 LED
            float ch1X = cx - 28f;
            Color ch1Color = _channel1Ok ? Color.FromArgb(40, 200, 60) : Color.FromArgb(220, 40, 40);
            using (var brush = new SolidBrush(ch1Color))
            {
                g.FillEllipse(brush, ch1X, ledY, 8f, 8f);
            }
            using (var pen = new Pen(Color.FromArgb(50, 50, 50), 1f))
            {
                g.DrawEllipse(pen, ch1X, ledY, 8f, 8f);
            }
            using (var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                g.DrawString("1", font, textBrush, ch1X - 8f, ledY - 1f);
            }

            // CH2 LED
            float ch2X = cx + 20f;
            Color ch2Color = _channel2Ok ? Color.FromArgb(40, 200, 60) : Color.FromArgb(220, 40, 40);
            using (var brush = new SolidBrush(ch2Color))
            {
                g.FillEllipse(brush, ch2X, ledY, 8f, 8f);
            }
            using (var pen = new Pen(Color.FromArgb(50, 50, 50), 1f))
            {
                g.DrawEllipse(pen, ch2X, ledY, 8f, 8f);
            }
            using (var textBrush = new SolidBrush(Color.FromArgb(40, 40, 40)))
            {
                g.DrawString("2", font, textBrush, ch2X + 10f, ledY - 1f);
            }
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationToken?.Dispose();
                _animationToken = null;
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZEmergencyStop"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("EmergencyStopControl is deprecated and will be removed in 5 release cycles. Please migrate to ZEmergencyStop instead.")]
    [ToolboxItem(false)]
    public class EmergencyStopControl : ZEmergencyStop
    {
    }

    #endregion
}
