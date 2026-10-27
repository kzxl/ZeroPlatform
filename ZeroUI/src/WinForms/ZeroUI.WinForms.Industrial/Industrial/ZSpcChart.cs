using System;

using ZeroUI.WinForms.Icons;using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;

namespace ZeroUI.WinForms.Industrial
{
    public struct SpcDataPoint
    {
        public int SubgroupIndex;
        public float Value;
        public bool IsOutOfControl;
        public string Note;

        public SpcDataPoint(int index, float val, string note = "")
        {
            SubgroupIndex = index;
            Value = val;
            IsOutOfControl = false;
            Note = note;
        }
    }

    /// <summary>
    /// Statistical Process Control (SPC) X-Bar Chart for Six Sigma quality inspection.
    /// Automatically computes Mean (X-bar), Upper/Lower Control Limits (UCL = X + 3s, LCL = X - 3s),
    /// and flags Western Electric out-of-control rule violations.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Industrial & SCADA")]
    [DefaultProperty("Title")]
    [Description("Statistical Process Control (SPC) X-Bar chart with automated control limits")]
    [ToolboxBitmap(typeof(ZeroIcons), "ZeroSpcChart.bmp")]
    public class ZSpcChart : Control
    {
        private readonly List<SpcDataPoint> _points = new List<SpcDataPoint>();
        private string _title = "SPC X-Bar Chart — CNC Shaft Diameter (Target: 12.000 mm)";
        private string _unit = "mm";
        private float _nominalTarget = 12.000f;
        private float? _usl = 12.020f; // Upper Spec Limit
        private float? _lsl = 11.980f; // Lower Spec Limit

        // Calculated stats
        private float _mean;
        private float _sigma;
        private float _ucl;
        private float _lcl;
        private float _cpk = 1.33f;

        public ZSpcChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(460, 220);
            BackColor = Color.FromArgb(15, 23, 42); // Dark slate
            Font = new Font("Segoe UI", 8.5f);

            LoadSampleData();
        }

        [Category("Appearance")]
        [DefaultValue("SPC X-Bar Chart — CNC Shaft Diameter (Target: 12.000 mm)")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("SPC Parameters")]
        [DefaultValue("mm")]
        public string Unit
        {
            get => _unit;
            set { _unit = value ?? ""; Invalidate(); }
        }

        [Category("SPC Parameters")]
        [DefaultValue(12.000f)]
        public float NominalTarget
        {
            get => _nominalTarget;
            set { _nominalTarget = value; Invalidate(); }
        }

        [Category("SPC Parameters")]
        [DefaultValue(12.020f)]
        public float? USL
        {
            get => _usl;
            set { _usl = value; Invalidate(); }
        }

        [Category("SPC Parameters")]
        [DefaultValue(11.980f)]
        public float? LSL
        {
            get => _lsl;
            set { _lsl = value; Invalidate(); }
        }

        [Browsable(false)]
        public float Mean => _mean;

        [Browsable(false)]
        public float UCL => _ucl;

        [Browsable(false)]
        public float LCL => _lcl;

        [Browsable(false)]
        public float Cpk => _cpk;

        public void SetData(IEnumerable<float> values)
        {
            _points.Clear();
            int idx = 1;
            foreach (var v in values)
            {
                _points.Add(new SpcDataPoint(idx++, v));
            }
            RecalculateStats();
            Invalidate();
        }

        public void AddSample(float value, string note = "")
        {
            _points.Add(new SpcDataPoint(_points.Count + 1, value, note));
            RecalculateStats();
            Invalidate();
        }

        public void ClearData()
        {
            _points.Clear();
            RecalculateStats();
            Invalidate();
        }

        private void LoadSampleData()
        {
            // 20 realistic CNC machining samples centered around 12.001mm
            float[] samples = new[]
            {
                12.002f, 11.998f, 12.005f, 12.001f, 11.996f,
                12.003f, 12.007f, 12.000f, 11.999f, 12.004f,
                12.002f, 12.008f, 12.006f, 11.997f, 12.001f,
                12.015f, 12.003f, 12.000f, 12.018f, 12.002f // Subgroup 19 has an alarm spike
            };

            for (int i = 0; i < samples.Length; i++)
            {
                _points.Add(new SpcDataPoint(i + 1, samples[i]));
            }
            RecalculateStats();
        }

