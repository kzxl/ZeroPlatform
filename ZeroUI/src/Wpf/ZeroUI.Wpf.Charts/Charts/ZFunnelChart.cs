using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// Represents a single stage in a funnel or pyramid conversion chart.
    /// </summary>
    public class FunnelStage
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public Color Color { get; set; } = Color.FromRgb(79, 70, 229);
        public string? Description { get; set; }

        public FunnelStage(string name, double value, Color color, string? description = null)
        {
            Name = name;
            Value = value;
            Color = color;
            Description = description;
        }

        public FunnelStage(string name, double value, uint argbColor, string? description = null)
        {
            Name = name;
            Value = value;
            Color = Color.FromArgb(
                (byte)((argbColor >> 24) & 0xFF),
                (byte)((argbColor >> 16) & 0xFF),
                (byte)((argbColor >> 8) & 0xFF),
                (byte)(argbColor & 0xFF));
            Description = description;
        }
    }

    /// <summary>
    /// High-performance Process Pipeline, Conversion Funnel, and Pyramid chart control for WPF
    /// with interactive stage selection and drop-off yield metrics.
    /// </summary>
    public class ZFunnelChart : FrameworkElement
    {
        private readonly List<FunnelStage> _stages = new List<FunnelStage>();

        private FunnelChartMode _mode = FunnelChartMode.Funnel;
        private string _valueSuffix = " pcs";
        private bool _showConversionRates = true;
        private bool _showPercentages = true;
        private double _neckWidth = 100.0;
        private double _segmentGap = 4.0;
        private int _hoverIndex = -1;
        private int _selectedIndex = -1;

        public event EventHandler? SelectedStageChanged;

        #region Properties

        public FunnelChartMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    InvalidateVisual();
                }
            }
        }

        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? string.Empty; InvalidateVisual(); }
        }

        public bool ShowConversionRates
        {
            get => _showConversionRates;
            set { _showConversionRates = value; InvalidateVisual(); }
        }

        public bool ShowPercentages
        {
            get => _showPercentages;
            set { _showPercentages = value; InvalidateVisual(); }
        }

        public double NeckWidth
        {
            get => _neckWidth;
            set { _neckWidth = Math.Max(20.0, value); InvalidateVisual(); }
        }

        public double SegmentGap
        {
            get => _segmentGap;
            set { _segmentGap = Math.Max(0.0, value); InvalidateVisual(); }
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    InvalidateVisual();
                    SelectedStageChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public FunnelStage? SelectedStage =>
            (_selectedIndex >= 0 && _selectedIndex < _stages.Count) ? _stages[_selectedIndex] : null;

        public List<FunnelStage> Stages => _stages;

        #endregion

        public ZFunnelChart()
        {
            ClipToBounds = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void AddStage(string name, double value, Color color, string? description = null)
        {
            _stages.Add(new FunnelStage(name, value, color, description));
            InvalidateVisual();
        }

        public void AddStage(string name, double value, uint argbColor, string? description = null)
        {
            _stages.Add(new FunnelStage(name, value, argbColor, description));
            InvalidateVisual();
        }

        public void ClearStages()
        {
            _stages.Clear();
            _selectedIndex = -1;
            _hoverIndex = -1;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_stages.Count == 0) return;

            Point pos = e.GetPosition(this);
            int count = _stages.Count;
            double topMargin = 24.0;
            double bottomMargin = 20.0;
            double availH = ActualHeight - topMargin - bottomMargin;
            double stageH = (availH - (count - 1) * _segmentGap) / count;

            int newHover = -1;
            for (int i = 0; i < count; i++)
            {
                double y1 = topMargin + i * (stageH + _segmentGap);
                double y2 = y1 + stageH;
                if (pos.Y >= y1 && pos.Y <= y2)
                {
                    newHover = i;
                    break;
                }
            }

            if (_hoverIndex != newHover)
            {
                _hoverIndex = newHover;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_hoverIndex >= 0 && _hoverIndex < _stages.Count)
            {
                SelectedIndex = (_selectedIndex == _hoverIndex) ? -1 : _hoverIndex;
            }
        }

        #if NETFRAMEWORK
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        }
        #else
        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double pixelsPerDip = 1.0)
        {
            return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush, pixelsPerDip);
        }
        #endif

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            #if NETFRAMEWORK
            double dpi = 1.0;
            #else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            #endif

            // Background Card
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));
            dc.DrawRectangle(null, ZeroWpfTheme.BorderPen, new Rect(0.5, 0.5, w - 1, h - 1));

            if (_stages.Count == 0)
            {
                var noteFt = CreateFormattedText("No funnel stages defined.", ZeroWpfTheme.RegularTypeface, 12.0, ZeroWpfTheme.TextMuted, dpi);
                dc.DrawText(noteFt, new Point(w / 2.0 - noteFt.Width / 2.0, h / 2.0 - noteFt.Height / 2.0));
                return;
            }

            int count = _stages.Count;
            double topMargin = 24.0;
            double bottomMargin = 20.0;
            double availH = h - topMargin - bottomMargin;
            if (availH <= 30) return;

            double stageH = (availH - (count - 1) * _segmentGap) / count;

            double funnelCenter = w * 0.42;
            double maxFunnelW = Math.Min(340.0, w * 0.40);
            double minFunnelW = Math.Max(40.0, Math.Min(_neckWidth, maxFunnelW * 0.35));

            double baseReferenceValue = (_mode == FunnelChartMode.Funnel)
                ? _stages[0].Value
                : _stages[_stages.Count - 1].Value;
            if (baseReferenceValue <= 0) baseReferenceValue = 1.0;

            for (int i = 0; i < count; i++)
            {
                var stage = _stages[i];
                double yTop = topMargin + i * (stageH + _segmentGap);
                double yBot = yTop + stageH;

                FunnelMath.CalculateSegmentWidths(i, count, (float)maxFunnelW, (float)minFunnelW, _mode, out float fwTop, out float fwBot);
                double wTop = fwTop;
                double wBot = fwBot;

                if (i == _hoverIndex || i == _selectedIndex)
                {
                    wTop += 8.0;
                    wBot += 8.0;
                }

                // Construct Trapezoid Path
                var geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(new Point(funnelCenter - wTop / 2.0, yTop), true, true);
                    ctx.LineTo(new Point(funnelCenter + wTop / 2.0, yTop), true, false);
                    ctx.LineTo(new Point(funnelCenter + wBot / 2.0, yBot), true, false);
                    ctx.LineTo(new Point(funnelCenter - wBot / 2.0, yBot), true, false);
                }
                geom.Freeze();

                // Gradient Fill
                Color cTop = stage.Color;
                Color cBot = Color.FromRgb(
                    (byte)Math.Max(0, cTop.R - 25),
                    (byte)Math.Max(0, cTop.G - 25),
                    (byte)Math.Max(0, cTop.B - 25));

                var lgb = new LinearGradientBrush(cTop, cBot, new Point(0, 0), new Point(0, 1));
                lgb.Freeze();

                Pen borderPen = (i == _selectedIndex)
                    ? new Pen(ZeroWpfTheme.PrimaryAccent, 2.5)
                    : ZeroWpfTheme.BorderPen;

                dc.DrawGeometry(lgb, borderPen, geom);

                // Stage Value in Center of Trapezoid
                string valStr = $"{stage.Value:N0}{_valueSuffix}";
                var centerFt = CreateFormattedText(valStr, ZeroWpfTheme.BoldTypeface, 11.0, Brushes.White, dpi);
                double cy = (yTop + yBot) / 2.0;
                dc.DrawText(centerFt, new Point(funnelCenter - centerFt.Width / 2.0, cy - centerFt.Height / 2.0));

                // Right-side Details Card: Stage Name, Yield %, Conversion Rate
                double infoX = funnelCenter + maxFunnelW / 2.0 + 20.0;
                if (infoX < w - 60)
                {
                    // Stage Name
                    var nameFt = CreateFormattedText(stage.Name, ZeroWpfTheme.BoldTypeface, 12.0, ZeroWpfTheme.TextPrimary, dpi);
                    dc.DrawText(nameFt, new Point(infoX, cy - 14));

                    // Description or Subtext
                    if (!string.IsNullOrEmpty(stage.Description))
                    {
                        var descFt = CreateFormattedText(stage.Description!, ZeroWpfTheme.RegularTypeface, 10.0, ZeroWpfTheme.TextSecondary, dpi);
                        dc.DrawText(descFt, new Point(infoX, cy + 2));
                    }

                    // Percent of Total Intake
                    if (_showPercentages)
                    {
                        double pct = FunnelMath.CalculateOverallYield(stage.Value, baseReferenceValue);
                        var pctFt = CreateFormattedText($"{pct:0.#}%", ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.PrimaryAccent, dpi);
                        dc.DrawText(pctFt, new Point(w - 70, cy - 14));
                    }

                    // Stage-to-Stage Conversion & Drop-off Rates
                    if (_showConversionRates && i > 0)
                    {
                        double prevVal = _stages[i - 1].Value;
                        double conv = FunnelMath.CalculateConversionRate(stage.Value, prevVal);
                        double drop = FunnelMath.CalculateDropOffRate(stage.Value, prevVal);

                        string rateText = $"↓ {conv:0.#}% (-{drop:0.#}%)";
                        Brush rateBrush = (drop > 25.0) ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.SuccessAccent;
                        var rateFt = CreateFormattedText(rateText, ZeroWpfTheme.RegularTypeface, 10.0, rateBrush, dpi);
                        dc.DrawText(rateFt, new Point(w - 110, cy + 2));
                    }
                }
            }
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
    private void OnThemeChanged() => InvalidateVisual();
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFunnelChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FunnelChart is deprecated and will be removed in 5 release cycles. Please migrate to ZFunnelChart instead.")]
    public class FunnelChart : ZFunnelChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZFunnelChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroFunnelChart is deprecated and will be removed in 5 release cycles. Please migrate to ZFunnelChart instead.")]
    public class ZeroFunnelChart : ZFunnelChart { }

    #endregion

}
