using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ZeroUI.Core.Localization;
using ZeroUI.Core.Pdf;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Icons;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Reporting
{
    /// <summary>
    /// Ultra-high-performance vector PDF Document & CAD Schematic Reader for ZeroUI WinForms.
    /// Features continuous vertical scrolling, vector anti-aliasing, multi-zoom engine (25%-400%, Fit Width, Fit Page),
    /// interactive full-text search with match highlighting, and direct printer hardware dispatch.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Reporting & Documents")]
    [DefaultProperty("Document")]
    [Description("Lightweight vector PDF document and CAD schematic viewer control")]
    [ToolboxBitmap(typeof(ZeroIcons), "DocumentPreviewControl.bmp")]
    public class ZPdfViewer : Control
    {
        private Panel _bookmarksHeader;
        private Label _lblOutline;

        private PdfDocumentModel _document;
        private readonly PdfLayoutEngine _layoutEngine = new PdfLayoutEngine();
        private readonly List<PdfSearchResult> _searchResults = new List<PdfSearchResult>();
        private int _currentSearchIndex = -1;

        // UI Controls
        private readonly Panel _toolbarPanel;
        private readonly Panel _canvasHost;
        private readonly Panel _bookmarksPanel;
        private readonly Splitter _bookmarksSplitter;
        private readonly ZTreeList _bookmarksTree;
        private readonly ZButton _btnBookmarks;
        private readonly ZButton _btnOpen;
        private readonly ZButton _btnPrint;
        private readonly ZButton _btnFirstPage;
        private readonly ZButton _btnPrevPage;
        private readonly ZButton _btnNextPage;
        private readonly ZButton _btnLastPage;
        private readonly Label _lblPageInfo;
        private readonly ZButton _btnFitWidth;
        private readonly ZButton _btnFitPage;
        private readonly ZButton _btnZoomOut;
        private readonly ZButton _btnZoomIn;
        private readonly ComboBox _comboZoom;
        private readonly ZButton _btnViewMode;
        private readonly ZTextBox _txtSearch;
        private readonly ZButton _btnSearchPrev;
        private readonly ZButton _btnSearchNext;
        private readonly Label _lblSearchCount;

        // Layout state
        private double _zoom = 1.0;
        private PdfViewMode _viewMode = PdfViewMode.ContinuousScroll;
        private int _activePageIndex = 0;
        private bool _isPanning = false;
        private Point _lastMousePos;

        // Scroll state
        private double _scrollY = 0;
        private readonly List<PdfPageLayoutRect> _visiblePagesCache = new List<PdfPageLayoutRect>();

        [Category("Document")]
        [Description("The active vector PDF document model.")]
        public PdfDocumentModel Document
        {
            get => _document;
            set
            {
                _document = value ?? PdfSampleGenerator.CreateIndustrialCadAndSopDocument();
                _activePageIndex = 0;
                _scrollY = 0;
                _searchResults.Clear();
                _currentSearchIndex = -1;
                PopulateBookmarksTree();
                UpdateSearchUI();
                RefreshDocumentLayout();
                UpdatePageInfo();
                _canvasHost.Invalidate();
            }
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        [Description("Shows or hides the bookmarks/outline sidebar.")]
        public bool ShowBookmarksSidebar
        {
            get => _bookmarksPanel?.Visible ?? false;
            set
            {
                if (_bookmarksPanel != null && _bookmarksPanel.Visible != value)
                {
                    _bookmarksPanel.Visible = value;
                    _bookmarksSplitter.Visible = value;
                    if (_btnBookmarks != null)
                    {
                        _btnBookmarks.Text = value ? "📑 Hide Outline" : "📑 Outline";
                    }
                    RefreshDocumentLayout();
                    _canvasHost.Invalidate();
                }
            }
        }

        public void ToggleBookmarksSidebar()
        {
            ShowBookmarksSidebar = !ShowBookmarksSidebar;
        }

        private void PopulateBookmarksTree()
        {
            if (_bookmarksTree == null) return;

            _bookmarksTree.ClearNodes();
            if (_document?.Bookmarks == null || _document.Bookmarks.Count == 0)
            {
                if (_btnBookmarks != null) _btnBookmarks.Enabled = false;
                ShowBookmarksSidebar = false;
                return;
            }

            if (_btnBookmarks != null) _btnBookmarks.Enabled = true;
            foreach (var b in _document.Bookmarks)
            {
                var node = CreateBookmarkNode(b);
                _bookmarksTree.AddNode(node);
            }
        }

        private ZeroTreeNode CreateBookmarkNode(PdfBookmarkModel bm)
        {
            var node = new ZeroTreeNode(bm.Title, "📑", $"Page {bm.PageIndex + 1}")
            {
                Tag = bm,
                IsExpanded = true
            };
            foreach (var child in bm.Children)
            {
                node.AddChild(CreateBookmarkNode(child));
            }
            return node;
        }

        [Category("Document")]
        [Description("Current zoom scale factor (0.2 to 4.0).")]
        public double Zoom
        {
            get => _zoom;
            set
            {
                double clamped = Math.Max(0.2, Math.Min(4.0, value));
                if (Math.Abs(_zoom - clamped) > 0.001)
                {
                    _zoom = clamped;
                    RefreshDocumentLayout();
                    UpdateZoomCombo();
                    _canvasHost.Invalidate();
                }
            }
        }

        [Category("Document")]
        [Description("Current display mode (Continuous Scroll or Single Page).")]
        public PdfViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                if (_viewMode != value)
                {
                    _viewMode = value;
                    _btnViewMode.Text = _viewMode == PdfViewMode.ContinuousScroll ? "📜 Continuous" : "📄 Single Page";
                    RefreshDocumentLayout();
                    _canvasHost.Invalidate();
                }
            }
        }

        public int CurrentPageIndex
        {
            get => _activePageIndex;
            set
            {
                if (_document != null && _document.Pages.Count > 0)
                {
                    int clamped = Math.Max(0, Math.Min(_document.Pages.Count - 1, value));
                    if (_activePageIndex != clamped)
                    {
                        _activePageIndex = clamped;
                        ScrollToPage(_activePageIndex);
                        UpdatePageInfo();
                    }
                }
            }
        }

        public ZPdfViewer()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Background;

            // Default sample industrial document
            _document = PdfSampleGenerator.CreateIndustrialCadAndSopDocument();

            // 1. Toolbar Panel
            _toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = ZeroTheme.Colors.Surface
            };
            _toolbarPanel.Paint += (s, e) =>
            {
                using var pen = new Pen(ZeroTheme.Colors.Border, 1);
                e.Graphics.DrawLine(pen, 0, _toolbarPanel.Height - 1, _toolbarPanel.Width, _toolbarPanel.Height - 1);
            };

            int btnX = 8;
            const int btnY = 7;
            const int btnH = 28;

            // Open Document
            _btnOpen = new ZButton { Text = "📂 Open", Left = btnX, Top = btnY, Width = 78, Height = btnH };
            _btnOpen.Click += (s, e) => OpenFileDialogPrompt();
            btnX += 84;

            // Print Document
            _btnPrint = new ZButton { Text = "🖨️ Print", Left = btnX, Top = btnY, Width = 78, Height = btnH };
            _btnPrint.Click += (s, e) => PrintDocument();
            btnX += 84;

            // Bookmarks / Outline Sidebar Toggle
            _btnBookmarks = new ZButton { Text = "📑 Outline", Left = btnX, Top = btnY, Width = 112, Height = btnH };
            _btnBookmarks.Click += (s, e) => ToggleBookmarksSidebar();
            btnX += 118;

            // Page Navigation: First, Prev, Next, Last
            _btnFirstPage = new ZButton { Text = "⏮", Left = btnX, Top = btnY, Width = 32, Height = btnH };
            _btnFirstPage.Click += (s, e) => CurrentPageIndex = 0;
            btnX += 36;

            _btnPrevPage = new ZButton { Text = "◀", Left = btnX, Top = btnY, Width = 32, Height = btnH };
            _btnPrevPage.Click += (s, e) => CurrentPageIndex--;
            btnX += 36;

            _lblPageInfo = new Label
            {
                Text = "Page 1 of 3",
                Left = btnX,
                Top = btnY + 4,
                Width = 95,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            btnX += 100;

            _btnNextPage = new ZButton { Text = "▶", Left = btnX, Top = btnY, Width = 32, Height = btnH };
            _btnNextPage.Click += (s, e) => CurrentPageIndex++;
            btnX += 36;

            _btnLastPage = new ZButton { Text = "⏭", Left = btnX, Top = btnY, Width = 32, Height = btnH };
            _btnLastPage.Click += (s, e) => CurrentPageIndex = _document.PageCount - 1;
            btnX += 42;

            // View Mode Toggle
            _btnViewMode = new ZButton { Text = "📜 Continuous", Left = btnX, Top = btnY, Width = 116, Height = btnH };
            _btnViewMode.Click += (s, e) => ViewMode = (_viewMode == PdfViewMode.ContinuousScroll) ? PdfViewMode.SinglePage : PdfViewMode.ContinuousScroll;
            btnX += 122;

            // Zoom Controls
            _btnZoomOut = new ZButton { Text = "−", Left = btnX, Top = btnY, Width = 32, Height = btnH };
            _btnZoomOut.Click += (s, e) => Zoom *= 0.85;
            btnX += 36;

            _btnZoomIn = new ZButton { Text = "+", Left = btnX, Top = btnY, Width = 32, Height = btnH };
            _btnZoomIn.Click += (s, e) => Zoom *= 1.15;
            btnX += 36;

            _comboZoom = new ComboBox
            {
                Left = btnX,
                Top = btnY + 1,
                Width = 92,
                Height = btnH,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = ZeroTheme.Colors.Surface,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Font = new Font("Segoe UI", 8.5f)
            };
            _comboZoom.Items.AddRange(new object[] { "Fit Width", "Fit Page", "50%", "75%", "100%", "125%", "150%", "200%" });
            _comboZoom.SelectedIndex = 0; // Fit Width default
            _comboZoom.SelectedIndexChanged += ComboZoom_SelectedIndexChanged;
            btnX += 98;

            _btnFitWidth = new ZButton { Text = "↔ Fit Width", Left = btnX, Top = btnY, Width = 88, Height = btnH };
            _btnFitWidth.Click += (s, e) => ApplyFitWidth();
            btnX += 94;

            _btnFitPage = new ZButton { Text = "⛶ Fit Page", Left = btnX, Top = btnY, Width = 82, Height = btnH };
            _btnFitPage.Click += (s, e) => ApplyFitPage();
            btnX += 88;

            // Search Box & Buttons
            _txtSearch = new ZTextBox { Left = btnX, Top = btnY, Width = 115, Height = btnH, Text = "" };
            _txtSearch.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    PerformSearch(_txtSearch.Text);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
            btnX += 120;

            _btnSearchPrev = new ZButton { Text = "▲", Left = btnX, Top = btnY, Width = 28, Height = btnH };
            _btnSearchPrev.Click += (s, e) => StepSearch(-1);
            btnX += 32;

            _btnSearchNext = new ZButton { Text = "▼", Left = btnX, Top = btnY, Width = 28, Height = btnH };
            _btnSearchNext.Click += (s, e) => StepSearch(1);
            btnX += 34;

            _lblSearchCount = new Label
            {
                Text = "",
                Left = btnX,
                Top = btnY + 5,
                Width = 80,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ZeroTheme.Colors.TextSecondary,
                Font = new Font("Segoe UI", 8.5f)
            };

            _toolbarPanel.Controls.AddRange(new Control[]
            {
                _btnOpen, _btnPrint, _btnBookmarks,
                _btnFirstPage, _btnPrevPage, _lblPageInfo, _btnNextPage, _btnLastPage,
                _btnViewMode,
                _btnZoomOut, _btnZoomIn, _comboZoom, _btnFitWidth, _btnFitPage,
                _txtSearch, _btnSearchPrev, _btnSearchNext, _lblSearchCount
            });

            // 2. Bookmarks Outline Sidebar
            _bookmarksPanel = new Panel
            {
                Dock = DockStyle.Left,
                Width = 230,
                BackColor = ZeroTheme.Colors.Surface,
                Visible = false
            };

            _bookmarksHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = ZeroTheme.Colors.HeaderBackground
            };
            _bookmarksHeader.Paint += (s, e) =>
            {
                using var pen = new Pen(ZeroTheme.Colors.Border, 1);
                e.Graphics.DrawLine(pen, 0, _bookmarksHeader.Height - 1, _bookmarksHeader.Width, _bookmarksHeader.Height - 1);
            };

            _lblOutline = new Label
            {
                Text = "📑 Document Outline",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Padding = new Padding(8, 0, 0, 0)
            };

            var btnCloseBookmarks = new ZButton
            {
                Text = "✕",
                Dock = DockStyle.Right,
                Width = 28,
                Height = 28
            };
            btnCloseBookmarks.Click += (s, e) => ShowBookmarksSidebar = false;

            _bookmarksHeader.Controls.Add(_lblOutline);
            _bookmarksHeader.Controls.Add(btnCloseBookmarks);

            _bookmarksTree = new ZTreeList
            {
                Dock = DockStyle.Fill,
                ShowCheckBoxes = false,
                ShowColumnHeaders = false,
                RowHeight = 28
            };
            _bookmarksTree.NodeSelected += (s, node) =>
            {
                if (node?.Tag is PdfBookmarkModel bm)
                {
                    CurrentPageIndex = bm.PageIndex;
                    ScrollToPage(bm.PageIndex);
                }
            };

            _bookmarksPanel.Controls.Add(_bookmarksTree);
            _bookmarksPanel.Controls.Add(_bookmarksHeader);

            _bookmarksSplitter = new Splitter
            {
                Dock = DockStyle.Left,
                Width = 4,
                Visible = false
            };

            // 3. Vector Canvas Viewport Panel
            _canvasHost = new CanvasPanel(this)
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(24, 24, 37) // Obsidian charcoal canvas
            };

            Controls.Add(_canvasHost);
            Controls.Add(_bookmarksSplitter);
            Controls.Add(_bookmarksPanel);
            Controls.Add(_toolbarPanel);

            // Reactivity
            ZeroTheme.ThemeChanged += OnThemeChanged;

            PopulateBookmarksTree();
            Size = new Size(820, 600);
            RefreshDocumentLayout();
            UpdatePageInfo();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_canvasHost == null) return;
            RefreshDocumentLayout();
            _canvasHost.Invalidate();
        }

        public void LoadDocument(string filePath)
        {
            _ = LoadDocumentAsync(filePath);
        }

        public async Task<bool> LoadDocumentAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return false;

            LoadingOverlayHandle? overlay = null;
            try
            {
                overlay = LoadingOverlay.Show(
                    _canvasHost,
                    "Loading PDF Document...",
                    $"Parsing {Path.GetFileName(filePath)}",
                    canCancel: false);

                var doc = await Task.Run(() => PdfParser.Parse(filePath), cancellationToken);

                if (cancellationToken.IsCancellationRequested) return false;

                Document = doc;
                return true;
            }
            catch (Exception ex)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    MessageBox.Show("Could not load PDF document:\n" + ex.Message, "PDF Load Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                return false;
            }
            finally
            {
                overlay?.Dispose();
            }
        }

        /// <summary>
        /// Displays an open file dialog to allow the user to select and load a PDF document.
        /// </summary>
        public async void OpenFileDialogPrompt()
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Open PDF Technical Document",
                Filter = "PDF Documents (*.pdf)|*.pdf|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    await LoadDocumentAsync(ofd.FileName);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ZPdfViewer] Failed to load document: {ex.Message}");
                }
            }
        }

        public void PrintDocument()
        {
            if (_document == null || _document.Pages.Count == 0) return;

            try
            {
                using var printDoc = new PrintDocument();
                int printPageIndex = 0;

                printDoc.PrintPage += (s, e) =>
                {
                    var doc = _document;
                    if (doc != null && e.Graphics != null && printPageIndex < doc.Pages.Count)
                    {
                        var page = doc.Pages[printPageIndex];
                        double scaleX = e.PageBounds.Width / page.Width;
                        double scaleY = e.PageBounds.Height / page.Height;
                        double scale = Math.Min(scaleX, scaleY);

                        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        e.Graphics.ScaleTransform((float)scale, (float)scale);

                        RenderPageElements(e.Graphics, page);

                        printPageIndex++;
                        e.HasMorePages = printPageIndex < doc.Pages.Count;
                    }
                    else
                    {
                        e.HasMorePages = false;
                    }
                };

                using var dlg = new PrintDialog { Document = printDoc };
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    printDoc.Print();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Printing failed:\n" + ex.Message, "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void ApplyFitWidth()
        {
            if (_document == null || _canvasHost.Width <= 0) return;
            Zoom = _layoutEngine.CalculateFitWidthZoom(_document, _canvasHost.Width);
        }

        public void ApplyFitPage()
        {
            if (_document == null || _canvasHost.Width <= 0 || _canvasHost.Height <= 0) return;
            var page = _document.GetPage(_activePageIndex) ?? (_document.Pages.Count > 0 ? _document.Pages[0] : null);
            if (page != null)
            {
                Zoom = _layoutEngine.CalculateFitPageZoom(page, _canvasHost.Width, _canvasHost.Height);
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
            _canvasHost.Invalidate();
        }

        public void StepSearch(int direction)
        {
            if (_searchResults.Count == 0) return;
            _currentSearchIndex = (_currentSearchIndex + direction + _searchResults.Count) % _searchResults.Count;
            FocusSearchResult(_searchResults[_currentSearchIndex]);
            UpdateSearchUI();
            _canvasHost.Invalidate();
        }

        private void FocusSearchResult(PdfSearchResult res)
        {
            CurrentPageIndex = res.PageIndex;
            if (_viewMode == PdfViewMode.ContinuousScroll && res.PageIndex < _layoutEngine.PageRects.Count)
            {
                var rect = _layoutEngine.PageRects[res.PageIndex];
                _scrollY = Math.Max(0, rect.Y + res.Y * _zoom - _canvasHost.Height / 3.0);
                ClampScroll();
                _canvasHost.Invalidate();
            }
        }

        private void UpdateSearchUI()
        {
            if (_searchResults.Count == 0)
            {
                _lblSearchCount.Text = string.IsNullOrEmpty(_txtSearch.Text) ? "" : "0 found";
            }
            else
            {
                _lblSearchCount.Text = $"{_currentSearchIndex + 1} / {_searchResults.Count}";
            }
        }

        private void RefreshDocumentLayout()
        {
            if (_canvasHost == null) return;
            _layoutEngine.ComputeLayout(_document, _canvasHost.Width, _zoom, _viewMode, _activePageIndex);
            ClampScroll();
        }

        private void ScrollToPage(int pageIndex)
        {
            if (_viewMode == PdfViewMode.SinglePage)
            {
                _scrollY = 0;
                RefreshDocumentLayout();
                _canvasHost.Invalidate();
                return;
            }

            if (pageIndex >= 0 && pageIndex < _layoutEngine.PageRects.Count)
            {
                _scrollY = _layoutEngine.PageRects[pageIndex].Y - _layoutEngine.PageSpacing;
                ClampScroll();
                _canvasHost.Invalidate();
            }
        }

        private void ClampScroll()
        {
            double maxScroll = Math.Max(0, _layoutEngine.TotalHeight - _canvasHost.Height);
            _scrollY = Math.Max(0, Math.Min(maxScroll, _scrollY));
        }

        private void UpdatePageInfo()
        {
            int total = _document?.PageCount ?? 1;
            int current = Math.Min(total, _activePageIndex + 1);
            _lblPageInfo.Text = $"Page {current} of {total}";
        }

        private void UpdateZoomCombo()
        {
            int pct = (int)Math.Round(_zoom * 100);
            _comboZoom.Text = $"{pct}%";
        }

        private void ComboZoom_SelectedIndexChanged(object? sender, EventArgs e)
        {
            switch (_comboZoom.SelectedIndex)
            {
                case 0: ApplyFitWidth(); break;
                case 1: ApplyFitPage(); break;
                case 2: Zoom = 0.50; break;
                case 3: Zoom = 0.75; break;
                case 4: Zoom = 1.00; break;
                case 5: Zoom = 1.25; break;
                case 6: Zoom = 1.50; break;
                case 7: Zoom = 2.00; break;
            }
        }

        #region Internal Canvas Panel & Drawing

        private sealed class CanvasPanel : Panel
        {
            private readonly ZPdfViewer _owner;

            public CanvasPanel(ZPdfViewer owner)
            {
                _owner = owner;
                SetStyle(
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw |
                    ControlStyles.Selectable, true);
                DoubleBuffered = true;
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Focus();
                if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Middle)
                {
                    _owner._isPanning = true;
                    _owner._lastMousePos = e.Location;
                    Cursor = Cursors.Hand;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (_owner._isPanning)
                {
                    Point last = _owner._lastMousePos;
                    double dy = last.Y - e.Y;
                    _owner._scrollY += dy;
                    _owner._lastMousePos = e.Location;
                    _owner.ClampScroll();

                    // Detect active page under center
                    int newActive = _owner._layoutEngine.GetActivePageIndex(_owner._scrollY, Height);
                    if (newActive != _owner._activePageIndex)
                    {
                        _owner._activePageIndex = newActive;
                        _owner.UpdatePageInfo();
                    }

                    Invalidate();
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (_owner._isPanning)
                {
                    _owner._isPanning = false;
                    Cursor = Cursors.Default;
                }
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    // Ctrl + Mouse Wheel = Zoom
                    double factor = e.Delta > 0 ? 1.15 : 0.85;
                    _owner.Zoom *= factor;
                }
                else
                {
                    // Scroll vertically
                    double scrollDelta = -e.Delta * 0.8;
                    _owner._scrollY += scrollDelta;
                    _owner.ClampScroll();

                    int newActive = _owner._layoutEngine.GetActivePageIndex(_owner._scrollY, Height);
                    if (newActive != _owner._activePageIndex)
                    {
                        _owner._activePageIndex = newActive;
                        _owner.UpdatePageInfo();
                    }
                    Invalidate();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                if (_owner._document == null || _owner._document.Pages.Count == 0)
                {
                    using var emptyBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    using var emptyFont = new Font("Segoe UI", 12f, FontStyle.Regular);
                    g.DrawString("No PDF Document Loaded.", emptyFont, emptyBrush, 20, 20);
                    return;
                }

                _owner._layoutEngine.GetVisiblePages(_owner._scrollY, Height, _owner._visiblePagesCache);

                for (int i = 0; i < _owner._visiblePagesCache.Count; i++)
                {
                    var pageRect = _owner._visiblePagesCache[i];
                    var pageModel = _owner._document.GetPage(pageRect.PageIndex);
                    if (pageModel == null) continue;

                    float drawX = (float)pageRect.X;
                    float drawY = (float)(pageRect.Y - _owner._scrollY);
                    float drawW = (float)pageRect.Width;
                    float drawH = (float)pageRect.Height;

                    // 1. Draw Ambient Drop Shadow
                    using (var shadowBrush = new SolidBrush(Color.FromArgb(30, 0, 0, 0)))
                    {
                        g.FillRectangle(shadowBrush, drawX + 4, drawY + 4, drawW, drawH);
                    }

                    // 2. Draw Paper Background (Crisp White)
                    using (var paperBrush = new SolidBrush(Color.White))
                    {
                        g.FillRectangle(paperBrush, drawX, drawY, drawW, drawH);
                    }

                    // 3. Draw Paper Outer Border
                    using (var borderPen = new Pen(Color.FromArgb(148, 163, 184), 1f))
                    {
                        g.DrawRectangle(borderPen, drawX, drawY, drawW, drawH);
                    }

                    // 4. Render Vector Elements in Page Local Coordinates
                    var state = g.Save();
                    g.SetClip(new RectangleF(drawX, drawY, drawW, drawH));
                    g.TranslateTransform(drawX, drawY);
                    g.ScaleTransform((float)_owner._zoom, (float)_owner._zoom);

                    RenderPageElements(g, pageModel);

                    // 5. Draw Search Match Highlights
                    if (_owner._searchResults.Count > 0)
                    {
                        for (int s = 0; s < _owner._searchResults.Count; s++)
                        {
                            var res = _owner._searchResults[s];
                            if (res.PageIndex == pageRect.PageIndex)
                            {
                                bool isCurrent = (s == _owner._currentSearchIndex);
                                Color highlightColor = isCurrent ? Color.FromArgb(180, 245, 158, 11) : Color.FromArgb(130, 250, 204, 21);
                                using var hlBrush = new SolidBrush(highlightColor);
                                using var hlPen = new Pen(Color.FromArgb(217, 119, 6), 1.2f);

                                var hlRect = new RectangleF((float)res.X, (float)res.Y, (float)res.Width, (float)res.Height);
                                g.FillRectangle(hlBrush, hlRect);
                                g.DrawRectangle(hlPen, hlRect.X, hlRect.Y, hlRect.Width, hlRect.Height);
                            }
                        }
                    }

                    g.Restore(state);
                }
            }
        }

        private static void RenderPageElements(Graphics g, PdfPageModel page)
        {
            for (int e = 0; e < page.Elements.Count; e++)
            {
                var el = page.Elements[e];
                switch (el.ElementType)
                {
                    case PdfVectorElementType.Line:
                        using (var pen = new Pen(Color.FromArgb((int)el.StrokeColor), (float)el.StrokeWidth))
                        {
                            if (el.DashPattern != null && el.DashPattern.Length > 0)
                            {
                                pen.DashPattern = el.DashPattern;
                            }
                            g.DrawLine(pen, (float)el.X, (float)el.Y, (float)el.X2, (float)el.Y2);
                        }
                        break;

                    case PdfVectorElementType.Rectangle:
                        if (el.IsFilled && (el.FillColor & 0xFF000000) != 0)
                        {
                            using var brush = new SolidBrush(Color.FromArgb((int)el.FillColor));
                            g.FillRectangle(brush, (float)el.X, (float)el.Y, (float)el.Width, (float)el.Height);
                        }
                        if (el.IsStroked && (el.StrokeColor & 0xFF000000) != 0)
                        {
                            using var pen = new Pen(Color.FromArgb((int)el.StrokeColor), (float)el.StrokeWidth);
                            g.DrawRectangle(pen, (float)el.X, (float)el.Y, (float)el.Width, (float)el.Height);
                        }
                        break;

                    case PdfVectorElementType.Ellipse:
                        if (el.IsFilled && (el.FillColor & 0xFF000000) != 0)
                        {
                            using var brush = new SolidBrush(Color.FromArgb((int)el.FillColor));
                            g.FillEllipse(brush, (float)el.X, (float)el.Y, (float)el.Width, (float)el.Height);
                        }
                        if (el.IsStroked && (el.StrokeColor & 0xFF000000) != 0)
                        {
                            using var pen = new Pen(Color.FromArgb((int)el.StrokeColor), (float)el.StrokeWidth);
                            g.DrawEllipse(pen, (float)el.X, (float)el.Y, (float)el.Width, (float)el.Height);
                        }
                        break;

                    case PdfVectorElementType.Text:
                        if (!string.IsNullOrEmpty(el.Text))
                        {
                            FontStyle style = FontStyle.Regular;
                            if (el.IsBold) style |= FontStyle.Bold;
                            if (el.IsItalic) style |= FontStyle.Italic;

                            using var font = new Font(el.FontFamily, (float)el.FontSize, style);
                            using var brush = new SolidBrush(Color.FromArgb((int)el.TextColor));
                            g.DrawString(el.Text, font, brush, (float)el.X, (float)el.Y);
                        }
                        break;
                }
            }
        }

        #endregion
    
    private void OnThemeChanged(object? sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.Background;
                _toolbarPanel.BackColor = ZeroTheme.Colors.Surface;
                _bookmarksPanel.BackColor = ZeroTheme.Colors.Surface;
                _bookmarksHeader.BackColor = ZeroTheme.Colors.HeaderBackground;
                _lblOutline.ForeColor = ZeroTheme.Colors.TextPrimary;
                _toolbarPanel.Invalidate();
                _canvasHost.Invalidate();
            }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ZeroTheme.ThemeChanged -= OnThemeChanged;
        }
        base.Dispose(disposing);
    }

}

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPdfViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PdfViewerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZPdfViewer instead.")]
    [ToolboxItem(false)]
    public class PdfViewerControl : ZPdfViewer
    {
    }

    #endregion
}
