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
    /// Interactive dialog for inspecting document pages (images or multi-page documents),
    /// panning/zooming, and visually selecting a Region of Interest (ROI) for targeted OCR/LLM extraction.
    /// </summary>
    public class ZOcrRoiDialog : Form
    {
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

        // Bottom Toolbar Controls
        private Panel _bottomBar = null!;
        private Label _lblStatus = null!;
        private ZButton _btnCancel = null!;
        private ZButton _btnScanAll = null!;
        private ZButton _btnScanRoi = null!;

        #region Public Properties

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
        public ZOcrRoiDialog(Bitmap image) : this(new[] { image }, 1)
        {
        }

        /// <summary>
        /// Initializes an ROI dialog for an in-memory list of document page bitmaps.
        /// </summary>
        public ZOcrRoiDialog(IReadOnlyList<Bitmap> pages, int initialPage = 1)
        {
            if (pages == null || pages.Count == 0)
                throw new ArgumentException("Danh sách trang tài liệu không được rỗng.", nameof(pages));

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
            LoadCurrentPage();
        }

        /// <summary>
        /// Initializes an ROI dialog with on-demand page loading (optimal for large multi-page PDF documents).
        /// </summary>
        /// <param name="pageLoader">Delegate taking 1-based page number and returning page Bitmap.</param>
        /// <param name="totalPages">Total page count.</param>
        /// <param name="initialPage">Initial 1-based page index.</param>
        public ZOcrRoiDialog(Func<int, Bitmap> pageLoader, int totalPages, int initialPage = 1)
        {
            _pageLoader = pageLoader ?? throw new ArgumentNullException(nameof(pageLoader));
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
            LoadCurrentPage();
        }

        #endregion

        #region UI Initialization

        private void InitializeComponent()
        {
            Text = "ZeroUI - Chọn vùng bóc tách dữ liệu (ROI Document Selector)";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1100, 800);
            MinimumSize = new Size(820, 600);
            DoubleBuffered = true;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            BackColor = ZeroTheme.Palette.Background;

            // 1. Top Toolbar (Height: 46px)
            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                Padding = new Padding(10, 6, 10, 6),
                BackColor = ZeroTheme.Palette.Surface
            };

            _btnPrevPage = new ZButton
            {
                Text = "◀ Trước",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(72, 32),
                Location = new Point(10, 8),
                Enabled = _currentPage > 1
            };
            _btnPrevPage.Click += (s, e) => NavigatePage(_currentPage - 1);

            _lblPageIndicator = new Label
            {
                Text = $"Trang {_currentPage} / {_totalPages}",
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(100, 32),
                Location = new Point(_btnPrevPage.Right + 4, 8),
                ForeColor = ZeroTheme.Palette.TextPrimary,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };

            _btnNextPage = new ZButton
            {
                Text = "Sau ▶",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(72, 32),
                Location = new Point(_lblPageIndicator.Right + 4, 8),
                Enabled = _currentPage < _totalPages
            };
            _btnNextPage.Click += (s, e) => NavigatePage(_currentPage + 1);

            int zoomLeft = _btnNextPage.Right + 16;

            _btnZoomOut = new ZButton
            {
                Text = "🔍 ➖",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(58, 32),
                Location = new Point(zoomLeft, 8)
            };
            _btnZoomOut.Click += (s, e) => _ocrViewer.Zoom /= 1.25;

            _btnZoomIn = new ZButton
            {
                Text = "🔍 ➕",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(58, 32),
                Location = new Point(_btnZoomOut.Right + 4, 8)
            };
            _btnZoomIn.Click += (s, e) => _ocrViewer.Zoom *= 1.25;

            _btnZoomFit = new ZButton
            {
                Text = "⛶ Vừa trang",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(88, 32),
                Location = new Point(_btnZoomIn.Right + 4, 8)
            };
            _btnZoomFit.Click += (s, e) => _ocrViewer.ZoomToFit();

            _btnZoom100 = new ZButton
            {
                Text = "1:1",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(48, 32),
                Location = new Point(_btnZoomFit.Right + 4, 8)
            };
            _btnZoom100.Click += (s, e) => _ocrViewer.Zoom = 1.0;

            _btnClearRoi = new ZButton
            {
                Text = "✖ Bỏ chọn vùng",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(110, 32),
                Location = new Point(_btnZoom100.Right + 16, 8)
            };
            _btnClearRoi.Click += (s, e) =>
            {
                _ocrViewer.ClearSelection();
                UpdateRoiStatus();
            };

            _lblHint = new Label
            {
                Text = "💡 Giữ chuột trái & kéo trên văn bản để khoanh vùng bảng hàng hóa cần trích xuất",
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 420,
                ForeColor = ZeroTheme.Palette.TextSecondary,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Italic)
            };

            _topBar.Controls.AddRange(new Control[]
            {
                _btnPrevPage, _lblPageIndicator, _btnNextPage,
                _btnZoomOut, _btnZoomIn, _btnZoomFit, _btnZoom100,
                _btnClearRoi, _lblHint
            });

            // 2. Bottom Action Bar (Height: 52px)
            _bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 54,
                Padding = new Padding(12, 10, 12, 10),
                BackColor = ZeroTheme.Palette.Surface
            };

            _lblStatus = new Label
            {
                Text = "Chưa chọn vùng (Mặc định sẽ trích xuất toàn bộ trang tài liệu)",
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 450,
                ForeColor = ZeroTheme.Palette.TextSecondary,
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            _btnCancel = new ZButton
            {
                Text = "Hủy bỏ",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(95, 36),
                Dock = DockStyle.Right
            };
            _btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _btnScanAll = new ZButton
            {
                Text = "Quét toàn bộ trang",
                ButtonStyle = ZeroButtonStyle.Secondary,
                Size = new Size(140, 36),
                Dock = DockStyle.Right
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
                Text = "⚡ Quét vùng chọn [ROI]",
                ButtonStyle = ZeroButtonStyle.Primary,
                Size = new Size(170, 36),
                Dock = DockStyle.Right,
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

            // Order docked right controls: Cancel (far right), ScanAll, ScanRoi
            _bottomBar.Controls.Add(_lblStatus);
            _bottomBar.Controls.Add(_btnScanRoi);
            _bottomBar.Controls.Add(_btnScanAll);
            _bottomBar.Controls.Add(_btnCancel);

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

            _lblPageIndicator.Text = $"Trang {_currentPage} / {_totalPages}";
            _btnPrevPage.Enabled = _currentPage > 1;
            _btnNextPage.Enabled = _currentPage < _totalPages;

            UpdateRoiStatus();
        }

        private void UpdateRoiStatus()
        {
            if (HasSelectedRoi && _ocrViewer.SelectedRoi.HasValue)
            {
                var r = _ocrViewer.SelectedRoi.Value;
                _lblStatus.Text = $"✅ Đã khoanh vùng ROI: [X={(int)r.X}, Y={(int)r.Y}, Rộng={(int)r.Width}px, Cao={(int)r.Height}px]";
                _lblStatus.ForeColor = ZeroTheme.Palette.Primary;
                _btnScanRoi.Enabled = true;
                _btnScanRoi.Text = $"⚡ Quét vùng chọn [ROI: {(int)r.Width}x{(int)r.Height}]";
            }
            else
            {
                _lblStatus.Text = "Chưa chọn vùng (Mặc định sẽ trích xuất toàn bộ trang tài liệu)";
                _lblStatus.ForeColor = ZeroTheme.Palette.TextSecondary;
                _btnScanRoi.Enabled = false;
                _btnScanRoi.Text = "⚡ Quét vùng chọn [ROI]";
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
