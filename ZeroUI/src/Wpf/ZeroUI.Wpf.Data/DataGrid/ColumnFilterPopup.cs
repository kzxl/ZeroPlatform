using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    /// <summary>
    /// Excel-style distinct value search and multi-checkbox filter popup.
    /// </summary>
    public class ColumnFilterPopup : Popup
    {
        private readonly int _columnIndex;
        private readonly string _columnName;
        private readonly Action<int, HashSet<string>?> _applyFilterCallback;

        private readonly TextBox _searchBox;
        private readonly CheckBox _selectAllBox;
        private readonly StackPanel _itemsPanel;
        private readonly List<(string Value, CheckBox CheckBox)> _valueItems = new List<(string, CheckBox)>();
        private readonly HashSet<string> _initialSelection;

        public ColumnFilterPopup(
            UIElement placementTarget,
            int columnIndex,
            string columnName,
            IEnumerable<string> distinctValues,
            HashSet<string>? currentSelectedValues,
            Action<int, HashSet<string>?> applyFilterCallback)
        {
            _columnIndex = columnIndex;
            _columnName = columnName;
            _applyFilterCallback = applyFilterCallback;
            _initialSelection = currentSelectedValues != null ? new HashSet<string>(currentSelectedValues, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            PlacementTarget = placementTarget;
            Placement = PlacementMode.Bottom;
            StaysOpen = false;
            AllowsTransparency = true;

            var border = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10),
                Width = 240,
                MaxHeight = 360,
                SnapsToDevicePixels = true
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Search
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Select all
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Scroll list
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Buttons

            // Header title
            var titleBlock = new TextBlock
            {
                Text = $"Filter: {columnName}",
                FontWeight = FontWeights.Bold,
                Foreground = ZeroWpfTheme.TextPrimary,
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(titleBlock, 0);
            mainGrid.Children.Add(titleBlock);

            // Search box
            _searchBox = new TextBox
            {
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                Padding = new Thickness(6, 4, 6, 4),
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 6)
            };
            _searchBox.TextChanged += (s, e) => FilterDisplayedItems(_searchBox.Text);
            Grid.SetRow(_searchBox, 1);
            mainGrid.Children.Add(_searchBox);

            // Select All Checkbox
            _selectAllBox = new CheckBox
            {
                Content = "(Select All)",
                Foreground = ZeroWpfTheme.TextPrimary,
                IsChecked = _initialSelection.Count == 0,
                Margin = new Thickness(2, 0, 0, 6),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11.5
            };
            _selectAllBox.Click += (s, e) =>
            {
                bool checkAll = _selectAllBox.IsChecked == true;
                for (int i = 0; i < _valueItems.Count; i++)
                {
                    if (_valueItems[i].CheckBox.Visibility == Visibility.Visible)
                    {
                        _valueItems[i].CheckBox.IsChecked = checkAll;
                    }
                }
            };
            Grid.SetRow(_selectAllBox, 2);
            mainGrid.Children.Add(_selectAllBox);

            // Scroll list of distinct values
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 180,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _itemsPanel = new StackPanel();

            foreach (var val in distinctValues)
            {
                string displayVal = string.IsNullOrEmpty(val) ? "(Blank)" : val;
                bool isChecked = _initialSelection.Count == 0 || _initialSelection.Contains(val);

                var cb = new CheckBox
                {
                    Content = displayVal,
                    Foreground = ZeroWpfTheme.TextPrimary,
                    IsChecked = isChecked,
                    Margin = new Thickness(4, 2, 2, 2),
                    FontSize = 11.5
                };
                _valueItems.Add((val, cb));
                _itemsPanel.Children.Add(cb);
            }

            scroll.Content = _itemsPanel;
            Grid.SetRow(scroll, 3);
            mainGrid.Children.Add(scroll);

            // Action buttons row
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var clearBtn = new Button
            {
                Content = "Clear",
                Background = Brushes.Transparent,
                Foreground = ZeroWpfTheme.TextSecondary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 11,
                Margin = new Thickness(0, 0, 6, 0)
            };
            clearBtn.Click += (s, e) =>
            {
                _applyFilterCallback(_columnIndex, null);
                IsOpen = false;
            };

            var applyBtn = new Button
            {
                Content = "Apply",
                Background = ZeroWpfTheme.PrimaryAccent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 4, 12, 4),
                FontSize = 11,
                FontWeight = FontWeights.Bold
            };
            applyBtn.Click += (s, e) =>
            {
                var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                bool allChecked = true;
                for (int i = 0; i < _valueItems.Count; i++)
                {
                    if (_valueItems[i].CheckBox.IsChecked == true)
                    {
                        selected.Add(_valueItems[i].Value);
                    }
                    else
                    {
                        allChecked = false;
                    }
                }

                if (allChecked)
                {
                    _applyFilterCallback(_columnIndex, null); // no filter
                }
                else
                {
                    _applyFilterCallback(_columnIndex, selected);
                }
                IsOpen = false;
            };

            btnPanel.Children.Add(clearBtn);
            btnPanel.Children.Add(applyBtn);

            Grid.SetRow(btnPanel, 4);
            mainGrid.Children.Add(btnPanel);

            border.Child = mainGrid;
            Child = border;
        }

        private void FilterDisplayedItems(string query)
        {
            bool hasQuery = !string.IsNullOrWhiteSpace(query);
            for (int i = 0; i < _valueItems.Count; i++)
            {
                var item = _valueItems[i];
                if (!hasQuery)
                {
                    item.CheckBox.Visibility = Visibility.Visible;
                }
                else
                {
                    bool match = item.Value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    item.CheckBox.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ColumnFilterPopup"/>.
    /// </summary>
    [Obsolete("ZeroColumnFilterPopup is deprecated. Use ColumnFilterPopup instead.")]
    public class ZeroColumnFilterPopup : ColumnFilterPopup
    {
        public ZeroColumnFilterPopup(
            UIElement placementTarget,
            int columnIndex,
            string columnName,
            IEnumerable<string> distinctValues,
            HashSet<string>? currentSelectedValues,
            Action<int, HashSet<string>?> applyFilterCallback)
            : base(placementTarget, columnIndex, columnName, distinctValues, currentSelectedValues, applyFilterCallback)
        {
        }
    }
}