        private void RecalculateStats()
        {
            if (_points.Count == 0)
            {
                _mean = _nominalTarget;
                _sigma = 0.005f;
                _ucl = _nominalTarget + (3 * _sigma);
                _lcl = _nominalTarget - (3 * _sigma);
                _cpk = 1.0f;
                return;
            }

            // 1. Mean
            float sum = 0;
            for (int i = 0; i < _points.Count; i++) sum += _points[i].Value;
            _mean = sum / _points.Count;

            // 2. Standard deviation
            double varianceSum = 0;
            for (int i = 0; i < _points.Count; i++)
            {
                float diff = _points[i].Value - _mean;
                varianceSum += diff * diff;
            }
            _sigma = (float)Math.Sqrt(varianceSum / Math.Max(1, _points.Count - 1));
            if (_sigma < 0.0001f) _sigma = 0.001f;

            // 3. 3-Sigma Control Limits
            _ucl = _mean + (3f * _sigma);
            _lcl = _mean - (3f * _sigma);

            // 4. Cpk index
            if (_usl.HasValue && _lsl.HasValue)
            {
                float cpu = (_usl.Value - _mean) / (3f * _sigma);
                float cpl = (_mean - _lsl.Value) / (3f * _sigma);
                _cpk = Math.Max(0f, Math.Min(cpu, cpl));
            }

            // 5. Flag out-of-control points (Western Electric rule 1: > 3-Sigma)
            for (int i = 0; i < _points.Count; i++)
            {
                var pt = _points[i];
                pt.IsOutOfControl = (pt.Value > _ucl || pt.Value < _lcl);
                _points[i] = pt;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width;
            int h = Height;

            // 1. Frame Background
            using (var brush = new SolidBrush(BackColor))
            {
                g.FillRectangle(brush, 0, 0, w, h);
            }
            using (var borderPen = new Pen(Color.FromArgb(51, 65, 85), 1f))
            {
                g.DrawRectangle(borderPen, 0, 0, w - 1, h - 1);
            }

            // 2. Header & Metrics Bar
            string stats = $"X̄: {_mean:F3} | UCL: {_ucl:F3} | LCL: {_lcl:F3} | σ: {_sigma:F4} | Cpk: {_cpk:F2}";
            var statFont = ZeroFontCache.Get("Segoe UI", 7.5f, FontStyle.Bold);
            Color statColor = _cpk >= 1.33f ? Color.FromArgb(52, 211, 153) : Color.FromArgb(251, 191, 36);
            using (var statBrush = new SolidBrush(statColor))
            {
                var sz = g.MeasureString(stats, statFont);
                float statsX = w - sz.Width - 10;
                g.DrawString(stats, statFont, statBrush, statsX, 8);

                // Draw title with ellipsis if constrained by stats
                var titleFont = ZeroFontCache.Get("Segoe UI", 8.5f, FontStyle.Bold);
                using var titleBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                float titleMaxW = Math.Max(40f, statsX - 16f);
                var titleRect = new RectangleF(8, 6, titleMaxW, 18);
                g.DrawString(_title, titleFont, titleBrush, titleRect, ZeroStringFormats.EllipsisNearCenter);
            }

            // 3. Plot Area Geometry
            int plotX = 52;
            int plotY = 28;
            int plotW = w - plotX - 14;
            int plotH = h - plotY - 24;

            if (plotW <= 20 || plotH <= 20) return;

            using (var plotBrush = new SolidBrush(Color.FromArgb(10, 15, 30)))
            {
                g.FillRectangle(plotBrush, plotX, plotY, plotW, plotH);
            }

            // Min and Max scale for Y
            float minY = _lcl - (_sigma * 1.5f);
            float maxY = _ucl + (_sigma * 1.5f);
            float rangeY = maxY - minY;
            if (rangeY <= 0) rangeY = 1f;

            // Helper to convert value to plot Y coordinate
            int ValueToY(float val)
            {
                float norm = 1f - ((val - minY) / rangeY);
                return plotY + (int)(plotH * Math.Max(0f, Math.Min(1f, norm)));
            }

            // 4. Draw Guide Bands (±1σ, ±2σ)
            using (var sigmaPen = new Pen(Color.FromArgb(30, 41, 59), 1f) { DashStyle = DashStyle.Dot })
            {
                g.DrawLine(sigmaPen, plotX, ValueToY(_mean + _sigma), plotX + plotW, ValueToY(_mean + _sigma));
                g.DrawLine(sigmaPen, plotX, ValueToY(_mean - _sigma), plotX + plotW, ValueToY(_mean - _sigma));
                g.DrawLine(sigmaPen, plotX, ValueToY(_mean + (2 * _sigma)), plotX + plotW, ValueToY(_mean + (2 * _sigma)));
                g.DrawLine(sigmaPen, plotX, ValueToY(_mean - (2 * _sigma)), plotX + plotW, ValueToY(_mean - (2 * _sigma)));
            }

            // 5. Draw Mean Line (Green solid)
            int meanY = ValueToY(_mean);
            using (var meanPen = new Pen(Color.FromArgb(16, 185, 129), 1.5f))
            {
                g.DrawLine(meanPen, plotX, meanY, plotX + plotW, meanY);
            }
            DrawYLabel(g, plotX, meanY, $"X̄ {_mean:F3}", Color.FromArgb(52, 211, 153));

            // 6. Draw UCL & LCL Lines (Red dashed)
            int uclY = ValueToY(_ucl);
            int lclY = ValueToY(_lcl);
            using (var limitPen = new Pen(Color.FromArgb(239, 68, 68), 1.5f) { DashStyle = DashStyle.Dash })
            {
                g.DrawLine(limitPen, plotX, uclY, plotX + plotW, uclY);
                g.DrawLine(limitPen, plotX, lclY, plotX + plotW, lclY);
            }
            DrawYLabel(g, plotX, uclY, $"UCL {_ucl:F3}", Color.FromArgb(248, 113, 113));
            DrawYLabel(g, plotX, lclY, $"LCL {_lcl:F3}", Color.FromArgb(248, 113, 113));

            // 7. Plot Subgroup Sample Points and Connect with Line
            if (_points.Count > 1)
            {
                float stepX = (float)plotW / (_points.Count - 1);
                PointF[] pts = new PointF[_points.Count];

                for (int i = 0; i < _points.Count; i++)
                {
                    pts[i] = new PointF(plotX + (i * stepX), ValueToY(_points[i].Value));
                }

                using (var linePen = new Pen(Color.FromArgb(96, 165, 250), 1.5f))
                {
                    g.DrawLines(linePen, pts);
                }

                // Draw Point markers
                using var dotBrush = new SolidBrush(Color.Empty);
                using var dotPen = new Pen(Color.White, 1f);
                using var glowPen = new Pen(Color.FromArgb(120, 239, 68, 68), 3f);

                for (int i = 0; i < _points.Count; i++)
                {
                    var p = pts[i];
                    bool ooc = _points[i].IsOutOfControl;

                    Color dotColor = ooc ? Color.FromArgb(239, 68, 68) : Color.FromArgb(59, 130, 246);
                    int r = ooc ? 5 : 3;

                    // Glow if out of control
                    if (ooc)
                    {
                        g.DrawEllipse(glowPen, p.X - r - 2, p.Y - r - 2, (r * 2) + 4, (r * 2) + 4);
                    }

                    dotBrush.Color = dotColor;
                    g.FillEllipse(dotBrush, p.X - r, p.Y - r, r * 2, r * 2);
                    g.DrawEllipse(dotPen, p.X - r, p.Y - r, r * 2, r * 2);
                }
            }

            // 8. X-Axis Subgroup labels at bottom
            var xFont = ZeroFontCache.Get("Segoe UI", 7f, FontStyle.Regular);
            using var xBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
            if (_points.Count > 0)
            {
                float stepX = (float)plotW / Math.Max(1, _points.Count - 1);
                for (int i = 0; i < _points.Count; i += Math.Max(1, _points.Count / 6))
                {
                    string lbl = $"#{_points[i].SubgroupIndex}";
                    float px = plotX + (i * stepX);
                    var sz = g.MeasureString(lbl, xFont);
                    g.DrawString(lbl, xFont, xBrush, px - (sz.Width / 2), h - 16);
                }
            }
        }

        private void DrawYLabel(Graphics g, int plotX, int y, string text, Color color)
        {
            var font = ZeroFontCache.Get("Segoe UI", 7f, FontStyle.Regular);
            using var brush = new SolidBrush(color);
            var sz = g.MeasureString(text, font);
            g.DrawString(text, font, brush, plotX - sz.Width - 4, y - (sz.Height / 2));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZSpcChart"/>.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroSpcChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSpcChart instead.")]
    [ToolboxItem(false)]
    public class ZeroSpcChart : ZSpcChart
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSpcChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SpcChart is deprecated and will be removed in 5 release cycles. Please migrate to ZSpcChart instead.")]
    [ToolboxItem(false)]
    public class SpcChart : ZSpcChart
    {
    }

    #endregion
}
