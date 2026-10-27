using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern anti-aliased multi-line text editor (Memo / Text Area) for ZeroUI in WPF.
    /// Provides smooth scrolling, word wrap, placeholder, character counter, and theme synchronization.
    /// </summary>
    public class ZMemoEdit : TextBox, IZeroEditor
    {
        #region Dependency Properties

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZMemoEdit),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ShowCharacterCountProperty =
            DependencyProperty.Register(nameof(ShowCharacterCount), typeof(bool), typeof(ZMemoEdit),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZMemoEdit),
                new FrameworkPropertyMetadata(new CornerRadius(5)));

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZMemoEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZMemoEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZMemoEdit),
                new PropertyMetadata(false, (d, e) => ((MemoEdit)d).IsReadOnly = (bool)e.NewValue));

        #endregion

        #region Properties

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool ShowCharacterCount
        {
            get => (bool)GetValue(ShowCharacterCountProperty);
            set => SetValue(ShowCharacterCountProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

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

        public event EventHandler? EditValueChanged;

        #endregion

        private bool _isInternalEditValueSync;

        static ZMemoEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZMemoEdit),
                new FrameworkPropertyMetadata(typeof(TextEdit)));
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZMemoEdit memo && !memo._isInternalEditValueSync)
            {
                memo.Text = e.NewValue?.ToString() ?? string.Empty;
                memo.EditValueChanged?.Invoke(memo, EventArgs.Empty);
            }
        }

        public void Reset()
        {
            Text = string.Empty;
            IsModified = false;
        }

        public new void Clear() => Reset();

        public ZMemoEdit()
        {
            AcceptsReturn = true;
            AcceptsTab = false;
            TextWrapping = TextWrapping.Wrap;
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

            MinHeight = 64;
            Padding = new Thickness(10, 8, 10, 8);
            SnapsToDevicePixels = true;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        protected override void OnTextChanged(TextChangedEventArgs e)
        {
            base.OnTextChanged(e);
            _isInternalEditValueSync = true;
            EditValue = Text;
            IsModified = true;
            _isInternalEditValueSync = false;
            EditValueChanged?.Invoke(this, EventArgs.Empty);

            if (ShowCharacterCount || !string.IsNullOrEmpty(Placeholder))
            {
                InvalidateVisual();
            }
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // 1. Placeholder Text when empty
            if (string.IsNullOrEmpty(Text) && !string.IsNullOrEmpty(Placeholder) && !IsFocused)
            {
                var ft = new FormattedText(
                    Placeholder,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    FontSize > 0 ? FontSize : 12.0,
                    ZeroWpfTheme.TextMuted,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                dc.DrawText(ft, new Point(Padding.Left + 2, Padding.Top + 2));
            }

            // 2. Character Counter Badge at bottom-right
            if (ShowCharacterCount)
            {
                int currentLen = Text?.Length ?? 0;
                string counterText = TextCounterHelper.FormatCharacterCount(currentLen, MaxLength, true);

                var ft = new FormattedText(
                    counterText,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    ZeroWpfTheme.RegularTypeface,
                    9.5,
                    (MaxLength > 0 && currentLen >= MaxLength) ? ZeroWpfTheme.DangerAccent : ZeroWpfTheme.TextMuted,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double badgeW = ft.Width + 8;
                double badgeH = ft.Height + 4;
                double badgeX = w - badgeW - 16;
                double badgeY = h - badgeH - 6;

                Brush badgeBg = ZeroWpfTheme.BgHover;
                dc.DrawRoundedRectangle(badgeBg, null, new Rect(badgeX, badgeY, badgeW, badgeH), 3, 3);
                dc.DrawText(ft, new Point(badgeX + 4, badgeY + 2));
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
    /// Legacy alias for <see cref="ZMemoEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("MemoEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZMemoEdit instead.")]
    public class MemoEdit : ZMemoEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZMemoEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroMemoEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZMemoEdit instead.")]
    public class ZeroMemoEdit : ZMemoEdit { }

    #endregion

}
