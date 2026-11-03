using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ZeroOcr.Core.Models;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Media.Ocr
{
    /// <summary>
    /// Localizable string resources for <see cref="ZOcrRoiDialog"/>.
    /// Default values are in Standard English.
    /// </summary>
    public class ZOcrRoiDialogStrings
    {
        public string Title { get; set; } = "ZeroUI - Region of Interest (ROI) Document Selector";
        public string PrevPage { get; set; } = "◀ Prev";
        public string NextPage { get; set; } = "Next ▶";
        public string PageIndicatorFormat { get; set; } = "Page {0} / {1}";
        public string ZoomFit { get; set; } = "⛶ Fit Page";
        public string ZoomActual { get; set; } = "1:1";
        public string ClearRoi { get; set; } = "✖ Clear ROI";
        public string HintDragToSelect { get; set; } = "💡 Drag left mouse on document to select region of interest (ROI)";
        public string StatusNoSelection { get; set; } = "No region selected (Full page will be extracted by default)";
        public string StatusSelectedFormat { get; set; } = "✅ Selected ROI: [X={0}, Y={1}, W={2}px, H={3}px]";
        public string ScanAll { get; set; } = "Extract Full Page";
        public string ScanRoi { get; set; } = "⚡ Extract Selected [ROI]";
        public string ScanRoiWithDimsFormat { get; set; } = "⚡ Extract Selected [ROI: {0}x{1}]";
        public string Cancel { get; set; } = "Cancel";

        public string ToolTipZoomOut { get; set; } = "Zoom Out (Ctrl + Wheel Down)";
        public string ToolTipZoomIn { get; set; } = "Zoom In (Ctrl + Wheel Up)";
        public string ToolTipZoomFit { get; set; } = "Fit to window";
        public string ToolTipZoomActual { get; set; } = "Actual size (100%)";
        public string ToolTipClearRoi { get; set; } = "Clear current ROI (Delete key)";

        public static ZOcrRoiDialogStrings English => new ZOcrRoiDialogStrings();

        public static ZOcrRoiDialogStrings Vietnamese => new ZOcrRoiDialogStrings
        {
            Title = "ZeroUI - Chọn vùng bóc tách dữ liệu (ROI Document Selector)",
            PrevPage = "◀ Trước",
            NextPage = "Sau ▶",
            PageIndicatorFormat = "Trang {0} / {1}",
            ZoomFit = "⛶ Vừa trang",
            ZoomActual = "1:1",
            ClearRoi = "✖ Bỏ chọn vùng",
            HintDragToSelect = "💡 Giữ chuột trái & kéo trên tài liệu để khoanh vùng bảng hàng hóa",
            StatusNoSelection = "Chưa chọn vùng (Mặc định sẽ trích xuất toàn bộ trang tài liệu)",
            StatusSelectedFormat = "✅ Đã khoanh vùng ROI: [X={0}, Y={1}, Rộng={2}px, Cao={3}px]",
            ScanAll = "Quét toàn bộ trang",
            ScanRoi = "⚡ Quét vùng chọn [ROI]",
            ScanRoiWithDimsFormat = "⚡ Quét vùng chọn [ROI: {0}x{1}]",
            Cancel = "Hủy bỏ",
            ToolTipZoomOut = "Thu nhỏ (Ctrl + Cuộn chuột xuống)",
            ToolTipZoomIn = "Phóng to (Ctrl + Cuộn chuột lên)",
            ToolTipZoomFit = "Thu phóng vừa cửa sổ",
            ToolTipZoomActual = "Kích thước gốc 100%",
            ToolTipClearRoi = "Bỏ chọn vùng ROI hiện tại (Phím Delete)"
        };
    }

    /// <summary>
    /// Interactive dialog for inspecting document pages (images or multi-page documents),
    /// panning/zooming, and visually selecting a Region of Interest (ROI) for targeted OCR/LLM extraction.
    /// </summary>
    public class ZOcrRoiDialog : Form
    {
        /// <summary>
        /// Global default localized strings across all dialog instances.
        /// Defaults to Standard English.
        /// </summary>
        public static ZOcrRoiDialogStrings DefaultStrings { get; set; } = new ZOcrRoiDialogStrings();

        private ZOcrRoiDialogStrings _strings = DefaultStrings;

        private readonly ZOcrViewer _ocrViewer;
        private readonly Func<int, Bitmap>? _pageLoader;
        private readonly IReadOnlyList<Bitmap>? _staticPages;
        private readonly int _totalPages;
        private int _currentPage = 1;
        private Bitmap? _currentBitmap;
        private Bitmap? _resultBitmap;

        // Top Toolbar Controls
        private Panel _topBar = null!;
        private ZButton _btnPrevPage = null!;
        private Label _lblPageIndicator = null!;
        private ZButton _btnNextPage = null!;
        private ZButton _btnZoomIn = null!;
        private ZButton _btnZoomOut = null!;
        private ZButton _btnZoomFit = null!;
        private ZButton _btnZoom100 = null!;
        private ZButton _btnClearRoi = null!;
        private Label _lblHint = null!;
        private ToolTip? _topToolTips;

        // Bottom Toolbar Controls
        private Panel _bottomBar = null!;
        private Label _lblStatus = null!;
        private ZButton _btnCancel = null!;
        private ZButton _btnScanAll = null!;
        private ZButton _btnScanRoi = null!;

        #region Public Properties

        /// <summary>
        /// Gets or sets the localized string set for this dialog instance.
        /// Setting this automatically reloads and refreshes all UI text.
        /// </summary>
        public ZOcrRoiDialogStrings Strings
        {
            get => _strings;
            set
            {
                _strings = value ?? new ZOcrRoiDialogStrings();
                ReloadStrings();
            }
        }

        /// <summary>
        /// Current 1-based page index.
        /// </summary>
        public int CurrentPage => _currentPage;

        /// <summary>
        /// Total number of document pages available.
        /// </summary>
        public int TotalPages => _totalPages;

        /// <summary>
        /// Selected Region of Interest (ROI) in document image pixel coordinates.
        /// Null if user chose to extract the entire page.
        /// </summary>
        public OcrRect? SelectedRoi => _ocrViewer.SelectedRoi;

        /// <summary>
        /// Returns true if a valid Region of Interest was selected.
        /// </summary>
        public bool HasSelectedRoi => _ocrViewer.SelectedRoi.HasValue && !_ocrViewer.SelectedRoi.Value.IsEmpty;

        /// <summary>
        /// Underlying <see cref="ZOcrViewer"/> control.
        /// </summary>
        public ZOcrViewer OcrViewer => _ocrViewer;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes an ROI dialog for a single bitmap document.
        /// </summary>
        public ZOcrRoiDialog(Bitmap image, ZOcrRoiDialogStrings? strings = null) : this(new[] { image }, 1, strings)
        {
        }

        /// <summary>
        /// Initializes an ROI dialog for an in-memory list of document page bitmaps.
        /// </summary>
        public ZOcrRoiDialog(IReadOnlyList<Bitmap> pages, int initialPage = 1, ZOcrRoiDialogStrings? strings = null)
        {
            if (pages == null || pages.Count == 0)
                throw new ArgumentException("Document page list cannot be empty.", nameof(pages));

            if (strings != null) _strings = strings;

            _staticPages = pages;
            _totalPages = pages.Count;
            _currentPage = Math.Max(1, Math.Min(initialPage, _totalPages));

            _ocrViewer = new ZOcrViewer
            {
                Dock = DockStyle.Fill,
                EnableRoiSelection = true,
                ShowBoundingBoxes = false,
                ShowConfidenceBadges = false
            };

            InitializeComponent();
            ReloadStrings();
            LoadCurrentPage();
        }

        /// <summary>
        /// Initializes an ROI dialog with on-demand page loading (optimal for large multi-page PDF documents).
        /// </summary>
        /// <param name="pageLoader">Delegate taking 1-based page number and returning page Bitmap.</param>
        /// <param name="totalPages">Total page count.</param>
        /// <param name="initialPage">Initial 1-based page index.</param>
        /// <param name="strings">Optional custom localized string resources.</param>
        public ZOcrRoiDialog(Func<int, Bitmap> pageLoader, int totalPages, int initialPage = 1, ZOcrRoiDialogStrings? strings = null)
        {
            _pageLoader = pageLoader ?? throw new ArgumentNullException(nameof(pageLoader));
            if (strings != null) _strings = strings;

            _totalPages = Math.Max(1, totalPages);
            _currentPage = Math.Max(1, Math.Min(initialPage, _totalPages));

            _ocrViewer = new ZOcrViewer
            {
                Dock = DockStyle.Fill,
                EnableRoiSelection = true,
                ShowBoundingBoxes = false,
                ShowConfidenceBadges = false
            };

            InitializeComponent();
            ReloadStrings();
            LoadCurrentPage();
        }

        #endregion

        #region Localization & Dynamic Reload

        /// <summary>
        /// Applies and reloads localized string resources into all controls.
        /// </summary>
        public void ReloadStrings(ZOcrRoiDialogStrings? customStrings = null)
        {
            if (customStrings != null)
            {
                _strings = customStrings;
            }

            Text = _strings.Title;
            if (_btnPrevPage != null) _btnPrevPage.Text = _strings.PrevPage;
            if (_btnNextPage != null) _btnNextPage.Text = _strings.NextPage;
            if (_lblPageIndicator != null) _lblPageIndicator.Text = string.Format(_strings.PageIndicatorFormat, _currentPage, _totalPages);
            if (_btnZoomFit != null) _btnZoomFit.Text = _strings.ZoomFit;
            if (_btnZoom100 != null) _btnZoom100.Text = _strings.ZoomActual;
            if (_btnClearRoi != null) _btnClearRoi.Text = _strings.ClearRoi;
            if (_lblHint != null) _lblHint.Text = _strings.HintDragToSelect;
            if (_btnScanAll != null) _btnScanAll.Text = _strings.ScanAll;
            if (_btnCancel != null) _btnCancel.Text = _strings.Cancel;

            if (_topToolTips != null)
            {
                if (_btnZoomOut != null) _topToolTips.SetToolTip(_btnZoomOut, _strings.ToolTipZoomOut);
                if (_btnZoomIn != null) _topToolTips.SetToolTip(_btnZoomIn, _strings.ToolTipZoomIn);
                if (_btnZoomFit != null) _topToolTips.SetToolTip(_btnZoomFit, _strings.ToolTipZoomFit);
                if (_btnZoom100 != null) _topToolTips.SetToolTip(_btnZoom100, _strings.ToolTipZoomActual);
                if (_btnClearRoi != null) _topToolTips.SetToolTip(_btnClearRoi, _strings.ToolTipClearRoi);
            }

            UpdateRoiStatus();
        }

        #endregion

        #region UI Initialization

        private void InitializeComponent()
        {
            Text = _strings.Title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 800);
            MinimumSize = new Size(820, 600);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            BackColor = ZeroTheme.Palette.Background;
            KeyPreview = true;
            KeyDown += ZOcrRoiDialog_KeyDown;

            // 1. Top Toolbar (Height: 44px) - Clean FlowLayout to eliminate button overlap & text truncation
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(8, 6, 8, 6),
                BackColor = ZeroTheme.Palette.Surface
            };

            var topActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _btnPrevPage = new ZButton
            {
                Text = _strings.PrevPage,
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(68, 30),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Margin = new Padding(0, 0, 4, 0),
                Enabled = _currentPage > 1
            };
            _btnPrevPage.Click += (s, e) => NavigatePage(_currentPage - 1);

            _lblPageIndicator = new Label
            {
                Text = string.Format(_strings.PageIndicatorFormat, _currentPage, _totalPages),
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(88, 30),
                Margin = new Padding(0, 0, 4, 0),
                ForeColor = ZeroTheme.Palette.TextPrimary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _btnNextPage = new ZButton
            {
                Text = _strings.NextPage,
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(68, 30),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0),
                Enabled = _currentPage < _totalPages
            };
            _btnNextPage.Click += (s, e) => NavigatePage(_currentPage + 1);

            var sep1 = new Panel
            {
                Size = new Size(1, 20),
                BackColor = ZeroTheme.Palette.Border,
                Margin = new Padding(2, 5, 8, 5)
            };

            _btnZoomOut = new ZButton
            {
                Text = "➖",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(36, 30),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 4, 0)
            };
            _btnZoomOut.Click += (s, e) => _ocrViewer.Zoom /= 1.25;

            _btnZoomIn = new ZButton
            {
                Text = "➕",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(36, 30),
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 4, 0)
            };
            _btnZoomIn.Click += (s, e) => _ocrViewer.Zoom *= 1.25;

            _btnZoomFit = new ZButton
            {
                Text = _strings.ZoomFit,
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(102, 30),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Margin = new Padding(0, 0, 4, 0)
            };
            _btnZoomFit.Click += (s, e) => _ocrViewer.ZoomToFit();

            _btnZoom100 = new ZButton
            {
                Text = _strings.ZoomActual,
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(46, 30),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnZoom100.Click += (s, e) => _ocrViewer.Zoom = 1.0;

            var sep2 = new Panel
            {
                Size = new Size(1, 20),
                BackColor = ZeroTheme.Palette.Border,
                Margin = new Padding(2, 5, 8, 5)
            };

            _btnClearRoi = new ZButton
            {
                Text = _strings.ClearRoi,
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(128, 30),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnClearRoi.Click += (s, e) =>
            {
                _ocrViewer.ClearSelection();
                UpdateRoiStatus();
            };

            topActions.Controls.AddRange(new Control[]
            {
                _btnPrevPage, _lblPageIndicator, _btnNextPage,
                sep1,
                _btnZoomOut, _btnZoomIn, _btnZoomFit, _btnZoom100,
                sep2,
                _btnClearRoi
            });

            _topToolTips = new ToolTip();
            _topToolTips.SetToolTip(_btnZoomOut, _strings.ToolTipZoomOut);
            _topToolTips.SetToolTip(_btnZoomIn, _strings.ToolTipZoomIn);
            _topToolTips.SetToolTip(_btnZoomFit, _strings.ToolTipZoomFit);
            _topToolTips.SetToolTip(_btnZoom100, _strings.ToolTipZoomActual);
            _topToolTips.SetToolTip(_btnClearRoi, _strings.ToolTipClearRoi);

            _lblHint = new Label
            {
                Text = _strings.HintDragToSelect,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Fill,
                ForeColor = ZeroTheme.Palette.TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };

            _topBar.Controls.Add(_lblHint);
            _topBar.Controls.Add(topActions);

            // 2. Bottom Action Bar (Height: 48px) - Spaced buttons with 8px margin
            _bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = ZeroTheme.Palette.Surface
            };

            _lblStatus = new Label
            {
                Text = _strings.StatusNoSelection,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill,
                ForeColor = ZeroTheme.Palette.TextSecondary,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            var bottomActions = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _btnScanAll = new ZButton
            {
                Text = _strings.ScanAll,
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(140, 32),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Margin = new Padding(0, 0, 8, 0)
            };
            _btnScanAll.Click += (s, e) =>
            {
                _ocrViewer.ClearSelection();
                if (_ocrViewer.Image != null)
                {
                    try
                    {
                        _resultBitmap = new Bitmap(_ocrViewer.Image);
                    }
                    catch
                    {
                        _resultBitmap = null;
                    }
                }
                DialogResult = DialogResult.OK;
                Close();
            };

            _btnScanRoi = new ZButton
            {
                Text = _strings.ScanRoi,
                ButtonStyle = ZeroButtonStyle.Primary,
                Size = new Size(175, 32),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Margin = new Padding(0, 0, 8, 0),
                Enabled = false
            };
            _btnScanRoi.Click += (s, e) =>
            {
                if (HasSelectedRoi)
                {
                    _resultBitmap = _ocrViewer.GetCroppedBitmap();
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            _btnCancel = new ZButton
            {
                Text = _strings.Cancel,
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(80, 32),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                Margin = Padding.Empty
            };
            _btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            bottomActions.Controls.AddRange(new Control[]
            {
                _btnScanAll,
                _btnScanRoi,
                _btnCancel
            });

            _bottomBar.Controls.Add(_lblStatus);
            _bottomBar.Controls.Add(bottomActions);

            // 3. Center OcrViewer Canvas
            _ocrViewer.RoiSelected += (s, roi) =>
            {
                UpdateRoiStatus();
            };

            Controls.Add(_ocrViewer);
            Controls.Add(_topBar);
            Controls.Add(_bottomBar);
        }

        #endregion

        #region Page & Navigation Logic

        private void NavigatePage(int newPage)
        {
            if (newPage < 1 || newPage > _totalPages || newPage == _currentPage) return;
            _currentPage = newPage;
            LoadCurrentPage();
        }

        private void LoadCurrentPage()
        {
            // Reset old page selection
            _ocrViewer.ClearSelection();

            // Load new bitmap
            if (_pageLoader != null)
            {
                _currentBitmap?.Dispose();
                _currentBitmap = _pageLoader(_currentPage);
                _ocrViewer.Image = _currentBitmap;
            }
            else if (_staticPages != null && _currentPage - 1 < _staticPages.Count)
            {
                _ocrViewer.Image = _staticPages[_currentPage - 1];
            }

            _lblPageIndicator.Text = string.Format(_strings.PageIndicatorFormat, _currentPage, _totalPages);
            _btnPrevPage.Enabled = _currentPage > 1;
            _btnNextPage.Enabled = _currentPage < _totalPages;

            UpdateRoiStatus();
        }

        private void UpdateRoiStatus()
        {
            if (HasSelectedRoi && _ocrViewer.SelectedRoi.HasValue)
            {
                var r = _ocrViewer.SelectedRoi.Value;
                _lblStatus.Text = string.Format(_strings.StatusSelectedFormat, (int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
                _lblStatus.ForeColor = ZeroTheme.Palette.Primary;
                _btnScanRoi.Enabled = true;
                _btnScanRoi.Text = string.Format(_strings.ScanRoiWithDimsFormat, (int)r.Width, (int)r.Height);
            }
            else
            {
                _lblStatus.Text = _strings.StatusNoSelection;
                _lblStatus.ForeColor = ZeroTheme.Palette.TextSecondary;
                _btnScanRoi.Enabled = false;
                _btnScanRoi.Text = _strings.ScanRoi;
            }
        }

        #endregion

        #region Helper Extraction Methods

        /// <summary>
        /// Retrieves the resulting image to process:
        /// Returns cropped Bitmap if a valid ROI was selected; otherwise returns the full page Bitmap.
        /// </summary>
        public Bitmap? GetResultBitmap()
        {
            if (_resultBitmap != null)
            {
                return _resultBitmap;
            }

            if (HasSelectedRoi)
            {
                return _ocrViewer.GetCroppedBitmap();
            }

            if (_ocrViewer.Image != null)
            {
                try
                {
                    // Clone image copy so caller owns lifetime
                    return new Bitmap(_ocrViewer.Image);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        #endregion

        private void ZOcrRoiDialog_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                _btnCancel.PerformClick();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                if (HasSelectedRoi)
                {
                    _btnScanRoi.PerformClick();
                }
                else
                {
                    _btnScanAll.PerformClick();
                }
            }
            else if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                e.Handled = true;
                _btnClearRoi.PerformClick();
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                e.Handled = true;
                NavigatePage(_currentPage - 1);
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                e.Handled = true;
                NavigatePage(_currentPage + 1);
            }
        }

        #region Cleanup

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentBitmap?.Dispose();
                _currentBitmap = null;
            }
            base.Dispose(disposing);
        }

        #endregion
    }
}
