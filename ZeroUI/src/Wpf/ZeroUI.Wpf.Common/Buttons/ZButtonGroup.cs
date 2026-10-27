using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern connected button group (Action Cluster / Action Strip) for WPF with vector rendering,
    /// seamless border geometry, toggle grouping, and dynamic theme reactivity.
    /// </summary>
    public class ZButtonGroup : FrameworkElement
    {
        private readonly ButtonGroupModel _model = new ButtonGroupModel();
        private readonly List<Rect> _itemBounds = new List<Rect>();
        private int _hoveredIndex = -1;
        private int _pressedIndex = -1;

        #region Dependency Properties

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(ZButtonGroup),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SelectionModeProperty =
            DependencyProperty.Register(nameof(SelectionMode), typeof(ButtonGroupSelectionMode), typeof(ZButtonGroup),
                new FrameworkPropertyMetadata(ButtonGroupSelectionMode.None, OnSelectionModeChanged));

        public static readonly DependencyProperty SizeModeProperty =
            DependencyProperty.Register(nameof(SizeMode), typeof(ButtonGroupSizeMode), typeof(ZButtonGroup),
                new FrameworkPropertyMetadata(ButtonGroupSizeMode.AutoFit, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ItemPaddingHorizontalProperty =
            DependencyProperty.Register(nameof(ItemPaddingHorizontal), typeof(double), typeof(ZButtonGroup),
                new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        #endregion

        #region Events & Properties

        public event EventHandler<ButtonGroupItemEventArgs>? ItemClick;
        public event EventHandler<ButtonGroupItem>? SelectionChanged;

        public ButtonGroupModel Model => _model;

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public ButtonGroupSelectionMode SelectionMode
        {
            get => (ButtonGroupSelectionMode)GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        public ButtonGroupSizeMode SizeMode
        {
            get => (ButtonGroupSizeMode)GetValue(SizeModeProperty);
            set => SetValue(SizeModeProperty, value);
        }

        public double ItemPaddingHorizontal
        {
            get => (double)GetValue(ItemPaddingHorizontalProperty);
            set => SetValue(ItemPaddingHorizontalProperty, value);
        }

        public IReadOnlyList<ButtonGroupItem> Items => _model.Items;

        public ButtonGroupItem? SelectedItem => _model.SelectedItem;

        #endregion

        public ZButtonGroup()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
            Height = 32;

            _model.ItemClicked += (s, e) => ItemClick?.Invoke(this, e);
            _model.SelectionChanged += (s, item) => SelectionChanged?.Invoke(this, item);
            _model.ItemsChanged += (s, e) =>
            {
                InvalidateMeasure();
                InvalidateVisual();
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnSelectionModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZButtonGroup group)
            {
                group._model.SelectionMode = (ButtonGroupSelectionMode)e.NewValue;
            }
        }

        #region Builder API

        public ButtonGroupItem AddButton(string id, string text, string? iconGlyph = null, ButtonGroupItemStyle style = ButtonGroupItemStyle.Secondary, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.Push, action)
            {
                Style = style
            };
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddToggle(string id, string text, string? iconGlyph = null, bool isChecked = false, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.Toggle, action)
            {
                IsChecked = isChecked,
                Style = ButtonGroupItemStyle.Secondary
            };
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddDropDown(string id, string text, string? iconGlyph = null, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.DropDown, action);
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddDropDown(string id, string text, string? iconGlyph, object? dropDownMenu, Action<ButtonGroupItem>? action = null)
        {
            var item = new ButtonGroupItem(id, text, iconGlyph, ButtonGroupItemType.DropDown, action, dropDownMenu);
            _model.Add(item);
            return item;
        }

        public ButtonGroupItem AddSeparator()
        {
            var item = new ButtonGroupItem(Guid.NewGuid().ToString("N"), string.Empty, null, ButtonGroupItemType.Separator);
            _model.Add(item);
            return item;
        }

        public void Clear() => _model.Clear();

        #endregion

        #region Measurement & Layout

        private double MeasureItemWidth(ButtonGroupItem item)
        {
            if (item.Type == ButtonGroupItemType.Separator)
            {
                return 6.0;
            }

            string caption = string.IsNullOrEmpty(item.IconGlyph)
                ? item.Text
                : $"{item.IconGlyph}  {item.Text}".Trim();

            if (item.Type == ButtonGroupItemType.DropDown)
            {
                caption += " ▾";
            }

            double pad = item.CustomPaddingHorizontal.HasValue
                ? (double)item.CustomPaddingHorizontal.Value
                : ItemPaddingHorizontal;

            double textW = 0.0;
            if (!string.IsNullOrEmpty(caption))
            {
                var ft = new FormattedText(
                    caption,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, item.IsChecked ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                    12.0,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                textW = ft.Width;
            }

            double w = textW + (pad * 2.0);

            if (!string.IsNullOrEmpty(item.BadgeText))
            {
                var badgeFt = new FormattedText(
                    item.BadgeText,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    10.0,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                w += Math.Max(16.0, badgeFt.Width + 8.0) + 6.0;
            }
            else if (item.ShowBadgeDot)
            {
                w += 10.0;
            }

            return Math.Max(item.MinWidth > 0 ? (double)item.MinWidth : 32.0, w);
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (_model.Count == 0)
            {
                return new Size(64.0, double.IsNaN(Height) ? 32.0 : Height);
            }

            double totalW = 0;
            for (int i = 0; i < _model.Count; i++)
            {
                var item = _model[i];
                if (!item.IsVisible) continue;
                totalW += MeasureItemWidth(item);
            }

            double height = double.IsNaN(Height) ? 32.0 : Height;
            return new Size(
                double.IsPositiveInfinity(availableSize.Width) ? totalW : Math.Min(totalW, availableSize.Width),
                double.IsPositiveInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height));
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            RecalculateLayout(finalSize.Width, finalSize.Height);
            return finalSize;
        }

        private void RecalculateLayout(double width, double height)
        {
            _itemBounds.Clear();
            if (_model.Count == 0 || width <= 0 || height <= 0) return;

            int visibleCount = 0;
            for (int i = 0; i < _model.Count; i++)
            {
                if (_model[i].IsVisible) visibleCount++;
            }
            if (visibleCount == 0) return;

            double currentX = 0;

            if (SizeMode == ButtonGroupSizeMode.EqualWidth)
            {
                double itemW = width / visibleCount;
                for (int i = 0; i < _model.Count; i++)
                {
                    var item = _model[i];
                    if (!item.IsVisible)
                    {
                        _itemBounds.Add(Rect.Empty);
                        continue;
                    }

                    double w = (i == _model.Count - 1) ? Math.Max(1.0, width - currentX) : itemW;
                    _itemBounds.Add(new Rect(currentX, 0, w, height));
                    currentX += w;
                }
            }
            else
            {
                var measuredWidths = new double[_model.Count];
                double totalMeasuredWidth = 0.0;

                for (int i = 0; i < _model.Count; i++)
                {
                    var item = _model[i];
                    if (!item.IsVisible)
                    {
                        measuredWidths[i] = 0.0;
                        continue;
                    }

                    measuredWidths[i] = MeasureItemWidth(item);
                    totalMeasuredWidth += measuredWidths[i];
                }

                if (SizeMode == ButtonGroupSizeMode.Fill && totalMeasuredWidth > 0.0 && totalMeasuredWidth < width)
                {
                    double scaleFactor = width / totalMeasuredWidth;
                    for (int i = 0; i < _model.Count; i++)
                    {
                        var item = _model[i];
                        if (!item.IsVisible)
                        {
                            _itemBounds.Add(Rect.Empty);
                            continue;
                        }

                        double w = measuredWidths[i] * scaleFactor;
                        if (i == _model.Count - 1)
                        {
                            w = Math.Max(1.0, width - currentX);
                        }

                        _itemBounds.Add(new Rect(currentX, 0, w, height));
                        currentX += w;
                    }
                }
                else
                {
                    // AutoFit: keep exact natural item dimensions
                    for (int i = 0; i < _model.Count; i++)
                    {
                        var item = _model[i];
                        if (!item.IsVisible)
                        {
                            _itemBounds.Add(Rect.Empty);
                            continue;
                        }

                        double w = measuredWidths[i];
                        _itemBounds.Add(new Rect(currentX, 0, w, height));
                        currentX += w;
                    }
                }
            }
        }

        #endregion

        #region Input Handling

        private int HitTest(Point pos)
        {
            for (int i = 0; i < _itemBounds.Count; i++)
            {
                if (_itemBounds[i].Contains(pos))
                {
                    var item = _model[i];
                    if (item.IsVisible && item.IsEnabled && item.Type != ButtonGroupItemType.Separator)
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
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _hoveredIndex = -1;
            _pressedIndex = -1;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed && IsEnabled)
            {
                Focus();
                _pressedIndex = HitTest(e.GetPosition(this));
                if (_pressedIndex >= 0)
                {
                    InvalidateVisual();
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressedIndex >= 0)
            {
                int releasedIndex = HitTest(e.GetPosition(this));
                if (releasedIndex == _pressedIndex)
                {
                    var item = _model[releasedIndex];
                    if (item.Type == ButtonGroupItemType.DropDown && item.DropDownMenu is System.Windows.Controls.ContextMenu cm)
                    {
                        cm.PlacementTarget = this;
                        cm.PlacementRectangle = _itemBounds[releasedIndex];
                        cm.IsOpen = true;
                    }

                    _model.TriggerClick(releasedIndex);
                }
                _pressedIndex = -1;
                InvalidateVisual();
                e.Handled = true;
            }
        }

        #endregion

        #region Rendering Pipeline

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0 || _model.Count == 0) return;

            RecalculateLayout(ActualWidth, ActualHeight);

            double radius = CornerRadius;
            Rect fullRect = new Rect(0, 0, ActualWidth, ActualHeight);

            // 1. Draw Container Background
            Brush bgTrack = ZeroWpfTheme.BgInput;
            Pen borderPen = new Pen(ZeroWpfTheme.BorderDefault, 1.0);

            dc.DrawRoundedRectangle(bgTrack, borderPen, fullRect, radius, radius);

            // 2. Render Items
            int lastVisible = -1;
            for (int i = 0; i < _model.Count; i++)
            {
                if (_model[i].IsVisible) lastVisible = i;
            }

            for (int i = 0; i < _model.Count; i++)
            {
                var item = _model[i];
                if (!item.IsVisible) continue;
                var bounds = _itemBounds[i];
                if (bounds.IsEmpty) continue;

                bool isHovered = (_hoveredIndex == i);
                bool isPressed = (_pressedIndex == i);

                if (item.Type == ButtonGroupItemType.Separator)
                {
                    double lineX = bounds.X + bounds.Width / 2;
                    dc.DrawLine(borderPen, new Point(lineX, bounds.Y + 6), new Point(lineX, bounds.Bottom - 6));
                    continue;
                }

                // Fill active or hovered state
                Brush? fill = GetItemBrush(item, isHovered, isPressed);
                if (fill != null)
                {
                    dc.DrawRectangle(fill, null, bounds);
                }

                // Render Text / Glyph
                string caption = string.IsNullOrEmpty(item.IconGlyph)
                    ? item.Text
                    : $"{item.IconGlyph}  {item.Text}".Trim();

                if (item.Type == ButtonGroupItemType.DropDown)
                {
                    caption += " ▾";
                }

                Brush textBrush = GetItemTextBrush(item);

                var formattedText = new FormattedText(
                    caption,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, item.IsChecked ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal),
                    12.0,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double textX = bounds.X + (bounds.Width - formattedText.Width) / 2.0;
                double textY = bounds.Y + (bounds.Height - formattedText.Height) / 2.0;

                if (!string.IsNullOrEmpty(item.BadgeText))
                {
                    textX -= 10.0;
                }

                dc.DrawText(formattedText, new Point(textX, textY));

                // Draw Badge Dot (notification / dirty indicator)
                if (item.ShowBadgeDot)
                {
                    Brush dotBrush = ParseHexBrush(item.BadgeColorHex, ZeroWpfTheme.DangerAccent);
                    Point center = new Point(bounds.Right - 8.0, bounds.Y + 7.0);
                    // Draw subtle separator ring
                    dc.DrawEllipse(bgTrack, null, center, 4.2, 4.2);
                    dc.DrawEllipse(dotBrush, null, center, 3.2, 3.2);
                }

                // Draw Badge Pill
                if (!string.IsNullOrEmpty(item.BadgeText))
                {
                    Brush badgeBg = ParseHexBrush(item.BadgeColorHex, ZeroWpfTheme.DangerAccent);
                    var badgeFt = new FormattedText(
                        item.BadgeText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        9.5,
                        Brushes.White,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip);

                    double badgeW = Math.Max(16.0, badgeFt.Width + 6.0);
                    double badgeH = 15.0;
                    Rect badgeRect = new Rect(bounds.Right - badgeW - 6.0, (bounds.Height - badgeH) / 2.0, badgeW, badgeH);

                    dc.DrawRoundedRectangle(badgeBg, null, badgeRect, 7.5, 7.5);
                    dc.DrawText(badgeFt, new Point(badgeRect.X + (badgeW - badgeFt.Width) / 2.0, badgeRect.Y + (badgeH - badgeFt.Height) / 2.0));
                }

                // Draw 1px divider between adjacent items
                if (i != lastVisible && item.Type != ButtonGroupItemType.Separator)
                {
                    dc.DrawLine(borderPen, new Point(bounds.Right, bounds.Y + 4), new Point(bounds.Right, bounds.Bottom - 4));
                }
            }

            // Re-stroke outer border to prevent internal fills from obscuring rounded edges
            dc.DrawRoundedRectangle(null, borderPen, fullRect, radius, radius);
        }

        private static Brush ParseHexBrush(string? hex, Brush fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                return brush;
            }
            catch
            {
                return fallback;
            }
        }

        private Brush GetItemTextBrush(ButtonGroupItem item)
        {
            if (item.IsChecked || item.Style == ButtonGroupItemStyle.Primary || item.Style == ButtonGroupItemStyle.Success || item.Style == ButtonGroupItemStyle.Danger)
            {
                return Brushes.White;
            }

            if (!string.IsNullOrEmpty(item.CustomForeColorHex))
            {
                return ParseHexBrush(item.CustomForeColorHex, ZeroWpfTheme.TextPrimary);
            }

            return item.IsEnabled ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextMuted;
        }

        private Brush? GetItemBrush(ButtonGroupItem item, bool isHovered, bool isPressed)
        {
            if (!item.IsEnabled || !IsEnabled) return null;

            if (item.IsChecked)
            {
                return ZeroWpfTheme.PrimaryAccent;
            }

            if (isPressed)
            {
                return ZeroWpfTheme.BgHover;
            }

            if (isHovered)
            {
                return ZeroWpfTheme.BgHover;
            }

            if (!string.IsNullOrEmpty(item.CustomBackColorHex))
            {
                return ParseHexBrush(item.CustomBackColorHex, Brushes.Transparent);
            }

            return item.Style switch
            {
                ButtonGroupItemStyle.Primary => ZeroWpfTheme.PrimaryAccent,
                ButtonGroupItemStyle.Success => ZeroWpfTheme.SuccessAccent,
                ButtonGroupItemStyle.Danger => ZeroWpfTheme.DangerAccent,
                _ => null
            };
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

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZButtonGroup"/>.
    /// </summary>
    [Obsolete("ButtonGroup is deprecated and will be removed in 5 release cycles. Please migrate to ZButtonGroup instead.")]
    public class ButtonGroup : ZButtonGroup
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZButtonGroup"/>.
    /// </summary>
    [Obsolete("ZeroButtonGroup is deprecated and will be removed in 5 release cycles. Please migrate to ZButtonGroup instead.")]
    public class ZeroButtonGroup : ZButtonGroup
    {
    }
}
