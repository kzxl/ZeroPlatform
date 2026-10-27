using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Analytics;
using ZeroUI.Core.Layout;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Charts
{
    /// <summary>
    /// High-performance Squarified Treemap chart for nested proportional and hierarchical data visualization in WPF.
    /// Thin WPF View layer rendering tiles computed by ZeroUI.Core.Analytics.TreemapEngine.
    /// </summary>
    public class ZTreemapChart : FrameworkElement
    {
        private readonly List<TreemapItem> _items = new List<TreemapItem>();
        private string _title = "Asset Allocation & Proportional Breakdown";
        private string _valuePrefix = "$";
        private string _valueSuffix = "";
        private int _hoverIndex = -1;
        private IReadOnlyList<TreeMapRect<TreemapItem>> _computedLayout = Array.Empty<TreeMapRect<TreemapItem>>();

        public List<TreemapItem> Items => _items;

        public string Title
        {
            get => _title;
            set { _title = value ?? string.Empty; InvalidateVisual(); }
        }

        public string ValuePrefix
        {
            get => _valuePrefix;
            set { _valuePrefix = value ?? string.Empty; InvalidateVisual(); }
        }

        public string ValueSuffix
        {
            get => _valueSuffix;
            set { _valueSuffix = value ?? string.Empty; InvalidateVisual(); }
        }

        public ZTreemapChart()
        {
            ClipToBounds = true;
            MinHeight = 220;
            MinWidth = 300;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public TreemapItem AddItem(string label, double value, Color color, string category = "")
        {
            uint rgba = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | (uint)color.B;
            var item = new TreemapItem(label, value, category, rgba);
            _items.Add(item);
            InvalidateVisual();
            return item;
        }

        public TreemapItem AddItem(string label, double value, string category = "")
        {
            var item = new TreemapItem(label, value, category);
            _items.Add(item);
            InvalidateVisual();
            return item;
        }

        public void Clear()
        {
            _items.Clear();
            _computedLayout = Array.Empty<TreeMapRect<TreemapItem>>();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 10 || h <= 10) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, w, h));

            double titleH = string.IsNullOrEmpty(_title) ? 6 : 30;
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
                dc.DrawText(titleText, new Point(12, 8));
            }

            double plotX = 10;
            double plotY = titleH;
            double plotW = Math.Max(0, w - 20);
            double plotH = Math.Max(0, h - titleH - 10);
            if (plotW <= 10 || plotH <= 10 || _items.Count == 0) return;

            _computedLayout = TreemapEngine.ComputeLayout(_items, 0, 0, plotW, plotH, padding: 3.0);
            double totalWeight = _items.Sum(x => Math.Max(0.0, x.Value));

            for (int i = 0; i < _computedLayout.Count; i++)
            {
                var r = _computedLayout[i];
                double rx = plotX + r.X;
                double ry = plotY + r.Y;
                double rw = r.Width;
                double rh = r.Height;
                if (rw < 2 || rh < 2) continue;

                var tileRect = new Rect(rx, ry, rw, rh);
                bool isHover = (i == _hoverIndex);

                Color baseColor = r.Item.ColorRgba.ToColor(Color.FromRgb(79, 70, 229));
                Color topColor = isHover ? Color.FromArgb(255, (byte)Math.Min(255, baseColor.R + 40), (byte)Math.Min(255, baseColor.G + 40), (byte)Math.Min(255, baseColor.B + 40)) : baseColor;
                Color botColor = Color.FromArgb(255, (byte)Math.Max(0, baseColor.R - 30), (byte)Math.Max(0, baseColor.G - 30), (byte)Math.Max(0, baseColor.B - 30));

                var grad = new LinearGradientBrush(topColor, botColor, new Point(0, 0), new Point(0, 1));
                grad.Freeze();

                var pen = isHover ? new Pen(Brushes.White, 2.0) : new Pen(new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1.0);
                if (pen.CanFreeze) pen.Freeze();

                dc.DrawRectangle(grad, pen, tileRect);

                if (rw >= 40 && rh >= 30)
                {
                    dc.PushClip(new RectangleGeometry(tileRect));

                    double pct = totalWeight > 0 ? (r.Item.Value / totalWeight) * 100.0 : 0.0;
                    string valText = $"{_valuePrefix}{r.Item.Value:N0}{_valueSuffix} ({pct:F1}%)";

                    var labelText = new FormattedText(
                        r.Item.Label,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        ZeroWpfTheme.BoldTypeface,
                        11,
                        Brushes.White,
                        1.0);
                    dc.DrawText(labelText, new Point(rx + 6, ry + 5));

                    if (rh >= 45)
                    {
                        var subText = new FormattedText(
                            valText,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            ZeroWpfTheme.RegularTypeface,
                            9.5,
                            new SolidColorBrush(Color.FromArgb(210, 255, 255, 255)),
                            1.0);
                        dc.DrawText(subText, new Point(rx + 6, ry + 22));
                    }

                    dc.Pop();
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pt = e.GetPosition(this);
            int newHover = -1;

            double titleH = string.IsNullOrEmpty(_title) ? 6 : 30;
            double px = pt.X - 10;
            double py = pt.Y - titleH;

            for (int i = 0; i < _computedLayout.Count; i++)
            {
                var r = _computedLayout[i];
                if (px >= r.X && px <= r.X + r.Width && py >= r.Y && py <= r.Y + r.Height)
                {
                    newHover = i;
                    break;
                }
            }

            if (newHover != _hoverIndex)
            {
                _hoverIndex = newHover;
                InvalidateVisual();

                if (_hoverIndex >= 0 && _hoverIndex < _computedLayout.Count)
                {
                    var item = _computedLayout[_hoverIndex].Item;
                    double totalWeight = _items.Sum(x => Math.Max(0.0, x.Value));
                    double pct = totalWeight > 0 ? (item.Value / totalWeight) * 100.0 : 0.0;

                    string tip = $"{item.Label}\n" +
                                 (string.IsNullOrEmpty(item.Category) ? "" : $"Category: {item.Category}\n") +
                                 $"Value: {_valuePrefix}{item.Value:N2}{_valueSuffix}\n" +
                                 $"Share: {pct:F2}%";
                    ToolTip = tip;
                    Cursor = Cursors.Hand;
                }
                else
                {
                    ToolTip = null;
                    Cursor = Cursors.Arrow;
                }
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                ToolTip = null;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
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
    /// Legacy alias for <see cref="ZTreemapChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("TreemapChart is deprecated and will be removed in 5 release cycles. Please migrate to ZTreemapChart instead.")]
    public class TreemapChart : ZTreemapChart { }

    /// <summary>
    /// Legacy alias for <see cref="ZTreemapChart"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroTreemapChart is deprecated and will be removed in 5 release cycles. Please migrate to ZTreemapChart instead.")]
    public class ZeroTreemapChart : ZTreemapChart { }

    #endregion

}
