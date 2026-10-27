using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using ZeroUI.Core.Pdf;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Reporting
{
    /// <summary>
    /// Vector PDF Document & CAD Schematic Reader for ZeroUI WPF.
    /// Features continuous vertical scrolling, vector anti-aliasing, multi-zoom engine (25%-400%, Fit Width, Fit Page),
    /// interactive full-text search with match highlighting, and direct printer hardware dispatch.
    /// </summary>
    public class ZPdfViewer : Control
    {
        private PdfDocumentModel _document;
        private readonly List<PdfSearchResult> _searchResults = new List<PdfSearchResult>();
        private int _currentSearchIndex = -1;

        // Layout state
        private double _zoomFactor = 1.0;
        private PdfViewMode _viewMode = PdfViewMode.ContinuousScroll;
        private int _currentPageIndex = 0;

        // Visual controls
        private Grid? _rootGrid;
        private Border? _toolbar;
        private ScrollViewer? _scrollViewer;
        private StackPanel? _pagesContainer;
        private TextBlock? _pageStatusLabel;
        private ComboBox? _zoomCombo;
        private TextBox? _txtSearch;
        private TextBlock? _lblSearchCount;
        private Button? _btnViewMode;

        private readonly List<PdfPageVisualHost> _pageVisualHosts = new List<PdfPageVisualHost>();

        public event EventHandler? DocumentChanged;
        public event EventHandler? PageIndexChanged;

        public PdfDocumentModel Document
        {
            get => _document;
            set
            {
                _document = value ?? PdfSampleGenerator.CreateIndustrialCadAndSopDocument();
                _currentPageIndex = 0;
                _searchResults.Clear();
                _currentSearchIndex = -1;
                UpdateSearchUI();
                RebuildPages();
                UpdatePageInfo();
                DocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                double clamped = Math.Max(0.2, Math.Min(4.0, value));
                if (Math.Abs(_zoomFactor - clamped) > 0.001)
                {
                    _zoomFactor = clamped;
                    ApplyZoom();
                    UpdateZoomCombo();
                }
            }
        }

        public PdfViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (_viewMode != value)
                {
                    _viewMode = value;
                    if (_btnViewMode != null)
                    {
                        _btnViewMode.Content = _viewMode == PdfViewMode.ContinuousScroll ? "📜 Continuous" : "📄 Single Page";
                    }
                    RebuildPages();
                }
            }
        }

        public int CurrentPageIndex
        {
            get => _currentPageIndex;
            set
            {
                if (_document != null && _document.Pages.Count > 0)
                {
                    int clamped = Math.Max(0, Math.Min(_document.Pages.Count - 1, value));
                    if (_currentPageIndex != clamped)
                    {
                        _currentPageIndex = clamped;
                        if (_viewMode == PdfViewMode.SinglePage)
                        {
                            RebuildPages();
                        }
                        else
                        {
                            ScrollToPage(_currentPageIndex);
                        }
                        UpdatePageInfo();
                        PageIndexChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        public ZPdfViewer()
        {
            Background = ZeroWpfTheme.BgPrimary;
            ClipToBounds = true;

            // Default document
            _document = PdfSampleGenerator.CreateIndustrialCadAndSopDocument();

            var rootGrid = new Grid();
            _rootGrid = rootGrid;
            AddLogicalChild(rootGrid);

            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Toolbar
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 1: Canvas ScrollViewer

            // 1. Build Command Toolbar
            _toolbar = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 6, 12, 6)
            };

            var barPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Open & Print buttons
            var btnOpen = CreateToolbarButton("📂 Open", (s, e) => OpenFileDialogPrompt());
            var btnPrint = CreateToolbarButton("🖨️ Print", (s, e) => ExecutePrint());
            barPanel.Children.Add(btnOpen);
            barPanel.Children.Add(btnPrint);
            barPanel.Children.Add(CreateSeparator());

            // Page Navigation: First, Prev, Status, Next, Last
            var btnFirst = CreateToolbarButton("⏮", (s, e) => CurrentPageIndex = 0);
            var btnPrev = CreateToolbarButton("◀", (s, e) => CurrentPageIndex--);
            var btnNext = CreateToolbarButton("▶", (s, e) => CurrentPageIndex++);
            var btnLast = CreateToolbarButton("⏭", (s, e) => CurrentPageIndex = _document.PageCount - 1);

            _pageStatusLabel = new TextBlock
            {
                Text = "Page 1 of 3",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 8, 0),
                Foreground = ZeroWpfTheme.TextPrimary,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };

            barPanel.Children.Add(btnFirst);
            barPanel.Children.Add(btnPrev);
            barPanel.Children.Add(_pageStatusLabel);
            barPanel.Children.Add(btnNext);
            barPanel.Children.Add(btnLast);
            barPanel.Children.Add(CreateSeparator());

            // View Mode
            _btnViewMode = CreateToolbarButton("📜 Continuous", (s, e) =>
                ViewMode = (_viewMode == PdfViewMode.ContinuousScroll) ? PdfViewMode.SinglePage : PdfViewMode.ContinuousScroll);
            barPanel.Children.Add(_btnViewMode);
            barPanel.Children.Add(CreateSeparator());

            // Zoom Controls
            var btnZoomOut = CreateToolbarButton("➖", (s, e) => ZoomFactor *= 0.85);
            var btnZoomIn = CreateToolbarButton("➕", (s, e) => ZoomFactor *= 1.15);
            var btnFitWidth = CreateToolbarButton("↔ Fit Width", (s, e) => ApplyFitWidth());
            var btnFitPage = CreateToolbarButton("⛶ Fit Page", (s, e) => ApplyFitPage());

            barPanel.Children.Add(btnZoomOut);
            barPanel.Children.Add(btnZoomIn);

            _zoomCombo = new ComboBox
            {
                Width = 85,
                Height = 28,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 6, 0)
            };
            _zoomCombo.Items.Add("50%");
            _zoomCombo.Items.Add("75%");
            _zoomCombo.Items.Add("100%");
            _zoomCombo.Items.Add("125%");
            _zoomCombo.Items.Add("150%");
            _zoomCombo.Items.Add("200%");
            _zoomCombo.SelectedIndex = 2; // 100%
            _zoomCombo.SelectionChanged += (s, e) =>
            {
                if (_zoomCombo.SelectedItem is string str && int.TryParse(str.TrimEnd('%'), out int pct))
                {
                    ZoomFactor = pct / 100.0;
                }
            };
            barPanel.Children.Add(_zoomCombo);
            barPanel.Children.Add(btnFitWidth);
            barPanel.Children.Add(btnFitPage);
            barPanel.Children.Add(CreateSeparator());

            // Full-Text Search Box
            _txtSearch = new TextBox
            {
                Width = 110,
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 4, 0),
                Background = ZeroWpfTheme.BgInput,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12
            };
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    PerformSearch(_txtSearch.Text);
                    e.Handled = true;
                }
            };

            var btnSearchPrev = CreateToolbarButton("▲", (s, e) => StepSearch(-1));
            var btnSearchNext = CreateToolbarButton("▼", (s, e) => StepSearch(1));

            _lblSearchCount = new TextBlock
            {
                Text = "",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 4, 0),
                Foreground = ZeroWpfTheme.TextSecondary,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11
            };

            barPanel.Children.Add(_txtSearch);
            barPanel.Children.Add(btnSearchPrev);
            barPanel.Children.Add(btnSearchNext);
            barPanel.Children.Add(_lblSearchCount);

            _toolbar.Child = barPanel;
            Grid.SetRow(_toolbar, 0);
            rootGrid.Children.Add(_toolbar);

            // 2. Build Document Viewport Canvas within ScrollViewer
            _scrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 37)),
                Padding = new Thickness(24)
            };

            _pagesContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _scrollViewer.Content = _pagesContainer;

            Grid.SetRow(_scrollViewer, 1);
            rootGrid.Children.Add(_scrollViewer);

            AddVisualChild(rootGrid);

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            UpdateTheme();
            RebuildPages();
            UpdatePageInfo();
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

        public void LoadDocument(string filePath)
        {
            if (!File.Exists(filePath)) return;
            Document = PdfParser.Parse(filePath);
        }

        public void OpenFileDialogPrompt()
        {
            var ofd = new OpenFileDialog
            {
                Title = "Open PDF Technical Document",
                Filter = "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                LoadDocument(ofd.FileName);
            }
        }

        public void ExecutePrint()
        {
            if (_document == null || _document.Pages.Count == 0 || _pageVisualHosts.Count == 0) return;

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                int pIndex = Math.Max(0, Math.Min(_pageVisualHosts.Count - 1, _currentPageIndex));
                printDialog.PrintVisual(_pageVisualHosts[pIndex], "ZeroUI PDF Technical Document");
            }
        }

        public void ApplyFitWidth()
        {
            if (_document == null || _scrollViewer == null || _scrollViewer.ActualWidth <= 0) return;
            double maxW = 1.0;
            for (int i = 0; i < _document.Pages.Count; i++)
            {
                if (_document.Pages[i].Width > maxW) maxW = _document.Pages[i].Width;
            }
            double usable = Math.Max(100, _scrollViewer.ActualWidth - 64);
            ZoomFactor = usable / maxW;
        }

        public void ApplyFitPage()
        {
            if (_document == null || _scrollViewer == null || _scrollViewer.ActualWidth <= 0 || _scrollViewer.ActualHeight <= 0) return;
            var page = _document.GetPage(_currentPageIndex) ?? (_document.Pages.Count > 0 ? _document.Pages[0] : null);
            if (page != null)
            {
                double usableW = Math.Max(100, _scrollViewer.ActualWidth - 64);
                double usableH = Math.Max(100, _scrollViewer.ActualHeight - 64);
                ZoomFactor = Math.Min(usableW / page.Width, usableH / page.Height);
            }
        }

        public void PerformSearch(string query)
        {
            _searchResults.Clear();
            _currentSearchIndex = -1;

            if (!string.IsNullOrWhiteSpace(query))
            {
                _searchResults.AddRange(PdfSearchEngine.Search(_document, query));
                if (_searchResults.Count > 0)
                {
                    _currentSearchIndex = 0;
                    FocusSearchResult(_searchResults[0]);
                }
            }

            UpdateSearchUI();
            InvalidateVisuals();
        }

        public void StepSearch(int direction)
        {
            if (_searchResults.Count == 0) return;
            _currentSearchIndex = (_currentSearchIndex + direction + _searchResults.Count) % _searchResults.Count;
            FocusSearchResult(_searchResults[_currentSearchIndex]);
            UpdateSearchUI();
            InvalidateVisuals();
        }

        private void FocusSearchResult(PdfSearchResult res)
        {
            CurrentPageIndex = res.PageIndex;
        }

        private void UpdateSearchUI()
        {
            if (_lblSearchCount == null) return;
            if (_searchResults.Count == 0)
            {
                _lblSearchCount.Text = string.IsNullOrEmpty(_txtSearch?.Text) ? "" : "0 found";
            }
            else
            {
                _lblSearchCount.Text = $"{_currentSearchIndex + 1} / {_searchResults.Count}";
            }
        }

        private void RebuildPages()
        {
            if (_pagesContainer == null || _document == null) return;

            _pagesContainer.Children.Clear();
            _pageVisualHosts.Clear();

            int start = (_viewMode == PdfViewMode.SinglePage) ? _currentPageIndex : 0;
            int count = (_viewMode == PdfViewMode.SinglePage) ? 1 : _document.Pages.Count;

            for (int i = 0; i < count; i++)
            {
                int pIndex = start + i;
                if (pIndex >= _document.Pages.Count) break;

                var pageModel = _document.Pages[pIndex];
                var host = new PdfPageVisualHost(this, pageModel);
                _pageVisualHosts.Add(host);

                var paperCard = new Border
                {
                    Width = pageModel.Width,
                    Height = pageModel.Height,
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 0, 16),
                    Effect = new DropShadowEffect
                    {
                        Color = Colors.Black,
                        Direction = 315,
                        ShadowDepth = 4,
                        Opacity = 0.25,
                        BlurRadius = 8
                    },
                    Child = host
                };

                _pagesContainer.Children.Add(paperCard);
            }

            ApplyZoom();
        }

        private void ScrollToPage(int pageIndex)
        {
            if (_pagesContainer == null || _scrollViewer == null || _viewMode == PdfViewMode.SinglePage) return;

            if (pageIndex >= 0 && pageIndex < _pagesContainer.Children.Count)
            {
                if (_pagesContainer.Children[pageIndex] is FrameworkElement elem)
                {
                    elem.BringIntoView();
                }
            }
        }

        private void ApplyZoom()
        {
            if (_pagesContainer != null)
            {
                _pagesContainer.LayoutTransform = new ScaleTransform(_zoomFactor, _zoomFactor);
            }
        }

        private void UpdateZoomCombo()
        {
            if (_zoomCombo != null)
            {
                int pct = (int)Math.Round(_zoomFactor * 100);
                _zoomCombo.Text = $"{pct}%";
            }
        }

        private void UpdatePageInfo()
        {
            if (_pageStatusLabel != null)
            {
                int total = _document?.PageCount ?? 1;
                int cur = Math.Min(total, _currentPageIndex + 1);
                _pageStatusLabel.Text = $"Page {cur} of {total}";
            }
        }

        private void InvalidateVisuals()
        {
            for (int i = 0; i < _pageVisualHosts.Count; i++)
            {
                _pageVisualHosts[i].InvalidateVisual();
            }
        }

        private void UpdateTheme()
        {
            Background = ZeroWpfTheme.BgPrimary;
            if (_toolbar != null)
            {
                _toolbar.Background = ZeroWpfTheme.BgCard;
                _toolbar.BorderBrush = ZeroWpfTheme.BorderDefault;
            }
            if (_pageStatusLabel != null)
            {
                _pageStatusLabel.Foreground = ZeroWpfTheme.TextPrimary;
            }
            if (_txtSearch != null)
            {
                _txtSearch.Background = ZeroWpfTheme.BgInput;
                _txtSearch.Foreground = ZeroWpfTheme.TextPrimary;
                _txtSearch.BorderBrush = ZeroWpfTheme.BorderDefault;
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

        #region Vector Page Rendering Host

        private sealed class PdfPageVisualHost : FrameworkElement
        {
            private readonly ZPdfViewer _owner;
            private readonly PdfPageModel _page;

            public PdfPageVisualHost(ZPdfViewer owner, PdfPageModel page)
            {
                _owner = owner;
                _page = page;
                Width = page.Width;
                Height = page.Height;
                ClipToBounds = true;
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);

                // Render vector elements
                for (int i = 0; i < _page.Elements.Count; i++)
                {
                    var el = _page.Elements[i];
                    switch (el.ElementType)
                    {
                        case PdfVectorElementType.Line:
                            var penLine = GetPen(el.StrokeColor, el.StrokeWidth, el.DashPattern);
                            dc.DrawLine(penLine, new Point(el.X, el.Y), new Point(el.X2, el.Y2));
                            break;

                        case PdfVectorElementType.Rectangle:
                            var brushRect = el.IsFilled ? GetBrush(el.FillColor) : null;
                            var penRect = el.IsStroked ? GetPen(el.StrokeColor, el.StrokeWidth, el.DashPattern) : null;
                            dc.DrawRectangle(brushRect, penRect, new Rect(el.X, el.Y, el.Width, el.Height));
                            break;

                        case PdfVectorElementType.Ellipse:
                            var brushEl = el.IsFilled ? GetBrush(el.FillColor) : null;
                            var penEl = el.IsStroked ? GetPen(el.StrokeColor, el.StrokeWidth, el.DashPattern) : null;
                            dc.DrawEllipse(brushEl, penEl, new Point(el.X + el.Width / 2.0, el.Y + el.Height / 2.0), el.Width / 2.0, el.Height / 2.0);
                            break;

                        case PdfVectorElementType.Text:
                            if (!string.IsNullOrEmpty(el.Text))
                            {
                                var brushText = GetBrush(el.TextColor);
                                var typeface = new Typeface(new FontFamily(el.FontFamily),
                                    el.IsItalic ? FontStyles.Italic : FontStyles.Normal,
                                    el.IsBold ? FontWeights.Bold : FontWeights.Normal,
                                    FontStretches.Normal);

#if NETCOREAPP || NET8_0_OR_GREATER
                                var formatted = new FormattedText(
                                    el.Text,
                                    CultureInfo.InvariantCulture,
                                    FlowDirection.LeftToRight,
                                    typeface,
                                    el.FontSize,
                                    brushText,
                                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
#else
                                var formatted = new FormattedText(
                                    el.Text,
                                    CultureInfo.InvariantCulture,
                                    FlowDirection.LeftToRight,
                                    typeface,
                                    el.FontSize,
                                    brushText,
                                    1.0);
#endif
                                dc.DrawText(formatted, new Point(el.X, el.Y));
                            }
                            break;
                    }
                }

                // Render active search highlights for this page
                if (_owner._searchResults.Count > 0)
                {
                    for (int s = 0; s < _owner._searchResults.Count; s++)
                    {
                        var res = _owner._searchResults[s];
                        if (res.PageIndex == _page.PageIndex)
                        {
                            bool isCur = (s == _owner._currentSearchIndex);
                            Color c = isCur ? Color.FromArgb(190, 245, 158, 11) : Color.FromArgb(130, 250, 204, 21);
                            var hlBrush = new SolidColorBrush(c);
                            hlBrush.Freeze();
                            var hlPen = new Pen(new SolidColorBrush(Color.FromRgb(217, 119, 6)), 1.2);
                            hlPen.Freeze();

                            dc.DrawRectangle(hlBrush, hlPen, new Rect(res.X, res.Y, res.Width, res.Height));
                        }
                    }
                }
            }

            private static SolidColorBrush GetBrush(uint argb)
            {
                byte a = (byte)(argb >> 24);
                byte r = (byte)(argb >> 16);
                byte g = (byte)(argb >> 8);
                byte b = (byte)argb;
                var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
                brush.Freeze();
                return brush;
            }

            private static Pen GetPen(uint argb, double width, float[]? dashes)
            {
                var brush = GetBrush(argb);
                var pen = new Pen(brush, Math.Max(0.5, width));
                if (dashes != null && dashes.Length > 0)
                {
                    var dList = new DoubleCollection();
                    for (int i = 0; i < dashes.Length; i++) dList.Add(dashes[i]);
                    pen.DashStyle = new DashStyle(dList, 0);
                }
                pen.Freeze();
                return pen;
            }
        }

        #endregion
    
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
    /// Convenience alias for <see cref="ZPdfViewer"/>.
    /// </summary>
    public class ZPdfViewerControl : ZPdfViewer { }

    /// <summary>
    /// Legacy alias for <see cref="ZPdfViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PdfViewerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPdfViewer instead.")]
    public class PdfViewerControl : ZPdfViewer { }

    /// <summary>
    /// Legacy alias for <see cref="ZPdfViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPdfViewer is deprecated and will be removed in 5 release cycles. Please migrate to ZPdfViewer instead.")]
    public class ZeroPdfViewer : ZPdfViewer { }

    /// <summary>
    /// Legacy alias for <see cref="ZPdfViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPdfViewerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPdfViewer instead.")]
    public class ZeroPdfViewerControl : ZPdfViewer { }

    #endregion

}
