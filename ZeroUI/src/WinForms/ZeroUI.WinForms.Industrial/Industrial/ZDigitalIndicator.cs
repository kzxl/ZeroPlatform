using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// High-contrast industrial digital telemetry readout panel with configurable engineering units,
    /// 4-tier alarm threshold color transitions (LowLow, Low, High, HighHigh), dynamic High-DPI scaling, and direct SCADA tag binding.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Industrial digital telemetry readout indicator with alarm threshold coloring")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroDigitalIndicator.bmp")]
    public class ZDigitalIndicator : ControlBase, IScadaBindable
    {
        private double _value = 48.7;
        private string _unit = "bar";
        private string _tagLabel = "PT-101";
        private string _format = "0.0";
        private double _lowLowAlarm = 10.0;
        private double _lowWarning = 20.0;
        private double _highWarning = 80.0;
        private double _highHighAlarm = 90.0;
        private bool _isHovered;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Telemetry Value")]
        [DefaultValue(48.7)]
        public double Value
        {
            get => _value;
            set { _value = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("bar")]
        public string Unit
        {
            get => _unit;
            set { _unit = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("PT-101")]
        public string TagLabel
        {
            get => _tagLabel;
            set { _tagLabel = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("0.0")]
        public string Format
        {
            get => _format;
            set { _format = value ?? "0.0"; Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(10.0)]
        public double LowLowAlarm
        {
            get => _lowLowAlarm;
            set { _lowLowAlarm = value; Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(20.0)]
        public double LowWarning
        {
            get => _lowWarning;
            set { _lowWarning = value; Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(80.0)]
        public double HighWarning
        {
            get => _highWarning;
            set { _highWarning = value; Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(90.0)]
        public double HighHighAlarm
        {
            get => _highHighAlarm;
            set { _highHighAlarm = value; Invalidate(); }
        }

        public ZDigitalIndicator()
        {
            Size = new Size(130, 56);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag != null)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => OnTagValueChanged(tag)));
                    return;
                }
                Value = tag.GetValue<double>();
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _isHovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _isHovered = false; Invalidate(); }

        private string FormatValue(double val)
        {
            string fmt = _format;
            if (string.IsNullOrWhiteSpace(fmt)) return val.ToString("0.0", CultureInfo.InvariantCulture);

            try
            {
                if (fmt.Contains("{0"))
                {
                    return string.Format(CultureInfo.InvariantCulture, fmt, val);
                }
                return val.ToString(fmt, CultureInfo.InvariantCulture);
            }
            catch
            {
                return val.ToString("0.0", CultureInfo.InvariantCulture);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = DpiScale;
            bool isDark = ZeroTheme.IsDark;
            Color panelBg = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
            Color borderColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
            Color labelColor = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);

            // Determine value color by alarm tiers
            Color valueColor;
            if (_value <= _lowLowAlarm || _value >= _highHighAlarm)
            {
                valueColor = Color.FromArgb(239, 68, 68); // Critical Red
                borderColor = valueColor;
            }
            else if (_value <= _lowWarning || _value >= _highWarning)
            {
                valueColor = Color.FromArgb(245, 158, 11); // Warning Amber
                borderColor = valueColor;
            }
            else
            {
                valueColor = isDark ? Color.FromArgb(56, 189, 248) : Color.FromArgb(2, 132, 199); // Normal Cyan/Blue
            }

            if (_isHovered)
            {
                borderColor = Color.FromArgb(59, 130, 246);
            }

            // 1. Industrial Beveled Panel
            var panelRect = new RectangleF(1f * scale, 1f * scale, Width - 3f * scale, Height - 3f * scale);
            using (var bgBrush = new SolidBrush(panelBg))
            using (var borderPen = new Pen(borderColor, _isHovered ? 2f * scale : 1.2f * scale))
            {
                g.FillRectangle(bgBrush, panelRect);
                g.DrawRectangle(borderPen, panelRect.X, panelRect.Y, panelRect.Width, panelRect.Height);
            }

            // 2. Tag Label (Top left)
            using (var labelFont = new Font(Font.FontFamily, 7.5f, FontStyle.Bold))
            using (var labelBrush = new SolidBrush(labelColor))
            {
                g.DrawString(_tagLabel, labelFont, labelBrush, 6f * scale, 5f * scale);
            }

            // 3. Digital Readout Value (Center-Left large numbers)
            string valText = FormatValue(_value);
            using (var numFont = new Font("Segoe UI", 16f, FontStyle.Bold))
            using (var numBrush = new SolidBrush(valueColor))
            {
                g.DrawString(valText, numFont, numBrush, 5f * scale, 20f * scale);

                // 4. Engineering Unit (Placed next to value)
                var valSize = g.MeasureString(valText, numFont);
                using (var unitFont = new Font(Font.FontFamily, 8.5f, FontStyle.Regular))
                using (var unitBrush = new SolidBrush(labelColor))
                {
                    g.DrawString(_unit, unitFont, unitBrush, (7f * scale) + valSize.Width, 29f * scale);
                }
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDigitalIndicator"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroDigitalIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZDigitalIndicator instead.")]
    [ToolboxItem(false)]
    public class ZeroDigitalIndicator : ZDigitalIndicator
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDigitalIndicator"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DigitalIndicator is deprecated and will be removed in 5 release cycles. Please migrate to ZDigitalIndicator instead.")]
    [ToolboxItem(false)]
    public class DigitalIndicator : ZDigitalIndicator
    {
    }

    #endregion
}
