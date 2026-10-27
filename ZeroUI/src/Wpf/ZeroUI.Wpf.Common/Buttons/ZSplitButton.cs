using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern SplitButton for WPF combining default action execution with a contextual dropdown menu.
    /// Provides vector-rendered styling, sub-zone hover states, and dynamic theme reactivity.
    /// </summary>
    public class ZSplitButton : FrameworkElement
    {
        private bool _isActionHovered;
        private bool _isDropDownHovered;
        private bool _isActionPressed;
        private bool _isDropDownPressed;

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ZSplitButton),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IconGlyphProperty =
            DependencyProperty.Register(nameof(IconGlyph), typeof(string), typeof(ZSplitButton),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty VariantProperty =
            DependencyProperty.Register(nameof(Variant), typeof(ButtonVariant), typeof(ZSplitButton),
                new FrameworkPropertyMetadata(ButtonVariant.Primary, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(ZSplitButton),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SplitWidthProperty =
            DependencyProperty.Register(nameof(SplitWidth), typeof(double), typeof(ZSplitButton),
                new FrameworkPropertyMetadata(28.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DropDownMenuProperty =
            DependencyProperty.Register(nameof(DropDownMenu), typeof(ContextMenu), typeof(ZSplitButton),
                new FrameworkPropertyMetadata(null));

        #endregion

        #region Properties & Events

        public event RoutedEventHandler? ActionClick;
        public event RoutedEventHandler? DropDownClick;

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string? IconGlyph
        {
            get => (string?)GetValue(IconGlyphProperty);
            set => SetValue(IconGlyphProperty, value);
        }

        public ButtonVariant Variant
        {
            get => (ButtonVariant)GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public double SplitWidth
        {
            get => (double)GetValue(SplitWidthProperty);
            set => SetValue(SplitWidthProperty, value);
        }

        public ContextMenu? DropDownMenu
        {
            get => (ContextMenu?)GetValue(DropDownMenuProperty);
            set => SetValue(DropDownMenuProperty, value);
        }

        #endregion

        public ZSplitButton()
        {
            Height = 32;
            Cursor = Cursors.Hand;
            Focusable = true;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        #region Measurement & Input

        protected override Size MeasureOverride(Size availableSize)
        {
            double width = 120;
            double height = double.IsNaN(Height) ? 32 : Height;
            return new Size(
                double.IsPositiveInfinity(availableSize.Width) ? width : Math.Min(width, availableSize.Width),
                double.IsPositiveInfinity(availableSize.Height) ? height : Math.Min(height, availableSize.Height));
        }

        private Rect ActionBounds => new Rect(0, 0, Math.Max(0, ActualWidth - SplitWidth), ActualHeight);
        private Rect DropDownBounds => new Rect(Math.Max(0, ActualWidth - SplitWidth), 0, SplitWidth, ActualHeight);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point p = e.GetPosition(this);
            bool inAction = ActionBounds.Contains(p);
            bool inDrop = DropDownBounds.Contains(p);

            if (_isActionHovered != inAction || _isDropDownHovered != inDrop)
            {
                _isActionHovered = inAction;
                _isDropDownHovered = inDrop;
                InvalidateVisual();
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _isActionHovered = false;
            _isDropDownHovered = false;
            _isActionPressed = false;
            _isDropDownPressed = false;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed && IsEnabled)
            {
                Focus();
                Point p = e.GetPosition(this);
                if (ActionBounds.Contains(p))
                {
                    _isActionPressed = true;
                    InvalidateVisual();
                }
                else if (DropDownBounds.Contains(p))
                {
                    _isDropDownPressed = true;
                    InvalidateVisual();
                }
                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            Point p = e.GetPosition(this);
            if (_isActionPressed && ActionBounds.Contains(p))
            {
                _isActionPressed = false;
                InvalidateVisual();
                ActionClick?.Invoke(this, new RoutedEventArgs());
                e.Handled = true;
            }
            else if (_isDropDownPressed && DropDownBounds.Contains(p))
            {
                _isDropDownPressed = false;
                InvalidateVisual();
                DropDownClick?.Invoke(this, new RoutedEventArgs());
                ShowDropDown();
                e.Handled = true;
            }
            else
            {
                _isActionPressed = false;
                _isDropDownPressed = false;
                InvalidateVisual();
            }
        }

        public void ShowDropDown()
        {
            if (DropDownMenu != null)
            {
                DropDownMenu.PlacementTarget = this;
                DropDownMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                DropDownMenu.IsOpen = true;
            }
        }

        #endregion

        #region Rendering Pipeline

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            Rect fullRect = new Rect(0, 0, ActualWidth, ActualHeight);
            double radius = CornerRadius;

            // 1. Resolve Brushes
            var (bg, fg, border) = GetColors();
            Pen borderPen = new Pen(border, 1.0);

            // 2. Draw Outer Container
            dc.DrawRoundedRectangle(bg, borderPen, fullRect, radius, radius);

            // 3. Sub-zone highlight
            double splitX = ActualWidth - SplitWidth;
            if (IsEnabled && splitX > 0)
            {
                if (_isActionHovered || _isActionPressed)
                {
                    Brush hBrush = new SolidColorBrush(Color.FromArgb(_isActionPressed ? (byte)40 : (byte)20, 255, 255, 255));
                    dc.DrawRectangle(hBrush, null, new Rect(0, 0, splitX, ActualHeight));
                }

                if (_isDropDownHovered || _isDropDownPressed)
                {
                    Brush hBrush = new SolidColorBrush(Color.FromArgb(_isDropDownPressed ? (byte)40 : (byte)20, 255, 255, 255));
                    dc.DrawRectangle(hBrush, null, new Rect(splitX, 0, SplitWidth, ActualHeight));
                }

                // 4. Draw 1px Divider
                Brush divBrush = Variant == ButtonVariant.Secondary
                    ? ZeroWpfTheme.BorderDefault
                    : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

                dc.DrawLine(new Pen(divBrush, 1.0), new Point(splitX, 4), new Point(splitX, ActualHeight - 4));
            }

            // 5. Action Text & Icon
            string caption = string.IsNullOrEmpty(IconGlyph)
                ? Text
                : $"{IconGlyph}  {Text}".Trim();

            var formattedText = new FormattedText(
                caption,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                12.5,
                fg,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textX = (splitX - formattedText.Width) / 2.0;
            double textY = (ActualHeight - formattedText.Height) / 2.0;
            dc.DrawText(formattedText, new Point(Math.Max(6, textX), textY));

            // 6. Chevron ▾
            var dropText = new FormattedText(
                "▾",
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                11.0,
                fg,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double dropX = splitX + (SplitWidth - dropText.Width) / 2.0;
            double dropY = (ActualHeight - dropText.Height) / 2.0;
            dc.DrawText(dropText, new Point(dropX, dropY));

            // Re-stroke outer border
            dc.DrawRoundedRectangle(null, borderPen, fullRect, radius, radius);
        }

        private (Brush bg, Brush fg, Brush border) GetColors()
        {
            if (!IsEnabled)
            {
                return (ZeroWpfTheme.BgInput, ZeroWpfTheme.TextMuted, ZeroWpfTheme.BorderDefault);
            }

            return Variant switch
            {
                ButtonVariant.Primary => (ZeroWpfTheme.PrimaryAccent, Brushes.White, ZeroWpfTheme.PrimaryAccentDark),
                ButtonVariant.Secondary => (ZeroWpfTheme.BgInput, ZeroWpfTheme.TextPrimary, ZeroWpfTheme.BorderDefault),
                ButtonVariant.Success => (ZeroWpfTheme.SuccessAccent, Brushes.White, Brushes.Transparent),
                ButtonVariant.Danger => (ZeroWpfTheme.DangerAccent, Brushes.White, Brushes.Transparent),
                _ => (ZeroWpfTheme.PrimaryAccent, Brushes.White, Brushes.Transparent)
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
    /// Backward-compatibility alias for <see cref="ZSplitButton"/>.
    /// </summary>
    [Obsolete("SplitButton is deprecated and will be removed in 5 release cycles. Please migrate to ZSplitButton instead.")]
    public class SplitButton : ZSplitButton
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZSplitButton"/>.
    /// </summary>
    [Obsolete("ZeroSplitButton is deprecated and will be removed in 5 release cycles. Please migrate to ZSplitButton instead.")]
    public class ZeroSplitButton : ZSplitButton
    {
    }
}
