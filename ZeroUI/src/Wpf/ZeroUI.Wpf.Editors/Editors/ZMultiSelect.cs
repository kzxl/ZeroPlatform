using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern chip-tag multi-selection dropdown control for WPF with tag removal,
    /// selection limits, vector rendering, and dynamic theme switching.
    /// </summary>
    public class ZMultiSelect : Control
    {
        private object? _itemsSource;
        private List<object> _selectedItems = new List<object>();
        private string? _displayMember;
        private string _placeholder = "Select items...";
        private int _maxSelections = 0;

        private readonly List<Rect> _chipRects = new List<Rect>();
        private readonly List<Rect> _chipCloseRects = new List<Rect>();
        private int _hoveredChipIndex = -1;
        private int _hoveredCloseIndex = -1;
        private bool _isHovered = false;

        public event EventHandler? SelectionChanged;

        public object? ItemsSource
        {
            get => _itemsSource;
            set
            {
                _itemsSource = value;
                InvalidateVisual();
            }
        }

        public List<object> SelectedItems
        {
            get => _selectedItems;
            set
            {
                _selectedItems = value ?? new List<object>();
                InvalidateVisual();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string? DisplayMember
        {
            get => _displayMember;
            set
            {
                _displayMember = value;
                InvalidateVisual();
            }
        }

        public string Placeholder
        {
            get => _placeholder;
            set
            {
                _placeholder = value ?? string.Empty;
                InvalidateVisual();
            }
        }

        public int MaxSelections
        {
            get => _maxSelections;
            set
            {
                _maxSelections = Math.Max(0, value);
                if (_maxSelections > 0 && _selectedItems.Count > _maxSelections)
                {
                    _selectedItems = _selectedItems.Take(_maxSelections).ToList();
                    InvalidateVisual();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        static ZMultiSelect()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZMultiSelect), new FrameworkPropertyMetadata(typeof(ZMultiSelect)));
        }

        public ZMultiSelect()
        {
            Height = 36;
            Width = 240;
            Cursor = Cursors.Hand;
            Focusable = true;

            Loaded += (s, e) => ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            Unloaded += (s, e) => ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    Dispatcher.BeginInvoke((Action)OnThemeChanged);
                return;
            }
            InvalidateVisual();
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            InvalidateVisual();
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _hoveredChipIndex = -1;
            _hoveredCloseIndex = -1;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pos = e.GetPosition(this);
            int prevChip = _hoveredChipIndex;
            int prevClose = _hoveredCloseIndex;

            _hoveredChipIndex = -1;
            _hoveredCloseIndex = -1;

            for (int i = 0; i < _chipRects.Count; i++)
            {
                if (_chipRects[i].Contains(pos))
                {
                    _hoveredChipIndex = i;
                    if (i < _chipCloseRects.Count && _chipCloseRects[i].Contains(pos))
                    {
                        _hoveredCloseIndex = i;
                    }
                    break;
                }
            }

            if (prevChip != _hoveredChipIndex || prevClose != _hoveredCloseIndex)
            {
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            Focus();
            var pos = e.GetPosition(this);

            // Check if user clicked an 'x' on a chip
            for (int i = 0; i < _chipCloseRects.Count; i++)
            {
                if (_chipCloseRects[i].Contains(pos))
                {
                    RemoveItemAt(i);
                    e.Handled = true;
                    return;
                }
            }
        }

        public void RemoveItemAt(int index)
        {
            if (index >= 0 && index < _selectedItems.Count)
            {
                _selectedItems.RemoveAt(index);
                InvalidateVisual();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private string GetItemDisplayText(object? item)
        {
            if (item == null) return string.Empty;
            if (!string.IsNullOrEmpty(_displayMember))
            {
                var prop = item.GetType().GetProperty(_displayMember);
                if (prop != null)
                {
                    var val = prop.GetValue(item);
                    return val?.ToString() ?? string.Empty;
                }
            }
            return item.ToString() ?? string.Empty;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            // Background
            dc.DrawRectangle(ZeroWpfTheme.BgInput, null, bounds);

            // Border
            var borderPen = IsFocused ? ZeroWpfTheme.AccentPen : (_isHovered ? ZeroWpfTheme.BorderPen : ZeroWpfTheme.GridLinePen);
            dc.DrawRectangle(null, borderPen, bounds);

            // Dropdown Chevron
            double arrowRight = bounds.Right - 12;
            double arrowCenterY = bounds.Height / 2;
            var arrowGeom = new StreamGeometry();
            using (var ctx = arrowGeom.Open())
            {
                ctx.BeginFigure(new Point(arrowRight - 4, arrowCenterY - 2), true, true);
                ctx.LineTo(new Point(arrowRight + 4, arrowCenterY - 2), true, false);
                ctx.LineTo(new Point(arrowRight, arrowCenterY + 3), true, false);
            }
            arrowGeom.Freeze();
            dc.DrawGeometry(ZeroWpfTheme.TextMuted, null, arrowGeom);

            // Chips / Placeholder
            _chipRects.Clear();
            _chipCloseRects.Clear();

            double usableWidth = bounds.Width - 28;
            double curX = 6;
            double curY = 4;
            double chipH = bounds.Height - 8;

            var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);

            if (_selectedItems.Count == 0)
            {
                var formattedPlaceholder = new FormattedText(
                    _placeholder,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    12.0,
                    ZeroWpfTheme.TextMuted,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(formattedPlaceholder, new Point(8, (bounds.Height - formattedPlaceholder.Height) / 2));
            }
            else
            {
                var chipBgBrush = new SolidColorBrush(Color.FromArgb(40, 59, 130, 246));
                chipBgBrush.Freeze();
                var chipBorderPen = new Pen(ZeroWpfTheme.PrimaryAccent, 1.0);
                chipBorderPen.Freeze();

                for (int i = 0; i < _selectedItems.Count; i++)
                {
                    string text = GetItemDisplayText(_selectedItems[i]);
                    var formattedText = new FormattedText(
                        text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        11.5,
                        ZeroWpfTheme.TextPrimary,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double chipW = formattedText.Width + 24;
                    if (curX + chipW > usableWidth && i > 0)
                    {
                        // Overflow badge e.g. "+2"
                        int remaining = _selectedItems.Count - i;
                        string moreText = $"+{remaining}";
                        var formattedMore = new FormattedText(
                            moreText,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            typeface,
                            11.5,
                            ZeroWpfTheme.PrimaryAccent,
                            VisualTreeHelper.GetDpi(this).PixelsPerDip);

                        var moreRect = new Rect(curX, curY, formattedMore.Width + 10, chipH);
                        dc.DrawRoundedRectangle(chipBgBrush, chipBorderPen, moreRect, 4, 4);
                        dc.DrawText(formattedMore, new Point(moreRect.X + 5, moreRect.Y + (chipH - formattedMore.Height) / 2));
                        break;
                    }

                    var chipRect = new Rect(curX, curY, chipW, chipH);
                    _chipRects.Add(chipRect);

                    dc.DrawRoundedRectangle(chipBgBrush, chipBorderPen, chipRect, 4, 4);
                    dc.DrawText(formattedText, new Point(chipRect.X + 6, chipRect.Y + (chipH - formattedText.Height) / 2));

                    // Close '×' button
                    double closeSize = 12;
                    var closeRect = new Rect(chipRect.Right - closeSize - 4, chipRect.Y + (chipH - closeSize) / 2, closeSize, closeSize);
                    _chipCloseRects.Add(closeRect);

                    var closeText = new FormattedText(
                        "×",
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        typeface,
                        11.0,
                        (_hoveredCloseIndex == i) ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.TextMuted,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    dc.DrawText(closeText, new Point(closeRect.X + (closeSize - closeText.Width) / 2, closeRect.Y + (closeSize - closeText.Height) / 2));

                    curX += chipW + 4;
                }
            }
        }
    }

    [Obsolete("MultiSelect is deprecated and will be removed in 5 release cycles. Please migrate to ZMultiSelect instead.")]
    public class MultiSelect : ZMultiSelect { }

    [Obsolete("ZeroMultiSelect is deprecated and will be removed in 5 release cycles. Please migrate to ZMultiSelect instead.")]
    public class ZeroMultiSelect : ZMultiSelect { }
}
