using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
    /// AutoComplete control for WPF with debounced async search and customizable suggestion list.
    /// </summary>
    public class ZAutoComplete : Control
    {
        private readonly Border _containerBorder;
        private readonly TextBox _innerBox;
        private readonly Button _clearButton;
        private readonly TextBlock _placeholderBlock;
        private readonly Popup _dropdownPopup;
        private readonly ListBox _resultsListBox;
        private readonly DispatcherTimer _debounceTimer;

        private CancellationTokenSource? _cts;

        #region Dependency Properties

        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(ZAutoComplete),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZAutoComplete),
                new FrameworkPropertyMetadata("Type to search...", OnPlaceholderChanged));

        public static readonly DependencyProperty DebounceMsProperty =
            DependencyProperty.Register(nameof(DebounceMs), typeof(int), typeof(ZAutoComplete),
                new FrameworkPropertyMetadata(200, OnDebounceMsChanged));

        public static readonly DependencyProperty MaxSuggestionsProperty =
            DependencyProperty.Register(nameof(MaxSuggestions), typeof(int), typeof(ZAutoComplete),
                new FrameworkPropertyMetadata(10));

        public static readonly DependencyProperty MinSearchLengthProperty =
            DependencyProperty.Register(nameof(MinSearchLength), typeof(int), typeof(ZAutoComplete),
                new FrameworkPropertyMetadata(1));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(ZAutoComplete),
                new FrameworkPropertyMetadata(new CornerRadius(6)));

        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(AutoCompleteItem), typeof(ZAutoComplete),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        #endregion

        #region CLR Properties

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

        public int MaxSuggestions
        {
            get => (int)GetValue(MaxSuggestionsProperty);
            set => SetValue(MaxSuggestionsProperty, value);
        }

        public int MinSearchLength
        {
            get => (int)GetValue(MinSearchLengthProperty);
            set => SetValue(MinSearchLengthProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public AutoCompleteItem? SelectedItem
        {
            get => (AutoCompleteItem?)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public IEnumerable<AutoCompleteItem>? ItemsSource { get; set; }
        public Func<string, CancellationToken, Task<IEnumerable<AutoCompleteItem>>>? AsyncItemsSource { get; set; }
        
        private bool _showClearButton = true;
        public bool ShowClearButton
        {
            get => _showClearButton;
            set
            {
                _showClearButton = value;
                UpdateClearButtonVisibility();
            }
        }

        public event EventHandler<AutoCompleteItemSelectedEventArgs>? ItemSelected;

        #endregion

        public ZAutoComplete()
        {
            Height = 32;
            Focusable = false;

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceMs) };
            _debounceTimer.Tick += DebounceTimer_Tick;

            _containerBorder = new Border
            {
                CornerRadius = CornerRadius,
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });

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

            var hostGrid = new Grid();

            _placeholderBlock = new TextBlock
            {
                Text = Placeholder,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0),
                Opacity = 0.5,
                IsHitTestVisible = false
            };
            hostGrid.Children.Add(_placeholderBlock);

            _innerBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center,
                SnapsToDevicePixels = true
            };
            _innerBox.TextChanged += InnerBox_TextChanged;
            _innerBox.GotFocus += (s, e) => UpdateThemeColors(isFocused: true);
            _innerBox.LostFocus += (s, e) => UpdateThemeColors(isFocused: false);
            _innerBox.KeyDown += InnerBox_KeyDown;

            hostGrid.Children.Add(_innerBox);
            Grid.SetColumn(hostGrid, 1);
            grid.Children.Add(hostGrid);

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
                SelectedItem = null;
                _innerBox.Focus();
                CloseDropdown();
            };
            Grid.SetColumn(_clearButton, 2);
            grid.Children.Add(_clearButton);

            _containerBorder.Child = grid;
            AddVisualChild(_containerBorder);

            _resultsListBox = new ListBox
            {
                MaxHeight = 280,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                ItemTemplate = CreateItemTemplate()
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_resultsListBox, ScrollBarVisibility.Disabled);
            _resultsListBox.SelectionChanged += ResultsListBox_SelectionChanged;

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
            _dropdownPopup.Opened += (s, e) => UpdateThemeColors(isFocused: _innerBox.IsFocused);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateThemeColors(isFocused: false);
        }

        private void InnerBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (Text != _innerBox.Text)
            {
                Text = _innerBox.Text;
            }
            UpdateClearButtonVisibility();
            _placeholderBlock.Visibility = string.IsNullOrEmpty(_innerBox.Text) ? Visibility.Visible : Visibility.Collapsed;

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private async void DebounceTimer_Tick(object? sender, EventArgs e)
        {
            _debounceTimer.Stop();

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            var query = _innerBox.Text;

            if (string.IsNullOrWhiteSpace(query) || query.Length < MinSearchLength)
            {
                CloseDropdown();
                return;
            }

            IEnumerable<AutoCompleteItem>? results = null;

            try
            {
                if (AsyncItemsSource != null)
                {
                    results = await AsyncItemsSource(query, token);
                }
                else if (ItemsSource != null)
                {
                    results = AutoCompleteFilterHelper.Filter(ItemsSource, query, MaxSuggestions);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                // Ignore other exceptions during fetch
            }

            if (token.IsCancellationRequested) return;

            var resultList = new List<AutoCompleteItem>();
            if (results != null)
            {
                foreach (var item in results)
                {
                    if (resultList.Count >= MaxSuggestions) break;
                    resultList.Add(item);
                }
            }

            _resultsListBox.ItemsSource = resultList;

            if (resultList.Count > 0)
            {
                OpenDropdown();
            }
            else
            {
                CloseDropdown();
            }
        }

        private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_resultsListBox.SelectedItem is AutoCompleteItem item)
            {
                CommitSelection(item);
            }
        }

        private void InnerBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (!_dropdownPopup.IsOpen && _resultsListBox.Items.Count > 0)
                {
                    OpenDropdown();
                }
                else if (_resultsListBox.SelectedIndex < _resultsListBox.Items.Count - 1)
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
                if (_dropdownPopup.IsOpen && _resultsListBox.SelectedItem is AutoCompleteItem item)
                {
                    CommitSelection(item);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                CloseDropdown();
                e.Handled = true;
            }
        }

        private void CommitSelection(AutoCompleteItem item)
        {
            SelectedItem = item;
            _innerBox.Text = item.DisplayText;
            Text = item.DisplayText;
            ItemSelected?.Invoke(this, new AutoCompleteItemSelectedEventArgs(item, Text));
            CloseDropdown();
        }

        private void OpenDropdown()
        {
            _dropdownPopup.Width = ActualWidth > 0 ? ActualWidth : 260;
            _dropdownPopup.IsOpen = true;
        }

        private void CloseDropdown()
        {
            _dropdownPopup.IsOpen = false;
            _resultsListBox.SelectedItem = null;
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAutoComplete ac)
            {
                string newVal = (string)e.NewValue ?? string.Empty;
                if (ac._innerBox.Text != newVal)
                {
                    ac._innerBox.Text = newVal;
                }
                ac.UpdateClearButtonVisibility();
            }
        }

        private static void OnPlaceholderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAutoComplete ac)
            {
                ac._placeholderBlock.Text = (string)e.NewValue ?? string.Empty;
            }
        }

        private static void OnDebounceMsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAutoComplete ac)
            {
                ac._debounceTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(50, (int)e.NewValue));
            }
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZAutoComplete ac)
            {
                var item = (AutoCompleteItem?)e.NewValue;
                if (item != null && ac._innerBox.Text != item.DisplayText)
                {
                    ac._innerBox.Text = item.DisplayText;
                }
            }
        }

        private void UpdateClearButtonVisibility()
        {
            _clearButton.Visibility = (ShowClearButton && !string.IsNullOrEmpty(_innerBox.Text)) 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            _placeholderBlock.Visibility = string.IsNullOrEmpty(_innerBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateThemeColors(bool isFocused)
        {
            _containerBorder.Background = ZeroWpfTheme.BgInput;
            _containerBorder.BorderBrush = isFocused ? ZeroWpfTheme.BorderFocus : ZeroWpfTheme.BorderDefault;
            _innerBox.Foreground = ZeroWpfTheme.TextPrimary;
            _innerBox.CaretBrush = ZeroWpfTheme.PrimaryAccent;
            _placeholderBlock.Foreground = ZeroWpfTheme.TextSecondary;
            _clearButton.Foreground = ZeroWpfTheme.TextSecondary;

            if (_dropdownPopup.Child is Border popupBorder)
            {
                popupBorder.Background = ZeroWpfTheme.BgCard;
                popupBorder.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
        }

        private static DataTemplate CreateItemTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            factory.SetValue(StackPanel.MarginProperty, new Thickness(4, 3, 4, 3));

            // Left icon/glyph
            var glyphBlock = new FrameworkElementFactory(typeof(TextBlock));
            glyphBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(AutoCompleteItem.Glyph)));
            glyphBlock.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 6, 0));
            glyphBlock.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            glyphBlock.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextSecondary);
            
            factory.AppendChild(glyphBlock);

            // Right text panel (Title + SubText)
            var sp = new FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);

            var title = new FrameworkElementFactory(typeof(TextBlock));
            title.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(AutoCompleteItem.DisplayText)));
            title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            title.SetValue(TextBlock.FontSizeProperty, 12.0);
            title.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextPrimary);
            sp.AppendChild(title);

            var sub = new FrameworkElementFactory(typeof(TextBlock));
            sub.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(AutoCompleteItem.SubText)));
            sub.SetValue(TextBlock.FontSizeProperty, 10.5);
            sub.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextMuted);
            sp.AppendChild(sub);

            factory.AppendChild(sp);

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
        
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
            ApplyTheme();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private void ApplyTheme() => UpdateThemeColors(isFocused: _innerBox.IsFocused);
        private void OnThemeChanged() => ApplyTheme();
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZAutoComplete"/>.
    /// </summary>
    [Obsolete("Use ZAutoComplete instead.")]
    public class ZeroAutoComplete : ZAutoComplete { }
}
