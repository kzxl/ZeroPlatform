using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Analytics;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// Clean, high-density Lollipop comparison chart (horizontal or vertical stems with circular dot heads) in WPF.
    /// Thin WPF rendering layer consuming ZeroUI.Core.Analytics.LollipopEngine.
    /// </summary>
    public class ZLollipopChart : FrameworkElement
    {
        private readonly List<LollipopItem> _items = new List<LollipopItem>();

        private string _title = "Category Benchmark Comparison";
        private LollipopOrientation _orientation = LollipopOrientation.Horizontal;
        private Color _stemColor = Color.FromRgb(148, 163, 184);
        private Color _dotColor = Color.FromRgb(59, 130, 246);
        private double _dotRadius = 6.5;
        private bool _showValueLabels = true;

        private LollipopLayoutResult? _lastLayout;
        private LollipopRenderItem? _hoverItem;

        public List<LollipopItem> Items => _items;

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public LollipopOrientation Orientation
        {
            get => _orientation;
            set { _orientation = value; InvalidateVisual(); }
        }

        public Color StemColor
        {
            get => _stemColor;
            set { _stemColor = value; InvalidateVisual(); }
        }

        public Color DotColor
        {
            get => _dotColor;
            set { _dotColor = value; InvalidateVisual(); }
        }

        public double DotRadius
        {
            get => _dotRadius;
            set { _dotRadius = Math.Max(2.0, value); InvalidateVisual(); }
        }

        public bool ShowValueLabels
        {
            get => _showValueLabels;
            set { _showValueLabels = value; InvalidateVisual(); }
        }

        public ZLollipopChart()
        {
            ClipToBounds = true;
            MinHeight = 220;
            MinWidth = 300;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void LoadSampleData()
        {
            _items.Clear();
            _items.Add(new LollipopItem("Product Alpha", 84, 0xFF3B82F6));
            _items.Add(new LollipopItem("Product Beta", 62, 0xFF10B981));
            _items.Add(new LollipopItem("Product Gamma", 95, 0xFFF97316));
            _items.Add(new LollipopItem("Product Delta", 43, 0xFFA855F7));
            _items.Add(new LollipopItem("Product Epsilon", 71, 0xFFEC4899));
            _items.Add(new LollipopItem("Product Zeta", 88, 0xFF0EA5E9));
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            LollipopRenderItem? newHover = null;

            if (_lastLayout != null)
            {
                foreach (var item in _lastLayout.RenderItems)
                {
                    if (item.Contains(pos.X, pos.Y, 6.0))
                    {
                        newHover = item;
                        break;
                    }
                }
            }

            if (newHover != _hoverItem)
            {
                _hoverItem = newHover;
                if (_hoverItem != null)
                {
                    ToolTip = $"{_hoverItem.SourceItem.Category}\nValue: {_hoverItem.SourceItem.Value:F1}";
                }
                else
                {
                    ToolTip = null;
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverItem != null)
            {
                _hoverItem = null;
                ToolTip = null;
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));

            // Title
            double topMargin = 36.0;
            if (!string.IsNullOrEmpty(_title))
            {
                var titleText = new FormattedText(
                    _title,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.BoldTypeface,
                    13,
                    ZeroWpfTheme.TextPrimary,
                    1.0);
                dc.DrawText(titleText, new Point(14, 8));
            }

            var activeItems = _items.Count > 0 ? _items : GetPreviewItems();
            if (activeItems.Count == 0) return;

            double leftMargin = (_orientation == LollipopOrientation.Horizontal) ? 120.0 : 45.0;
            double rightMargin = _showValueLabels ? 50.0 : 20.0;
            double bottomMargin = (_orientation == LollipopOrientation.Vertical) ? 50.0 : 30.0;
            double plotX = leftMargin;
            double plotY = topMargin;
            double plotW = Math.Max(10.0, w - leftMargin - rightMargin);
            double plotH = Math.Max(10.0, h - topMargin - bottomMargin);

            _lastLayout = LollipopEngine.ComputeLayout(
                activeItems,
                plotX,
                plotY,
                plotW,
                plotH,
                _orientation,
                _dotRadius
            );

            if (_lastLayout.RenderItems.Count == 0) return;

            // Draw baseline
            var basePen = new Pen(ZeroWpfTheme.BorderDefault, 1.5);
            basePen.Freeze();

            if (_orientation == LollipopOrientation.Horizontal)
            {
                double bx = _lastLayout.RenderItems[0].StemStartX;
                dc.DrawLine(basePen, new Point(bx, plotY), new Point(bx, plotY + plotH));
            }
            else
            {
                double by = _lastLayout.RenderItems[0].StemStartY;
                dc.DrawLine(basePen, new Point(plotX, by), new Point(plotX + plotW, by));
            }

            // Draw each Lollipop
            foreach (var ri in _lastLayout.RenderItems)
            {
                bool isHover = (_hoverItem == ri);
                Color curDotColor = ri.SourceItem.ColorRgba.ToColor(_dotColor);
                Color curStemColor = _stemColor;

                if (isHover)
                {
                    curDotColor = Color.FromRgb(
                        (byte)Math.Min(255, curDotColor.R + 40),
                        (byte)Math.Min(255, curDotColor.G + 40),
                        (byte)Math.Min(255, curDotColor.B + 40));
                }

                // Stem Line
                var stemPen = new Pen(new SolidColorBrush(curStemColor), isHover ? 2.5 : 1.5);
                stemPen.Freeze();
                dc.DrawLine(stemPen, new Point(ri.StemStartX, ri.StemStartY), new Point(ri.StemEndX, ri.StemEndY));

                // Dot Head
                double r = ri.DotRadius + (isHover ? 2.0 : 0.0);
                var dotBrush = new SolidColorBrush(curDotColor);
                dotBrush.Freeze();
                var borderPen = new Pen(isHover ? Brushes.White : new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1.5);
                borderPen.Freeze();
                dc.DrawEllipse(dotBrush, borderPen, new Point(ri.DotCenterX, ri.DotCenterY), r, r);

                // Labels
                if (_orientation == LollipopOrientation.Horizontal)
                {
                    // Category Label
                    var catText = new FormattedText(
                        ri.SourceItem.Category,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface,
                        9.5,
                        ZeroWpfTheme.TextPrimary,
                        1.0);
                    dc.DrawText(catText, new Point(ri.StemStartX - catText.Width - 8.0, ri.DotCenterY - (catText.Height / 2.0)));

                    // Value Label
                    if (_showValueLabels)
                    {
                        var valText = new FormattedText(
                            ri.SourceItem.Value.ToString("F0"),
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            ZeroWpfTheme.RegularTypeface,
                            9.0,
                            ZeroWpfTheme.TextSecondary,
                            1.0);
                        dc.DrawText(valText, new Point(ri.DotCenterX + r + 6.0, ri.DotCenterY - (valText.Height / 2.0)));
                    }
                }
                else
                {
                    // Category Label
                    var catText = new FormattedText(
                        ri.SourceItem.Category,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.RegularTypeface,
                        9.5,
                        ZeroWpfTheme.TextPrimary,
                        1.0);
                    dc.DrawText(catText, new Point(ri.DotCenterX - (catText.Width / 2.0), ri.StemStartY + 6.0));

                    // Value Label
                    if (_showValueLabels)
                    {
                        var valText = new FormattedText(
                            ri.SourceItem.Value.ToString("F0"),
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            ZeroWpfTheme.RegularTypeface,
                            9.0,
                            ZeroWpfTheme.TextSecondary,
                            1.0);
                        dc.DrawText(valText, new Point(ri.DotCenterX - (valText.Width / 2.0), ri.DotCenterY - r - valText.Height - 3.0));
                    }
                }
            }
        }

        private static List<LollipopItem> GetPreviewItems()
        {
            return new List<LollipopItem>
            {
                new LollipopItem("Alpha", 50),
                new LollipopItem("Beta", 75),
                new LollipopItem("Gamma", 40),
                new LollipopItem("Delta", 90)
            };
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
    /// Legacy alias for <see cref="ZLollipopChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LollipopChart is deprecated and will be removed in 5 release cycles. Please migrate to ZLollipopChart instead.")]
    public class LollipopChart : ZLollipopChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZLollipopChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLollipopChart is deprecated and will be removed in 5 release cycles. Please migrate to ZLollipopChart instead.")]
    public class ZeroLollipopChart : ZLollipopChart { }

    #endregion

}
