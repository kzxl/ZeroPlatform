using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Common;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    /// <summary>
    /// Modern debounced search and density toolbar for GridControl in WPF.
    /// </summary>
    public class ZGridSearchBar : Border
    {
        private readonly TextBox _searchTextBox;
        private readonly TextBlock _matchCounter;
        private readonly SimpleButton _densityBtn;
        private readonly SimpleButton _exportBtn;
        private readonly DispatcherTimer _debounceTimer;

        private GridDensity _currentDensity = GridDensity.Middle;

        public event EventHandler<string>? SearchTriggered;
        public event EventHandler<GridDensity>? DensityChanged;
        public event EventHandler? ExportTriggered;

        public ZGridSearchBar()
        {
            Height = 44;
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(0, 0, 0, 1);
            Padding = new Thickness(12, 6, 12, 6);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); // Search box
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // Match counter
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Spacer
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // Density button
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // Export button

            // 1. Search Box container
            var searchBoxBorder = new Border
            {
                Background = ZeroWpfTheme.BgInput,
                BorderBrush = ZeroWpfTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 2, 8, 2)
            };

            var searchStack = new DockPanel();
            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                Foreground = ZeroWpfTheme.TextMuted
            };
            DockPanel.SetDock(searchIcon, Dock.Left);
            searchStack.Children.Add(searchIcon);

            _searchTextBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = ZeroWpfTheme.TextPrimary,
                CaretBrush = ZeroWpfTheme.PrimaryAccent,
                FontSize = 12.5,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            searchStack.Children.Add(_searchTextBox);
            searchBoxBorder.Child = searchStack;
            Grid.SetColumn(searchBoxBorder, 0);
            grid.Children.Add(searchBoxBorder);

            // 2. Match Counter
            _matchCounter = new TextBlock
            {
                Text = "",
                FontSize = 12,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 0, 0)
            };
            Grid.SetColumn(_matchCounter, 1);
            grid.Children.Add(_matchCounter);

            // 3. Density Button
            _densityBtn = CreateToolbarButton("📏 Density: Normal");
            _densityBtn.Margin = new Thickness(0, 0, 8, 0);
            _densityBtn.Click += (s, e) => ToggleDensity();
            Grid.SetColumn(_densityBtn, 3);
            grid.Children.Add(_densityBtn);

            // 4. Export Button
            _exportBtn = CreateToolbarButton("📥 Export CSV");
            _exportBtn.Click += (s, e) => ExportTriggered?.Invoke(this, EventArgs.Empty);
            Grid.SetColumn(_exportBtn, 4);
            grid.Children.Add(_exportBtn);

            Child = grid;

            // Debounce timer (150ms)
            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                SearchTriggered?.Invoke(this, _searchTextBox.Text.Trim());
            };

            _searchTextBox.TextChanged += (s, e) =>
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void UpdateTheme()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            _searchTextBox.Foreground = ZeroWpfTheme.TextPrimary;
            _matchCounter.Foreground = ZeroWpfTheme.TextSecondary;
        }

        public void SetMatchCount(int count, int total)
        {
            if (count == total)
            {
                _matchCounter.Text = $"Showing {total:N0} records";
            }
            else
            {
                _matchCounter.Text = $"Matched {count:N0} of {total:N0} records";
            }
        }

        private void ToggleDensity()
        {
            _currentDensity = _currentDensity switch
            {
                GridDensity.Compact => GridDensity.Middle,
                GridDensity.Middle => GridDensity.Loose,
                GridDensity.Loose => GridDensity.Compact,
                _ => GridDensity.Middle
            };

            _densityBtn.Content = _currentDensity switch
            {
                GridDensity.Compact => "📏 Density: Compact",
                GridDensity.Middle => "📏 Density: Normal",
                GridDensity.Loose => "📏 Density: Comfortable",
                _ => "📏 Density"
            };

            DensityChanged?.Invoke(this, _currentDensity);
        }

        private SimpleButton CreateToolbarButton(string title)
        {
            var btn = new SimpleButton
            {
                Variant = ButtonVariant.Secondary,
                Content = title,
                Height = 30,
                Padding = new Thickness(10, 2, 10, 2),
                FontSize = 12
            };
            return btn;
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += UpdateTheme;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= UpdateTheme;
    }
}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZGridSearchBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridSearchBar is deprecated and will be removed in 5 release cycles. Please migrate to ZGridSearchBar instead.")]
    public class GridSearchBar : ZGridSearchBar { }

    /// <summary>
    /// Legacy alias for <see cref="ZGridSearchBar"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroGridSearchBar is deprecated and will be removed in 5 release cycles. Please migrate to ZGridSearchBar instead.")]
    public class ZeroGridSearchBar : ZGridSearchBar { }

    #endregion
}
