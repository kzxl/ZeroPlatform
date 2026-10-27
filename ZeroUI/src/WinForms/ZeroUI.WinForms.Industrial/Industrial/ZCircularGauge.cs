using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using ZeroUI.WinForms.Base;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Modern anti-aliased circular gauge/meter for OEE, Yield rate, and equipment efficiency
    /// with Per-Monitor V2 High-DPI scaling and configurable readout formatting.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroGauge.bmp")]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("Value")]
    [Description("Circular gauge meter for OEE, Yield, and equipment efficiency")]
    public class ZCircularGauge : ControlBase
    {
        private float _value = 85f; // 0 to 100
        private string _title = "OEE Rate";
        private string _suffix = "%";
        private string _valueFormat = "0.0";
        private int _thickness = 8;
        private Color _gaugeColor = Color.FromArgb(16, 185, 129);  // Emerald
        private Color _trackColor = Color.FromArgb(229, 231, 235); // Gray track

        public ZCircularGauge()
        {
            Size = new Size(110, 110);
            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f);
        }

        [Category("Data")]
        [DefaultValue(85f)]
        public float Value
        {
            get => _value;
            set
            {
                _value = Math.Max(0f, Math.Min(100f, value));
                Invalidate();
            }
        }

        [Category("Data")]
        [DefaultValue("OEE Rate")]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Category("Data")]
        [DefaultValue("%")]
        public string Suffix
        {
            get => _suffix;
            set { _suffix = value; Invalidate(); }
        }

        [Category("Appearance")]
        [Description("Custom format string for the center readout value. Defaults to '0.0'.")]
        [DefaultValue("0.0")]
        public string ValueFormat
        {
            get => _valueFormat;
            set { _valueFormat = value ?? "0.0"; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(8)]
        public int Thickness
        {
            get => _thickness;
            set { _thickness = Math.Max(4, value); Invalidate(); }
        }

        [Category("Appearance")]
        public Color GaugeColor
        {
            get => _gaugeColor;
            set { _gaugeColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color TrackColor
        {
            get => _trackColor;
            set { _trackColor = value; Invalidate(); }
        }

        private string FormatValue(float val)
        {
            string fmt = _valueFormat;
            if (string.IsNullOrWhiteSpace(fmt)) return val.ToString("0.0", CultureInfo.InvariantCulture) + _suffix;

            try
            {
                if (fmt.Contains("{0"))
                {
                    return string.Format(CultureInfo.InvariantCulture, fmt, val) + _suffix;
                }
                return val.ToString(fmt, CultureInfo.InvariantCulture) + _suffix;
            }
            catch
            {
                return val.ToString("0.0", CultureInfo.InvariantCulture) + _suffix;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float scale = DpiScale;
            float effThickness = Math.Max(2f, _thickness * scale);
            float pad = effThickness + 2f * scale;
            float size = Math.Min(Width, Height) - (pad * 2f);
            if (size <= 0) return;

            float x = (Width - size) / 2f;
            float y = (Height - size) / 2f - (4f * scale);
            var arcRect = new RectangleF(x, y, size, size);

            // 1. Draw Background Track Ring (270 degrees arch)
            float startAngle = 135f;
            float sweepLength = 270f;
            Color effTrackColor = _trackColor != Color.FromArgb(229, 231, 235) ? _trackColor : CurrentPalette.Border;

            using (var trackPen = new Pen(effTrackColor, effThickness))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawArc(trackPen, arcRect, startAngle, sweepLength);
            }

            // 2. Draw Active Progress Arc
            float activeSweep = (_value / 100f) * sweepLength;
            if (activeSweep > 0.5f)
            {
                using var gaugePen = new Pen(_gaugeColor, effThickness);
                gaugePen.StartCap = LineCap.Round;
                gaugePen.EndCap = LineCap.Round;
                g.DrawArc(gaugePen, arcRect, startAngle, activeSweep);
            }

            // 3. Draw Center Value & Title
            string valText = FormatValue(_value);
            using var valFont = new Font("Segoe UI", 12.5f * scale, FontStyle.Bold);
            Size valSize = TextRenderer.MeasureText(g, valText, valFont);
            Rectangle valRect = new Rectangle(0, (int)(y + (size / 2f) - valSize.Height / 2f - (4f * scale)), Width, valSize.Height);
            TextRenderer.DrawText(g, valText, valFont, valRect, CurrentPalette.TextPrimary, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            if (!string.IsNullOrEmpty(_title))
            {
                using var titleFont = new Font("Segoe UI", 7.5f * scale, FontStyle.Regular);
                Rectangle titleRect = new Rectangle(0, valRect.Bottom - (int)(2f * scale), Width, (int)(16f * scale));
                TextRenderer.DrawText(g, _title, titleFont, titleRect, CurrentPalette.TextSecondary, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZCircularGauge"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroGauge is deprecated. Please use CircularGauge instead.")]
    [ToolboxItem(false)]
    public class ZeroGauge : CircularGauge
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZCircularGauge"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroCircularGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZCircularGauge instead.")]
    [ToolboxItem(false)]
    public class ZeroCircularGauge : ZCircularGauge
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZCircularGauge"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CircularGauge is deprecated and will be removed in 5 release cycles. Please migrate to ZCircularGauge instead.")]
    [ToolboxItem(false)]
    public class CircularGauge : ZCircularGauge
    {
    }

    #endregion
}
