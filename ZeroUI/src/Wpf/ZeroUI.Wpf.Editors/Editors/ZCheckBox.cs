using System;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern anti-aliased flat CheckBox control for ZeroUI in WPF.
    /// Supports two-state and three-state (Checked, Unchecked, Indeterminate),
    /// keyboard spacebar toggling, smooth vector checkmark / indeterminate dash, and responsive theme synchronization.
    /// </summary>
    public class ZCheckBox : FrameworkElement, IZeroEditor
    {
        private bool _isHovered = false;
        private bool _isPressed = false;

        #region Dependency Properties

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZCheckBox),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool?), typeof(ZCheckBox),
                new FrameworkPropertyMetadata((bool?)false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnIsCheckedChanged));

        public static readonly DependencyProperty ThreeStateProperty =
            DependencyProperty.Register(nameof(ThreeState), typeof(bool), typeof(ZCheckBox),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ZCheckBox),
                new FrameworkPropertyMetadata("CheckBox", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BoxSizeProperty =
            DependencyProperty.Register(nameof(BoxSize), typeof(double), typeof(ZCheckBox),
                new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(double), typeof(ZCheckBox),
                new FrameworkPropertyMetadata(4.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZCheckBox),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZCheckBox),
                new PropertyMetadata(false));

        #endregion

        #region Properties & Events

        public event EventHandler? EditValueChanged;
        public event EventHandler<bool?>? CheckedChanged;

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool? IsChecked
        {
            get => (bool?)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public bool ThreeState
        {
            get => (bool)GetValue(ThreeStateProperty);
            set => SetValue(ThreeStateProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public double BoxSize
        {
            get => (double)GetValue(BoxSizeProperty);
            set => SetValue(BoxSizeProperty, value);
        }

        public double CornerRadius
        {
            get => (double)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public void Reset()
        {
            IsChecked = false;
            EditValue = false;
            IsModified = false;
        }

        public void Clear() => Reset();

        #endregion

        public ZCheckBox()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
            Height = 28;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCheckBox cb)
            {
                bool? val = null;
                if (e.NewValue is bool b) val = b;
                else if (e.NewValue != null && bool.TryParse(e.NewValue.ToString(), out bool pb)) val = pb;

                if (cb.IsChecked != val)
                {
                    cb.IsChecked = val;
                }
                cb.EditValueChanged?.Invoke(cb, EventArgs.Empty);
            }
        }

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCheckBox cb)
            {
                bool? newVal = (bool?)e.NewValue;
                if (!Equals(cb.EditValue, newVal))
                {
                    cb.EditValue = newVal;
                }
                cb.IsModified = true;
                cb.CheckedChanged?.Invoke(cb, newVal);
                cb.EditValueChanged?.Invoke(cb, EventArgs.Empty);
            }
        }

        public void Toggle()
        {
            if (!IsEnabled || ReadOnly) return;

            if (ThreeState)
            {
                if (IsChecked == false)
                    IsChecked = true;
                else if (IsChecked == true)
                    IsChecked = null;
                else
                    IsChecked = false;
            }
            else
            {
                IsChecked = !(IsChecked == true);
            }
        }

        #region Input Handling

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
            _isPressed = false;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed && IsEnabled)
            {
                _isPressed = true;
                Focus();
                CaptureMouse();
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
                _isPressed = false;
                if (_isHovered && IsEnabled)
                {
                    Toggle();
                }
                InvalidateVisual();
                e.Handled = true;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Space && IsEnabled)
            {
                Toggle();
                e.Handled = true;
            }
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            InvalidateVisual();
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            InvalidateVisual();
        }

        #endregion

        #region Measure & Render

        protected override Size MeasureOverride(Size availableSize)
        {
            double boxSize = BoxSize;
            double spacing = 8;
            double textWidth = 0;
            double textHeight = 0;

            if (!string.IsNullOrEmpty(Text))
            {
                var formattedText = new FormattedText(
                    Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    13.0,
                    ZeroWpfTheme.TextPrimary,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                textWidth = formattedText.Width;
                textHeight = formattedText.Height;
            }

            double totalWidth = boxSize + spacing + textWidth + 8;
            double totalHeight = Math.Max(boxSize, Math.Max(textHeight, 24));

            return new Size(
                double.IsPositiveInfinity(availableSize.Width) ? totalWidth : Math.Min(totalWidth, availableSize.Width),
                double.IsPositiveInfinity(availableSize.Height) ? totalHeight : Math.Min(totalHeight, availableSize.Height));
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double size = BoxSize;
            double y = Math.Round((ActualHeight - size) / 2.0);
            Rect boxRect = new Rect(2, y, size, size);

            bool isChecked = IsChecked == true;
            bool isIndeterminate = IsChecked == null;
            bool isHot = (_isHovered || _isPressed) && IsEnabled;

            // 1. Focus Glow Ring
            if (IsFocused)
            {
                Rect focusRect = new Rect(boxRect.X - 2, boxRect.Y - 2, boxRect.Width + 4, boxRect.Height + 4);
                dc.DrawRoundedRectangle(null, new Pen(ZeroWpfTheme.BorderFocus, 1.5), focusRect, CornerRadius + 2, CornerRadius + 2);
            }

            // 2. Box Fill & Stroke
            Brush fillBrush;
            Pen borderPen;

            if (!IsEnabled)
            {
                fillBrush = isChecked || isIndeterminate ? ZeroWpfTheme.BgDisabled : ZeroWpfTheme.BgPrimary;
                borderPen = ZeroWpfTheme.BorderPen;
            }
            else if (isChecked || isIndeterminate)
            {
                fillBrush = isHot ? ZeroWpfTheme.PrimaryAccentDark : ZeroWpfTheme.PrimaryAccent;
                borderPen = new Pen(fillBrush, 1.2);
            }
            else
            {
                fillBrush = isHot ? ZeroWpfTheme.BgHover : ZeroWpfTheme.BgInput;
                borderPen = isHot ? ZeroWpfTheme.AccentPen : ZeroWpfTheme.BorderPen;
            }

            dc.DrawRoundedRectangle(fillBrush, borderPen, boxRect, CornerRadius, CornerRadius);

            // 3. Glyphs (Checkmark or Dash)
            if (isChecked)
            {
                // Crisp vector checkmark
                Pen checkPen = new Pen(Brushes.White, 2.0)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round
                };

                StreamGeometry geom = new StreamGeometry();
                using (var ctx = geom.Open())
                {
                    ctx.BeginFigure(new Point(boxRect.Left + 4.5, boxRect.Top + (size * 0.52)), false, false);
                    ctx.LineTo(new Point(boxRect.Left + (size * 0.42), boxRect.Top + (size * 0.72)), true, false);
                    ctx.LineTo(new Point(boxRect.Left + (size * 0.76), boxRect.Top + (size * 0.28)), true, false);
                }
                geom.Freeze();
                dc.DrawGeometry(null, checkPen, geom);
            }
            else if (isIndeterminate)
            {
                // Horizontal indeterminate dash
                Pen dashPen = new Pen(Brushes.White, 2.2)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                dc.DrawLine(dashPen,
                    new Point(boxRect.Left + 4.5, boxRect.Top + (size / 2.0)),
                    new Point(boxRect.Right - 4.5, boxRect.Top + (size / 2.0)));
            }

            // 4. Label Text
            if (!string.IsNullOrEmpty(Text))
            {
                Brush textBrush = IsEnabled ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextMuted;

                var formattedText = new FormattedText(
                    Text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    13.0,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double textY = Math.Round((ActualHeight - formattedText.Height) / 2.0);
                dc.DrawText(formattedText, new Point(boxRect.Right + 8, textY));
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
    /// Convenience alias for <see cref="ZCheckBox"/>.
    /// </summary>
    public class ZCheckEdit : ZCheckBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZCheckBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CheckEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZCheckBox instead.")]
    public class CheckEdit : ZCheckBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZCheckBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCheckBox is deprecated and will be removed in 5 release cycles. Please migrate to ZCheckBox instead.")]
    public class ZeroCheckBox : ZCheckBox { }

    #endregion

}
