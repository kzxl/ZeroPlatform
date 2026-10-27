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
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Industrial
{
    /// <summary>
    /// Industrial high-density edgewise profile panel meter for control rooms and SCADA racks.
    /// Features calibrated tick graduations, color-coded alarm zones (ISA-18.2 / ISA-101),
    /// multi-style pointers (Flag, Bar, Line), and dual vertical/horizontal orientation.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroLinearGauge.bmp")]
    [Category("ZeroUI - SCADA")]
    public class ZEdgewiseMeter : ControlBase, IScadaBindable
    {
        private double _value = 65.4;
        private double _minimum = 0.0;
        private double _maximum = 100.0;
        private EdgewiseOrientation _orientation = EdgewiseOrientation.Vertical;
        private EdgewisePointerStyle _pointerStyle = EdgewisePointerStyle.Flag;
        private string _title = "PRESSURE";
        private string _unit = "bar";
        private int _decimalPlaces = 1;
        private double _highAlarmThreshold = 85.0;
        private double _lowAlarmThreshold = 15.0;
        private double _cautionThreshold = 75.0;
        private bool _showAlarmZones = true;
        private Color _barColor = Color.FromArgb(0, 190, 255);

        public ZEdgewiseMeter()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            Size = new Size(72, 240);
        }

        #region Properties

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Process Value")]
        [DefaultValue(65.4)]
        public double Value
        {
            get => _value;
            set
            {
                double clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (Math.Abs(_value - clamped) > 0.0001)
                {
                    _value = clamped;
                    Invalidate();
                }
            }
        }

        [Category("Scale Limits")]
        [DefaultValue(0.0)]
        public double Minimum
        {
            get => _minimum;
            set
            {
                _minimum = value;
                if (_maximum <= _minimum) _maximum = _minimum + 1.0;
                Value = _value;
                Invalidate();
            }
        }

        [Category("Scale Limits")]
        [DefaultValue(100.0)]
        public double Maximum
        {
            get => _maximum;
            set
            {
                _maximum = value;
                if (_maximum <= _minimum) _maximum = _minimum + 1.0;
                Value = _value;
                Invalidate();
            }
        }

        [Category("Layout & Style")]
        [DefaultValue(EdgewiseOrientation.Vertical)]
        public EdgewiseOrientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    // Swap default dimensions when orientation flips
                    Size = new Size(Height, Width);
                    Invalidate();
                }
            }
        }

        [Category("Layout & Style")]
        [DefaultValue(EdgewisePointerStyle.Flag)]
        public EdgewisePointerStyle PointerStyle
        {
            get => _pointerStyle;
            set { _pointerStyle = value; Invalidate(); }
        }

        [Category("Telemetry Header")]
        [DefaultValue("PRESSURE")]
        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; Invalidate(); }
        }

        [Category("Telemetry Header")]
        [DefaultValue("bar")]
        public string Unit
        {
            get => _unit;
            set { _unit = value ?? string.Empty; Invalidate(); }
        }

        [Category("Telemetry Header")]
        [DefaultValue(1)]
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set { _decimalPlaces = Math.Max(0, Math.Min(4, value)); Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(85.0)]
        public double HighAlarmThreshold
        {
            get => _highAlarmThreshold;
            set { _highAlarmThreshold = value; Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(15.0)]
        public double LowAlarmThreshold
        {
            get => _lowAlarmThreshold;
            set { _lowAlarmThreshold = value; Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(75.0)]
        public double CautionThreshold
        {
            get => _cautionThreshold;
            set { _cautionThreshold = value; Invalidate(); }
        }

        [Category("Alarm Limits")]
        [DefaultValue(true)]
        public bool ShowAlarmZones
        {
            get => _showAlarmZones;
            set { _showAlarmZones = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BarColor
        {
            get => _barColor;
            set { _barColor = value; Invalidate(); }
        }

        #endregion

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag?.Value == null) return;
            if (double.TryParse(tag.Value.ToString(), out var v))
            {
                Value = v;
            }
        }

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            int w = Width;
            int h = Height;
            if (w < 20 || h < 20) return;

            // 1. Meter Bezel Frame
            var meterRect = new RectangleF(1, 1, w - 2, h - 2);
            DrawBezel(g, meterRect);

            // 2. Inner Dial Plate
            var innerRect = new RectangleF(meterRect.X + 4, meterRect.Y + 4, meterRect.Width - 8, meterRect.Height - 8);
            using (var dialBrush = new SolidBrush(Color.FromArgb(18, 20, 24)))
            {
                g.FillRectangle(dialBrush, innerRect);
            }

            if (_orientation == EdgewiseOrientation.Vertical)
            {
                DrawVerticalMeter(g, innerRect);
            }
            else
            {
                DrawHorizontalMeter(g, innerRect);
            }
        }

        private void DrawBezel(Graphics g, RectangleF rect)
        {
            using (var brush = new LinearGradientBrush(rect, Color.FromArgb(70, 74, 80), Color.FromArgb(28, 30, 34), 90f))
            {
                g.FillRectangle(brush, rect);
            }
            using (var pen = new Pen(Color.FromArgb(90, 95, 102), 1.5f))
            {
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        private void DrawVerticalMeter(Graphics g, RectangleF bounds)
        {
            // Header: Title and Digital Value
            float headerHeight = 38f;
            var headerRect = new RectangleF(bounds.X, bounds.Y + 2f, bounds.Width, headerHeight);

            var titleFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            var valueFont = ZeroFontCache.Get("Consolas", 9.5f, FontStyle.Bold);
            var unitFont = ZeroFontCache.Get("Segoe UI", 7f, FontStyle.Regular);

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var titleBrush = new SolidBrush(Color.FromArgb(170, 175, 185)))
            {
                g.DrawString(_title, titleFont, titleBrush, new RectangleF(headerRect.X, headerRect.Y, headerRect.Width, 14f), sf);
            }

            // Digital value readout
            string valText = _value.ToString($"F{_decimalPlaces}");
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                Color valColor = _value >= _highAlarmThreshold ? Color.FromArgb(255, 60, 60) :
                                 _value <= _lowAlarmThreshold ? Color.FromArgb(255, 180, 40) :
                                 Color.FromArgb(0, 230, 255);

                using (var valBrush = new SolidBrush(valColor))
                {
                    g.DrawString(valText, valueFont, valBrush, new RectangleF(headerRect.X, headerRect.Y + 14f, headerRect.Width, 14f), sf);
                }
            }

            // Scale track region
            float trackTop = bounds.Y + headerHeight + 6f;
            float trackBottom = bounds.Bottom - 18f;
            float trackHeight = trackBottom - trackTop;
            float trackX = bounds.X + bounds.Width * 0.38f;
            float trackWidth = 8f;

            if (trackHeight <= 20) return;

            // Alarm Zones strip
            if (_showAlarmZones)
            {
                DrawVerticalAlarmZones(g, trackX, trackTop, trackWidth, trackHeight);
            }

            // Scale Track background
            var trackRect = new RectangleF(trackX, trackTop, trackWidth, trackHeight);
            using (var trackBg = new SolidBrush(Color.FromArgb(32, 36, 42)))
            {
                g.FillRectangle(trackBg, trackRect);
            }

            // Bargraph fill if PointerStyle == Bar
            double norm = (_value - _minimum) / (_maximum - _minimum);
            norm = Math.Max(0.0, Math.Min(1.0, norm));
            float barFillHeight = (float)(trackHeight * norm);

            if (_pointerStyle == EdgewisePointerStyle.Bar)
            {
                var barRect = new RectangleF(trackX, trackBottom - barFillHeight, trackWidth, barFillHeight);
                Color activeBarColor = _value >= _highAlarmThreshold ? Color.FromArgb(240, 40, 45) : _barColor;
                using (var brush = new LinearGradientBrush(barRect, activeBarColor, Color.FromArgb(10, 120, 200), 90f))
                {
                    g.FillRectangle(brush, barRect);
                }
            }

            // Graduation Ticks & Labels on Left Side
            DrawVerticalTicks(g, bounds.X + 2f, trackX - 2f, trackTop, trackBottom, trackHeight);

            // Pointer (Flag or Line)
            float pointerY = trackBottom - barFillHeight;
            if (_pointerStyle == EdgewisePointerStyle.Flag)
            {
                float flagWidth = bounds.Width - (trackX + trackWidth) - 4f;
                PointF[] flagPts = new[]
                {
                    new PointF(trackX + trackWidth + 1f, pointerY),
                    new PointF(trackX + trackWidth + 7f, pointerY - 5f),
                    new PointF(trackX + trackWidth + flagWidth, pointerY - 5f),
                    new PointF(trackX + trackWidth + flagWidth, pointerY + 5f),
                    new PointF(trackX + trackWidth + 7f, pointerY + 5f)
                };

                using (var flagBrush = new LinearGradientBrush(new RectangleF(trackX, pointerY - 5f, flagWidth + 8f, 10f),
                    Color.FromArgb(255, 230, 40), Color.FromArgb(200, 160, 0), 45f))
                {
                    g.FillPolygon(flagBrush, flagPts);
                }
                using (var flagPen = new Pen(Color.FromArgb(50, 40, 0), 1f))
                {
                    g.DrawPolygon(flagPen, flagPts);
                }
            }
            else if (_pointerStyle == EdgewisePointerStyle.Line)
            {
                using (var linePen = new Pen(Color.FromArgb(255, 60, 60), 2f))
                {
                    g.DrawLine(linePen, bounds.X + 4f, pointerY, bounds.Right - 4f, pointerY);
                }
            }

            // Bottom Unit caption
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var unitBrush = new SolidBrush(Color.FromArgb(140, 145, 155)))
            {
                g.DrawString(_unit, unitFont, unitBrush, new RectangleF(bounds.X, trackBottom + 2f, bounds.Width, 14f), sf);
            }
        }

        private void DrawVerticalAlarmZones(Graphics g, float trackX, float trackTop, float trackWidth, float trackHeight)
        {
            float zoneX = trackX - 3f;
            float zoneW = 3f;

            // Low alarm zone
            if (_lowAlarmThreshold > _minimum)
            {
                float loFrac = (float)((_lowAlarmThreshold - _minimum) / (_maximum - _minimum));
                float loH = trackHeight * loFrac;
                var loRect = new RectangleF(zoneX, trackTop + trackHeight - loH, zoneW, loH);
                using (var loBrush = new SolidBrush(Color.FromArgb(240, 160, 20)))
                {
                    g.FillRectangle(loBrush, loRect);
                }
            }

            // Normal zone
            using (var normalBrush = new SolidBrush(Color.FromArgb(40, 180, 70)))
            {
                g.FillRectangle(normalBrush, zoneX, trackTop, zoneW, trackHeight);
            }

            // Caution zone
            if (_cautionThreshold < _maximum)
            {
                float cautFrac = (float)((_maximum - _cautionThreshold) / (_maximum - _minimum));
                float cautH = trackHeight * cautFrac;
                var cautRect = new RectangleF(zoneX, trackTop, zoneW, cautH);
                using (var cautBrush = new SolidBrush(Color.FromArgb(240, 175, 20)))
                {
                    g.FillRectangle(cautBrush, cautRect);
                }
            }

            // High alarm zone
            if (_highAlarmThreshold < _maximum)
            {
                float hiFrac = (float)((_maximum - _highAlarmThreshold) / (_maximum - _minimum));
                float hiH = trackHeight * hiFrac;
                var hiRect = new RectangleF(zoneX, trackTop, zoneW, hiH);
                using (var hiBrush = new SolidBrush(Color.FromArgb(235, 40, 45)))
                {
                    g.FillRectangle(hiBrush, hiRect);
                }
            }
        }

        private void DrawVerticalTicks(Graphics g, float leftX, float rightX, float topY, float bottomY, float height)
        {
            var tickFont = ZeroFontCache.Get("Segoe UI", 6.5f, FontStyle.Regular);
            using (var tickPen = new Pen(Color.FromArgb(140, 145, 155), 1f))
            using (var textBrush = new SolidBrush(Color.FromArgb(160, 165, 175)))
            using (var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center })
            {
                int majorSteps = 4;
                for (int i = 0; i <= majorSteps; i++)
                {
                    float y = bottomY - (height * i / majorSteps);
                    g.DrawLine(tickPen, rightX - 5f, y, rightX, y);

                    double val = _minimum + (_maximum - _minimum) * i / majorSteps;
                    string lbl = val.ToString("G3");
                    g.DrawString(lbl, tickFont, textBrush, new RectangleF(leftX, y - 6f, rightX - 7f - leftX, 12f), sf);

                    // Minor ticks
                    if (i < majorSteps)
                    {
                        float midY = y - (height / (majorSteps * 2));
                        g.DrawLine(tickPen, rightX - 2.5f, midY, rightX, midY);
                    }
                }
            }
        }

        private void DrawHorizontalMeter(Graphics g, RectangleF bounds)
        {
            // Left block: Title & value
            float labelW = Math.Max(60f, bounds.Width * 0.28f);
            var titleFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            var valFont = ZeroFontCache.Get("Consolas", 9f, FontStyle.Bold);

            using (var titleBrush = new SolidBrush(Color.FromArgb(170, 175, 185)))
            {
                g.DrawString(_title, titleFont, titleBrush, bounds.X + 2f, bounds.Y + 2f);
            }

            string valText = $"{_value.ToString($"F{_decimalPlaces}")} {_unit}";
            using (var valBrush = new SolidBrush(Color.FromArgb(0, 230, 255)))
            {
                g.DrawString(valText, valFont, valBrush, bounds.X + 2f, bounds.Y + 16f);
            }

            // Track horizontal
            float trackLeft = bounds.X + labelW + 4f;
            float trackRight = bounds.Right - 8f;
            float trackW = trackRight - trackLeft;
            float trackY = bounds.Y + bounds.Height * 0.45f;
            float trackH = 8f;

            if (trackW <= 10) return;

            // Track background
            using (var bgBrush = new SolidBrush(Color.FromArgb(32, 36, 42)))
            {
                g.FillRectangle(bgBrush, trackLeft, trackY, trackW, trackH);
            }

            double norm = (_value - _minimum) / (_maximum - _minimum);
            norm = Math.Max(0.0, Math.Min(1.0, norm));
            float barW = (float)(trackW * norm);

            if (_pointerStyle == EdgewisePointerStyle.Bar)
            {
                var barRect = new RectangleF(trackLeft, trackY, barW, trackH);
                using (var brush = new LinearGradientBrush(barRect, Color.FromArgb(10, 120, 200), _barColor, 0f))
                {
                    g.FillRectangle(brush, barRect);
                }
            }

            // Pointer Flag / Line
            float pointerX = trackLeft + barW;
            if (_pointerStyle == EdgewisePointerStyle.Flag)
            {
                PointF[] flagPts = new[]
                {
                    new PointF(pointerX, trackY + trackH + 1f),
                    new PointF(pointerX - 4f, trackY + trackH + 6f),
                    new PointF(pointerX - 4f, bounds.Bottom - 2f),
                    new PointF(pointerX + 4f, bounds.Bottom - 2f),
                    new PointF(pointerX + 4f, trackY + trackH + 6f)
                };
                using (var flagBrush = new SolidBrush(Color.FromArgb(255, 230, 40)))
                {
                    g.FillPolygon(flagBrush, flagPts);
                }
            }
            else if (_pointerStyle == EdgewisePointerStyle.Line)
            {
                using (var linePen = new Pen(Color.FromArgb(255, 60, 60), 2f))
                {
                    g.DrawLine(linePen, pointerX, bounds.Y + 2f, pointerX, bounds.Bottom - 2f);
                }
            }
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZEdgewiseMeter"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("EdgewiseMeter is deprecated and will be removed in 5 release cycles. Please migrate to ZEdgewiseMeter instead.")]
    [ToolboxItem(false)]
    public class EdgewiseMeter : ZEdgewiseMeter
    {
    }

    #endregion
}
