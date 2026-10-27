using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern anti-aliased flat RadioButton control for ZeroUI in WPF.
    /// Provides mutual exclusion across siblings or GroupName, keyboard navigation,
    /// and responsive ZeroWpfTheme styling.
    /// </summary>
    public class ZRadioButton : FrameworkElement
    {
        private bool _isHovered = false;
        private bool _isPressed = false;

        #region Dependency Properties

        public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(nameof(IsChecked), typeof(bool), typeof(ZRadioButton),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnIsCheckedChanged));

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ZRadioButton),
                new FrameworkPropertyMetadata("Option", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GroupNameProperty =
            DependencyProperty.Register(nameof(GroupName), typeof(string), typeof(ZRadioButton),
                new FrameworkPropertyMetadata(string.Empty));

        public static readonly DependencyProperty RadioSizeProperty =
            DependencyProperty.Register(nameof(RadioSize), typeof(double), typeof(ZRadioButton),
                new FrameworkPropertyMetadata(18.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty AutoCheckProperty =
            DependencyProperty.Register(nameof(AutoCheck), typeof(bool), typeof(ZRadioButton),
                new FrameworkPropertyMetadata(true));

        #endregion

        #region Properties & Events

        public bool IsChecked
        {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string GroupName
        {
            get => (string)GetValue(GroupNameProperty);
            set => SetValue(GroupNameProperty, value);
        }

        public double RadioSize
        {
            get => (double)GetValue(RadioSizeProperty);
            set => SetValue(RadioSizeProperty, value);
        }

        public bool AutoCheck
        {
            get => (bool)GetValue(AutoCheckProperty);
            set => SetValue(AutoCheckProperty, value);
        }

        public event EventHandler<bool>? CheckedChanged;

        #endregion

        public ZRadioButton()
        {
            Cursor = Cursors.Hand;
            Focusable = true;
            Height = 28;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZRadioButton rb)
            {
                bool isChecked = (bool)e.NewValue;
                if (isChecked)
                {
                    rb.UncheckSiblings();
                }
                rb.CheckedChanged?.Invoke(rb, isChecked);
            }
        }

        private void UncheckSiblings()
        {
            if (Parent is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is ZRadioButton other && other != this)
                    {
                        if (string.IsNullOrEmpty(GroupName) && string.IsNullOrEmpty(other.GroupName))
                        {
                            other.IsChecked = false;
                        }
                        else if (!string.IsNullOrEmpty(GroupName) && string.Equals(GroupName, other.GroupName, StringComparison.Ordinal))
                        {
                            other.IsChecked = false;
                        }
                    }
                }
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var ft = new FormattedText(
                Text ?? string.Empty,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.RegularTypeface,
                13.0,
                Brushes.Black,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double w = RadioSize + 8 + ft.Width + 4;
            double h = Math.Max(RadioSize + 6, ft.Height + 6);
            return new Size(w, h);
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
            _isPressed = false;
            InvalidateVisual();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Focus();
                _isPressed = true;
                if (AutoCheck && !IsChecked)
                {
                    IsChecked = true;
                }
                InvalidateVisual();
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            _isPressed = false;
            InvalidateVisual();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                if (AutoCheck && !IsChecked)
                {
                    IsChecked = true;
                }
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            bool isDark = ZeroWpfTheme.IsDark;
            double radioRadius = RadioSize / 2.0;
            double cy = h / 2.0;
            var center = new Point(radioRadius + 2, cy);

            // 1. Radio Ring
            Brush ringBg = _isPressed ? ZeroWpfTheme.BgActive : (_isHovered ? ZeroWpfTheme.BgHover : ZeroWpfTheme.BgInput);
            Pen ringPen = IsChecked
                ? new Pen(ZeroWpfTheme.PrimaryAccent, 2.0)
                : (_isHovered ? new Pen(ZeroWpfTheme.BorderFocus, 1.5) : ZeroWpfTheme.BorderPen);

            dc.DrawEllipse(ringBg, ringPen, center, radioRadius, radioRadius);

            // 2. Checked Dot
            if (IsChecked)
            {
                Brush dotBrush = ZeroWpfTheme.PrimaryAccent;
                double dotRadius = radioRadius * 0.52;
                dc.DrawEllipse(dotBrush, null, center, dotRadius, dotRadius);
            }

            // 3. Focus Cue
            if (IsFocused)
            {
                Pen focusPen = new Pen(ZeroWpfTheme.PrimaryAccentDark, 1.0) { DashStyle = DashStyles.Dot };
                focusPen.Freeze();
                dc.DrawEllipse(null, focusPen, center, radioRadius + 3, radioRadius + 3);
            }

            // 4. Text Label
            if (!string.IsNullOrEmpty(Text))
            {
                Brush textBrush = IsEnabled ? ZeroWpfTheme.TextPrimary : ZeroWpfTheme.TextMuted;
                var ft = new FormattedText(
                    Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    13.0,
                    textBrush,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double textX = center.X + radioRadius + 8;
                double textY = cy - ft.Height / 2.0;
                dc.DrawText(ft, new Point(textX, textY));
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

    /// <summary>
    /// Backward-compatibility alias for <see cref="RadioEdit"/>.
    /// </summary>
    [Obsolete("ZeroRadioButton is deprecated. Use RadioEdit instead.")]
    public class ZeroRadioButton : RadioEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZRadioButton"/>.
    /// </summary>
    public class ZRadioEdit : ZRadioButton { }

    /// <summary>
    /// Legacy alias for <see cref="ZRadioButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("RadioEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioButton instead.")]
    public class RadioEdit : ZRadioButton { }

    /// <summary>
    /// Legacy alias for <see cref="ZRadioButton"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroRadioEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZRadioButton instead.")]
    public class ZeroRadioEdit : ZRadioButton { }

    #endregion

}
