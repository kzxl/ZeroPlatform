using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Continuous statistical distribution histogram control with Sturges/Freedman-Diaconis binning
    /// and Gaussian normal distribution curve overlay.
    /// Thin WinForms rendering layer consuming ZeroUI.Core.Analytics.HistogramEngine.
    /// </summary>
    [ToolboxItem(true)]
    [ToolboxBitmap(typeof(ZeroIcons), "ChartControl.bmp")]
    [Category("ZeroUI - Charts & Analytics")]
    [Description("Continuous distribution histogram with automatic binning and Gaussian normal curve overlay")]
    public class ZHistogramChart : Control
    {
        private readonly List<double> _data = new List<double>();
        private readonly ToolTip _toolTip = new ToolTip();

        private int _binCount = 0;
        private bool _showNormalCurve = true;
        private bool _showGridLines = true;
        private string _title = "Statistical Distribution Histogram";
        private string _xAxisTitle = "Value";
        private string _yAxisTitle = "Frequency";
        private Color _barColor = Color.FromArgb(59, 130, 246);
        private Color _curveColor = Color.FromArgb(239, 68, 68);

        private int _hoverBinIndex = -1;
        private readonly List<RectangleF> _binRects = new List<RectangleF>();
        private HistogramResult? _lastResult;

        public List<double> Data => _data;

        [Category("Appearance")]
        [DefaultValue("Statistical Distribution Histogram")]
        public string Title
        {
            get => _title;
            set { _title = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Value")]
        public string XAxisTitle
        {
            get => _xAxisTitle;
            set { _xAxisTitle = value ?? ""; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue("Frequency")]
        public string YAxisTitle
        {
            get => _yAxisTitle;
            set { _yAxisTitle = value ?? ""; Invalidate(); }
        }

        [Category("Behavior")]
        [DefaultValue(0)]
        [Description("Number of frequency bins. If 0, automatically computed using Sturges' formula.")]
        public int BinCount
        {
            get => _binCount;
            set { _binCount = Math.Max(0, value); Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        [Description("Whether to overlay a parametric Gaussian normal distribution bell curve.")]
        public bool ShowNormalCurve
        {
            get => _showNormalCurve;
            set { _showNormalCurve = value; Invalidate(); }
        }

        [Category("Appearance")]
        [DefaultValue(true)]
        public bool ShowGridLines
        {
            get => _showGridLines;
            set { _showGridLines = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color BarColor
        {
            get => _barColor;
            set { _barColor = value; Invalidate(); }
        }

        [Category("Appearance")]
        public Color CurveColor
        {
            get => _curveColor;
            set { _curveColor = value; Invalidate(); }
        }

        public ZHistogramChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.SupportsTransparentBackColor, true);

            Size = new Size(540, 320);
            Font = new Font("Segoe UI", 9f);
        }

        public void SetData(IEnumerable<double> values)
        {
            _data.Clear();
            if (values != null) _data.AddRange(values);
            Invalidate();
        }

        public void LoadSampleData()
        {
            _data.Clear();
            var rand = new Random(42);
            // Generate normally distributed random sample using Box-Muller transform
            for (int i = 0; i < 300; i++)
            {
                double u1 = 1.0 - rand.NextDouble();
                double u2 = 1.0 - rand.NextDouble();
                double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                _data.Add(50.0 + 8.5 * randStdNormal);
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int prevHover = _hoverBinIndex;
            _hoverBinIndex = -1;

            for (int i = 0; i < _binRects.Count; i++)
            {
                if (_binRects[i].Contains(e.Location))
                {
                    _hoverBinIndex = i;
                    break;
                }
            }

            if (prevHover != _hoverBinIndex)
            {
                if (_hoverBinIndex >= 0 && _lastResult != null && _hoverBinIndex < _lastResult.Bins.Count)
                {
                    var bin = _lastResult.Bins[_hoverBinIndex];
                    _toolTip.SetToolTip(this, $"Bin [{bin.LowerBound:F1} - {bin.UpperBound:F1}]\nCount: {bin.Count}\nShare: {bin.RelativeFrequency:P1}");
                }
                else
                {
                    _toolTip.SetToolTip(this, null);
                }
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverBinIndex != -1)
            {
                _hoverBinIndex = -1;
                _toolTip.SetToolTip(this, null);
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool isDark = ZeroTheme.IsDark;
            var bgColor = isDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(255, 255, 255);
            var textPrimary = isDark ? Color.FromArgb(241, 245, 249) : Color.FromArgb(30, 41, 59);
            var textSecondary = isDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(100, 116, 139);
            var gridColor = isDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(226, 232, 240);
            var axisColor = isDark ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);

            g.Clear(bgColor);

            // Title
            float topMargin = 38f;
            if (!string.IsNullOrEmpty(_title))
            {
                using (var titleFont = new Font(Font.FontFamily, 10.5f, FontStyle.Bold))
                using (var brush = new SolidBrush(textPrimary))
                {
                    g.DrawString(_title, titleFont, brush, 16f, 10f);
                }
            }

            var activeData = _data.Count > 0 ? _data : GetPreviewData();
            _lastResult = HistogramEngine.Compute(activeData, _binCount);
            _binRects.Clear();

            if (_lastResult.Bins.Count == 0) return;

            float leftMargin = 50f;
            float rightMargin = 25f;
            float bottomMargin = 40f;
            float plotX = leftMargin;
            float plotY = topMargin;
            float plotWidth = Math.Max(10f, Width - leftMargin - rightMargin);
            float plotHeight = Math.Max(10f, Height - topMargin - bottomMargin);

            int maxCount = Math.Max(1, _lastResult.MaxBinCount);
            int yAxisMax = (int)Math.Ceiling(maxCount * 1.15);

            // Grid Lines & Y-Ticks
            int ySteps = 4;
            using (var gridPen = new Pen(gridColor, 1f) { DashStyle = DashStyle.Dash })
            using (var textBrush = new SolidBrush(textSecondary))
            using (var smallFont = new Font(Font.FontFamily, 8f))
            {
                for (int s = 0; s <= ySteps; s++)
                {
                    float ratio = (float)s / ySteps;
                    float y = plotY + plotHeight - (ratio * plotHeight);
                    int val = (int)(ratio * yAxisMax);

                    if (_showGridLines && s > 0)
                    {
                        g.DrawLine(gridPen, plotX, y, plotX + plotWidth, y);
                    }
                    g.DrawString(val.ToString(), smallFont, textBrush, plotX - 35f, y - 7f);
                }

                // Y-Axis Title
                if (!string.IsNullOrEmpty(_yAxisTitle))
                {
                    var state = g.Save();
                    g.TranslateTransform(12f, plotY + (plotHeight / 2f));
                    g.RotateTransform(-90f);
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                    {
                        g.DrawString(_yAxisTitle, smallFont, textBrush, 0, 0, sf);
                    }
                    g.Restore(state);
                }
            }

            // Draw Histogram Bins
            int k = _lastResult.Bins.Count;
            float colWidth = plotWidth / k;

            for (int i = 0; i < k; i++)
            {
                var bin = _lastResult.Bins[i];
                float barH = (float)bin.Count / yAxisMax * plotHeight;
                float barX = plotX + i * colWidth;
                float barY = plotY + plotHeight - barH;
                var rect = new RectangleF(barX + 1.5f, barY, Math.Max(1f, colWidth - 3f), barH);
                _binRects.Add(rect);

                bool isHover = (i == _hoverBinIndex);
                var curBarColor = isHover ? Color.FromArgb(200, _barColor) : _barColor;

                using (var brush = new SolidBrush(curBarColor))
                {
                    g.FillRectangle(brush, rect);
                }
                using (var borderPen = new Pen(Color.FromArgb(180, isDark ? Color.FromArgb(30, 41, 59) : Color.White), 1f))
                {
                    g.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width, rect.Height);
                }

                // X-Axis tick label
                using (var textBrush = new SolidBrush(textSecondary))
                using (var smallFont = new Font(Font.FontFamily, 7.5f))
                using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                {
                    if (k <= 10 || i % 2 == 0 || i == k - 1)
                    {
                        g.DrawString(bin.LowerBound.ToString("F1"), smallFont, textBrush, barX, plotY + plotHeight + 6f, sf);
                    }
                    if (i == k - 1)
                    {
                        g.DrawString(bin.UpperBound.ToString("F1"), smallFont, textBrush, barX + colWidth, plotY + plotHeight + 6f, sf);
                    }
                }
            }

            // Draw Gaussian Bell Curve
            if (_showNormalCurve && _lastResult.GaussianCurve.Count > 1)
            {
                var pts = new List<PointF>();
                double minX = _lastResult.Min;
                double maxX = _lastResult.Max;
                double spanX = Math.Max(1e-9, maxX - minX);

                foreach (var cp in _lastResult.GaussianCurve)
                {
                    float cx = plotX + (float)((cp.X - minX) / spanX) * plotWidth;
                    float cy = plotY + plotHeight - (float)(cp.ScaledCount / yAxisMax * plotHeight);
                    if (cx >= plotX && cx <= plotX + plotWidth)
                    {
                        pts.Add(new PointF(cx, Math.Max(plotY, Math.Min(plotY + plotHeight, cy))));
                    }
                }

                if (pts.Count > 1)
                {
                    using (var curvePen = new Pen(_curveColor, 2f))
                    {
                        g.DrawCurve(curvePen, pts.ToArray(), 0.5f);
                    }
                }
            }

            // Draw Baseline Axis
            using (var axisPen = new Pen(axisColor, 1.2f))
            {
                g.DrawLine(axisPen, plotX, plotY + plotHeight, plotX + plotWidth, plotY + plotHeight);
                g.DrawLine(axisPen, plotX, plotY, plotX, plotY + plotHeight);
            }
        }

        private static List<double> GetPreviewData()
        {
            var list = new List<double>();
            var r = new Random(123);
            for (int i = 0; i < 150; i++)
            {
                double u1 = 1.0 - r.NextDouble();
                double u2 = 1.0 - r.NextDouble();
                double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
                list.Add(50.0 + 10.0 * normal);
            }
            return list;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZHistogramChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HistogramChart is deprecated and will be removed in 5 release cycles. Please migrate to ZHistogramChart instead.")]
    [ToolboxItem(false)]
    public class HistogramChart : ZHistogramChart
    {
    }

    #endregion
}
