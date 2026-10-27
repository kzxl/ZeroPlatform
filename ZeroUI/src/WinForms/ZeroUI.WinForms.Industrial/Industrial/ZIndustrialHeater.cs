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
    public enum ZeroHeaterState
    {
        Off,
        Heating,
        Warning,
        OverheatTrip
    }

    /// <summary>
    /// Industrial electric heating element with thermal glow rendering,
    /// dynamic temperature telemetry, setpoint reference, and over-temperature trip monitoring.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial electric heater with thermal glow and temperature telemetry")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroIndustrialHeater.bmp")]
    public class ZIndustrialHeater : ControlBase, IScadaBindable, IAnimationFrameListener
    {
        private ZeroHeaterState _state = ZeroHeaterState.Heating;
        private double _temperatureC = 185.4;
        private double _setpointC = 200.0;
        private double _highAlarmC = 230.0;
        private string _tagLabel = "HTR-301";
        private float _glowPhase = 0f;
        private bool _isHovered;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Process Dynamics")]
        [DefaultValue(ZeroHeaterState.Heating)]
        public ZeroHeaterState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(185.4)]
        public double TemperatureC
        {
            get => _temperatureC;
            set { _temperatureC = value; CheckAlarmThresholds(); Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(200.0)]
        public double SetpointC
        {
            get => _setpointC;
            set { _setpointC = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(230.0)]
        public double HighAlarmC
        {
            get => _highAlarmC;
            set { _highAlarmC = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("HTR-301")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZIndustrialHeater()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(150, 110);
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
            if (_state != ZeroHeaterState.Heating && _state != ZeroHeaterState.Warning) return;

            _glowPhase += (float)(deltaSeconds * 4.0);
            if (IsHandleCreated && Visible)
            {
                Invalidate();
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (double.TryParse(tag.Value?.ToString(), out var temp))
            {
                TemperatureC = temp;
            }
        }

        private void CheckAlarmThresholds()
        {
            if (_temperatureC >= _highAlarmC)
            {
                _state = ZeroHeaterState.OverheatTrip;
            }
            else if (_temperatureC >= _setpointC + 15.0)
            {
                _state = ZeroHeaterState.Warning;
            }
            else if (_temperatureC > 30.0)
            {
                _state = ZeroHeaterState.Heating;
            }
            else
            {
                _state = ZeroHeaterState.Off;
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
            Color elementColor, housingColor, textColor;

            float pulse = (float)(Math.Sin(_glowPhase) * 0.15 + 0.85);

            switch (_state)
            {
                case ZeroHeaterState.Heating:
                    int r = (int)Math.Min(255, 249 * pulse);
                    int gr = (int)Math.Min(255, 115 * pulse);
                    elementColor = Color.FromArgb(r, gr, 22); // Vibrant Orange-Red Thermal Glow
                    housingColor = isDark ? Color.FromArgb(45, 25, 20) : Color.FromArgb(254, 242, 242);
                    break;
                case ZeroHeaterState.Warning:
                case ZeroHeaterState.OverheatTrip:
                    elementColor = Color.FromArgb(239, 68, 68); // Red
                    housingColor = isDark ? Color.FromArgb(69, 10, 10) : Color.FromArgb(254, 226, 226);
                    break;
                default: // Off
                    elementColor = isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);
                    housingColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(241, 245, 249);
                    break;
            }

            textColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // 1. Header Tag
            var fontTag = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(_tagLabel, fontTag, textBrush, 8f, 6f);
            }

            // 2. Housing Box
            var boxRect = new RectangleF(8f, 24f, Width - 16f, Height - 48f);
            using (var boxBrush = new SolidBrush(housingColor))
            using (var boxPen = new Pen(_isHovered ? Color.FromArgb(59, 130, 246) : elementColor, _isHovered ? 2f : 1.2f))
            {
                g.FillRectangle(boxBrush, boxRect);
                g.DrawRectangle(boxPen, boxRect.X, boxRect.Y, boxRect.Width, boxRect.Height);
            }

            // 3. Serpentine Heating Ribbon (Coil pattern)
            using (var coilPen = new Pen(elementColor, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                using (var path = new GraphicsPath())
                {
                    float coilLeft = boxRect.Left + 12f;
                    float coilRight = boxRect.Right - 12f;
                    float coilTop = boxRect.Top + 8f;
                    float coilBottom = boxRect.Bottom - 8f;
                    float loops = 5;
                    float step = (coilRight - coilLeft) / loops;

                    path.StartFigure();
                    path.AddLine(coilLeft, boxRect.Top, coilLeft, coilBottom);

                    for (int i = 0; i < (int)loops; i++)
                    {
                        float x1 = coilLeft + i * step;
                        float x2 = x1 + step;
                        float yPeak = (i % 2 == 0) ? coilTop : coilBottom;
                        path.AddLine(x1, (i % 2 == 0) ? coilBottom : coilTop, x2, yPeak);
                    }
                    path.AddLine(coilRight, coilBottom, coilRight, boxRect.Top);

                    g.DrawPath(coilPen, path);
                }
            }

            // 4. Temperature Telemetry Readout
            var dataFont = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Regular);
            using (var valBrush = new SolidBrush(textColor))
            using (var tempBrush = new SolidBrush(elementColor))
            {
                string tempStr = $"{_temperatureC:0.0}°C";
                g.DrawString(tempStr, dataFont, tempBrush, 8f, Height - 20f);

                string spStr = $"SP: {_setpointC:0.0}°C";
                var spSize = g.MeasureString(spStr, dataFont);
                g.DrawString(spStr, dataFont, valBrush, Width - spSize.Width - 8f, Height - 20f);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialHeater"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroIndustrialHeater is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialHeater instead.")]
    [ToolboxItem(false)]
    public class ZeroIndustrialHeater : ZIndustrialHeater
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialHeater"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialHeater is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialHeater instead.")]
    [ToolboxItem(false)]
    public class IndustrialHeater : ZIndustrialHeater
    {
    }

    #endregion
}
