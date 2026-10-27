using System;

using ZeroUI.WinForms.Icons;using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
using ZeroUI.Core.Localization;
using ZeroUI.WinForms.Editors;
using ZeroUI.WinForms.Overlays;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Reporting
{
    /// <summary>
    /// Modern Enterprise Print Preview and Page Layout control for ZeroUI WinForms.
    /// Provides vector page rendering, interactive zoom (Fit Page, Fit Width, 25%-400%),
    /// multi-page navigation, and direct printing dispatch for warehouse tickets, barcodes, and reports.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Reporting & Documents")]
    [DefaultProperty("Document")]
    [Description("Enterprise vector print preview control with toolbar, zoom, and printer dispatch")]
    [ToolboxBitmap(typeof(ZeroIcons), "DocumentPreviewControl.bmp")]
    public class ZDocumentPreview : Control
    {
        private PrintDocument? _document;
        private readonly PrintPreviewControl _previewControl;
        private readonly Panel _toolbarPanel;

        private readonly ZButton _btnPrint;
        private readonly ZButton _btnPrevPage;
        private readonly ZButton _btnNextPage;
        private readonly ZButton _btnZoomIn;
        private readonly ZButton _btnZoomOut;
        private readonly ZButton _btnFitPage;
        private readonly Label _lblPageInfo;

        [Category("Printing")]
        public PrintDocument? Document
        {
            get => _document;
            set
            {
                _document = value;
                _previewControl.Document = value;
                UpdatePageInfo();
            }
        }

        [Category("Printing")]
        public double Zoom
        {
            get => _previewControl.Zoom;
            set
            {
                _previewControl.Zoom = Math.Max(0.2, Math.Min(4.0, value));
                UpdatePageInfo();
            }
        }

        public int StartPage
        {
            get => _previewControl.StartPage;
            set
            {
                _previewControl.StartPage = Math.Max(0, value);
                UpdatePageInfo();
            }
        }

        public ZDocumentPreview()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            BackColor = ZeroTheme.Colors.Background;
            Size = new Size(680, 520);

            // Native PrintPreviewControl
            _previewControl = new PrintPreviewControl
            {
                Dock = DockStyle.Fill,
                BackColor = ZeroTheme.Colors.Background,
                AutoZoom = true
            };
            Controls.Add(_previewControl);

            // 1. Toolbar Panel
            _toolbarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = ZeroTheme.Colors.Surface
            };
            _toolbarPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(ZeroTheme.Colors.Border))
                {
                    e.Graphics.DrawLine(pen, 0, _toolbarPanel.Height - 1, _toolbarPanel.Width, _toolbarPanel.Height - 1);
                }
            };

            // Buttons
            _btnPrint = new ZButton { Text = Localizer.GetString(StringId.PrintButton), Width = 90, Height = 30, Location = new Point(10, 7) };
            _btnPrint.Click += (s, e) => Print();

            _btnPrevPage = new ZButton { Text = "◀", Width = 34, Height = 30, Location = new Point(100, 7) };
            _btnPrevPage.Click += (s, e) => { if (_previewControl.StartPage > 0) _previewControl.StartPage--; UpdatePageInfo(); };

            _lblPageInfo = new Label
            {
                Text = "Page 1",
                AutoSize = false,
                Width = 70,
                Height = 30,
                Location = new Point(138, 7),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ZeroTheme.Colors.TextPrimary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };

            _btnNextPage = new ZButton { Text = "▶", Width = 34, Height = 30, Location = new Point(212, 7) };
            _btnNextPage.Click += (s, e) => { _previewControl.StartPage++; UpdatePageInfo(); };

            _btnZoomOut = new ZButton { Text = "−", Width = 34, Height = 30, Location = new Point(260, 7) };
            _btnZoomOut.Click += (s, e) => Zoom = _previewControl.Zoom * 0.8;

            _btnZoomIn = new ZButton { Text = "+", Width = 34, Height = 30, Location = new Point(298, 7) };
            _btnZoomIn.Click += (s, e) => Zoom = _previewControl.Zoom * 1.25;

            _btnFitPage = new ZButton { Text = Localizer.GetString(StringId.ZoomFit), Width = 70, Height = 30, Location = new Point(336, 7) };
            _btnFitPage.Click += (s, e) => _previewControl.AutoZoom = true;

            _toolbarPanel.Controls.Add(_btnPrint);
            _toolbarPanel.Controls.Add(_btnPrevPage);
            _toolbarPanel.Controls.Add(_lblPageInfo);
            _toolbarPanel.Controls.Add(_btnNextPage);
            _toolbarPanel.Controls.Add(_btnZoomOut);
            _toolbarPanel.Controls.Add(_btnZoomIn);
            _toolbarPanel.Controls.Add(_btnFitPage);

            Controls.Add(_toolbarPanel);

            Localizer.CultureChanged += (s, e) => UpdateLocalizedStrings();
            UpdateLocalizedStrings();

            // Theme Sync
            ZeroTheme.ThemeChanged += OnThemeChanged;
        }

        public void Print()
        {
            if (_document == null) return;
            using (var dlg = new PrintDialog { Document = _document, UseEXDialog = true })
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    _document.Print();
                }
            }
        }

        private void UpdatePageInfo()
        {
            _lblPageInfo.Text = Localizer.GetFormattedString(StringId.PrintStatusFormat, _previewControl.StartPage + 1, Math.Max(1, _previewControl.StartPage + 1));
        }

        private void UpdateLocalizedStrings()
        {
            _btnPrint.Text = Localizer.GetString(StringId.PrintButton);
            _btnFitPage.Text = Localizer.GetString(StringId.ZoomFit);
            UpdatePageInfo();
        }

        public void InvalidatePreview()
        {
            _previewControl.InvalidatePreview();
            UpdatePageInfo();
        }
    
    private void OnThemeChanged(object? sender, EventArgs e)
    {
                BackColor = ZeroTheme.Colors.Background;
                _toolbarPanel.BackColor = ZeroTheme.Colors.Surface;
                _previewControl.BackColor = ZeroTheme.Colors.Background;
                _lblPageInfo.ForeColor = ZeroTheme.Colors.TextPrimary;
                _toolbarPanel.Invalidate();
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

    /// <summary>
    /// Legacy alias for DocumentPreviewControl.
    /// Preserved for 100% backward compatibility.
    /// </summary>
    [Obsolete("ZeroPrintPreview is deprecated. Please use DocumentPreviewControl instead.")]
    [ToolboxItem(false)]
    public class ZeroPrintPreview : DocumentPreviewControl
    {
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZDocumentPreview"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("DocumentPreviewControl is deprecated and will be removed in 5 release cycles. Please migrate to ZDocumentPreview instead.")]
    [ToolboxItem(false)]
    public class DocumentPreviewControl : ZDocumentPreview
    {
    }

    #endregion
}
