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
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum ZeroMotorDirection
    {
        Forward,
        Reverse
    }

    public enum ZeroMotorState
    {
        Stopped,
        Running,
        OverloadTrip,
        Fault
    }

    /// <summary>
    /// Industrial 3-phase induction motor drive component with cooling fin vector geometry,
    /// dynamic shaft rotation indicators, RPM telemetry readouts, and SCADA tag engine synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroIndustrialMotor.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial electric motor drive with cooling fins and dynamic telemetry status")]
    public class ZIndustrialMotor : ControlBase, IScadaBindable, IAnimationFrameListener
    {
        private ZeroMotorState _state = ZeroMotorState.Running;
        private ZeroMotorDirection _direction = ZeroMotorDirection.Forward;
        private DeviceStatusFlags _statusFlags = DeviceStatusFlags.None;
        private double _speedRpm = 1450.0;
        private double _ratedRpm = 1500.0;
        private double _currentAmps = 14.2;
        private string _tagLabel = "M-101";
        private float _shaftAngle = 0f;
        private bool _isHovered;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        [Description("SCADA tag path to bind for dynamic motor telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Safety & Interlocks")]
        [DefaultValue(DeviceStatusFlags.None)]
        [Description("Active OSHA LOTO, safety interlock, and maintenance status flags")]
        public DeviceStatusFlags StatusFlags
        {
            get => _statusFlags;
            set { _statusFlags = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(ZeroMotorState.Running)]
        public ZeroMotorState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(ZeroMotorDirection.Forward)]
        public ZeroMotorDirection Direction
        {
            get => _direction;
            set { _direction = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(1450.0)]
        public double SpeedRpm
        {
            get => _speedRpm;
            set { _speedRpm = Math.Max(0, value); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(1500.0)]
        public double RatedRpm
        {
            get => _ratedRpm;
            set { _ratedRpm = Math.Max(1.0, value); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(14.2)]
        public double CurrentAmps
        {
            get => _currentAmps;
            set { _currentAmps = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("M-101")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZIndustrialMotor()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(160, 110);
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
            if (_state != ZeroMotorState.Running || _speedRpm <= 0) return;

            float step = (float)(_speedRpm / 60.0 * 360.0 * deltaSeconds);
            if (_direction == ZeroMotorDirection.Reverse) step = -step;

            _shaftAngle = (_shaftAngle + step) % 360f;
            if (IsHandleCreated && Visible)
            {
                Invalidate();
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (double.TryParse(tag.Value?.ToString(), out var rpm))
            {
                SpeedRpm = rpm;
                State = rpm > 5 ? ZeroMotorState.Running : ZeroMotorState.Stopped;
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
            Color primaryFill, finColor, shaftColor, textColor;

            switch (_state)
            {
                case ZeroMotorState.Running:
                    primaryFill = isDark ? Color.FromArgb(16, 80, 50) : Color.FromArgb(220, 252, 231);
                    finColor = isDark ? Color.FromArgb(34, 197, 94) : Color.FromArgb(22, 163, 74);
                    break;
                case ZeroMotorState.OverloadTrip:
                case ZeroMotorState.Fault:
                    primaryFill = isDark ? Color.FromArgb(80, 20, 20) : Color.FromArgb(254, 226, 226);
                    finColor = isDark ? Color.FromArgb(239, 68, 68) : Color.FromArgb(220, 38, 38);
                    break;
                default: // Stopped
                    primaryFill = isDark ? Color.FromArgb(35, 40, 50) : Color.FromArgb(241, 245, 249);
                    finColor = isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);
                    break;
            }

            shaftColor = isDark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(51, 65, 85);
            textColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // Bounds calculations
            float padX = 12f;
            float padY = 10f;
            float motorW = Width - 50f - padX;
            float motorH = Height - padY * 2 - 20f;
            var bodyRect = new RectangleF(padX, padY + 18f, motorW, motorH);

            // 1. Motor Feet / Mount Base
            using (var footBrush = new SolidBrush(isDark ? Color.FromArgb(45, 55, 72) : Color.FromArgb(203, 213, 225)))
            {
                g.FillRectangle(footBrush, bodyRect.Left + 8f, bodyRect.Bottom - 4f, 18f, 6f);
                g.FillRectangle(footBrush, bodyRect.Right - 26f, bodyRect.Bottom - 4f, 18f, 6f);
            }

            // 2. Terminal Box (Top mount)
            using (var boxBrush = new SolidBrush(isDark ? Color.FromArgb(55, 65, 81) : Color.FromArgb(226, 232, 240)))
            using (var boxPen = new Pen(finColor, 1.2f))
            {
                var boxRect = new RectangleF(bodyRect.Left + 20f, bodyRect.Top - 8f, 32f, 10f);
                g.FillRectangle(boxBrush, boxRect);
                g.DrawRectangle(boxPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);
            }

            // 3. Stator Body (Cylindrical main casing)
            using (var bodyBrush = new LinearGradientBrush(bodyRect, primaryFill,
                isDark ? Color.FromArgb(20, 25, 35) : Color.FromArgb(203, 213, 225), LinearGradientMode.Vertical))
            using (var borderPen = new Pen(_isHovered ? Color.FromArgb(59, 130, 246) : finColor, _isHovered ? 1.8f : 1.2f))
            {
                g.FillRectangle(bodyBrush, bodyRect);
                g.DrawRectangle(borderPen, bodyRect.X, bodyRect.Y, bodyRect.Width, bodyRect.Height);
            }

            // 4. Stator Cooling Fins (Parallel lines)
            using (var finPen = new Pen(finColor, 1.5f))
            {
                float finSpacing = bodyRect.Width / 6f;
                for (int i = 1; i < 6; i++)
                {
                    float fx = bodyRect.Left + i * finSpacing;
                    g.DrawLine(finPen, fx, bodyRect.Top + 2f, fx, bodyRect.Bottom - 2f);
                }
            }

            // 5. Motor Shaft & Pulley
            float shaftStartX = bodyRect.Right;
            float shaftY = bodyRect.Top + bodyRect.Height * 0.5f - 6f;
            using (var shaftBrush = new SolidBrush(shaftColor))
            using (var shaftPen = new Pen(finColor, 1f))
            {
                var shaftRect = new RectangleF(shaftStartX, shaftY, 22f, 12f);
                g.FillRectangle(shaftBrush, shaftRect);
                g.DrawRectangle(shaftPen, shaftRect.X, shaftRect.Y, shaftRect.Width, shaftRect.Height);

                // Rotating cross indicator on shaft face
                float crossCenterX = shaftRect.Right + 8f;
                float crossCenterY = shaftY + 6f;
                var state = g.Save();
                g.TranslateTransform(crossCenterX, crossCenterY);
                g.RotateTransform(_shaftAngle);

                using (var hubBrush = new SolidBrush(finColor))
                using (var hubPen = new Pen(shaftColor, 2f))
                {
                    g.FillEllipse(hubBrush, -6f, -6f, 12f, 12f);
                    g.DrawLine(hubPen, -5f, 0, 5f, 0);
                    g.DrawLine(hubPen, 0, -5f, 0, 5f);
                }
                g.Restore(state);
            }

            // 6. Header Tag Label
            var fontTag = ZeroFontCache.Get("Segoe UI", 8.5f * DpiScale, FontStyle.Bold);
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(_tagLabel, fontTag, textBrush, padX, padY - 2f);
            }

            // 7. Telemetry Readouts (RPM, Amps, State)
            var dataFont = ZeroFontCache.Get("Segoe UI", 8f * DpiScale, FontStyle.Regular);
            using (var valBrush = new SolidBrush(textColor))
            using (var stateBrush = new SolidBrush(finColor))
            {
                string info = $"{_speedRpm:0} RPM | {_currentAmps:0.0}A";
                g.DrawString(info, dataFont, valBrush, padX, bodyRect.Bottom + 4f);

                string stateText = _state.ToString().ToUpperInvariant();
                var stateSize = g.MeasureString(stateText, dataFont);
                g.DrawString(stateText, dataFont, stateBrush, Width - stateSize.Width - 10f, padY - 2f);
            }

            // 8. Safety & Interlock Badges
            if (_statusFlags != DeviceStatusFlags.None)
            {
                ZDeviceBadgeRenderer.DrawBadges(g, ClientRectangle, _statusFlags, isDark);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialMotor"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroIndustrialMotor is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialMotor instead.")]
    [ToolboxItem(false)]
    public class ZeroIndustrialMotor : ZIndustrialMotor
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialMotor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialMotor is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialMotor instead.")]
    [ToolboxItem(false)]
    public class IndustrialMotor : ZIndustrialMotor
    {
    }

    #endregion
}
