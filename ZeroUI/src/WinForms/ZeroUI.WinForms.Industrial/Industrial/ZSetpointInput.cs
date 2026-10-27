using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial setpoint input box with engineering range limits, touch keypad launcher,
    /// validation boundaries, and direct SCADA tag write-back.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial setpoint input control with on-screen numeric keypad support")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSetpointInput.bmp")]
    public class ZSetpointInput : ControlBase, IScadaBindable
    {
        private double _setpointValue = 50.0;
        private double _minValue = 0.0;
        private double _maxValue = 100.0;
        private string _unit = "°C";
        private string _tagLabel = "TEMP SP";
        private bool _isHovered;

        private int _baseWidth = 150;
        private int _baseHeight = 60;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        public event EventHandler<double>? SetpointChanged;

        [Category("Setpoint Value")]
        [DefaultValue(50.0)]
        public double SetpointValue
        {
            get => _setpointValue;
            set
            {
                double clamped = Math.Max(_minValue, Math.Min(_maxValue, value));
                if (Math.Abs(_setpointValue - clamped) > 0.0001)
                {
                    _setpointValue = clamped;
                    Invalidate();
                    SetpointChanged?.Invoke(this, _setpointValue);
                }
            }
        }

        [Category("Limits")]
        [DefaultValue(0.0)]
        public double MinValue
        {
            get => _minValue;
            set { _minValue = value; Invalidate(); }
        }

        [Category("Limits")]
        [DefaultValue(100.0)]
        public double MaxValue
        {
            get => _maxValue;
            set { _maxValue = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("°C")]
        public string Unit
        {
            get => _unit;
            set { _unit = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("TEMP SP")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        public ZSetpointInput()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(150, 60);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
            Cursor = Cursors.Hand;
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
            if (double.TryParse(tag.Value?.ToString(), out var val))
            {
                SetpointValue = val;
            }
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);

            using (var keypad = new ZNumericKeypad(_tagLabel, _setpointValue, _minValue, _maxValue, _unit))
            {
                if (keypad.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    SetpointValue = keypad.ResultValue;
                    if (!string.IsNullOrEmpty(BoundTagPath))
                    {
                        ZeroTagEngine.SetTagValue(BoundTagPath!, _setpointValue);
                    }
                }
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        protected override void OnApplyDpiScaling(float scaleFactor, float factorRatio)
        {
            base.OnApplyDpiScaling(scaleFactor, factorRatio);
            Width = (int)Math.Round(_baseWidth * scaleFactor);
            Height = (int)Math.Round(_baseHeight * scaleFactor);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            bool isDark = ZeroTheme.IsDark;
            Color panelBg = isDark ? Color.FromArgb(15, 23, 42) : Color.White;
            Color borderCol = _isHovered ? Color.FromArgb(59, 130, 246) : (isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225));
            Color labelCol = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            Color valCol = isDark ? Color.FromArgb(245, 158, 11) : Color.FromArgb(217, 119, 6); // Amber

            float factor = DpiScale;
            float penWidth = (_isHovered ? 2f : 1.2f) * factor;
            float pad = penWidth;

            // 1. Outer Box
            var rect = new RectangleF(pad, pad, Width - pad * 2f, Height - pad * 2f);
            using (var bgBrush = new SolidBrush(panelBg))
            using (var borderPen = new Pen(borderCol, penWidth))
            {
                g.FillRectangle(bgBrush, rect);
                g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
            }

            // 2. Tag Label (Top Left)
            using (var fontTag = new Font(Font.FontFamily, 7.5f * factor, FontStyle.Bold))
            using (var brushTag = new SolidBrush(labelCol))
            {
                g.DrawString(_tagLabel, fontTag, brushTag, 6f * factor, 4f * factor);

                // Range descriptor (Top Right)
                string rangeStr = $"[{_minValue:0.#} - {_maxValue:0.#}]";
                var rangeSize = g.MeasureString(rangeStr, fontTag);
                g.DrawString(rangeStr, fontTag, brushTag, Width - rangeSize.Width - 6f * factor, 4f * factor);
            }

            // 3. Setpoint Value
            string valStr = $"{_setpointValue:0.##}";
            using (var valFont = new Font("Segoe UI", 15f * factor, FontStyle.Bold))
            using (var brushVal = new SolidBrush(valCol))
            {
                g.DrawString(valStr, valFont, brushVal, 6f * factor, 20f * factor);

                // Unit
                var valSize = g.MeasureString(valStr, valFont);
                using (var unitFont = new Font(Font.FontFamily, 8.5f * factor, FontStyle.Regular))
                using (var brushUnit = new SolidBrush(labelCol))
                {
                    g.DrawString(_unit, unitFont, brushUnit, 8f * factor + valSize.Width, 27f * factor);
                }
            }

            // 4. Edit pencil hint icon (Bottom Right)
            using (var iconFont = new Font(Font.FontFamily, 7.5f * factor, FontStyle.Regular))
            using (var iconBrush = new SolidBrush(labelCol))
            {
                var tapSize = g.MeasureString("✎ TAP", iconFont);
                g.DrawString("✎ TAP", iconFont, iconBrush, Width - tapSize.Width - 6f * factor, Height - tapSize.Height - 4f * factor);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZSetpointInput"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroSetpointInput is deprecated and will be removed in 5 release cycles. Please migrate to ZSetpointInput instead.")]
    [ToolboxItem(false)]
    public class ZeroSetpointInput : ZSetpointInput
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSetpointInput"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SetpointInput is deprecated and will be removed in 5 release cycles. Please migrate to ZSetpointInput instead.")]
    [ToolboxItem(false)]
    public class SetpointInput : ZSetpointInput
    {
    }

    #endregion
}
