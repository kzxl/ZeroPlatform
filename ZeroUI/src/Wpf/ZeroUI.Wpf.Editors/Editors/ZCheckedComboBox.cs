using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Localization;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    public class CheckedComboItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private string _text = string.Empty;
        private object _value;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public string Text
        {
            get => _text;
            set
            {
                if (_text != value)
                {
                    _text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        public object Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public CheckedComboItem(object value, string text, bool isChecked = false)
        {
            _value = value;
            _text = text;
            _isChecked = isChecked;
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// Modern anti-aliased Multi-Select CheckedComboBox for ZeroUI WPF.
    /// Features drop-down list with check boxes, "Select All" toggle, live search filter,
    /// and dynamic summary formatting. Implements <see cref="IZeroEditor"/>.
    /// </summary>
    public class ZCheckedComboBox : ZeroWpfControlBase, IZeroEditor
    {
        private readonly ObservableCollection<CheckedComboItem> _items = new ObservableCollection<CheckedComboItem>();
        private readonly ObservableCollection<CheckedComboItem> _filteredItems = new ObservableCollection<CheckedComboItem>();

        private Border? _border;
        private TextBlock? _arrow;
        private Border? _popupBorder;
        private Separator? _separator;
        private Popup? _popup;
        private TextBox? _searchBox;
        private TextBlock? _displayTextBlock;
        private CheckBox? _selectAllBox;
        private ListBox? _listBox;
        private bool _isUpdatingSelectAll = false;

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZCheckedComboBox), new PropertyMetadata(null));

        public static readonly DependencyProperty SummaryFormatProperty =
            DependencyProperty.Register(nameof(SummaryFormat), typeof(string), typeof(ZCheckedComboBox), new PropertyMetadata(null));

        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(nameof(IsDropDownOpen), typeof(bool), typeof(ZCheckedComboBox), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZCheckedComboBox), new PropertyMetadata(false));

        public string Placeholder
        {
            get => (string?)GetValue(PlaceholderProperty) ?? ZeroLocalizer.GetString(ZeroStringId.CheckedComboPlaceholder);
            set => SetValue(PlaceholderProperty, value);
        }

        public string SummaryFormat
        {
            get => (string?)GetValue(SummaryFormatProperty) ?? ZeroLocalizer.GetString(ZeroStringId.CheckedComboSummaryFormat);
            set => SetValue(SummaryFormatProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public ObservableCollection<CheckedComboItem> Items => _items;

        public IEnumerable<object> CheckedValues => _items.Where(i => i.IsChecked).Select(i => i.Value);

        public object? EditValue
        {
            get => CheckedValues.ToList();
            set
            {
                if (value is IEnumerable enumerable && !(value is string))
                {
                    var set = new HashSet<object>();
                    foreach (var item in enumerable)
                    {
                        if (item != null) set.Add(item);
                    }
                    foreach (var itm in _items)
                    {
                        itm.IsChecked = set.Contains(itm.Value);
                    }
                }
                else if (value == null)
                {
                    Reset();
                }
                else
                {
                    foreach (var itm in _items)
                    {
                        itm.IsChecked = Equals(itm.Value, value);
                    }
                }
                IsModified = true;
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool IsModified { get; set; }

        public event EventHandler? SelectionChanged;
        public event EventHandler? EditValueChanged;

        static ZCheckedComboBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZCheckedComboBox), new FrameworkPropertyMetadata(typeof(ZCheckedComboBox)));
        }

        public ZCheckedComboBox()
        {
            Background = ZeroWpfTheme.BgInput;
            Foreground = ZeroWpfTheme.TextPrimary;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);
            Height = 36;
            FontSize = 13.0;
            Cursor = Cursors.Hand;

            _items.CollectionChanged += (s, e) =>
            {
                if (e.NewItems != null)
                {
                    foreach (CheckedComboItem item in e.NewItems)
                    {
                        item.PropertyChanged += Item_PropertyChanged;
                    }
                }
                if (e.OldItems != null)
                {
                    foreach (CheckedComboItem item in e.OldItems)
                    {
                        item.PropertyChanged -= Item_PropertyChanged;
                    }
                }
                UpdateFilteredItems();
                UpdateDisplayText();
            };

            ZeroLocalizer.CultureChanged += (s, e) =>
            {
                UpdateDisplayText();
                if (_selectAllBox != null) _selectAllBox.Content = ZeroLocalizer.GetString(ZeroStringId.CheckedComboSelectAll);
            };

            BuildVisualTemplate();
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            Background = ZeroWpfTheme.BgInput;
            Foreground = ZeroWpfTheme.TextPrimary;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            if (_border != null)
            {
                _border.Background = ZeroWpfTheme.BgInput;
                _border.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_arrow != null) _arrow.Foreground = ZeroWpfTheme.TextSecondary;
            if (_popupBorder != null)
            {
                _popupBorder.Background = ZeroWpfTheme.BgCard;
                _popupBorder.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_searchBox != null)
            {
                _searchBox.Background = ZeroWpfTheme.BgInput;
                _searchBox.Foreground = ZeroWpfTheme.TextPrimary;
                _searchBox.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_selectAllBox != null)
            {
                _selectAllBox.Foreground = ZeroWpfTheme.TextPrimary;
            }
            if (_separator != null)
            {
                _separator.Background = ZeroWpfTheme.BorderDefault;
            }
            UpdateDisplayText();
        }

        private Grid? _rootGrid;

        private void BuildVisualTemplate()
        {
            var rootGrid = new Grid();
            _rootGrid = rootGrid;

            _border = new Border
            {
                Background = Background,
                BorderBrush = BorderBrush,
                BorderThickness = BorderThickness,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 0, 10, 0)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24, GridUnitType.Pixel) });

            _displayTextBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Foreground = ZeroWpfTheme.TextMuted,
                Text = Placeholder
            };
            Grid.SetColumn(_displayTextBlock, 0);
            headerGrid.Children.Add(_displayTextBlock);

            _arrow = new TextBlock
            {
                Text = "▼",
                FontSize = 9.0,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(_arrow, 1);
            headerGrid.Children.Add(_arrow);

            _border.Child = headerGrid;
            rootGrid.Children.Add(_border);

            // Popup construction
            _popup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            _popupBorder = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6),
                Width = 260,
                MaxHeight = 320
            };

            var popupStack = new StackPanel();

            _searchBox = new TextBox
            {
                Height = 28,
                Margin = new Thickness(2, 2, 2, 6),
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                Padding = new Thickness(4, 2, 4, 2)
            };
            _searchBox.TextChanged += (s, e) => UpdateFilteredItems();
            popupStack.Children.Add(_searchBox);

            _selectAllBox = new CheckBox
            {
                Content = ZeroLocalizer.GetString(ZeroStringId.CheckedComboSelectAll),
                FontWeight = FontWeights.Bold,
                Foreground = ZeroWpfTheme.TextPrimary,
                Margin = new Thickness(6, 2, 6, 6)
            };
            _selectAllBox.Click += SelectAllBox_Click;
            popupStack.Children.Add(_selectAllBox);

            _separator = new Separator
            {
                Margin = new Thickness(0, 0, 0, 4),
                Background = ZeroWpfTheme.BorderDefault
            };
            popupStack.Children.Add(_separator);

            _listBox = new ListBox
            {
                ItemsSource = _filteredItems,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                MaxHeight = 220
            };
            ScrollViewer.SetVerticalScrollBarVisibility(_listBox, ScrollBarVisibility.Auto);

            var itemTemplate = new DataTemplate(typeof(CheckedComboItem));
            var factory = new FrameworkElementFactory(typeof(CheckBox));
            factory.SetBinding(CheckBox.IsCheckedProperty, new System.Windows.Data.Binding(nameof(CheckedComboItem.IsChecked)) { Mode = System.Windows.Data.BindingMode.TwoWay });
            factory.SetBinding(CheckBox.ContentProperty, new System.Windows.Data.Binding(nameof(CheckedComboItem.Text)));
            factory.SetResourceReference(CheckBox.ForegroundProperty, "ZeroUI.TextPrimary");
            factory.SetValue(CheckBox.MarginProperty, new Thickness(4, 3, 4, 3));
            itemTemplate.VisualTree = factory;
            _listBox.ItemTemplate = itemTemplate;

            popupStack.Children.Add(_listBox);
            _popupBorder.Child = popupStack;
            _popup.Child = _popupBorder;

            rootGrid.Children.Add(_popup);
            AddVisualChild(rootGrid);
            AddLogicalChild(rootGrid);
        }

        protected override int VisualChildrenCount => _rootGrid != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _rootGrid ?? throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootGrid != null)
            {
                _rootGrid.Measure(constraint);
                return _rootGrid.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootGrid?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (ReadOnly) return;

            if (!IsDropDownOpen)
            {
                IsDropDownOpen = true;
                _searchBox?.Focus();
            }
        }

        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZCheckedComboBox cb && cb._popup != null)
            {
                cb._popup.IsOpen = (bool)e.NewValue;
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CheckedComboItem.IsChecked))
            {
                UpdateDisplayText();
                UpdateSelectAllState();
                IsModified = true;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void SelectAllBox_Click(object sender, RoutedEventArgs e)
        {
            if (ReadOnly || _isUpdatingSelectAll || _selectAllBox == null) return;

            bool target = _selectAllBox.IsChecked == true;
            foreach (var item in _items)
            {
                item.IsChecked = target;
            }
        }

        private void UpdateSelectAllState()
        {
            if (_selectAllBox == null) return;
            _isUpdatingSelectAll = true;

            int count = _items.Count(i => i.IsChecked);
            _selectAllBox.IsChecked = CheckedComboHelper.CalculateSelectAllState(count, _items.Count);

            _isUpdatingSelectAll = false;
        }

        private void UpdateFilteredItems()
        {
            _filteredItems.Clear();
            string query = _searchBox?.Text.Trim() ?? string.Empty;
            foreach (var item in _items)
            {
                if (string.IsNullOrEmpty(query) || item.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _filteredItems.Add(item);
                }
            }
        }

        private void UpdateDisplayText()
        {
            if (_displayTextBlock == null) return;

            var checkedList = _items.Where(i => i.IsChecked).Select(i => i.Text).ToList();
            _displayTextBlock.Text = CheckedComboHelper.FormatDisplayText(checkedList, Placeholder, SummaryFormat, 2);
            _displayTextBlock.Foreground = checkedList.Count == 0 ? ZeroWpfTheme.TextMuted : ZeroWpfTheme.TextPrimary;
        }

        public void AddItem(object value, string text, bool isChecked = false)
        {
            _items.Add(new CheckedComboItem(value, text, isChecked));
        }

        public void Reset()
        {
            foreach (var item in _items)
            {
                item.IsChecked = false;
            }
            IsModified = false;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _items.Clear();
            IsModified = false;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZCheckedComboBox"/>.
    /// </summary>
    public class ZCheckedComboBoxEdit : ZCheckedComboBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZCheckedComboBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("CheckedComboBoxEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZCheckedComboBox instead.")]
    public class CheckedComboBoxEdit : ZCheckedComboBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZCheckedComboBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroCheckedComboBox is deprecated and will be removed in 5 release cycles. Please migrate to ZCheckedComboBox instead.")]
    public class ZeroCheckedComboBox : ZCheckedComboBox { }

    #endregion

}
