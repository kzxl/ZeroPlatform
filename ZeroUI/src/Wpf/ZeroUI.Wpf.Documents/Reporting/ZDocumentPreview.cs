using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Localization;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Reporting
{
    /// <summary>
    /// Vector Document & Report Print Preview Control for ZeroUI WPF.
    /// Provides integrated document pagination, high-DPI paper canvas preview with drop shadow,
    /// dynamic zoom (25% to 500%), page navigation, and direct hardware print dispatch.
    /// </summary>
    public class ZDocumentPreview : Control
    {
        private readonly List<Visual> _pages = new List<Visual>();
        private Border? _toolbar;
        private readonly ScrollViewer _scrollViewer;
        private readonly Border _paperCanvas;
        private readonly TextBlock _pageStatusLabel;
        private readonly ComboBox _zoomCombo;

        private int _currentPageIndex = 0;
        private double _zoomFactor = 1.0;

        // Standard A4 Paper in DIP at 96 DPI: 210mm x 297mm ≈ 794 x 1123 DIP
        private double _paperWidth = 794;
        private double _paperHeight = 1123;
        private Grid? _rootGrid;

        private readonly Button _btnPrint;
        private readonly Button _btnFit;

        public event EventHandler? PrintRequested;

        public int PageCount => _pages.Count > 0 ? _pages.Count : 1;

        public int CurrentPageIndex
        {
            get => _currentPageIndex;
            set
            {
                int clamped = Math.Max(0, Math.Min(PageCount - 1, value));
                if (_currentPageIndex != clamped)
                {
                    _currentPageIndex = clamped;
                    UpdatePageDisplay();
                }
            }
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                _zoomFactor = Math.Max(0.25, Math.Min(5.0, value));
                ApplyZoom();
            }
        }

        public double PaperWidth
        {
            get => _paperWidth;
            set { _paperWidth = value; UpdatePageDisplay(); }
        }

        public double PaperHeight
        {
            get => _paperHeight;
            set { _paperHeight = value; UpdatePageDisplay(); }
        }

        public ZDocumentPreview()
        {
            Background = ZeroWpfTheme.BgPrimary;
            ClipToBounds = true;

            var rootGrid = new Grid();
            _rootGrid = rootGrid;
            AddLogicalChild(rootGrid);
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Toolbar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Viewport

            // 1. Build Command Toolbar
            _toolbar = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 6, 12, 6)
            };

            var barPanel = new StackPanel { Orientation = Orientation.Horizontal };

            _btnPrint = CreateToolbarButton(ZeroLocalizer.GetString(ZeroStringId.PrintButton), (s, e) => ExecutePrint());
            barPanel.ControlsAdd(_btnPrint);

            var separator1 = CreateSeparator();
            barPanel.Children.Add(separator1);

            var btnZoomOut = CreateToolbarButton("➖", (s, e) => ZoomFactor -= 0.15);
            var btnZoomIn = CreateToolbarButton("➕", (s, e) => ZoomFactor += 0.15);
            _btnFit = CreateToolbarButton(ZeroLocalizer.GetString(ZeroStringId.ZoomFit), (s, e) => ZoomFactor = 1.0);
            barPanel.Children.Add(btnZoomOut);
            barPanel.Children.Add(btnZoomIn);
            barPanel.Children.Add(_btnFit);

            _zoomCombo = new ComboBox
            {
                Width = 72,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0)
            };
            _zoomCombo.Items.Add("50%");
            _zoomCombo.Items.Add("75%");
            _zoomCombo.Items.Add("100%");
            _zoomCombo.Items.Add("125%");
            _zoomCombo.Items.Add("150%");
            _zoomCombo.Items.Add("200%");
            _zoomCombo.SelectedIndex = 2;
            _zoomCombo.SelectionChanged += (s, e) =>
            {
                if (_zoomCombo.SelectedItem is string str && int.TryParse(str.TrimEnd('%'), out int pct))
                {
                    _zoomFactor = pct / 100.0;
                    ApplyZoom();
                }
            };
            barPanel.Children.Add(_zoomCombo);

            var separator2 = CreateSeparator();
            barPanel.Children.Add(separator2);

            var btnFirst = CreateToolbarButton("⏮", (s, e) => CurrentPageIndex = 0);
            var btnPrev = CreateToolbarButton("◀", (s, e) => CurrentPageIndex--);
            var btnNext = CreateToolbarButton("▶", (s, e) => CurrentPageIndex++);
            var btnLast = CreateToolbarButton("⏭", (s, e) => CurrentPageIndex = PageCount - 1);

            barPanel.Children.Add(btnFirst);
            barPanel.Children.Add(btnPrev);

            _pageStatusLabel = new TextBlock
            {
                Text = "Page 1 of 1",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                Foreground = ZeroWpfTheme.TextPrimary,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };
            barPanel.Children.Add(_pageStatusLabel);
            barPanel.Children.Add(btnNext);
            barPanel.Children.Add(btnLast);

            _toolbar.Child = barPanel;
            Grid.SetRow(_toolbar, 0);
            rootGrid.Children.Add(_toolbar);

            // 2. Build Document Paper Canvas within ScrollViewer
            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = ZeroWpfTheme.BgPrimary,
                Padding = new Thickness(24)
            };

            var canvasContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _paperCanvas = new Border
            {
                Width = _paperWidth,
                Height = _paperHeight,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(1),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 315,
                    ShadowDepth = 6,
                    Opacity = 0.35,
                    BlurRadius = 12
                }
            };

            canvasContainer.Children.Add(_paperCanvas);
            _scrollViewer.Content = canvasContainer;
            Grid.SetRow(_scrollViewer, 1);
            rootGrid.Children.Add(_scrollViewer);

            AddVisualChild(rootGrid);
            ZeroLocalizer.CultureChanged += (s, e) =>
            {
                UpdateLocalizedStrings();
                UpdatePageDisplay();
            };
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateLocalizedStrings();
            UpdatePageDisplay();
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

        private void UpdateTheme()
        {
            Background = ZeroWpfTheme.BgPrimary;
            if (_toolbar != null)
            {
                _toolbar.Background = ZeroWpfTheme.BgCard;
                _toolbar.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_scrollViewer != null)
            {
                _scrollViewer.Background = ZeroWpfTheme.BgPrimary;
            }
            if (_pageStatusLabel != null)
            {
                _pageStatusLabel.Foreground = ZeroWpfTheme.TextPrimary;
            }
        }

        public void SetPages(IEnumerable<Visual> pages)
        {
            _pages.Clear();
            if (pages != null)
            {
                _pages.AddRange(pages);
            }
            _currentPageIndex = 0;
            UpdatePageDisplay();
        }

        public void ExecutePrint()
        {
            PrintRequested?.Invoke(this, EventArgs.Empty);

            var dialog = new PrintDialog();
            if (dialog.ShowDialog() == true)
            {
                if (_pages.Count > 0 && _currentPageIndex < _pages.Count)
                {
                    dialog.PrintVisual(_pages[_currentPageIndex], "ZeroUI Document Print");
                }
                else
                {
                    dialog.PrintVisual(_paperCanvas, "ZeroUI Document Print");
                }
            }
        }

        private void ApplyZoom()
        {
            _paperCanvas.LayoutTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
        }

        private void UpdateLocalizedStrings()
        {
            _btnPrint.Content = ZeroLocalizer.GetString(ZeroStringId.PrintButton);
            _btnFit.Content = ZeroLocalizer.GetString(ZeroStringId.ZoomFit);
        }

        private void UpdatePageDisplay()
        {
            _pageStatusLabel.Text = ZeroLocalizer.GetFormattedString(ZeroStringId.PrintStatusFormat, _currentPageIndex + 1, PageCount);
            _paperCanvas.Width = _paperWidth;
            _paperCanvas.Height = _paperHeight;

            if (_pages.Count > 0 && _currentPageIndex < _pages.Count)
            {
                var pageVisual = _pages[_currentPageIndex];
                if (pageVisual is UIElement uiElem)
                {
                    _paperCanvas.Child = uiElem;
                }
                else
                {
                    var host = new VisualHost(pageVisual);
                    _paperCanvas.Child = host;
                }
            }
            else
            {
                // Default placeholder preview
                var sample = new Canvas();
                var text = new TextBlock
                {
                    Text = "Enterprise Document Print Preview\r\nReady for vector printing",
                    Foreground = Brushes.Gray,
                    FontSize = 16,
                    FontFamily = new FontFamily("Segoe UI"),
                    Margin = new Thickness(48, 48, 0, 0)
                };
                sample.Children.Add(text);
                _paperCanvas.Child = sample;
            }
        }

        private static Button CreateToolbarButton(string text, RoutedEventHandler onClick)
        {
            var btn = new Button
            {
                Content = text,
                Height = 28,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(2, 0, 2, 0),
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };
            btn.Click += onClick;
            return btn;
        }

        private static Border CreateSeparator()
        {
            return new Border
            {
                Width = 1,
                Height = 18,
                Background = ZeroWpfTheme.BorderDefault,
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private sealed class VisualHost : FrameworkElement
        {
            private readonly Visual _visual;
            public VisualHost(Visual visual) { _visual = visual; AddVisualChild(visual); }
            protected override int VisualChildrenCount => 1;
            protected override Visual GetVisualChild(int index) => _visual;
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
    /// Convenience alias for <see cref="ZDocumentPreview"/>.
    /// </summary>
    public class ZDocumentPreviewControl : ZDocumentPreview { }

    /// <summary>
    /// Legacy alias for <see cref="ZDocumentPreview"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DocumentPreviewControl is deprecated and will be removed in 5 release cycles. Please migrate to ZDocumentPreview instead.")]
    public class DocumentPreviewControl : ZDocumentPreview { }

    /// <summary>
    /// Legacy alias for <see cref="ZDocumentPreview"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPrintPreview is deprecated and will be removed in 5 release cycles. Please migrate to ZDocumentPreview instead.")]
    public class ZeroPrintPreview : ZDocumentPreview { }

    /// <summary>
    /// Legacy alias for <see cref="ZDocumentPreview"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroDocumentPreview is deprecated and will be removed in 5 release cycles. Please migrate to ZDocumentPreview instead.")]
    public class ZeroDocumentPreview : ZDocumentPreview { }

    #endregion

    internal static class PanelExtensions
    {
        public static void ControlsAdd(this Panel panel, UIElement element)
        {
            panel.Children.Add(element);
        }
    }
}
