using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    public enum SensorType
    {
        Proximity,
        Photoelectric,
        LevelSwitch,
        PressureSwitch
    }

    public enum SensorState
    {
        Inactive,
        Active,
        Fault
    }

    /// <summary>
    /// Industrial digital sensor component representing proximity switches, optical sensors,
    /// level switches, and pressure switches with dynamic indicator LEDs and SCADA tag telemetry.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial sensor element with dynamic detection LED and telemetry binding")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroIndustrialSensor.bmp")]
    public class ZIndustrialSensor : Control, IScadaBindable
    {
        private SensorType _sensorType = SensorType.Proximity;
        private SensorState _state = SensorState.Inactive;
        private string _tagLabel = "PX-101";
        private bool _isHovered;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Appearance")]
        [DefaultValue(SensorType.Proximity)]
        public SensorType SensorType
        {
            get => _sensorType;
            set { _sensorType = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(SensorState.Inactive)]
        public SensorState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("PX-101")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZIndustrialSensor()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(110, 80);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 8.5f);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!ZeroDesignHelper.IsInDesignMode(this))
            {
                ZeroTagEngine.RegisterBindable(this);
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            ZeroTagEngine.UnregisterBindable(this);
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (tag.Value is bool b)
            {
                State = b ? SensorState.Active : SensorState.Inactive;
            }
            else if (int.TryParse(tag.Value?.ToString(), out var i))
            {
                State = i != 0 ? SensorState.Active : SensorState.Inactive;
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
            Color ledColor, bodyColor, textColor;

            switch (_state)
            {
                case SensorState.Active:
                    ledColor = Color.FromArgb(245, 158, 11); // Amber / Yellow Active
                    break;
                case SensorState.Fault:
                    ledColor = Color.FromArgb(239, 68, 68); // Red
                    break;
                default: // Inactive
                    ledColor = isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225);
                    break;
            }

            bodyColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
            textColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // 1. Tag Header
            using (var fontTag = new Font(Font.FontFamily, 8f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(_tagLabel, fontTag, textBrush, 6f, 4f);
            }

            // 2. Sensor Housing Geometry based on SensorType
            float cy = Height * 0.5f + 4f;
            using (var bodyBrush = new SolidBrush(bodyColor))
            using (var bodyPen = new Pen(_isHovered ? Color.FromArgb(59, 130, 246) : Color.FromArgb(100, 116, 139), _isHovered ? 2f : 1.2f))
            {
                switch (_sensorType)
                {
                    case SensorType.Proximity:
                        // Threaded barrel cylinder M12
                        var proxRect = new RectangleF(10f, cy - 10f, 55f, 20f);
                        g.FillRectangle(bodyBrush, proxRect);
                        g.DrawRectangle(bodyPen, proxRect.X, proxRect.Y, proxRect.Width, proxRect.Height);
                        // Sensing face (active sensing end)
                        using (var faceBrush = new SolidBrush(Color.FromArgb(59, 130, 246)))
                        {
                            g.FillRectangle(faceBrush, proxRect.Right, proxRect.Y + 2f, 6f, proxRect.Height - 4f);
                        }
                        break;

                    case SensorType.Photoelectric:
                        // Optical block head with dual lens
                        var optRect = new RectangleF(10f, cy - 12f, 45f, 24f);
                        g.FillRectangle(bodyBrush, optRect);
                        g.DrawRectangle(bodyPen, optRect.X, optRect.Y, optRect.Width, optRect.Height);
                        // Optical lenses
                        using (var lensBrush = new SolidBrush(Color.FromArgb(239, 68, 68)))
                        {
                            g.FillEllipse(lensBrush, optRect.Right - 4f, optRect.Y + 3f, 6f, 6f);
                            g.FillEllipse(lensBrush, optRect.Right - 4f, optRect.Bottom - 9f, 6f, 6f);
                        }
                        break;

                    case SensorType.LevelSwitch:
                        // Float probe with buoyant float ring
                        g.DrawLine(bodyPen, 15f, cy, 55f, cy);
                        var floatRect = new RectangleF(30f, cy - 12f, 16f, 24f);
                        g.FillEllipse(bodyBrush, floatRect);
                        g.DrawEllipse(bodyPen, floatRect);
                        break;

                    case SensorType.PressureSwitch:
                        // Round dial casing with nipple port
                        var dialRect = new RectangleF(14f, cy - 14f, 28f, 28f);
                        g.FillEllipse(bodyBrush, dialRect);
                        g.DrawEllipse(bodyPen, dialRect);
                        g.DrawLine(bodyPen, dialRect.Right, cy, dialRect.Right + 12f, cy);
                        break;
                }
            }

            // 3. Status Indicator LED (Glow effect when active)
            float ledX = Width - 24f;
            var ledRect = new RectangleF(ledX, cy - 6f, 12f, 12f);
            using (var ledBrush = new SolidBrush(ledColor))
            using (var ledPen = new Pen(Color.FromArgb(51, 65, 85), 1.2f))
            {
                g.FillEllipse(ledBrush, ledRect);
                g.DrawEllipse(ledPen, ledRect);

                if (_state == SensorState.Active)
                {
                    using (var glowPen = new Pen(Color.FromArgb(120, ledColor.R, ledColor.G, ledColor.B), 3f))
                    {
                        g.DrawEllipse(glowPen, ledRect.X - 1f, ledRect.Y - 1f, ledRect.Width + 2f, ledRect.Height + 2f);
                    }
                }
            }

            // 4. State descriptor
            using (var stateFont = new Font(Font.FontFamily, 7.5f, FontStyle.Regular))
            using (var stateBrush = new SolidBrush(textColor))
            {
                string desc = _state == SensorState.Active ? "DETECTED" : _state.ToString().ToUpperInvariant();
                g.DrawString(desc, stateFont, stateBrush, 6f, Height - 16f);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialSensor"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroIndustrialSensor is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialSensor instead.")]
    [ToolboxItem(false)]
    public class ZeroIndustrialSensor : ZIndustrialSensor
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZIndustrialSensor"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("IndustrialSensor is deprecated and will be removed in 5 release cycles. Please migrate to ZIndustrialSensor instead.")]
    [ToolboxItem(false)]
    public class IndustrialSensor : ZIndustrialSensor
    {
    }

    #endregion
}
