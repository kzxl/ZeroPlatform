using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Layout;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Docking
{
    public enum DockPosition
    {
        Left,
        Right,
        Top,
        Bottom,
        Document
    }

    [Obsolete("ZeroDockPosition is deprecated. Use DockPosition instead.")]
    public enum ZeroDockPosition
    {
        Left = DockPosition.Left,
        Right = DockPosition.Right,
        Top = DockPosition.Top,
        Bottom = DockPosition.Bottom,
        Document = DockPosition.Document
    }

    /// <summary>
    /// Represents an individual docking panel with headers, pin toggle, and hosted content.
    /// </summary>
    public class ZDockPanelControl : HeaderedContentControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZDockPanelControl), new PropertyMetadata("Panel"));

        public static readonly DependencyProperty DockPositionProperty =
            DependencyProperty.Register(nameof(DockPosition), typeof(DockPosition), typeof(ZDockPanelControl), new PropertyMetadata(DockPosition.Document));

        public static readonly DependencyProperty IsPinnedProperty =
            DependencyProperty.Register(nameof(IsPinned), typeof(bool), typeof(ZDockPanelControl), new PropertyMetadata(true));

        public string PanelKey { get; set; } = string.Empty;

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public DockPosition DockPosition
        {
            get => (DockPosition)GetValue(DockPositionProperty);
            set => SetValue(DockPositionProperty, value);
        }

        public bool IsPinned
        {
            get => (bool)GetValue(IsPinnedProperty);
            set => SetValue(IsPinnedProperty, value);
        }

        public ZDockPanelControl()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZDockPanelControl"/>.
    /// </summary>
    [Obsolete("DockPanelControl is deprecated and will be removed in 5 release cycles. Please migrate to ZDockPanelControl instead.")]
    public class DockPanelControl : ZDockPanelControl
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDockPanelControl"/>.
    /// </summary>
    [Obsolete("ZeroDockPanel is deprecated and will be removed in 5 release cycles. Please migrate to ZDockPanelControl instead.")]
    public class ZeroDockPanel : ZDockPanelControl
    {
    }

    /// <summary>
    /// Visual Studio-style multi-region dock manager hosting Left, Right, Top, Bottom,
    /// and Document tab panels with interactive splitters, pin toggles, and layout serialization.
    /// </summary>
    public class ZDockManager : Grid
    {
        private readonly ObservableCollection<ZDockPanelControl> _panels = new ObservableCollection<ZDockPanelControl>();
        private readonly TabControl _documentTabs;
        private readonly ContentControl _leftHost;
        private readonly ContentControl _rightHost;
        private readonly ContentControl _bottomHost;
        private readonly Grid _centerGrid;

        public ObservableCollection<ZDockPanelControl> Panels => _panels;

        public ZDockManager()
        {
            Background = ZeroWpfTheme.BgPrimary;

            // Define grid structure: Left (Col 0), Splitter (Col 1), Center (Col 2), Splitter (Col 3), Right (Col 4)
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240, GridUnitType.Pixel), MinWidth = 120 });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4, GridUnitType.Pixel) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260, GridUnitType.Pixel), MinWidth = 120 });

            // Center area has Document Tabs (Row 0), Splitter (Row 1), Bottom (Row 2)
            _centerGrid = new Grid();
            _centerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _centerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4, GridUnitType.Pixel) });
            _centerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(160, GridUnitType.Pixel), MinHeight = 80 });

            _documentTabs = new TabControl
            {
                Background = ZeroWpfTheme.BgPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1)
            };
            Grid.SetRow(_documentTabs, 0);
            _centerGrid.Children.Add(_documentTabs);

            var hSplitter = new GridSplitter
            {
                Height = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Background = ZeroWpfTheme.BorderDefault
            };
            Grid.SetRow(hSplitter, 1);
            _centerGrid.Children.Add(hSplitter);

            _bottomHost = new ContentControl { Background = ZeroWpfTheme.BgCard };
            Grid.SetRow(_bottomHost, 2);
            _centerGrid.Children.Add(_bottomHost);

            Grid.SetColumn(_centerGrid, 2);
            Children.Add(_centerGrid);

            // Left Host & Splitter
            _leftHost = new ContentControl { Background = ZeroWpfTheme.BgCard };
            Grid.SetColumn(_leftHost, 0);
            Children.Add(_leftHost);

            var vSplitterLeft = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = ZeroWpfTheme.BorderDefault
            };
            Grid.SetColumn(vSplitterLeft, 1);
            Children.Add(vSplitterLeft);

            // Right Host & Splitter
            _rightHost = new ContentControl { Background = ZeroWpfTheme.BgCard };
            Grid.SetColumn(_rightHost, 4);
            Children.Add(_rightHost);

            var vSplitterRight = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = ZeroWpfTheme.BorderDefault
            };
            Grid.SetColumn(vSplitterRight, 3);
            Children.Add(vSplitterRight);

            _panels.CollectionChanged += (s, e) => RebuildLayout();
        }

        public void AddPanel(ZDockPanelControl panel)
        {
            _panels.Add(panel);
        }

        private static void DisconnectElement(object? content)
        {
            if (content is FrameworkElement fe && fe.Parent != null)
            {
                if (fe.Parent is ContentControl cc)
                {
                    cc.Content = null;
                }
                else if (fe.Parent is Decorator decorator)
                {
                    decorator.Child = null;
                }
                else if (fe.Parent is Panel panel)
                {
                    panel.Children.Remove(fe);
                }
            }
        }

        public void RebuildLayout()
        {
            _documentTabs.Items.Clear();
            _leftHost.Content = null;
            _rightHost.Content = null;
            _bottomHost.Content = null;

            foreach (var panel in _panels)
            {
                DisconnectElement(panel.Content);

                switch (panel.DockPosition)
                {
                    case DockPosition.Document:
                        var tabItem = new TabItem
                        {
                            Header = panel.Title,
                            Content = panel.Content
                        };
                        _documentTabs.Items.Add(tabItem);
                        break;

                    case DockPosition.Left:
                        if (_leftHost.Content == null)
                        {
                            _leftHost.Content = CreatePanelWrapper(panel);
                        }
                        break;

                    case DockPosition.Right:
                        if (_rightHost.Content == null)
                        {
                            _rightHost.Content = CreatePanelWrapper(panel);
                        }
                        break;

                    case DockPosition.Bottom:
                        if (_bottomHost.Content == null)
                        {
                            _bottomHost.Content = CreatePanelWrapper(panel);
                        }
                        break;
                }
            }

            if (_documentTabs.Items.Count > 0 && _documentTabs.SelectedIndex == -1)
            {
                _documentTabs.SelectedIndex = 0;
            }
        }

        private UIElement CreatePanelWrapper(ZDockPanelControl panel)
        {
            var headerGrid = new Grid
            {
                Background = ZeroWpfTheme.BgCard,
                Height = 28
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32, GridUnitType.Pixel) });

            var titleBlock = new TextBlock
            {
                Text = panel.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(titleBlock, 0);
            headerGrid.Children.Add(titleBlock);

            var pinBtn = new Button
            {
                Content = "📌",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = ZeroWpfTheme.TextSecondary,
                FontSize = 10,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(pinBtn, 1);
            headerGrid.Children.Add(pinBtn);

            var mainPanel = new DockPanel();
            DockPanel.SetDock(headerGrid, Dock.Top);
            mainPanel.Children.Add(headerGrid);

            var border = new Border
            {
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Child = panel.Content as UIElement
            };
            mainPanel.Children.Add(border);

            return new Border
            {
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                Child = mainPanel
            };
        }

        #region Dock Layout Serialization & Persistence

        /// <summary>
        /// Captures the complete current WPF docking layout state to a WorkspaceLayoutState.
        /// </summary>
        public WorkspaceLayoutState SaveLayout()
        {
            var state = new WorkspaceLayoutState
            {
                Version = "1.1",
                SavedAt = DateTime.UtcNow,
                Containers = new DockContainerLayoutState
                {
                    LeftWidth = (int)ColumnDefinitions[0].Width.Value,
                    RightWidth = (int)ColumnDefinitions[4].Width.Value,
                    BottomHeight = _centerGrid != null ? (int)_centerGrid.RowDefinitions[2].Height.Value : 160,
                    ActiveDocumentTitle = (_documentTabs.SelectedItem as TabItem)?.Header?.ToString() ?? string.Empty
                }
            };

            for (int i = 0; i < _panels.Count; i++)
            {
                var p = _panels[i];
                state.DockPanels.Add(new DockPanelLayoutState
                {
                    Name = !string.IsNullOrEmpty(p.PanelKey) ? p.PanelKey : p.Title,
                    Title = p.Title,
                    DockPosition = p.DockPosition.ToString(),
                    IsPinned = p.IsPinned,
                    OrderIndex = i
                });
            }

            return state;
        }

        /// <summary>
        /// Restores docking layout from a saved WorkspaceLayoutState.
        /// </summary>
        public void RestoreLayout(WorkspaceLayoutState state)
        {
            if (state == null) return;

            if (state.Containers != null)
            {
                if (state.Containers.LeftWidth > 0)
                    ColumnDefinitions[0].Width = new GridLength(state.Containers.LeftWidth, GridUnitType.Pixel);
                if (state.Containers.RightWidth > 0)
                    ColumnDefinitions[4].Width = new GridLength(state.Containers.RightWidth, GridUnitType.Pixel);
                if (_centerGrid != null && state.Containers.BottomHeight > 0)
                    _centerGrid.RowDefinitions[2].Height = new GridLength(state.Containers.BottomHeight, GridUnitType.Pixel);
            }

            var map = new Dictionary<string, ZDockPanelControl>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in _panels)
            {
                string key = !string.IsNullOrEmpty(p.PanelKey) ? p.PanelKey : p.Title;
                map[key] = p;
            }

            foreach (var pState in state.DockPanels)
            {
                string key = !string.IsNullOrEmpty(pState.Name) ? pState.Name : pState.Title;
                if (map.TryGetValue(key, out var p))
                {
                    if (Enum.TryParse<DockPosition>(pState.DockPosition, true, out var pos))
                    {
                        p.DockPosition = pos;
                    }
                    p.IsPinned = pState.IsPinned;
                }
            }

            RebuildLayout();

            if (state.Containers != null && !string.IsNullOrEmpty(state.Containers.ActiveDocumentTitle))
            {
                foreach (TabItem item in _documentTabs.Items)
                {
                    if (string.Equals(item.Header?.ToString(), state.Containers.ActiveDocumentTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        _documentTabs.SelectedItem = item;
                        break;
                    }
                }
            }
        }

        public string SaveLayoutToJson() => ZeroWorkspaceSerializer.Serialize(SaveLayout());
        public void RestoreLayoutFromJson(string json) => RestoreLayout(ZeroWorkspaceSerializer.Deserialize(json));
        public void SaveLayout(string filePath) => System.IO.File.WriteAllText(filePath, SaveLayoutToJson(), System.Text.Encoding.UTF8);
        public void RestoreLayout(string filePath)
        {
            if (System.IO.File.Exists(filePath))
                RestoreLayoutFromJson(System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8));
        }

        #endregion
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZDockManager"/>.
    /// </summary>
    [Obsolete("DockManager is deprecated and will be removed in 5 release cycles. Please migrate to ZDockManager instead.")]
    public class DockManager : ZDockManager
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZDockManager"/>.
    /// </summary>
    [Obsolete("ZeroDockManager is deprecated and will be removed in 5 release cycles. Please migrate to ZDockManager instead.")]
    public class ZeroDockManager : ZDockManager
    {
    }
}
