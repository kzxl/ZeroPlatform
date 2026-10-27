using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Color threshold zone for <see cref="ZGaugeChart"/>.
    /// </summary>
    public class GaugeZone
    {
        public double Start { get; set; }
        public double End { get; set; }
        public Color Color { get; set; }
        public string Label { get; set; } = string.Empty;

        public GaugeZone() { }
        public GaugeZone(double start, double end, Color color, string label = "")
        {
            Start = start;
            End = end;
            Color = color;
            Label = label;
        }
    }

    /// <summary>
    /// Dashboard-style radial gauge chart with needle pointer, color threshold zones,
    /// tick marks, and integrated digital readout.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Radial dashboard gauge chart with needle pointer, threshold zones, and digital readout")]
    public class ZGaugeChart : Control
    {
        private double _value = 0;
        private double _min = 0;
        private double _max = 100;
        private string _unit = string.Empty;
        private string _title = string.Empty;
        private bool _showLabel = true;
        private bool _showTicks = true;
        private List<GaugeZone> _zones = new List<GaugeZone>();

        public event EventHandler? ValueChanged;

        [Category("Data")]
        [DefaultValue(0.0)]
        [Description("Current value displayed by the gauge needle.")]
        public double Value
        {
            get => _value;
            set
            {
                if (Math.Abs(_value - value) > double.Epsilon)
                {
                    _value = Math.Max(_min, Math.Min(_max, value));
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        [Category("Data")]
        [DefaultValue(0.0)]
        [Description("Minimum scale value.")]
        public double Min
        {
            get => _min;
            set
            {
                _min = value;
                if (_value < _min) _value = _min;
                Invalidate();
            }
        }

        [Category("Data")]
        [DefaultValue(100.0)]
        [Description("Maximum scale value.")]
        public double Max
        {
            get => _max;
            set
            {
                _max = Math.Max(_min + 1.0, value);
                if (_value > _max) _value = _max;
                Invalidate();
            }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("Unit label displayed below the value readout (e.g. °C, RPM, bar).")]
        public string Unit
        {
            get => _unit;
            set { _unit = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("")]
        [Description("Title text displayed at the top of the gauge.")]
        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Whether to display min/max labels and digital readout.")]
        public bool ShowLabel
        {
            get => _showLabel;
            set { _showLabel = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Whether to render dial tick marks.")]
        public bool ShowTicks
        {
            get => _showTicks;
            set { _showTicks = value; Invalidate(); }
        }

        [Category("Data")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Description("Color threshold zones along the gauge arc.")]
        public List<GaugeZone> Zones => _zones;

        public ZGaugeChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(200, 200);
            BackColor = Color.Transparent;

            // Default industrial color zones: Normal (green), Warning (amber), Danger (red)
            _zones.Add(new GaugeZone(0, 60, Color.FromArgb(46, 204, 113)));
            _zones.Add(new GaugeZone(60, 85, Color.FromArgb(241, 196, 15)));
            _zones.Add(new GaugeZone(85, 100, Color.FromArgb(231, 76, 60)));

            ZeroTheme.ThemeChanged += (s, e) => Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var colors = ZeroTheme.Colors;
            int diameter = Math.Min(Width, Height) - 24;
            if (diameter <= 20) return;

            int centerX = Width / 2;
            int centerY = Height / 2 + 10;
            int radius = diameter / 2;
            var arcRect = new Rectangle(centerX - radius, centerY - radius, diameter, diameter);

            const float startAngle = 135f;
            const float sweepAngle = 270f;
            const float trackWidth = 14f;

            // 1. Background Arc Track
            using (var trackPen = new Pen(colors.Border, trackWidth))
            {
                trackPen.StartCap = LineCap.Round;
                trackPen.EndCap = LineCap.Round;
                g.DrawArc(trackPen, arcRect, startAngle, sweepAngle);
            }

            // 2. Colored Threshold Zones
            double range = _max - _min;
            if (range > 0 && _zones.Count > 0)
            {
                foreach (var zone in _zones)
                {
                    double zStart = Math.Max(_min, Math.Min(_max, zone.Start));
                    double zEnd = Math.Max(_min, Math.Min(_max, zone.End));
                    if (zEnd <= zStart) continue;

                    float zoneStartAngle = startAngle + (float)((zStart - _min) / range * sweepAngle);
                    float zoneSweep = (float)((zEnd - zStart) / range * sweepAngle);

                    using (var zonePen = new Pen(zone.Color, trackWidth - 4))
                    {
                        g.DrawArc(zonePen, arcRect, zoneStartAngle, zoneSweep);
                    }
                }
            }

            // 3. Dial Ticks
            if (_showTicks)
            {
                using var tickPen = new Pen(colors.TextSecondary, 1.5f);
                for (int i = 0; i <= 10; i++)
                {
                    float angleDeg = startAngle + (i / 10f) * sweepAngle;
                    float angleRad = (float)(angleDeg * Math.PI / 180.0);

                    float innerR = radius - trackWidth - (i % 5 == 0 ? 8 : 4);
                    float outerR = radius - trackWidth;

                    float x1 = centerX + (float)Math.Cos(angleRad) * innerR;
                    float y1 = centerY + (float)Math.Sin(angleRad) * innerR;
                    float x2 = centerX + (float)Math.Cos(angleRad) * outerR;
                    float y2 = centerY + (float)Math.Sin(angleRad) * outerR;

                    g.DrawLine(tickPen, x1, y1, x2, y2);
                }
            }

            // 4. Needle Pointer
            float currentFraction = range > 0 ? (float)((_value - _min) / range) : 0f;
            float needleAngleDeg = startAngle + currentFraction * sweepAngle;
            float needleAngleRad = (float)(needleAngleDeg * Math.PI / 180.0);
            float needleLength = radius - trackWidth - 6;

            float tipX = centerX + (float)Math.Cos(needleAngleRad) * needleLength;
            float tipY = centerY + (float)Math.Sin(needleAngleRad) * needleLength;

            float baseAngleLeft = needleAngleRad - (float)(Math.PI / 2);
            float baseAngleRight = needleAngleRad + (float)(Math.PI / 2);
            const float needleBaseWidth = 5f;

            var needlePoints = new[]
            {
                new PointF(tipX, tipY),
                new PointF(centerX + (float)Math.Cos(baseAngleLeft) * needleBaseWidth, centerY + (float)Math.Sin(baseAngleLeft) * needleBaseWidth),
                new PointF(centerX + (float)Math.Cos(baseAngleRight) * needleBaseWidth, centerY + (float)Math.Sin(baseAngleRight) * needleBaseWidth)
            };

            Color needleColor = colors.PrimaryAccent;
            using (var needleBrush = new SolidBrush(needleColor))
            {
                g.FillPolygon(needleBrush, needlePoints);
            }

            // Center Pivot Cap
            using (var capBrush = new SolidBrush(colors.TextPrimary))
            {
                g.FillEllipse(capBrush, centerX - 8, centerY - 8, 16, 16);
            }
            using (var centerDotBrush = new SolidBrush(colors.Surface))
            {
                g.FillEllipse(centerDotBrush, centerX - 3, centerY - 3, 6, 6);
            }

            // 5. Readout & Labels
            if (_showLabel)
            {
                // Value text
                var valueFont = ZeroFontCache.Get("Segoe UI", 13f, FontStyle.Bold);
                string valueStr = _value.ToString("0.0");
                if (!string.IsNullOrEmpty(_unit)) valueStr += " " + _unit;

                var valueSize = g.MeasureString(valueStr, valueFont);
                using (var textBrush = new SolidBrush(colors.TextPrimary))
                {
                    g.DrawString(valueStr, valueFont, textBrush, centerX - valueSize.Width / 2, centerY + 18);
                }

                // Min / Max labels
                var labelFont = ZeroFontCache.Get("Segoe UI", 8f, FontStyle.Regular);
                using (var labelBrush = new SolidBrush(colors.TextSecondary))
                {
                    string minStr = _min.ToString("0");
                    string maxStr = _max.ToString("0");
                    g.DrawString(minStr, labelFont, labelBrush, centerX - radius + 5, centerY + radius / 2 - 5);
                    var maxSz = g.MeasureString(maxStr, labelFont);
                    g.DrawString(maxStr, labelFont, labelBrush, centerX + radius - maxSz.Width - 5, centerY + radius / 2 - 5);
                }
            }

            // Title
            if (!string.IsNullOrEmpty(_title))
            {
                var titleFont = ZeroFontCache.Get("Segoe UI", 9f, FontStyle.Bold);
                var titleSize = g.MeasureString(_title, titleFont);
                using var titleBrush = new SolidBrush(colors.TextSecondary);
                g.DrawString(_title, titleFont, titleBrush, centerX - titleSize.Width / 2, 6);
            }
        }
    }

    [Obsolete("ZeroGaugeChart is deprecated. Use ZGaugeChart instead.")]
    [ToolboxItem(false)]
    public class ZeroGaugeChart : ZGaugeChart { }
}
