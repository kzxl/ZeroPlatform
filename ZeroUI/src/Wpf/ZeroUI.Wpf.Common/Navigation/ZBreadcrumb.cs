using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Navigation
{
    /// <summary>
    /// Interactive hierarchical path navigator supporting segmented crumb buttons,
    /// chevrons, direct path string editing, back/forward history, and theme reactivity.
    /// </summary>
    public class ZBreadcrumb : FrameworkElement
    {
        private readonly ObservableCollection<BreadcrumbItem> _items = new ObservableCollection<BreadcrumbItem>();
        private readonly Stack<string> _backHistory = new Stack<string>();
        private readonly Stack<string> _forwardHistory = new Stack<string>();

        private string _separator = "›";
        private int _hoveredIndex = -1;
        private int _hoveredChevronIndex = -1;
        private readonly List<Rect> _crumbBounds = new List<Rect>();
        private readonly List<Rect> _chevronBounds = new List<Rect>();

        public ObservableCollection<BreadcrumbItem> Items => _items;

        public event EventHandler<BreadcrumbItem>? ItemClicked;
        public event EventHandler<string>? PathChanged;
        public event EventHandler<int>? ChevronClicked;

        public string Separator
        {
            get => _separator;
            set { _separator = value ?? "›"; InvalidateVisual(); }
        }

        public bool CanGoBack => _backHistory.Count > 0;
        public bool CanGoForward => _forwardHistory.Count > 0;

        public string Path
        {
            get
            {
                var list = new List<string>();
                for (int i = 0; i < _items.Count; i++) list.Add(_items[i].DisplayText);
                return string.Join(" / ", list);
            }
            set
            {
                string oldPath = Path;
                if (oldPath != value && !string.IsNullOrEmpty(oldPath))
                {
                    _backHistory.Push(oldPath);
                    _forwardHistory.Clear();
                }

                SetPathInternal(value);
            }
        }

        private void SetPathInternal(string? value)
        {
            _items.Clear();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var parts = value!.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();
                    _items.Add(new BreadcrumbItem(part, part));
                }
            }
            InvalidateVisual();
            PathChanged?.Invoke(this, Path);
        }

        public bool Back()
        {
            if (_backHistory.Count == 0) return false;
            _forwardHistory.Push(Path);
            string prev = _backHistory.Pop();
            SetPathInternal(prev);
            return true;
        }

        public bool Forward()
        {
            if (_forwardHistory.Count == 0) return false;
            _backHistory.Push(Path);
            string next = _forwardHistory.Pop();
            SetPathInternal(next);
            return true;
        }

        public ZBreadcrumb()
        {
            Height = 32;
            Focusable = true;
            ClipToBounds = true;
            _items.CollectionChanged += (s, e) => InvalidateVisual();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(Math.Min(availableSize.Width, 400), 32);
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 0 || height <= 0) return;

#if NETFRAMEWORK
            double dpi = 1.0;
#else
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
#endif
            _crumbBounds.Clear();
            _chevronBounds.Clear();

            // Background Card
            dc.DrawRectangle(ZeroWpfTheme.BgCard, null, new Rect(0, 0, width, height));
            dc.DrawLine(ZeroWpfTheme.BorderPen, new Point(0, height - 0.5), new Point(width, height - 0.5));

            double curX = 10.0;

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                bool isLast = (i == _items.Count - 1);
                bool isHovered = (i == _hoveredIndex);

                var tf = isLast ? ZeroWpfTheme.BoldTypeface : ZeroWpfTheme.RegularTypeface;
                var brush = isLast
                    ? ZeroWpfTheme.PrimaryAccent
                    : (isHovered ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextSecondary);

                var ft = CreateFormattedText(item.DisplayText, tf, 12.0, brush, dpi);

                double crumbWidth = ft.Width + 14;
                Rect crumbRect = new Rect(curX, (height - 22) / 2.0, crumbWidth, 22);
                _crumbBounds.Add(crumbRect);

                // Hover Pill
                if (isHovered && !isLast)
                {
                    dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, crumbRect, 4, 4);
                }

                // Crumb Text
                dc.DrawText(ft, new Point(curX + 7, (height - ft.Height) / 2.0));
                curX += crumbWidth;

                // Separator
                if (!isLast)
                {
                    var sepFt = CreateFormattedText(_separator, ZeroWpfTheme.BoldTypeface, 11.0, ZeroWpfTheme.TextMuted, dpi);
                    Rect sepRect = new Rect(curX + 1, (height - 20) / 2.0, sepFt.Width + 12, 20);
                    _chevronBounds.Add(sepRect);

                    if (i == _hoveredChevronIndex)
                    {
                        dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, sepRect, 3, 3);
                    }

                    dc.DrawText(sepFt, new Point(curX + 6, (height - sepFt.Height) / 2.0));
                    curX += sepRect.Width;
                }
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Point pt = e.GetPosition(this);

            // Check chevron click
            for (int c = 0; c < _chevronBounds.Count; c++)
            {
                if (_chevronBounds[c].Contains(pt))
                {
                    ChevronClicked?.Invoke(this, c);
                    return;
                }
            }

            // Check crumb click
            for (int i = 0; i < _crumbBounds.Count; i++)
            {
                if (_crumbBounds[i].Contains(pt) && i < _items.Count)
                {
                    var clickedItem = _items[i];
                    ItemClicked?.Invoke(this, clickedItem);

                    if (i < _items.Count - 1)
                    {
                        _backHistory.Push(Path);
                        _forwardHistory.Clear();

                        while (_items.Count > i + 1)
                        {
                            _items.RemoveAt(_items.Count - 1);
                        }

                        InvalidateVisual();
                        PathChanged?.Invoke(this, Path);
                    }
                    break;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point pt = e.GetPosition(this);

            int newHoveredCrumb = -1;
            for (int i = 0; i < _crumbBounds.Count; i++)
            {
                if (_crumbBounds[i].Contains(pt))
                {
                    newHoveredCrumb = i;
                    break;
                }
            }

            int newHoveredChevron = -1;
            for (int c = 0; c < _chevronBounds.Count; c++)
            {
                if (_chevronBounds[c].Contains(pt))
                {
                    newHoveredChevron = c;
                    break;
                }
            }

            if (_hoveredIndex != newHoveredCrumb || _hoveredChevronIndex != newHoveredChevron)
            {
                _hoveredIndex = newHoveredCrumb;
                _hoveredChevronIndex = newHoveredChevron;
                Cursor = (_hoveredIndex >= 0 && _hoveredIndex < _items.Count - 1) || _hoveredChevronIndex >= 0 ? Cursors.Hand : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredIndex != -1 || _hoveredChevronIndex != -1)
            {
                _hoveredIndex = -1;
                _hoveredChevronIndex = -1;
                Cursor = Cursors.Arrow;
                InvalidateVisual();
            }
        }

        private static FormattedText CreateFormattedText(string text, Typeface typeface, double fontSize, Brush brush, double dpi)
        {
#if NETFRAMEWORK
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush);
#else
            return new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontSize,
                brush,
                dpi);
#endif
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

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZBreadcrumb"/>.
    /// </summary>
    [Obsolete("BreadcrumbControl is deprecated and will be removed in 5 release cycles. Please migrate to ZBreadcrumb instead.")]
    public class BreadcrumbControl : ZBreadcrumb
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZBreadcrumb"/>.
    /// </summary>
    [Obsolete("ZeroBreadcrumb is deprecated and will be removed in 5 release cycles. Please migrate to ZBreadcrumb instead.")]
    public class ZeroBreadcrumb : ZBreadcrumb
    {
    }
}
