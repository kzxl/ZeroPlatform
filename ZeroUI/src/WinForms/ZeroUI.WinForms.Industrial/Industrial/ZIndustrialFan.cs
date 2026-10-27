using System;

using System.ComponentModel;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum ZeroFanState
    {
        Stopped,
        Running,
        Fault
    }

    /// <summary>
    /// Industrial ventilation and exhaust fan component with dynamic vector blade rotation,
    /// protective wire cage shroud, and SCADA tag engine telemetry binding.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial ventilation fan with animated rotating blades")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroIndustrialFan.bmp")]
    public class ZIndustrialFan : ControlBase, IScadaBindable, IAnimationFrameListener
    {
        private ZeroFanState _state = ZeroFanState.Running;
        private double _speedRpm = 1200.0;
        private string _tagLabel = "FAN-201";
        private float _bladeAngle = 0f;
        private bool _isHovered;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Process Dynamics")]
        [DefaultValue(ZeroFanState.Running)]
        public ZeroFanState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(1200.0)]
        public double SpeedRpm
        {
            get => _speedRpm;
            set { _speedRpm = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("FAN-201")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZIndustrialFan()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(130, 140);
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
            if (_state != ZeroFanState.Running || _speedRpm <= 0) return;

            float step = (float)(_speedRpm / 60.0 * 360.0 * deltaSeconds);
            _bladeAngle = (_bladeAngle + step) % 360f;
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
                SpeedRpm = speed;
                State = speed > 10 ? ZeroFanState.Running : ZeroFanState.Stopped;
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
            Color bladeColor, frameColor, textColor;

            switch (_state)
            {
                case ZeroFanState.Running:
                    bladeColor = isDark ? Color.FromArgb(56, 189, 248) : Color.FromArgb(2, 132, 199);
                    frameColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
                    break;
                case ZeroFanState.Fault:
                    bladeColor = isDark ? Color.FromArgb(248, 113, 113) : Color.FromArgb(220, 38, 38);
                    frameColor = isDark ? Color.FromArgb(69, 10, 10) : Color.FromArgb(254, 226, 226);
                    break;
                default: // Stopped
                    bladeColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
                    frameColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
                    break;
            }

            textColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // 1. Tag & Status Header
            var fontTag = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(_tagLabel, fontTag, textBrush, 6f, 4f);
            }

            // 2. Outer Circular Shroud
            float diameter = Math.Min(Width - 20f, Height - 50f);
            float cx = Width * 0.5f;
            float cy = 24f + diameter * 0.5f;
            var shroudRect = new RectangleF(cx - diameter * 0.5f, cy - diameter * 0.5f, diameter, diameter);

            using (var shroudBrush = new SolidBrush(frameColor))
            using (var shroudPen = new Pen(_isHovered ? Color.FromArgb(59, 130, 246) : bladeColor, _isHovered ? 2.5f : 1.5f))
            {
                g.FillEllipse(shroudBrush, shroudRect);
                g.DrawEllipse(shroudPen, shroudRect);
            }

            // 3. Rotating Blades (4 aerodynamic blades)
            var state = g.Save();
            g.TranslateTransform(cx, cy);
            g.RotateTransform(_bladeAngle);

            float bladeRadius = diameter * 0.42f;
            using (var bladeBrush = new SolidBrush(bladeColor))
            {
                for (int i = 0; i < 4; i++)
                {
                    using (var bladePath = new GraphicsPath())
                    {
                        bladePath.AddBezier(0, -6f, bladeRadius * 0.5f, -14f, bladeRadius * 0.8f, -18f, bladeRadius, 0);
                        bladePath.AddBezier(bladeRadius, 0, bladeRadius * 0.7f, 6f, bladeRadius * 0.3f, 4f, 0, 6f);
                        bladePath.CloseFigure();
                        g.FillPath(bladeBrush, bladePath);
                    }
                    g.RotateTransform(90f);
                }
            }
            g.Restore(state);

            // 4. Center Hub & Protective Wire Grille
            using (var wirePen = new Pen(isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225), 1f))
            {
                g.DrawLine(wirePen, cx - diameter * 0.5f, cy, cx + diameter * 0.5f, cy);
                g.DrawLine(wirePen, cx, cy - diameter * 0.5f, cx, cy + diameter * 0.5f);
            }

            using (var hubBrush = new SolidBrush(isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252)))
            using (var hubPen = new Pen(bladeColor, 1.5f))
            {
                float hubSize = diameter * 0.22f;
                var hubRect = new RectangleF(cx - hubSize * 0.5f, cy - hubSize * 0.5f, hubSize, hubSize);
                g.FillEllipse(hubBrush, hubRect);
                g.DrawEllipse(hubPen, hubRect);
            }

            // 5. Telemetry Footer (RPM & Status)
            var dataFont = ZeroFontCache.Get(Font.FontFamily.Name, 8f, FontStyle.Regular);
            using (var valBrush = new SolidBrush(textColor))
            {
                string info = $"{_speedRpm:0} RPM";
                var infoSize = g.MeasureString(info, dataFont);
                g.DrawString(info, dataFont, valBrush, cx - infoSize.Width * 0.5f, Height - 18f);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialFan"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroIndustrialFan is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialFan instead.")]
    [ToolboxItem(false)]
    public class ZeroIndustrialFan : ZIndustrialFan
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialFan"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialFan is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialFan instead.")]
    [ToolboxItem(false)]
    public class IndustrialFan : ZIndustrialFan
    {
    }

    #endregion
}
