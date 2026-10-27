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
    public enum CylinderState
    {
        Retracted,
        Extended,
        Moving,
        Fault
    }

    /// <summary>
    /// Industrial double-acting pneumatic cylinder component with animated piston extension,
    /// dual end-of-stroke magnetic reed switch sensors, and telemetry position synchronization.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial pneumatic cylinder with animated piston rod and magnetic limit sensors")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroPneumaticCylinder.bmp")]
    public class ZPneumaticCylinder : ControlBase, IScadaBindable, IAnimationFrameListener
    {
        private CylinderState _state = CylinderState.Extended;
        private double _extensionPercent = 100.0; // 0 = fully retracted, 100 = fully extended
        private double _targetExtension = 100.0;
        private string _tagLabel = "CYL-501";
        private bool _isHovered;
        private IDisposable? _clockToken;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Process Dynamics")]
        [DefaultValue(CylinderState.Extended)]
        public CylinderState State
        {
            get => _state;
            set { _state = value; Invalidate(); }
        }

        [Category("Process Dynamics")]
        [DefaultValue(100.0)]
        public double ExtensionPercent
        {
            get => _extensionPercent;
            set
            {
                _extensionPercent = Math.Max(0.0, Math.Min(100.0, value));
                _targetExtension = _extensionPercent;
                UpdateState();
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("CYL-501")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZPneumaticCylinder()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(200, 80);
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
            if (Math.Abs(_extensionPercent - _targetExtension) > 0.5)
            {
                double step = 80.0 * deltaSeconds; // 80% per second
                if (_extensionPercent < _targetExtension)
                {
                    _extensionPercent = Math.Min(_targetExtension, _extensionPercent + step);
                }
                else
                {
                    _extensionPercent = Math.Max(_targetExtension, _extensionPercent - step);
                }
                UpdateState();
                if (IsHandleCreated && Visible)
                {
                    Invalidate();
                }
            }
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (double.TryParse(tag.Value?.ToString(), out var pos))
            {
                _targetExtension = Math.Max(0.0, Math.Min(100.0, pos));
            }
        }

        private void UpdateState()
        {
            if (Math.Abs(_extensionPercent - _targetExtension) > 1.0)
            {
                _state = CylinderState.Moving;
            }
            else if (_extensionPercent <= 2.0)
            {
                _state = CylinderState.Retracted;
            }
            else if (_extensionPercent >= 98.0)
            {
                _state = CylinderState.Extended;
            }
            else
            {
                _state = CylinderState.Moving;
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
            Color barrelColor, rodColor, sensorOnColor, sensorOffColor, textColor;

            barrelColor = isDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(226, 232, 240);
            rodColor = isDark ? Color.FromArgb(203, 213, 225) : Color.FromArgb(148, 163, 184);
            sensorOnColor = Color.FromArgb(34, 197, 94); // Green
            sensorOffColor = isDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(203, 213, 225);
            textColor = isDark ? Color.FromArgb(248, 250, 252) : Color.FromArgb(15, 23, 42);

            // 1. Tag Header
            var fontTag = ZeroFontCache.Get(Font.FontFamily.Name, 8.5f, FontStyle.Bold);
            using (var textBrush = new SolidBrush(textColor))
            {
                g.DrawString(_tagLabel, fontTag, textBrush, 8f, 4f);

                string stateText = _state.ToString().ToUpperInvariant();
                var stateSize = g.MeasureString(stateText, fontTag);
                using (var stateBrush = new SolidBrush(_state == CylinderState.Fault ? Color.FromArgb(239, 68, 68) : sensorOnColor))
                {
                    g.DrawString(stateText, fontTag, stateBrush, Width - stateSize.Width - 8f, 4f);
                }
            }

            // 2. Geometry: Cylinder Barrel
            float barrelLeft = 8f;
            float barrelTop = 24f;
            float barrelWidth = Width * 0.55f;
            float barrelHeight = 32f;
            var barrelRect = new RectangleF(barrelLeft, barrelTop, barrelWidth, barrelHeight);

            // 3. Piston Rod (Translates horizontally with extension)
            float maxStroke = Width - barrelRect.Right - 24f;
            float currentStroke = (float)(maxStroke * (_extensionPercent / 100.0));
            float rodTop = barrelTop + barrelHeight * 0.5f - 5f;
            float rodHeight = 10f;
            var rodRect = new RectangleF(barrelRect.Right - 4f, rodTop, currentStroke + 8f, rodHeight);

            // Draw Rod
            using (var rodBrush = new SolidBrush(rodColor))
            using (var rodPen = new Pen(isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(71, 85, 105), 1f))
            {
                g.FillRectangle(rodBrush, rodRect);
                g.DrawRectangle(rodPen, rodRect.X, rodRect.Y, rodRect.Width, rodRect.Height);

                // Rod Clevis / Eye End
                var clevisRect = new RectangleF(rodRect.Right, rodTop - 3f, 14f, rodHeight + 6f);
                g.FillEllipse(rodBrush, clevisRect);
                g.DrawEllipse(rodPen, clevisRect);
                g.FillEllipse(Brushes.Black, clevisRect.X + 4f, clevisRect.Y + 4f, 6f, 6f);
            }

            // Draw Barrel
            using (var barrelBrush = new LinearGradientBrush(barrelRect, barrelColor,
                isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(148, 163, 184), LinearGradientMode.Vertical))
            using (var barrelPen = new Pen(_isHovered ? Color.FromArgb(59, 130, 246) : Color.FromArgb(100, 116, 139), _isHovered ? 2f : 1.2f))
            {
                g.FillRectangle(barrelBrush, barrelRect);
                g.DrawRectangle(barrelPen, barrelRect.X, barrelRect.Y, barrelRect.Width, barrelRect.Height);
            }

            // 4. End-of-Stroke Magnetic Reed Sensors (Top rail)
            bool isRetractedSensor = _extensionPercent <= 5.0;
            bool isExtendedSensor = _extensionPercent >= 95.0;

            // Retract Sensor (Left)
            using (var s1Brush = new SolidBrush(isRetractedSensor ? sensorOnColor : sensorOffColor))
            {
                g.FillRectangle(s1Brush, barrelLeft + 6f, barrelTop - 4f, 10f, 4f);
            }

            // Extend Sensor (Right)
            using (var s2Brush = new SolidBrush(isExtendedSensor ? sensorOnColor : sensorOffColor))
            {
                g.FillRectangle(s2Brush, barrelRect.Right - 16f, barrelTop - 4f, 10f, 4f);
            }

            // 5. Position Readout (%)
            var dataFont = ZeroFontCache.Get(Font.FontFamily.Name, 8f, FontStyle.Regular);
            using (var valBrush = new SolidBrush(textColor))
            {
                string info = $"Stroke: {_extensionPercent:0}%";
                g.DrawString(info, dataFont, valBrush, 8f, Height - 18f);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZPneumaticCylinder"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroPneumaticCylinder is deprecated and will be removed in 5 release cycles. Please migrate to ZPneumaticCylinder instead.")]
    [ToolboxItem(false)]
    public class ZeroPneumaticCylinder : ZPneumaticCylinder
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPneumaticCylinder"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PneumaticCylinder is deprecated and will be removed in 5 release cycles. Please migrate to ZPneumaticCylinder instead.")]
    [ToolboxItem(false)]
    public class PneumaticCylinder : ZPneumaticCylinder
    {
    }

    #endregion
}
