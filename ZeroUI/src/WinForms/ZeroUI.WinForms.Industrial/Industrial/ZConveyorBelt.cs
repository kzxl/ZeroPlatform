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
    public enum ConveyorDirection
    {
        LeftToRight,
        RightToLeft
    }

    public enum ConveyorState
    {
        Stopped,
        Running,
        Jammed,
        Fault
    }

    /// <summary>
    /// Industrial conveyor belt component with moving belt tracking markers,
    /// end-roller kinematics, speed readout, and material jam detection.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial material conveyor belt with animated kinematics and jam detection")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroConveyorBelt.bmp")]
    public class ZConveyorBelt : Control, IScadaBindable, IAnimationFrameListener
    {
        private ConveyorState _state = ConveyorState.Running;
        private ConveyorDirection _direction = ConveyorDirection.LeftToRight;
        private double _speedMpm = 24.0; // meters per minute
        private string _tagLabel = "CV-401";
        private float _beltOffset = 0f;
        private bool _isHovered;
        private bool _jamBlink;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Process Dynamics")]
        [DefaultValue(ConveyorState.Running)]
        public ConveyorState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(ConveyorDirection.LeftToRight)]
        public ConveyorDirection Direction
        {
            get => _direction;
            set { _direction = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(24.0)]
        public double SpeedMpm
        {
            get => _speedMpm;
            set { _speedMpm = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("CV-401")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZConveyorBelt()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(220, 90);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
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

        public void OnAnimationFrame(double deltaSeconds, long frameCount)
        {
            OnAnimationFrameTick(deltaSeconds, frameCount);
        }

        private void OnAnimationFrameTick(double deltaSeconds, long frameCount)
        {
            if (_state == ConveyorState.Running && _speedMpm > 0)
            {
                float step = (float)(_speedMpm * 2.0 * deltaSeconds);
                if (_direction == ConveyorDirection.RightToLeft) step = -step;

                _beltOffset = (_beltOffset + step) % 20f;
                if (_beltOffset < 0) _beltOffset += 20f;
            }

            if (_state == ConveyorState.Jammed || _state == ConveyorState.Fault)
            {
                if (frameCount % 30 == 0) _jamBlink = !_jamBlink;
            }

            if (IsHandleCreated && Visible)
            {
                Invalidate();
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (double.TryParse(tag.Value?.ToString(), out var speed))
            {
                SpeedMpm = speed;
                State = speed > 0.5 ? ConveyorState.Running : ConveyorState.Stopped;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            bool isDark = ZeroTheme.IsDark;
            Color beltColor, frameColor, rollerColor, textColor;

            switch (_state)
            {
                case ConveyorState.Running:
                    beltColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(71, 85, 105);
                    frameColor = isDark ? Color.FromArgb(34, 197, 94) : Color.FromArgb(22, 163, 74);
                    break;
                case ConveyorState.Jammed:
                case ConveyorState.Fault:
                    beltColor = isDark ? Color.FromArgb(80, 20, 20) : Color.FromArgb(254, 202, 202);
                    frameColor = _jamBlink ? Color.FromArgb(239, 68, 68) : Color.FromArgb(185, 28, 28);
                    break;
                default: // Stopped
                    beltColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(203, 213, 225);
                    frameColor = isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);
                    break;
            }

            rollerColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            textColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // 1. Tag & Status Header
            using (var fontTag = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(textColor))
            using (var stateBrush = new SolidBrush(frameColor))
            {
                g.DrawString(_tagLabel, fontTag, textBrush, 8f, 4f);
                string stateStr = _state.ToString().ToUpperInvariant();
                var stateSize = g.MeasureString(stateStr, fontTag);
                g.DrawString(stateStr, fontTag, stateBrush, Width - stateSize.Width - 8f, 4f);
            }

            // 2. Geometry calculations
            float r = 16f; // Roller radius
            float leftRollerX = 16f + r;
            float rightRollerX = Width - 16f - r;
            float rollerY = 46f;

            // 3. Main Belt Surface (Capsule shape)
            using (var beltBrush = new SolidBrush(beltColor))
            using (var beltPen = new Pen(_isHovered ? Color.FromArgb(59, 130, 246) : frameColor, _isHovered ? 2f : 1.5f))
            {
                using (var path = new GraphicsPath())
                {
                    path.AddArc(leftRollerX - r, rollerY - r, r * 2f, r * 2f, 90f, 180f);
                    path.AddLine(leftRollerX, rollerY - r, rightRollerX, rollerY - r);
                    path.AddArc(rightRollerX - r, rollerY - r, r * 2f, r * 2f, 270f, 180f);
                    path.AddLine(rightRollerX, rollerY + r, leftRollerX, rollerY + r);
                    path.CloseFigure();

                    g.FillPath(beltBrush, path);
                    g.DrawPath(beltPen, path);
                }
            }

            // 4. Moving Tracking Markers on top belt surface
            if (_state == ConveyorState.Running)
            {
                using (var markPen = new Pen(frameColor, 2f))
                {
                    float markerSpacing = 20f;
                    float startX = leftRollerX + (_beltOffset % markerSpacing);
                    while (startX < rightRollerX)
                    {
                        g.DrawLine(markPen, startX, rollerY - r + 3f, startX + 4f, rollerY - r + 8f);
                        startX += markerSpacing;
                    }
                }
            }

            // 5. Left and Right Rollers (Pulleys)
            using (var rollerBrush = new SolidBrush(rollerColor))
            using (var rollerPen = new Pen(frameColor, 1.2f))
            {
                g.FillEllipse(rollerBrush, leftRollerX - 8f, rollerY - 8f, 16f, 16f);
                g.DrawEllipse(rollerPen, leftRollerX - 8f, rollerY - 8f, 16f, 16f);

                g.FillEllipse(rollerBrush, rightRollerX - 8f, rollerY - 8f, 16f, 16f);
                g.DrawEllipse(rollerPen, rightRollerX - 8f, rollerY - 8f, 16f, 16f);
            }

            // 6. Support Legs
            using (var legPen = new Pen(rollerColor, 3f))
            {
                g.DrawLine(legPen, leftRollerX, rollerY + r, leftRollerX - 6f, Height - 16f);
                g.DrawLine(legPen, rightRollerX, rollerY + r, rightRollerX + 6f, Height - 16f);
            }

            // 7. Telemetry Readout (Speed)
            using (var dataFont = new Font(Font.FontFamily, 8f, FontStyle.Regular))
            using (var valBrush = new SolidBrush(textColor))
            {
                string info = $"{_speedMpm:0.0} m/min | {(_direction == ConveyorDirection.LeftToRight ? "→ FWD" : "← REV")}";
                g.DrawString(info, dataFont, valBrush, 8f, Height - 16f);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZConveyorBelt"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroConveyorBelt is deprecated and will be removed in 5 release cycles. Please migrate to ZConveyorBelt instead.")]
    [ToolboxItem(false)]
    public class ZeroConveyorBelt : ZConveyorBelt
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZConveyorBelt"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ConveyorBelt is deprecated and will be removed in 5 release cycles. Please migrate to ZConveyorBelt instead.")]
    [ToolboxItem(false)]
    public class ConveyorBelt : ZConveyorBelt
    {
    }

    #endregion
}
