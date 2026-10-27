using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern search input control with vector search icon, placeholder text, clear button,
    /// and debounced text change notifications.
    /// </summary>
    public class ZSearchControl : Control
    {
        private readonly TextBox _innerBox;
        private readonly Border _containerBorder;
        private readonly Button _clearButton;
        private readonly TextBlock _placeholderBlock;
        private readonly DispatcherTimer _debounceTimer;

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ZSearchControl),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZSearchControl),
                new FrameworkPropertyMetadata("Search...", OnPlaceholderChanged));

        public static readonly DependencyProperty DebounceMsProperty =
            DependencyProperty.Register(nameof(DebounceMs), typeof(int), typeof(ZSearchControl),
                new FrameworkPropertyMetadata(200, OnDebounceMsChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZSearchControl),
                new FrameworkPropertyMetadata(new CornerRadius(6)));

        #endregion

        #region Properties & Events

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public int DebounceMs
        {
            get => (int)GetValue(DebounceMsProperty);
            set => SetValue(DebounceMsProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public event EventHandler<string>? DebouncedTextChanged;

        #endregion

        public ZSearchControl()
        {
            Height = 32;
            Focusable = false;

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DebounceMs)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                DebouncedTextChanged?.Invoke(this, Text);
            };

            // 1. Container Border
            _containerBorder = new Border
            {
                CornerRadius = CornerRadius,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true
            };

            // 2. Grid Layout: [Icon (28px) | Text Field (Auto) | Clear Button (28px)]
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

            // 3. Search Icon (Left)
            var iconBlock = new TextBlock
            {
                Text = "🔍",
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.65,
                IsHitTestVisible = false
            };
            Grid.SetColumn(iconBlock, 0);
            grid.Children.Add(iconBlock);

            // 4. Central Host Grid: Placeholder + TextBox
            var centerGrid = new Grid();

            _placeholderBlock = new TextBlock
            {
                Text = Placeholder,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                Opacity = 0.5,
                IsHitTestVisible = false
            };
            centerGrid.Children.Add(_placeholderBlock);

            _innerBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
            _innerBox.TextChanged += (s, e) =>
            {
                if (Text != _innerBox.Text)
                {
                    Text = _innerBox.Text;
                }
                UpdateClearButtonVisibility();
                _placeholderBlock.Visibility = string.IsNullOrEmpty(_innerBox.Text) ? Visibility.Visible : Visibility.Collapsed;

                _debounceTimer.Stop();
                _debounceTimer.Start();
            };

            _innerBox.GotFocus += (s, e) => UpdateThemeColors(isFocused: true);
            _innerBox.LostFocus += (s, e) => UpdateThemeColors(isFocused: false);

            centerGrid.Children.Add(_innerBox);
            Grid.SetColumn(centerGrid, 1);
            grid.Children.Add(centerGrid);

            // 5. Clear Button (Right)
            _clearButton = new Button
            {
                Content = "✕",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Width = 18,
                Height = 18,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _clearButton.Click += (s, e) =>
            {
                Text = string.Empty;
                _innerBox.Focus();
            };
            Grid.SetColumn(_clearButton, 2);
            grid.Children.Add(_clearButton);

            _containerBorder.Child = grid;
            AddVisualChild(_containerBorder);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateThemeColors(isFocused: false);
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSearchControl sb)
            {
                string newVal = (string)e.NewValue ?? string.Empty;
                if (sb._innerBox.Text != newVal)
                {
                    sb._innerBox.Text = newVal;
                }
                sb.UpdateClearButtonVisibility();
            }
        }

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSearchControl sb)
            {
                sb._placeholderBlock.Text = (string)e.NewValue ?? string.Empty;
            }
        }

        private static void OnDebounceMsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSearchControl sb)
            {
                sb._debounceTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, (int)e.NewValue));
            }
        }

        private void UpdateClearButtonVisibility()
        {
            _clearButton.Visibility = string.IsNullOrEmpty(_innerBox.Text) ? Visibility.Collapsed : Visibility.Visible;
            _placeholderBlock.Visibility = string.IsNullOrEmpty(_innerBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateThemeColors(bool isFocused)
        {
            _containerBorder.Background = ZeroWpfTheme.BgInput;
            _containerBorder.BorderBrush = isFocused
                ? ZeroWpfTheme.BorderFocus
                : ZeroWpfTheme.BorderDefault;

            _innerBox.Foreground = ZeroWpfTheme.TextPrimary;
            _innerBox.CaretBrush = ZeroWpfTheme.PrimaryAccent;
            _placeholderBlock.Foreground = ZeroWpfTheme.TextSecondary;
            _clearButton.Foreground = ZeroWpfTheme.TextSecondary;
        }

        #region Visual Tree Overrides

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _containerBorder;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            _containerBorder.Measure(constraint);
            return _containerBorder.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _containerBorder.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
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
    private void OnThemeChanged() => UpdateThemeColors(isFocused: _innerBox.IsFocused);
}

    /// <summary>
    /// Backward-compatibility alias for <see cref="SearchControl"/>.
    /// </summary>
    [Obsolete("ZeroSearchBox is deprecated. Use SearchControl instead.")]
    public class ZeroSearchBox : SearchControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSearchControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SearchControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchControl instead.")]
    public class SearchControl : ZSearchControl { }

    /// <summary>
    /// Legacy alias for <see cref="ZSearchControl"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSearchControl is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchControl instead.")]
    public class ZeroSearchControl : ZSearchControl { }

    #endregion

}
