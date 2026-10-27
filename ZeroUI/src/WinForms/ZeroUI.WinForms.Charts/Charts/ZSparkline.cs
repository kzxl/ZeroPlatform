using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.Core.Scada;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Lightweight zero-axis micro trend sparkline control.
    /// Ideal for embedding adjacent to sensor telemetry readouts, KPI cards, and table cells.
    /// Utilizes a fixed circular ring buffer with zero heap allocation during stream updates.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [Description("Compact micro-trend sparkline graph for inline sensor telemetry")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSparkline.bmp")]
    public class ZSparkline : Control, IScadaBindable
    {
        private readonly float[] _buffer;
        private int _head = 0;
        private int _count = 0;
        private Color _lineColor = Color.FromArgb(56, 189, 248); // Sky blue
        private bool _showGradientFill = true;
        private float _minVal = 0f;
        private float _maxVal = 100f;

        [Category("SCADA Telemetry")]
        public string? BoundTagPath { get; set; }

        [Category("Appearance")]
        public Color LineColor
        {
            get => _lineColor;
            set { _lineColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowGradientFill
        {
            get => _showGradientFill;
            set { _showGradientFill = value; Invalidate(); }
        }

        [Category("Scale")]
        [DefaultValue(0f)]
        public float MinValue
        {
            get => _minVal;
            set { _minVal = value; Invalidate(); }
        }

        [Category("Scale")]
        [DefaultValue(100f)]
        public float MaxValue
        {
            get => _maxVal;
            set { _maxVal = Math.Max(value, _minVal + 0.001f); Invalidate(); }
        }

        public ZSparkline() : this(40) { }

        public ZSparkline(int capacity)
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(100, 32);
            BackColor = Color.Transparent;

            _buffer = new float[Math.Max(10, capacity)];
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

        public void AddValue(float value)
        {
            _buffer[_head] = value;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length) _count++;
            Invalidate();
        }

        public void OnTagValueChanged(IScadaTag tag)
        {
            if (tag == null) return;
            if (float.TryParse(tag.Value?.ToString(), out var v))
            {
                AddValue(v);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_count < 2) return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float w = Width - 4f;
            float h = Height - 6f;
            float range = _maxVal - _minVal;
            if (range <= 0.0001f) range = 1f;

            var points = new PointF[_count];
            int start = (_head - _count + _buffer.Length) % _buffer.Length;

            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % _buffer.Length;
                float val = _buffer[idx];
                float normY = Math.Max(0f, Math.Min(1f, (val - _minVal) / range));

                float px = 2f + (i / (float)(_count - 1)) * w;
                float py = 3f + (1f - normY) * h;
                points[i] = new PointF(px, py);
            }

            // 1. Optional Gradient Fill underneath curve
            if (_showGradientFill)
            {
                using (var fillPath = new GraphicsPath())
                {
                    fillPath.AddLines(points);
                    fillPath.AddLine(points[points.Length - 1].X, Height, points[0].X, Height);
                    fillPath.CloseFigure();

                    using (var fillBrush = new LinearGradientBrush(
                        new PointF(0, 0), new PointF(0, Height),
                        Color.FromArgb(60, _lineColor), Color.FromArgb(0, _lineColor)))
                    {
                        g.FillPath(fillBrush, fillPath);
                    }
                }
            }

            // 2. Trend Polyline
            using (var linePen = new Pen(_lineColor, 1.8f) { LineJoin = LineJoin.Round })
            {
                g.DrawLines(linePen, points);
            }

            // 3. Highlight Dot on most recent point
            var lastPt = points[points.Length - 1];
            using (var dotBrush = new SolidBrush(_lineColor))
            using (var borderPen = new Pen(Color.White, 1f))
            {
                g.FillEllipse(dotBrush, lastPt.X - 3f, lastPt.Y - 3f, 6f, 6f);
                g.DrawEllipse(borderPen, lastPt.X - 3f, lastPt.Y - 3f, 6f, 6f);
            }
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZSparkline"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroSparkline is deprecated and will be removed in 5 release cycles. Please migrate to ZSparkline instead.")]
    [ToolboxItem(false)]
    public class ZeroSparkline : ZSparkline
    {
        public ZeroSparkline() : base() { }
        public ZeroSparkline(int capacity) : base(capacity) { }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSparkline"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("Sparkline is deprecated and will be removed in 5 release cycles. Please migrate to ZSparkline instead.")]
    [ToolboxItem(false)]
    public class Sparkline : ZSparkline
    {
    }

    #endregion
}
