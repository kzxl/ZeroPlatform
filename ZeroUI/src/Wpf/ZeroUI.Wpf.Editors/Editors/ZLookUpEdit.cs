using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Virtualized, high-performance searchable autocomplete dropdown & lookup box for WPF.
    /// Features instant debounced filtering across large datasets, multi-property item layout,
    /// non-activating flyweight popup, and keyboard navigation.
    /// </summary>
    public class ZLookUpEdit : Control
    {
        private readonly Border _containerBorder;
        private readonly TextBox _searchBox;
        private readonly Button _clearButton;
        private readonly Button _chevronButton;
        private readonly TextBlock _placeholderBlock;
        private readonly Popup _dropdownPopup;
        private readonly ListBox _resultsListBox;
        private readonly DispatcherTimer _debounceTimer;

        private readonly List<LookUpItem> _allItems = new List<LookUpItem>();
        private readonly List<LookUpItem> _filteredItems = new List<LookUpItem>();

        #region Dependency Properties

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(LookUpItem), typeof(ZLookUpEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZLookUpEdit),
                new FrameworkPropertyMetadata("Search items...", OnPlaceholderChanged));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZLookUpEdit),
                new FrameworkPropertyMetadata(new CornerRadius(6)));

        #endregion

        #region Properties & Events

        public LookUpItem? SelectedItem
        {
            get => (LookUpItem?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public string? SelectedKey => SelectedItem?.Key;

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public event EventHandler<LookUpItem?>? SelectedItemChanged;

        #endregion

        public ZLookUpEdit()
        {
            Height = 32;
            Focusable = false;

            // Container Border
            _containerBorder = new Border
            {
                CornerRadius = CornerRadius,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true
            };

            // Grid: [Search Box (Auto) | Clear (22px) | Chevron (22px)]
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

            var hostGrid = new Grid();
            _placeholderBlock = new TextBlock
            {
                Text = Placeholder,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                Opacity = 0.5,
                IsHitTestVisible = false
            };
            hostGrid.Children.Add(_placeholderBlock);

            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(8, 0, 0, 0),
                SnapsToDevicePixels = true
            };
            hostGrid.Children.Add(_searchBox);
            Grid.SetColumn(hostGrid, 0);
            grid.Children.Add(hostGrid);

            // Clear Button
            _clearButton = new Button
            {
                Content = "✕",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Width = 18,
                Height = 18,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Cursor = Cursors.Hand
            };
            Grid.SetColumn(_clearButton, 1);
            grid.Children.Add(_clearButton);

            // Chevron Button
            _chevronButton = new Button
            {
                Content = "▼",
                FontSize = 9,
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            Grid.SetColumn(_chevronButton, 2);
            grid.Children.Add(_chevronButton);

            _containerBorder.Child = grid;
            AddVisualChild(_containerBorder);

            // Popup ListBox
            _resultsListBox = new ListBox
            {
                MaxHeight = 220,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ItemTemplate = CreateItemTemplate()
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_resultsListBox, ScrollBarVisibility.Disabled);
            _resultsListBox.SelectionChanged += (s, e) =>
            {
                if (_resultsListBox.SelectedItem is LookUpItem item)
                {
                    SelectedItem = item;
                    _searchBox.Text = item.DisplayText;
                    CloseDropdown();
                }
            };

            var popupBorder = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4),
                SnapsToDevicePixels = true,
                Child = _resultsListBox
            };

            _dropdownPopup = new Popup
            {
                PlacementTarget = _containerBorder,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                Child = popupBorder
            };

            // Wire events
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                PerformFilter(_searchBox.Text);
            };

            _searchBox.TextChanged += (s, e) =>
            {
                _placeholderBlock.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                _clearButton.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Collapsed : Visibility.Visible;
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };

            _searchBox.GotFocus += (s, e) =>
            {
                UpdateThemeColors(isFocused: true);
                OpenDropdown();
            };
            _searchBox.LostFocus += (s, e) => UpdateThemeColors(isFocused: false);
            _searchBox.KeyDown += SearchBox_KeyDown;

            _clearButton.Click += (s, e) =>
            {
                SelectedItem = null;
                _searchBox.Text = string.Empty;
                _searchBox.Focus();
            };

            _chevronButton.Click += (s, e) =>
            {
                if (_dropdownPopup.IsOpen) CloseDropdown();
                else { _searchBox.Focus(); OpenDropdown(); }
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateThemeColors(isFocused: false);
        }

        public void SetItems(IEnumerable<LookUpItem> items)
        {
            _allItems.Clear();
            if (items != null)
            {
                _allItems.AddRange(items);
            }
            PerformFilter(_searchBox.Text);
        }

        private void PerformFilter(string query)
        {
            _filteredItems.Clear();
            _filteredItems.AddRange(LookUpFilterHelper.Filter(_allItems, query, 100));

            _resultsListBox.ItemsSource = null;
            _resultsListBox.ItemsSource = _filteredItems;

            if (_dropdownPopup.IsOpen && _filteredItems.Count > 0)
            {
                _resultsListBox.SelectedIndex = 0;
            }
        }

        private void OpenDropdown()
        {
            PerformFilter(_searchBox.Text);
            _dropdownPopup.Width = ActualWidth > 0 ? ActualWidth : 260;
            _dropdownPopup.IsOpen = true;
        }

        public void CloseDropdown()
        {
            _dropdownPopup.IsOpen = false;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (!_dropdownPopup.IsOpen) OpenDropdown();
                else if (_resultsListBox.SelectedIndex < _filteredItems.Count - 1)
                {
                    _resultsListBox.SelectedIndex++;
                    _resultsListBox.ScrollIntoView(_resultsListBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (_resultsListBox.SelectedIndex > 0)
                {
                    _resultsListBox.SelectedIndex--;
                    _resultsListBox.ScrollIntoView(_resultsListBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (_dropdownPopup.IsOpen && _resultsListBox.SelectedItem is LookUpItem item)
                {
                    SelectedItem = item;
                    _searchBox.Text = item.DisplayText;
                    CloseDropdown();
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                CloseDropdown();
                e.Handled = true;
            }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZLookUpEdit lookup)
            {
                var item = (LookUpItem?)e.NewValue;
                if (item != null && lookup._searchBox.Text != item.DisplayText)
                {
                    lookup._searchBox.Text = item.DisplayText;
                }
                lookup.SelectedItemChanged?.Invoke(lookup, item);
            }
        }

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZLookUpEdit lookup)
            {
                lookup._placeholderBlock.Text = (string)e.NewValue ?? string.Empty;
            }
        }

        private void UpdateThemeColors(bool isFocused)
        {
            _containerBorder.Background = ZeroWpfTheme.BgInput;
            _containerBorder.BorderBrush = isFocused ? ZeroWpfTheme.BorderFocus : ZeroWpfTheme.BorderDefault;
            _searchBox.Foreground = ZeroWpfTheme.TextPrimary;
            _searchBox.CaretBrush = ZeroWpfTheme.PrimaryAccent;
            _placeholderBlock.Foreground = ZeroWpfTheme.TextSecondary;
            _clearButton.Foreground = ZeroWpfTheme.TextSecondary;
            _chevronButton.Foreground = ZeroWpfTheme.TextSecondary;
        }

        private static DataTemplate CreateItemTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(Grid));
            factory.SetValue(Grid.MarginProperty, new Thickness(4, 3, 4, 3));

            var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);

            factory.AppendChild(col0);
            factory.AppendChild(col1);

            // Left text panel (Title + SubText)
            var sp = new FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(Grid.ColumnProperty, 0);

            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(LookUpItem.DisplayText)));
            title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            title.SetValue(TextBlock.FontSizeProperty, 12.0);
            title.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextPrimary);
            sp.AppendChild(title);

            var sub = new FrameworkElementFactory(typeof(TextBlock));
            sub.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(LookUpItem.SubText)));
            sub.SetValue(TextBlock.FontSizeProperty, 10.5);
            sub.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextMuted);
            sp.AppendChild(sub);

            factory.AppendChild(sp);

            // Right category tag
            var tagBorder = new FrameworkElementFactory(typeof(Border));
            tagBorder.SetValue(Grid.ColumnProperty, 1);
            tagBorder.SetValue(Border.BackgroundProperty, ZeroWpfTheme.BgHover);
            tagBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
            tagBorder.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
            tagBorder.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);

            var catText = new FrameworkElementFactory(typeof(TextBlock));
            catText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(LookUpItem.Category)));
            catText.SetValue(TextBlock.FontSizeProperty, 10.0);
            catText.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.PrimaryAccent);
            tagBorder.AppendChild(catText);

            factory.AppendChild(tagBorder);
            template.VisualTree = factory;
            return template;
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
    private void OnThemeChanged() => UpdateThemeColors(isFocused: _searchBox.IsFocused);
}

    /// <summary>
    /// Backward-compatibility alias for <see cref="LookUpEdit"/>.
    /// </summary>
    [Obsolete("ZeroLookup is deprecated. Use LookUpEdit instead.")]
    public class ZeroLookup : LookUpEdit
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("LookUpEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZLookUpEdit instead.")]
    public class LookUpEdit : ZLookUpEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroLookUpEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZLookUpEdit instead.")]
    public class ZeroLookUpEdit : ZLookUpEdit { }

    #endregion

}
