using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Data;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    /// <summary>
    /// Runtime Column Customization tool window for ZeroUI WPF GridControl.
    /// Allows users to view hidden columns and restore their visibility via double-click or button.
    /// </summary>
    public class ColumnChooserWindow : Window
    {
        private readonly ZGrid _grid;
        private readonly ListBox _hiddenColumnsList;
        private readonly Button _btnShowColumn;
        private readonly Button _btnShowAll;
        private readonly TextBlock _lblInfo;

        public ColumnChooserWindow(ZGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));

            Title = "Customization";
            WindowStyle = WindowStyle.ToolWindow;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = 260;
            Height = 340;
            MinWidth = 200;
            MinHeight = 220;
            Topmost = true;
            ShowInTaskbar = false;

            ApplyThemeStyles();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            var rootGrid = new Grid
            {
                Margin = new Thickness(10)
            };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _lblInfo = new TextBlock
            {
                Text = "Double-click column to show in grid:",
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 8)
            };
            Grid.SetRow(_lblInfo, 0);
            rootGrid.Children.Add(_lblInfo);

            _hiddenColumnsList = new ListBox
            {
                DisplayMemberPath = nameof(ZeroColumn.HeaderText),
                BorderThickness = new Thickness(1),
                FontSize = 12.5,
                Margin = new Thickness(0, 0, 0, 10)
            };
            _hiddenColumnsList.MouseDoubleClick += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                {
                    ShowSelectedColumn();
                }
            };
            Grid.SetRow(_hiddenColumnsList, 1);
            rootGrid.Children.Add(_hiddenColumnsList);

            var bottomGrid = new Grid();
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            bottomGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnShowColumn = new Button
            {
                Content = "Add Column",
                Height = 30,
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            _btnShowColumn.Click += (s, e) => ShowSelectedColumn();
            Grid.SetColumn(_btnShowColumn, 0);
            bottomGrid.Children.Add(_btnShowColumn);

            _btnShowAll = new Button
            {
                Content = "Show All",
                Height = 30,
                FontSize = 12,
                Cursor = Cursors.Hand
            };
            _btnShowAll.Click += (s, e) => ShowAllColumns();
            Grid.SetColumn(_btnShowAll, 2);
            bottomGrid.Children.Add(_btnShowAll);

            Grid.SetRow(bottomGrid, 2);
            rootGrid.Children.Add(bottomGrid);

            Content = rootGrid;

            RefreshColumns();
        }

        private void ApplyThemeStyles()
        {
            Background = ZeroWpfTheme.BgCard;
            Foreground = ZeroWpfTheme.TextPrimary;

            if (_lblInfo != null)
            {
                _lblInfo.Foreground = ZeroWpfTheme.TextSecondary;
            }

            if (_hiddenColumnsList != null)
            {
                _hiddenColumnsList.Background = ZeroWpfTheme.BgInput;
                _hiddenColumnsList.Foreground = ZeroWpfTheme.TextPrimary;
                _hiddenColumnsList.BorderBrush = ZeroWpfTheme.BorderDefault;
            }

            if (_btnShowColumn != null)
            {
                _btnShowColumn.Background = ZeroWpfTheme.PrimaryAccent;
                _btnShowColumn.Foreground = Brushes.White;
                _btnShowColumn.BorderBrush = ZeroWpfTheme.PrimaryAccentDark;
            }

            if (_btnShowAll != null)
            {
                _btnShowAll.Background = ZeroWpfTheme.BgHover;
                _btnShowAll.Foreground = ZeroWpfTheme.TextPrimary;
                _btnShowAll.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
        }

        public void RefreshColumns()
        {
            _hiddenColumnsList.Items.Clear();

            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                var col = _grid.Columns[i];
                if (!col.IsVisible)
                {
                    _hiddenColumnsList.Items.Add(col);
                }
            }

            bool hasItems = _hiddenColumnsList.Items.Count > 0;
            _btnShowColumn.IsEnabled = hasItems;
            _btnShowAll.IsEnabled = hasItems;
        }

        private void ShowSelectedColumn()
        {
            if (_hiddenColumnsList.SelectedItem is ZeroColumn col)
            {
                col.IsVisible = true;
                _grid.InvalidateVisual();
                RefreshColumns();
            }
        }

        private void ShowAllColumns()
        {
            for (int i = 0; i < _grid.Columns.Count; i++)
            {
                _grid.Columns[i].IsVisible = true;
            }
            _grid.InvalidateVisual();
            RefreshColumns();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            Hide();
            base.OnClosing(e);
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += ApplyThemeStyles;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= ApplyThemeStyles;
    }
}
}
