using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern interactive hyperlink and web address editor for ZeroUI in WPF.
    /// Supports direct shell navigation, custom display text, editable mode with inline launch button,
    /// and dark theme accent styling.
    /// </summary>
    public class ZHyperlinkEdit : Control, IZeroEditor
    {
        private bool _isHovered;
        private bool _isLaunchHovered;
        private Rect _launchButtonRect;

        #region Dependency Properties

        public static readonly DependencyProperty TargetUrlProperty =
            DependencyProperty.Register(nameof(TargetUrl), typeof(string), typeof(ZHyperlinkEdit),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.AffectsRender, OnTargetUrlChanged));

        public static readonly DependencyProperty DisplayTextProperty =
            DependencyProperty.Register(nameof(DisplayText), typeof(string), typeof(ZHyperlinkEdit),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsEditableProperty =
            DependencyProperty.Register(nameof(IsEditable), typeof(bool), typeof(ZHyperlinkEdit),
                new PropertyMetadata(false, OnIsEditableChanged));

        public static readonly DependencyProperty RequireConfirmationProperty =
            DependencyProperty.Register(nameof(RequireConfirmation), typeof(bool), typeof(ZHyperlinkEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZHyperlinkEdit),
                new PropertyMetadata(new CornerRadius(5)));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZHyperlinkEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZHyperlinkEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZHyperlinkEdit),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        #endregion

        #region Properties & Events

        public string TargetUrl
        {
            get => (string)GetValue(TargetUrlProperty);
            set => SetValue(TargetUrlProperty, value);
        }

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            set => SetValue(DisplayTextProperty, value);
        }

        public bool IsEditable
        {
            get => (bool)GetValue(IsEditableProperty);
            set => SetValue(IsEditableProperty, value);
        }

        public bool RequireConfirmation
        {
            get => (bool)GetValue(RequireConfirmationProperty);
            set => SetValue(RequireConfirmationProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public event EventHandler<HyperlinkNavigateEventArgs>? HyperlinkClick;
        public event EventHandler? EditValueChanged;

        #endregion

        private bool _isInternalSync;

        static ZHyperlinkEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZHyperlinkEdit),
                new FrameworkPropertyMetadata(typeof(ZHyperlinkEdit)));
        }

        public ZHyperlinkEdit()
        {
            Focusable = true;
            Cursor = Cursors.Hand;
            Height = 32;
            Width = 200;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private static void OnTargetUrlChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZHyperlinkEdit link && !link._isInternalSync)
            {
                link._isInternalSync = true;
                link.EditValue = e.NewValue;
                link._isInternalSync = false;
                link.EditValueChanged?.Invoke(link, EventArgs.Empty);
                link.InvalidateVisual();
            }
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZHyperlinkEdit link && !link._isInternalSync)
            {
                link._isInternalSync = true;
                link.TargetUrl = e.NewValue?.ToString() ?? string.Empty;
                link._isInternalSync = false;
                link.EditValueChanged?.Invoke(link, EventArgs.Empty);
                link.InvalidateVisual();
            }
        }

        private static void OnIsEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZHyperlinkEdit link)
            {
                link.Cursor = (bool)e.NewValue ? Cursors.IBeam : Cursors.Hand;
                link.InvalidateVisual();
            }
        }

        public void Reset()
        {
            TargetUrl = string.Empty;
            DisplayText = string.Empty;
            IsModified = false;
        }

        public void Clear() => Reset();

        public void Navigate()
        {
            if (string.IsNullOrWhiteSpace(TargetUrl)) return;

            HyperlinkHelper.TryCreateUri(TargetUrl, out var uri);
            var args = new HyperlinkNavigateEventArgs(TargetUrl, uri);
            HyperlinkClick?.Invoke(this, args);

            if (args.Cancel || args.Handled) return;

            if (RequireConfirmation)
            {
                var res = MessageBox.Show($"Open external link?\n\n{TargetUrl}", "Confirm Navigation", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (res != MessageBoxResult.Yes) return;
            }

            HyperlinkHelper.OpenTarget(TargetUrl);
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
            _isLaunchHovered = false;
            InvalidateVisual();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var pt = e.GetPosition(this);
            bool launchHover = _launchButtonRect.Contains(pt);
            if (launchHover != _isLaunchHovered)
            {
                _isLaunchHovered = launchHover;
                InvalidateVisual();
            }
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pt = e.GetPosition(this);
                if (!IsEditable || _launchButtonRect.Contains(pt))
                {
                    Navigate();
                    e.Handled = true;
                }
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter && !IsEditable)
            {
                Navigate();
                e.Handled = true;
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            var bounds = new Rect(0, 0, w, h);
            var radius = CornerRadius.TopLeft;

            // Background & Border if editable or focused
            if (IsEditable)
            {
                Brush bg = ZeroWpfTheme.BgInput;
                Brush borderBrush = IsFocused ? ZeroWpfTheme.PrimaryAccent : (_isHovered ? ZeroWpfTheme.PrimaryAccentHover : ZeroWpfTheme.BorderDefault);
                dc.DrawRoundedRectangle(bg, new Pen(borderBrush, 1.0), bounds, radius, radius);

                // Launch button on right
                double btnW = 24;
                _launchButtonRect = new Rect(w - btnW - 3, 3, btnW, h - 6);
                if (_isLaunchHovered)
                {
                    dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, _launchButtonRect, 3, 3);
                }

                // Launch icon ↗
                var iconFt = new FormattedText("↗", System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight, ZeroWpfTheme.RegularTypeface, 13.0, ZeroWpfTheme.PrimaryAccent,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(iconFt, new Point(_launchButtonRect.X + (_launchButtonRect.Width - iconFt.Width) / 2.0, (_launchButtonRect.Height - iconFt.Height) / 2.0 + 3));
            }
            else
            {
                _launchButtonRect = Rect.Empty;
                if (_isHovered)
                {
                    dc.DrawRoundedRectangle(ZeroWpfTheme.BgHover, null, bounds, radius, radius);
                }
            }

            // Text to show
            string text = !string.IsNullOrEmpty(DisplayText) ? DisplayText : (TargetUrl ?? string.Empty);
            if (string.IsNullOrEmpty(text))
            {
                text = "https://...";
            }

            Brush textBrush = IsEditable ? ZeroWpfTheme.TextPrimary : (_isHovered ? ZeroWpfTheme.PrimaryAccentHover : ZeroWpfTheme.PrimaryAccent);
            var ft = new FormattedText(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                ZeroWpfTheme.RegularTypeface,
                FontSize > 0 ? FontSize : 13.0,
                textBrush,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textX = 8;
            double textY = (h - ft.Height) / 2.0;
            dc.DrawText(ft, new Point(textX, textY));

            // Underline for pure hyperlink mode
            if (!IsEditable && _isHovered)
            {
                var underlinePen = new Pen(ZeroWpfTheme.PrimaryAccent, 1.0);
                dc.DrawLine(underlinePen, new Point(textX, textY + ft.Height + 1), new Point(textX + ft.Width, textY + ft.Height + 1));
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
    /// Legacy alias for <see cref="ZHyperlinkEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("HyperlinkEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZHyperlinkEdit instead.")]
    public class HyperlinkEdit : ZHyperlinkEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZHyperlinkEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroHyperlinkEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZHyperlinkEdit instead.")]
    public class ZeroHyperlinkEdit : ZHyperlinkEdit { }

    #endregion

}
