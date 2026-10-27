using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Rendering;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Represents a single stage in a funnel or pyramid conversion chart.
    /// </summary>
    public class FunnelStage
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public Color Color { get; set; } = Color.FromArgb(79, 70, 229);
        public string? Description { get; set; }

        public FunnelStage(string name, double value, Color color, string? description = null)
        {
            Name = name;
            Value = value;
            Color = color;
            Description = description;
        }
    }

    /// <summary>
    /// High-performance Process Pipeline, Conversion Funnel, and Pyramid chart control with stage drop-off metrics.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Charts")]
    [DefaultProperty("Stages")]
    [DefaultEvent("SelectedStageChanged")]
    [Description("High-performance Funnel and Pyramid chart with conversion and drop-off metrics")]
    [ToolboxBitmap(typeof(ZeroIcons), "FunnelChart.bmp")]
    public class ZFunnelChart : Control
    {
        private readonly List<FunnelStage> _stages = new List<FunnelStage>();

        private FunnelChartMode _mode = FunnelChartMode.Funnel;
        private string _valueSuffix = " pcs";
        private bool _showConversionRates = true;
        private bool _showPercentages = true;
        private int _neckWidth = 100;
        private int _segmentGap = 4;
        private int _hoverIndex = -1;
        private int _selectedIndex = -1;

        public event EventHandler? SelectedStageChanged;

        public ZFunnelChart()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor, true);

            BackColor = Color.Transparent;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            Size = new Size(500, 320);

            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        #region Properties

        [Category("ZeroUI - Presentation")]
        [Description("Visual chart mode: Funnel (taper downward) or Pyramid (expand downward).")]
        [DefaultValue(FunnelChartMode.Funnel)]
        public FunnelChartMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI - Appearance")]
        [Description("Value unit suffix displayed on data labels.")]
        [DefaultValue(" pcs")]
        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? string.Empty; Invalidate(); }
        }

        [Category("ZeroUI - Appearance")]
        [Description("Displays conversion rate relative to the preceding stage.")]
        [DefaultValue(true)]
        public bool ShowConversionRates
        {
            get => _showConversionRates;
            set { _showConversionRates = value; Invalidate(); }
        }

        [Category("ZeroUI - Appearance")]
        [Description("Displays percentage of the initial intake value.")]
        [DefaultValue(true)]
        public bool ShowPercentages
        {
            get => _showPercentages;
            set { _showPercentages = value; Invalidate(); }
        }

        [Category("ZeroUI - Appearance")]
        [Description("Minimum width in pixels for the narrowest apex/neck tier.")]
        [DefaultValue(100)]
        public int NeckWidth
        {
            get => _neckWidth;
            set { _neckWidth = Math.Max(20, value); Invalidate(); }
        }

        [Category("ZeroUI - Appearance")]
        [Description("Vertical gap between consecutive segments.")]
        [DefaultValue(4)]
        public int SegmentGap
        {
            get => _segmentGap;
            set { _segmentGap = Math.Max(0, value); Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    Invalidate();
                    SelectedStageChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Browsable(false)]
        public FunnelStage? SelectedStage =>
            (_selectedIndex >= 0 && _selectedIndex < _stages.Count) ? _stages[_selectedIndex] : null;

        [Browsable(false)]
        public List<FunnelStage> Stages => _stages;

        #endregion

        public void AddStage(string name, double value, Color color, string? description = null)
        {
            _stages.Add(new FunnelStage(name, value, color, description));
            Invalidate();
        }

        public void ClearStages()
        {
            _stages.Clear();
            _selectedIndex = -1;
            _hoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_stages.Count == 0) return;

            int count = _stages.Count;
            int topMargin = 24;
            int bottomMargin = 20;
            int availH = Height - topMargin - bottomMargin;
            float stageH = (availH - (count - 1) * _segmentGap) / (float)count;

            int newHover = -1;
            for (int i = 0; i < count; i++)
            {
                float y1 = topMargin + i * (stageH + _segmentGap);
                float y2 = y1 + stageH;
                if (e.Y >= y1 && e.Y <= y2)
                {
                    newHover = i;
                    break;
                }
            }

            if (_hoverIndex != newHover)
            {
                _hoverIndex = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && _hoverIndex >= 0 && _hoverIndex < _stages.Count)
            {
                SelectedIndex = (_selectedIndex == _hoverIndex) ? -1 : _hoverIndex;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            var palette = ZeroTheme.Colors;

            if (_stages.Count == 0)
            {
                using var noteBrush = new SolidBrush(palette.TextSecondary);
                g.DrawString("No funnel stages defined.", Font, noteBrush, ClientRectangle, ZeroStringFormats.Center);
                return;
            }

            int count = _stages.Count;
            int topMargin = 24;
            int bottomMargin = 20;
            int availH = Height - topMargin - bottomMargin;
            if (availH <= 30) return;

            float stageH = (availH - (count - 1) * _segmentGap) / (float)count;

            // Compute Widths: Funnel occupies center 45% of width
            float funnelCenter = Width * 0.45f;
            float maxFunnelW = Math.Min(340f, Width * 0.42f);
            float minFunnelW = Math.Max(40f, Math.Min(_neckWidth, maxFunnelW * 0.35f));

            double baseReferenceValue = (_mode == FunnelChartMode.Funnel)
                ? _stages[0].Value
                : _stages[_stages.Count - 1].Value;
            if (baseReferenceValue <= 0) baseReferenceValue = 1.0;

            var centerFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);
            var nameFont = ZeroFontCache.Get(9f, FontStyle.Bold);
            var descFont = ZeroFontCache.Get(7.5f, FontStyle.Regular);
            var tagFont = ZeroFontCache.Get(8f, FontStyle.Bold);
            var rateFont = ZeroFontCache.Get(8.5f, FontStyle.Bold);

            using var centerBrush = new SolidBrush(Color.White);
            using var nameBrush = new SolidBrush(palette.TextPrimary);
            using var descBrush = new SolidBrush(palette.TextSecondary);
            using var borderPen = new Pen(palette.Surface, 1.5f);
            using var selectedPen = new Pen(palette.Primary, 2.5f);
            using var topTagBrush = new SolidBrush(palette.Success);
            using var rateBrush = new SolidBrush(palette.Success);

            for (int i = 0; i < count; i++)
            {
                var stage = _stages[i];
                float yTop = topMargin + i * (stageH + _segmentGap);
                float yBot = yTop + stageH;

                // Pure geometric calculation from FunnelMath
                FunnelMath.CalculateSegmentWidths(i, count, maxFunnelW, minFunnelW, _mode, out float wTop, out float wBot);

                // Expand slightly on hover or selection
                if (i == _hoverIndex || i == _selectedIndex)
                {
                    wTop += 8f;
                    wBot += 8f;
                }

                PointF[] trapezoid = new PointF[]
                {
                    new PointF(funnelCenter - wTop / 2f, yTop),
                    new PointF(funnelCenter + wTop / 2f, yTop),
                    new PointF(funnelCenter + wBot / 2f, yBot),
                    new PointF(funnelCenter - wBot / 2f, yBot)
                };

                // Draw Trapezoid with sleek gradient
                Color fillA = stage.Color;
                Color fillB = Color.FromArgb(
                    Math.Max(0, fillA.R - 25),
                    Math.Max(0, fillA.G - 25),
                    Math.Max(0, fillA.B - 25));

                using (var lgb = new LinearGradientBrush(
                    new PointF(funnelCenter - wTop / 2f, yTop),
                    new PointF(funnelCenter + wTop / 2f, yBot),
                    fillA,
                    fillB))
                {
                    g.FillPolygon(lgb, trapezoid);
                }

                // Draw selection highlight or standard border
                if (i == _selectedIndex)
                {
                    g.DrawPolygon(selectedPen, trapezoid);
                }
                else
                {
                    g.DrawPolygon(borderPen, trapezoid);
                }

                // Center Value Label inside trapezoid
                double pctOfBase = FunnelMath.CalculateOverallYield(stage.Value, baseReferenceValue);
                string centerLabel = $"{stage.Value:N0}{_valueSuffix}";
                if (_showPercentages && ((_mode == FunnelChartMode.Funnel && i > 0) || (_mode == FunnelChartMode.Pyramid && i < count - 1)))
                {
                    centerLabel += $" ({pctOfBase:F1}%)";
                }

                var cSz = g.MeasureString(centerLabel, centerFont);
                float cy = yTop + stageH / 2f - cSz.Height / 2f;
                g.DrawString(centerLabel, centerFont, centerBrush, funnelCenter - cSz.Width / 2f, cy);

                // Left Label: Stage Name & description
                float textRight = funnelCenter - Math.Max(wTop, wBot) / 2f - 12f;
                var nSz = g.MeasureString(stage.Name, nameFont);
                float nx = Math.Max(8f, textRight - nSz.Width);
                float ny = yTop + (stageH - nSz.Height) / 2f;
                if (!string.IsNullOrEmpty(stage.Description)) ny -= 6f;

                g.DrawString(stage.Name, nameFont, nameBrush, nx, ny);

                if (!string.IsNullOrEmpty(stage.Description))
                {
                    var dSz = g.MeasureString(stage.Description, descFont);
                    float dx = Math.Max(8f, textRight - dSz.Width);
                    g.DrawString(stage.Description, descFont, descBrush, dx, ny + nSz.Height);
                }

                // Right Label: Conversion Rate relative to previous stage
                if (_showConversionRates)
                {
                    float rightX = funnelCenter + Math.Max(wTop, wBot) / 2f + 16f;
                    float midY = yTop + stageH / 2f;

                    if (i == 0)
                    {
                        g.DrawString("100% INWARD INTAKE", tagFont, topTagBrush, rightX, midY - 6f);
                    }
                    else
                    {
                        double prevVal = _stages[i - 1].Value;
                        double convRate = FunnelMath.CalculateConversionRate(stage.Value, prevVal);
                        double dropOff = FunnelMath.CalculateDropOffRate(stage.Value, prevVal);

                        string rateText = $"Yield: {convRate:F1}%  (Loss: -{dropOff:F1}%)";
                        Color rateColor = convRate >= 95 ? palette.Success : (convRate >= 80 ? palette.Warning : palette.Danger);

                        rateBrush.Color = rateColor;
                        g.DrawString(rateText, rateFont, rateBrush, rightX, midY - 6f);
                    }
                }
            }
        }
    
    private void OnThemeChanged(object sender, EventArgs e) => Invalidate();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    /// <summary>
    /// Legacy alias for FunnelChart.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroFunnelChart is deprecated and will be removed in 5 release cycles. Please migrate to ZFunnelChart instead.")]
    [ToolboxItem(false)]
    public class ZeroFunnelChart : ZFunnelChart
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFunnelChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FunnelChart is deprecated and will be removed in 5 release cycles. Please migrate to ZFunnelChart instead.")]
    [ToolboxItem(false)]
    public class FunnelChart : ZFunnelChart
    {
    }

    #endregion
}
