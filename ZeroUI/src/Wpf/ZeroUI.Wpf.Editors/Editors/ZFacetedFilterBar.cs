using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Represents a single selectable facet item with an associated count badge.
    /// </summary>
    public class FacetItemModel : INotifyPropertyChanged
    {
        private string _key = "";
        private string _displayText = "";
        private int _count;
        private bool _isSelected;

        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(nameof(Key)); }
        }

        public string DisplayText
        {
            get => _displayText;
            set { _displayText = value; OnPropertyChanged(nameof(DisplayText)); }
        }

        public int Count
        {
            get => _count;
            set
            {
                _count = value;
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(nameof(CountBadge));
            }
        }

        public string CountBadge => Count > 0 ? Count.ToString("N0") : "";

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public override string ToString() => $"{DisplayText} ({CountBadge})";
    }

    /// <summary>
    /// Represents a column in the <see cref="FacetedFilterBar"/>.
    /// </summary>
    public class FacetColumnModel : INotifyPropertyChanged
    {
        private string _key = "";
        private string _title = "";
        private FacetItemModel? _selectedItem;

        public string Key
        {
            get => _key;
            set { _key = value; OnPropertyChanged(nameof(Key)); }
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(Title)); }
        }

        public ObservableCollection<FacetItemModel> Items { get; } = new ObservableCollection<FacetItemModel>();

        public FacetItemModel? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem != value)
                {
                    if (_selectedItem != null) _selectedItem.IsSelected = false;
                    _selectedItem = value;
                    if (_selectedItem != null) _selectedItem.IsSelected = true;
                    OnPropertyChanged(nameof(SelectedItem));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    /// <summary>
    /// Universal cascading drill-down Faceted Filter Bar for ZeroUI in WPF.
    /// Supports multi-column faceted search, automatic count badges, clear all,
    /// and theme integration.
    /// </summary>
    public class ZFacetedFilterBar : Control, IZeroEditor
    {
        private readonly ObservableCollection<FacetColumnModel> _columns = new ObservableCollection<FacetColumnModel>();
        private Grid? _rootGrid;
        private Grid? _columnsGrid;
        private Button? _btnClearAll;
        private TextBlock? _txtSummary;

        #region Dependency Properties

        public static readonly DependencyProperty HeaderTitleProperty =
            DependencyProperty.Register(nameof(HeaderTitle), typeof(string), typeof(ZFacetedFilterBar),
                new PropertyMetadata("METADATA DRILL-DOWN"));

        public static readonly DependencyProperty SummaryTextProperty =
            DependencyProperty.Register(nameof(SummaryText), typeof(string), typeof(ZFacetedFilterBar),
                new PropertyMetadata("· All Items"));

        public static readonly DependencyProperty ShowHeaderProperty =
            DependencyProperty.Register(nameof(ShowHeader), typeof(bool), typeof(ZFacetedFilterBar),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZFacetedFilterBar),
                new PropertyMetadata(false));

        #endregion

        #region Properties & Events

        public string HeaderTitle
        {
            get => (string)GetValue(HeaderTitleProperty);
            set => SetValue(HeaderTitleProperty, value);
        }

        public string SummaryText
        {
            get => (string)GetValue(SummaryTextProperty);
            set => SetValue(SummaryTextProperty, value);
        }

        public bool ShowHeader
        {
            get => (bool)GetValue(ShowHeaderProperty);
            set => SetValue(ShowHeaderProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        public ObservableCollection<FacetColumnModel> Columns => _columns;

        /// <summary>
        /// Fired when any facet selection changes.
        /// </summary>
        public event EventHandler<(string ColumnKey, string? SelectedItemKey)>? SelectionChanged;

        /// <summary>
        /// Fired when the "Clear All" action is executed.
        /// </summary>
        public event EventHandler? FiltersCleared;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get
            {
                var dict = new Dictionary<string, string>();
                foreach (var col in _columns)
                {
                    if (col.SelectedItem != null && !string.IsNullOrEmpty(col.SelectedItem.Key))
                    {
                        dict[col.Key] = col.SelectedItem.Key;
                    }
                }
                return dict;
            }
            set
            {
                if (value is IDictionary<string, string> dict)
                {
                    foreach (var col in _columns)
                    {
                        if (dict.TryGetValue(col.Key, out var targetKey))
                        {
                            foreach (var itm in col.Items)
                            {
                                if (itm.Key == targetKey) { col.SelectedItem = itm; break; }
                            }
                        }
                        else
                        {
                            col.SelectedItem = null;
                        }
                    }
                }
                else if (value == null)
                {
                    ClearAll();
                }
            }
        }

        public event EventHandler? EditValueChanged;

        public bool IsModified { get; set; }

        public void Reset() => ClearAll();
        public void Clear() => ClearAll();

        #endregion

        static ZFacetedFilterBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZFacetedFilterBar), new FrameworkPropertyMetadata(typeof(ZFacetedFilterBar)));
        }

        public ZFacetedFilterBar()
        {
            Height = 175;
            Focusable = true;

            _columns.CollectionChanged += (_, _) => RebuildColumns();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            BuildVisualTree();
        }

        private void BuildVisualTree()
        {
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(26) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Header row
            var headerBorder = new Border
            {
                Padding = new Thickness(8, 2, 8, 2),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            headerBorder.SetBinding(Border.VisibilityProperty, new Binding(nameof(ShowHeader)) { Source = this, Converter = new BooleanToVisibilityConverter() });

            var headerDock = new DockPanel { LastChildFill = true };

            var titlePanel = new StackPanel { Orientation = Orientation.Horizontal };
            DockPanel.SetDock(titlePanel, Dock.Left);

            var txtTitle = new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            txtTitle.SetBinding(TextBlock.TextProperty, new Binding(nameof(HeaderTitle)) { Source = this });
            titlePanel.Children.Add(txtTitle);

            _txtSummary = new TextBlock
            {
                FontSize = 10,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _txtSummary.SetBinding(TextBlock.TextProperty, new Binding(nameof(SummaryText)) { Source = this });
            titlePanel.Children.Add(_txtSummary);
            headerDock.Children.Add(titlePanel);

            _btnClearAll = new Button
            {
                Content = "Clear All",
                Padding = new Thickness(6, 1, 6, 1),
                FontSize = 10,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnClearAll.Click += (_, _) => ClearAll();
            DockPanel.SetDock(_btnClearAll, Dock.Right);
            headerDock.Children.Add(_btnClearAll);

            headerBorder.Child = headerDock;
            rootGrid.Children.Add(headerBorder);

            // Columns row
            _columnsGrid = new Grid();
            Grid.SetRow(_columnsGrid, 1);
            rootGrid.Children.Add(_columnsGrid);

            _rootGrid = rootGrid;
            AddVisualChild(rootGrid);
            AddLogicalChild(rootGrid);
            ApplyTheme();
        }

        public void ClearAll()
        {
            foreach (var col in _columns)
            {
                col.SelectedItem = null;
            }
            IsModified = false;
            FiltersCleared?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RebuildColumns()
        {
            if (_columnsGrid == null) return;

            _columnsGrid.ColumnDefinitions.Clear();
            _columnsGrid.Children.Clear();

            int count = _columns.Count;
            if (count == 0) return;

            for (int i = 0; i < count; i++)
            {
                _columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                if (i < count - 1)
                {
                    _columnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1) }); // Divider
                }
            }

            int colIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var colModel = _columns[i];

                var colGrid = new Grid();
                colGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });
                colGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // Column Header
                var colHeader = new Border
                {
                    Background = ZeroWpfTheme.BgHover,
                    Padding = new Thickness(6, 2, 6, 2)
                };
                var txtColTitle = new TextBlock
                {
                    Text = colModel.Title.ToUpperInvariant(),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ZeroWpfTheme.TextSecondary
                };
                colHeader.Child = txtColTitle;
                colGrid.Children.Add(colHeader);

                // ListBox
                var lb = new ListBox
                {
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    ItemsSource = colModel.Items,
                    SelectedItem = colModel.SelectedItem
                };
                Grid.SetRow(lb, 1);

                // Custom ItemTemplate for Text + CountBadge
                var dt = new DataTemplate();
                var factory = new FrameworkElementFactory(typeof(DockPanel));
                factory.SetValue(DockPanel.MarginProperty, new Thickness(2, 1, 2, 1));
                factory.SetValue(DockPanel.LastChildFillProperty, true);

                var badgeFactory = new FrameworkElementFactory(typeof(TextBlock));
                badgeFactory.SetValue(DockPanel.DockProperty, Dock.Right);
                badgeFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(FacetItemModel.CountBadge)));
                badgeFactory.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextMuted);
                badgeFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
                badgeFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
                factory.AppendChild(badgeFactory);

                var textFactory = new FrameworkElementFactory(typeof(TextBlock));
                textFactory.SetBinding(TextBlock.TextProperty, new Binding(nameof(FacetItemModel.DisplayText)));
                textFactory.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextPrimary);
                textFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
                textFactory.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
                factory.AppendChild(textFactory);

                dt.VisualTree = factory;
                lb.ItemTemplate = dt;

                lb.SelectionChanged += (s, e) =>
                {
                    colModel.SelectedItem = lb.SelectedItem as FacetItemModel;
                    IsModified = true;
                    SelectionChanged?.Invoke(this, (colModel.Key, colModel.SelectedItem?.Key));
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                };

                Grid.SetColumn(colGrid, colIdx);
                _columnsGrid.Children.Add(colGrid);

                colIdx++;
                if (i < count - 1)
                {
                    // Vertical Separator
                    var sep = new Border
                    {
                        Width = 1,
                        Background = ZeroWpfTheme.BorderSubtle,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    Grid.SetColumn(sep, colIdx);
                    _columnsGrid.Children.Add(sep);
                    colIdx++;
                }
            }
        }

        private void ApplyTheme()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            Foreground = ZeroWpfTheme.TextPrimary;

            if (_btnClearAll != null)
            {
                _btnClearAll.Foreground = ZeroWpfTheme.PrimaryAccent;
            }
        }

        #region Visual Children Plumbing

        protected override int VisualChildrenCount => _rootGrid != null ? 1 : 0;
        protected override Visual GetVisualChild(int index)
        {
            if (_rootGrid == null || index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootGrid;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootGrid != null)
            {
                _rootGrid.Measure(constraint);
                return _rootGrid.DesiredSize;
            }
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootGrid?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        #endregion
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += ApplyTheme;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= ApplyTheme;
    }
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZFacetedFilterBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("FacetedFilterBar is deprecated and will be removed in 5 release cycles. Please migrate to ZFacetedFilterBar instead.")]
    public class FacetedFilterBar : ZFacetedFilterBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZFacetedFilterBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroFacetedFilterBar is deprecated and will be removed in 5 release cycles. Please migrate to ZFacetedFilterBar instead.")]
    public class ZeroFacetedFilterBar : ZFacetedFilterBar { }

    #endregion

}
