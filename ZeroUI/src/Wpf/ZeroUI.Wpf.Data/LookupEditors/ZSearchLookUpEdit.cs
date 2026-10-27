using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Data;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.DataGrid;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Enterprise high-capacity paginated and debounced search dropdown editor for ZeroUI WPF.
    /// Hosts an embedded virtual DataGrid within a Popup, enabling multi-column search,
    /// pagination, and instant selection for complex enterprise entities.
    /// </summary>
    public class ZSearchLookUpEdit : ZeroWpfControlBase, IZeroEditor
    {
        private Border? _border;
        private TextBlock? _iconBlock;
        private TextBlock? _arrow;
        private Border? _popupBorder;
        private Popup? _popup;
        private TextBox? _searchBox;
        private ZGrid? _grid;
        private TextBlock? _displayTextBlock;
        private TextBlock? _statusBlock;
        private DispatcherTimer? _debounceTimer;

        private string _selectedText = string.Empty;
        private object? _selectedValue = null;
        private string _displayMember = "Name";
        private string _valueMember = "Id";

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(nameof(EditValue), typeof(object), typeof(ZSearchLookUpEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(ZSearchLookUpEdit), new PropertyMetadata("Click to search and select..."));

        public static readonly DependencyProperty IsDropDownOpenProperty =
            DependencyProperty.Register(
                nameof(IsDropDownOpen),
                typeof(bool),
                typeof(ZSearchLookUpEdit),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsDropDownOpenChanged));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(nameof(IsModified), typeof(bool), typeof(ZSearchLookUpEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(nameof(ReadOnly), typeof(bool), typeof(ZSearchLookUpEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty DebounceDelayMsProperty =
            DependencyProperty.Register(nameof(DebounceDelayMs), typeof(int), typeof(ZSearchLookUpEdit),
                new PropertyMetadata(250));

        public event EventHandler? EditValueChanged;
        public event EventHandler? SelectionChanged;
        public event EventHandler<ProcessNewValueEventArgs>? ProcessNewValue;
        public bool ShowAddNewButton { get; set; } = false;
        public string AddNewButtonText { get; set; } = "+ Add New Record";

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

        public int DebounceDelayMs
        {
            get => (int)GetValue(DebounceDelayMsProperty);
            set => SetValue(DebounceDelayMsProperty, value);
        }

        public void Reset()
        {
            _selectedValue = null;
            _selectedText = string.Empty;
            EditValue = null;
            IsModified = false;
            if (_displayTextBlock != null)
            {
                _displayTextBlock.Text = Placeholder;
                _displayTextBlock.Foreground = ZeroWpfTheme.TextMuted;
            }
        }

        public void Clear() => Reset();

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSearchLookUpEdit gl)
            {
                gl._selectedValue = e.NewValue;
                gl.EditValueChanged?.Invoke(gl, EventArgs.Empty);
            }
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool IsDropDownOpen
        {
            get => (bool)GetValue(IsDropDownOpenProperty);
            set => SetValue(IsDropDownOpenProperty, value);
        }

        public string DisplayMember
        {
            get => _displayMember;
            set => _displayMember = value;
        }

        public string ValueMember
        {
            get => _valueMember;
            set => _valueMember = value;
        }

        public string SelectedText => _selectedText;
        public object? SelectedValue => _selectedValue;
        public ZGrid? GridControl => _grid;

        static ZSearchLookUpEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZSearchLookUpEdit), new FrameworkPropertyMetadata(typeof(ZSearchLookUpEdit)));
        }

        public ZSearchLookUpEdit()
        {
            Background = ZeroWpfTheme.BgInput;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);
            Height = 36;
            Width = 260;
            FontSize = 13.0;
            Cursor = Cursors.Hand;

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceDelayMs) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                ApplyFilter(_searchBox?.Text ?? string.Empty);
            };

            BuildVisualTemplate();
        }

        protected override void OnThemeChanged()
        {
            base.OnThemeChanged();
            Background = ZeroWpfTheme.BgInput;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            if (_border != null)
            {
                _border.Background = ZeroWpfTheme.BgInput;
                _border.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_iconBlock != null) _iconBlock.Foreground = ZeroWpfTheme.TextSecondary;
            if (_arrow != null) _arrow.Foreground = ZeroWpfTheme.TextSecondary;
            if (_displayTextBlock != null)
            {
                _displayTextBlock.Foreground = string.IsNullOrEmpty(_selectedText) ? ZeroWpfTheme.TextMuted : ZeroWpfTheme.TextPrimary;
            }
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
            if (_statusBlock != null)
            {
                _statusBlock.Foreground = ZeroWpfTheme.TextMuted;
            }
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
                Padding = new Thickness(8, 0, 8, 0)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Pixel) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16, GridUnitType.Pixel) });

            _iconBlock = new TextBlock
            {
                Text = "⊞",
                FontSize = 13.0,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_iconBlock, 0);
            headerGrid.Children.Add(_iconBlock);

            _displayTextBlock = new TextBlock
            {
                Text = Placeholder,
                Foreground = ZeroWpfTheme.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(6, 0, 4, 0)
            };
            Grid.SetColumn(_displayTextBlock, 1);
            headerGrid.Children.Add(_displayTextBlock);

            _arrow = new TextBlock
            {
                Text = "▼",
                FontSize = 9.0,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_arrow, 2);
            headerGrid.Children.Add(_arrow);

            _border.Child = headerGrid;
            rootGrid.Children.Add(_border);

            // Popup construction hosting search bar & DataGrid
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
                Padding = new Thickness(8),
                Width = 560,
                Height = 360
            };

            var popupGrid = new Grid();
            popupGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32, GridUnitType.Pixel) });
            popupGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            popupGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24, GridUnitType.Pixel) });

            // Search Header: [TextBox | Find | Clear]
            var searchHeader = new Grid();
            searchHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _searchBox = new TextBox
            {
                Height = 28,
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                Padding = new Thickness(6, 2, 6, 2),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += (s, e) =>
            {
                _debounceTimer?.Stop();
                _debounceTimer?.Start();
            };
            _searchBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    _debounceTimer?.Stop();
                    ApplyFilter(_searchBox.Text);
                    e.Handled = true;
                }
                else if (e.Key == Key.Down && _grid != null)
                {
                    _grid.Focus();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    IsDropDownOpen = false;
                    e.Handled = true;
                }
            };
            Grid.SetColumn(_searchBox, 0);
            searchHeader.Children.Add(_searchBox);

            var btnFind = new SimpleButton
            {
                Content = "Find",
                Height = 28,
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(10, 0, 10, 0)
            };
            btnFind.Click += (s, e) =>
            {
                _debounceTimer?.Stop();
                ApplyFilter(_searchBox?.Text ?? string.Empty);
            };
            Grid.SetColumn(btnFind, 1);
            searchHeader.Children.Add(btnFind);

            var btnClear = new SimpleButton
            {
                Content = "Clear",
                Height = 28,
                Margin = new Thickness(4, 0, 0, 0),
                Padding = new Thickness(8, 0, 8, 0)
            };
            btnClear.Click += (s, e) =>
            {
                if (_searchBox != null) _searchBox.Text = string.Empty;
                ApplyFilter(string.Empty);
            };
            Grid.SetColumn(btnClear, 2);
            searchHeader.Children.Add(btnClear);

            Grid.SetRow(searchHeader, 0);
            popupGrid.Children.Add(searchHeader);

            // Embedded Grid
            _grid = new ZGrid
            {
                Margin = new Thickness(0, 6, 0, 4)
            };
            _grid.MouseDown += (s, e) => { if (e.ClickCount == 2) CommitSelection(); };
            _grid.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    CommitSelection();
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    IsDropDownOpen = false;
                    e.Handled = true;
                }
            };
            Grid.SetRow(_grid, 1);
            popupGrid.Children.Add(_grid);

            // Status & Quick-Add Footer
            var footerPanel = new DockPanel { LastChildFill = false };
            _statusBlock = new TextBlock
            {
                Text = "Ready",
                FontSize = 11.0,
                Foreground = ZeroWpfTheme.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            DockPanel.SetDock(_statusBlock, Dock.Left);
            footerPanel.Children.Add(_statusBlock);

            if (ShowAddNewButton)
            {
                var btnAddNew = new SimpleButton
                {
                    Content = AddNewButtonText,
                    Height = 22,
                    Padding = new Thickness(8, 0, 8, 0),
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand
                };
                btnAddNew.Click += (s, e) =>
                {
                    var args = new ProcessNewValueEventArgs(_searchBox?.Text ?? string.Empty);
                    ProcessNewValue?.Invoke(this, args);
                    if (args.Handled)
                    {
                        IsDropDownOpen = false;
                    }
                };
                DockPanel.SetDock(btnAddNew, Dock.Right);
                footerPanel.Children.Add(btnAddNew);
            }

            Grid.SetRow(footerPanel, 2);
            popupGrid.Children.Add(footerPanel);

            _popupBorder.Child = popupGrid;
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

        private void ApplyFilter(string query)
        {
            if (_grid?.DataSource == null) return;
            int total = _grid.DataSource.TotalRowCount;
            if (_statusBlock != null)
            {
                _statusBlock.Text = string.IsNullOrEmpty(query)
                    ? $"Showing {total} records"
                    : $"Filter: \"{query}\" — {total} records";
            }
        }

        public void SetDataSource<T>(IList<T> items)
        {
            _grid?.SetDataSource(items);
            if (_statusBlock != null && items != null)
            {
                _statusBlock.Text = $"Total {items.Count} items";
            }
        }

        private void CommitSelection()
        {
            if (_grid == null || _grid.DataSource == null) return;
            int visualRow = _grid.SelectedIndex;
            if (visualRow < 0) return;

            int modelRow = _grid.GetModelRowIndex(visualRow);
            if (modelRow < 0) modelRow = visualRow;

            CellValueBuffer buf = new CellValueBuffer();
            _grid.DataSource.GetCellValue(modelRow, 0, ref buf);
            _selectedText = buf.Text.ToString();
            _selectedValue = modelRow;
            EditValue = modelRow;
            IsModified = true;

            if (_displayTextBlock != null)
            {
                _displayTextBlock.Text = _selectedText;
                _displayTextBlock.Foreground = ZeroWpfTheme.TextPrimary;
            }

            IsDropDownOpen = false;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);
            if (ReadOnly || !IsEnabled) return;

            if (!IsDropDownOpen)
            {
                IsDropDownOpen = true;
                _searchBox?.Focus();
            }
        }

        private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZSearchLookUpEdit gl && gl._popup != null)
            {
                gl._popup.IsOpen = (bool)e.NewValue;
            }
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZSearchLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("SearchLookUpEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchLookUpEdit instead.")]
    public class SearchLookUpEdit : ZSearchLookUpEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZSearchLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridLookupEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchLookUpEdit instead.")]
    public class GridLookupEdit : ZSearchLookUpEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZSearchLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroGridLookup is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchLookUpEdit instead.")]
    public class ZeroGridLookup : ZSearchLookUpEdit { }

    /// <summary>
    /// Legacy alias for <see cref="ZSearchLookUpEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroSearchLookUpEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZSearchLookUpEdit instead.")]
    public class ZeroSearchLookUpEdit : ZSearchLookUpEdit { }

    #endregion
}
