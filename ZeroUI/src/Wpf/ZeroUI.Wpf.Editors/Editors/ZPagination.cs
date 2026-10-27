using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    internal enum WpfPagerButtonKind
    {
        First,
        Prev,
        PageNumber,
        Ellipsis,
        Next,
        Last
    }

    internal class WpfPagerButton
    {
        public WpfPagerButtonKind Kind { get; set; }
        public int PageNumber { get; set; }
        public string Text { get; set; } = string.Empty;
        public Rect Bounds { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsActive { get; set; } = false;
    }

    /// <summary>
    /// Modern, high-performance pagination control for WPF backed by headless PaginationModel.
    /// Provides zero-allocation page boundary math, windowed page lists, and dynamic theme reactivity.
    /// </summary>
    public class ZPagination : FrameworkElement
    {
        private readonly PaginationModel _model = new PaginationModel();
        private readonly List<WpfPagerButton> _buttons = new List<WpfPagerButton>();
        private int _hoveredIndex = -1;
        private int _pressedIndex = -1;

        #region Dependency Properties

        public static readonly DependencyProperty TotalCountProperty =
            DependencyProperty.Register(nameof(TotalCount), typeof(int), typeof(ZPagination),
                new FrameworkPropertyMetadata(0, OnTotalCountChanged));

        public static readonly DependencyProperty PageSizeProperty =
            DependencyProperty.Register(nameof(PageSize), typeof(int), typeof(ZPagination),
                new FrameworkPropertyMetadata(20, OnPageSizeChanged));

        public static readonly DependencyProperty CurrentPageProperty =
            DependencyProperty.Register(nameof(CurrentPage), typeof(int), typeof(ZPagination),
                new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnCurrentPageChanged));

        #endregion

        #region Events & Properties

        public event EventHandler<int>? PageChanged;
        public event EventHandler<int>? PageSizeChanged;

        public PaginationModel Model => _model;

        public int TotalCount
        {
            get => (int)GetValue(TotalCountProperty);
            set => SetValue(TotalCountProperty, value);
        }

        public int PageSize
        {
            get => (int)GetValue(PageSizeProperty);
            set => SetValue(PageSizeProperty, value);
        }

        public int CurrentPage
        {
            get => (int)GetValue(CurrentPageProperty);
            set => SetValue(CurrentPageProperty, value);
        }

        public int TotalPages => _model.TotalPages;
        public void GoToPage(int page) => _model.GoToPage(page);
        public void NextPage() => _model.NextPage();
        public void PrevPage() => _model.PrevPage();
        public void FirstPage() => _model.FirstPage();
        public void LastPage() => _model.LastPage();

        #endregion

        public ZPagination()
        {
            Height = 36;
            Cursor = Cursors.Arrow;
            Focusable = true;

            _model.PageChanged += (s, page) =>
            {
                if (CurrentPage != page) CurrentPage = page;
                RecalculateLayout(ActualWidth, ActualHeight);
                InvalidateVisual();
                PageChanged?.Invoke(this, page);
            };

            _model.PageSizeChanged += (s, size) =>
            {
                if (PageSize != size) PageSize = size;
                RecalculateLayout(ActualWidth, ActualHeight);
                InvalidateVisual();
                PageSizeChanged?.Invoke(this, size);
            };

            _model.StateChanged += (s, e) =>
            {
                RecalculateLayout(ActualWidth, ActualHeight);
                InvalidateVisual();
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnTotalCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPagination p) p._model.TotalCount = (int)e.NewValue;
        }

        private static void OnPageSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPagination p) p._model.PageSize = (int)e.NewValue;
        }

        private static void OnCurrentPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPagination p) p._model.GoToPage((int)e.NewValue);
        }

        #region Measurement & Layout

        protected override Size MeasureOverride(Size availableSize)
        {
            double minWidth = 350;
            double height = double.IsNaN(Height) ? 36 : Height;
            return new Size(
                double.IsPositiveInfinity(availableSize.Width) ? minWidth : Math.Min(minWidth, availableSize.Width),
                double.IsPositiveInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            RecalculateLayout(finalSize.Width, finalSize.Height);
            return finalSize;
        }

        private void RecalculateLayout(double width, double height)
        {
            _buttons.Clear();
            if (width <= 0 || height <= 0) return;

            double btnH = 28;
            double btnY = (height - btnH) / 2.0;
            double currentRight = width - 14;

            var cluster = new List<WpfPagerButton>
            {
                new WpfPagerButton { Kind = WpfPagerButtonKind.Last, Text = "⏭", IsEnabled = _model.CanLast },
                new WpfPagerButton { Kind = WpfPagerButtonKind.Next, Text = "▶", IsEnabled = _model.CanNext }
            };

            int[] pages = _model.GetVisiblePages(7);
            for (int i = pages.Length - 1; i >= 0; i--)
            {
                int p = pages[i];
                if (p == -1)
                {
                    cluster.Add(new WpfPagerButton { Kind = WpfPagerButtonKind.Ellipsis, Text = "...", IsEnabled = false });
                }
                else
                {
                    cluster.Add(new WpfPagerButton
                    {
                        Kind = WpfPagerButtonKind.PageNumber,
                        PageNumber = p,
                        Text = p.ToString(),
                        IsActive = (p == _model.CurrentPage)
                    });
                }
            }

            cluster.Add(new WpfPagerButton { Kind = WpfPagerButtonKind.Prev, Text = "◀", IsEnabled = _model.CanPrev });
            cluster.Add(new WpfPagerButton { Kind = WpfPagerButtonKind.First, Text = "⏮", IsEnabled = _model.CanFirst });

            double btnW = 32;
            for (int i = 0; i < cluster.Count; i++)
            {
                var btn = cluster[i];
                currentRight -= btnW;
                btn.Bounds = new Rect(currentRight, btnY, btnW, btnH);
                _buttons.Add(btn);
            }
        }

        #endregion

        #region Input Handling

        private int HitTest(Point pos)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                if (_buttons[i].Bounds.Contains(pos))
                {
                    if (_buttons[i].IsEnabled && _buttons[i].Kind != WpfPagerButtonKind.Ellipsis)
                    {
                        return i;
                    }
                    return -1;
                }
            }
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.GetPosition(this));
            if (_hoveredIndex != idx)
            {
                _hoveredIndex = idx;
                Cursor = (_hoveredIndex >= 0) ? Cursors.Hand : Cursors.Arrow;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _pressedIndex = -1;
            Cursor = Cursors.Arrow;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed && IsEnabled)
            {
                Focus();
                _pressedIndex = HitTest(e.GetPosition(this));
                if (_pressedIndex >= 0) InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressedIndex >= 0)
            {
                int released = HitTest(e.GetPosition(this));
                if (released == _pressedIndex)
                {
                    ExecuteAction(_buttons[released]);
                }
                _pressedIndex = -1;
                InvalidateVisual();
                e.Handled = true;
            }
        }

        private void ExecuteAction(WpfPagerButton btn)
        {
            switch (btn.Kind)
            {
                case WpfPagerButtonKind.First: _model.FirstPage(); break;
                case WpfPagerButtonKind.Prev: _model.PrevPage(); break;
                case WpfPagerButtonKind.Next: _model.NextPage(); break;
                case WpfPagerButtonKind.Last: _model.LastPage(); break;
                case WpfPagerButtonKind.PageNumber: _model.GoToPage(btn.PageNumber); break;
            }
        }

        #endregion

        #region Rendering Pipeline

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            RecalculateLayout(ActualWidth, ActualHeight);

            // 1. Draw Top Border
            Pen borderPen = new Pen(ZeroWpfTheme.BorderDefault, 1.0);
            dc.DrawLine(borderPen, new Point(0, 0), new Point(ActualWidth, 0));

            // 2. Summary Text
            string summary = _model.TotalCount == 0
                ? "No items"
                : $"Showing {_model.StartItemIndex:N0} - {_model.EndItemIndex:N0} of {_model.TotalCount:N0} items";

            var summaryText = new FormattedText(
                summary,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                12.0,
                ZeroWpfTheme.TextSecondary,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double summaryY = (ActualHeight - summaryText.Height) / 2.0;
            dc.DrawText(summaryText, new Point(14, summaryY));

            // 3. Draw Buttons
            for (int i = 0; i < _buttons.Count; i++)
            {
                var btn = _buttons[i];
                bool isHovered = (_hoveredIndex == i);
                bool isPressed = (_pressedIndex == i);

                if (btn.IsActive)
                {
                    dc.DrawRoundedRectangle(ZeroWpfTheme.PrimaryAccent, null, btn.Bounds, 4, 4);

                    var text = new FormattedText(
                        btn.Text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        12.0,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double tx = btn.Bounds.X + (btn.Bounds.Width - text.Width) / 2.0;
                    double ty = btn.Bounds.Y + (btn.Bounds.Height - text.Height) / 2.0;
                    dc.DrawText(text, new Point(tx, ty));
                }
                else
                {
                    if (isHovered || isPressed)
                    {
                        dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, btn.Bounds, 4, 4);
                    }

                    Brush fg = btn.IsEnabled ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextMuted;
                    var text = new FormattedText(
                        btn.Text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                        12.0,
                        fg,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double tx = btn.Bounds.X + (btn.Bounds.Width - text.Width) / 2.0;
                    double ty = btn.Bounds.Y + (btn.Bounds.Height - text.Height) / 2.0;
                    dc.DrawText(text, new Point(tx, ty));
                }
            }
        }

        #endregion
    
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
    /// Convenience alias for <see cref="ZPagination"/>.
    /// </summary>
    public class ZPaginationControl : ZPagination { }

    /// <summary>
    /// Legacy alias for <see cref="ZPagination"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PaginationControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPagination instead.")]
    public class PaginationControl : ZPagination { }

    /// <summary>
    /// Legacy alias for <see cref="ZPagination"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPaginationControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPagination instead.")]
    public class ZeroPaginationControl : ZPagination { }

    #endregion

}
