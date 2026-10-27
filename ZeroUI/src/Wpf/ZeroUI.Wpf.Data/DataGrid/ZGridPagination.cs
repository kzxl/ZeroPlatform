using System;
using System.Windows;
using System.Windows.Controls;
using ZeroUI.Wpf.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.DataGrid
{
    /// <summary>
    /// Modern pagination toolbar for GridControl in WPF.
    /// </summary>
    public class ZGridPagination : Border
    {
        private readonly ComboBoxEdit _pageSizeCombo;
        private readonly SimpleButton _prevBtn;
        private readonly SimpleButton _nextBtn;
        private readonly TextBlock _pageInfo;
        private readonly TextBlock _recordsSummary;
        private readonly TextBlock _sizeLabel;

        private int _currentPage = 1;
        private int _pageSize = 100;
        private int _totalRecords = 0;

        public event EventHandler<int>? PageChanged;
        public event EventHandler<int>? PageSizeChanged;

        public int CurrentPage => _currentPage;
        public int PageSize => _pageSize;
        public int TotalPages => _pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)_totalRecords / _pageSize));

        public ZGridPagination()
        {
            Height = 40;
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(0, 1, 0, 0);
            Padding = new Thickness(12, 4, 12, 4);

            var stack = new DockPanel();

            // Left: Record summary
            _recordsSummary = new TextBlock
            {
                Text = "Total records: 0",
                FontSize = 12,
                Foreground = ZeroWpfTheme.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_recordsSummary, Dock.Left);
            stack.Children.Add(_recordsSummary);

            // Right: Navigation stack
            var rightPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            _sizeLabel = new TextBlock
            {
                Text = "Page size:",
                FontSize = 12,
                Foreground = ZeroWpfTheme.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            rightPanel.Children.Add(_sizeLabel);

            _pageSizeCombo = new ComboBoxEdit
            {
                Width = 78,
                Height = 26,
                FontSize = 12,
                Margin = new Thickness(0, 0, 16, 0)
            };
            _pageSizeCombo.Items.Add("50");
            _pageSizeCombo.Items.Add("100");
            _pageSizeCombo.Items.Add("500");
            _pageSizeCombo.Items.Add("1,000");
            _pageSizeCombo.Items.Add("All");
            _pageSizeCombo.SelectedIndex = 1; // 100
            _pageSizeCombo.SelectionChanged += (s, e) =>
            {
                string sel = _pageSizeCombo.SelectedItem?.ToString() ?? "100";
                _pageSize = sel switch
                {
                    "50" => 50,
                    "100" => 100,
                    "500" => 500,
                    "1,000" => 1000,
                    "All" => int.MaxValue,
                    _ => 100
                };
                _currentPage = 1;
                UpdateState();
                PageSizeChanged?.Invoke(this, _pageSize);
            };
            rightPanel.Children.Add(_pageSizeCombo);

            _prevBtn = new SimpleButton
            {
                Variant = ButtonVariant.Secondary,
                Content = "◀",
                Width = 30,
                Height = 26,
                Padding = new Thickness(0),
                FontSize = 10,
                Margin = new Thickness(0, 0, 6, 0)
            };
            _prevBtn.Click += (s, e) =>
            {
                if (_currentPage > 1)
                {
                    _currentPage--;
                    UpdateState();
                    PageChanged?.Invoke(this, _currentPage);
                }
            };
            rightPanel.Children.Add(_prevBtn);

            _pageInfo = new TextBlock
            {
                Text = "1 / 1",
                FontSize = 12,
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };
            rightPanel.Children.Add(_pageInfo);

            _nextBtn = new SimpleButton
            {
                Variant = ButtonVariant.Secondary,
                Content = "▶",
                Width = 30,
                Height = 26,
                Padding = new Thickness(0),
                FontSize = 10
            };
            _nextBtn.Click += (s, e) =>
            {
                if (_currentPage < TotalPages)
                {
                    _currentPage++;
                    UpdateState();
                    PageChanged?.Invoke(this, _currentPage);
                }
            };
            rightPanel.Children.Add(_nextBtn);

            DockPanel.SetDock(rightPanel, Dock.Right);
            stack.Children.Add(rightPanel);

            Child = stack;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged += OnThemeChanged;
        }

        private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
        {
            ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            _recordsSummary.Foreground = ZeroWpfTheme.TextSecondary;
            _sizeLabel.Foreground = ZeroWpfTheme.TextMuted;
            _pageInfo.Foreground = ZeroWpfTheme.TextPrimary;
        }

        public void UpdateTotalRecords(int total)
        {
            _totalRecords = total;
            UpdateState();
        }

        private void UpdateState()
        {
            int totalP = TotalPages;
            _pageInfo.Text = $"{_currentPage} / {totalP}";
            _prevBtn.IsEnabled = _currentPage > 1;
            _nextBtn.IsEnabled = _currentPage < totalP;
            _recordsSummary.Text = $"Total records: {_totalRecords:N0}";
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZGridPagination"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("GridPagination is deprecated and will be removed in 5 release cycles. Please migrate to ZGridPagination instead.")]
    public class GridPagination : ZGridPagination { }

    /// <summary>
    /// Legacy alias for <see cref="ZGridPagination"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroGridPagination is deprecated and will be removed in 5 release cycles. Please migrate to ZGridPagination instead.")]
    public class ZeroGridPagination : ZGridPagination { }

    #endregion
}
