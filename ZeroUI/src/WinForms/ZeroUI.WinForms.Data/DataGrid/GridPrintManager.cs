using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// Vector printing and print-preview engine for GridControl.
    /// Provides zero-configuration paginated vector reports with repeated column headers,
    /// zebra shading, custom margins, and high-DPI crisp vector output.
    /// </summary>
    public sealed class GridPrintManager : IDisposable
    {
        private readonly ZGrid _grid;
        private int _currentPrintRow = 0;
        private int _pageNumber = 1;
        private PrintDocument? _printDocument;

        public string ReportTitle { get; set; } = "ZeroUI DataGrid Report";
        public bool ShowZebraRows { get; set; } = true;
        public bool RepeatHeadersOnEveryPage { get; set; } = true;
        public int RowHeight { get; set; } = 22;
        public int HeaderHeight { get; set; } = 26;

        public GridPrintManager(ZGrid grid)
        {
            _grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public PrintDocument CreatePrintDocument()
        {
            var doc = new PrintDocument
            {
                DocumentName = ReportTitle
            };
            doc.DefaultPageSettings.Margins = new Margins(40, 40, 40, 40);

            doc.BeginPrint += (s, e) =>
            {
                _currentPrintRow = 0;
                _pageNumber = 1;
            };

            doc.PrintPage += OnPrintPage;
            return doc;
        }

        public void ShowPrintPreview(IWin32Window? owner = null)
        {
            _printDocument?.Dispose();
            _printDocument = CreatePrintDocument();

            using (var previewDlg = new PrintPreviewDialog
            {
                Document = _printDocument,
                Width = 900,
                Height = 650,
                ShowIcon = false
            })
            {
                if (owner != null)
                {
                    previewDlg.ShowDialog(owner);
                }
                else
                {
                    previewDlg.ShowDialog();
                }
            }
        }

        public void Print(PrinterSettings? printerSettings = null)
        {
            _printDocument?.Dispose();
            _printDocument = CreatePrintDocument();
            if (printerSettings != null)
            {
                _printDocument.PrinterSettings = printerSettings;
            }
            _printDocument.Print();
        }

        private void OnPrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics!;
            Rectangle bounds = e.MarginBounds;
            int totalRows = _grid.RowCount;
            var visibleCols = _grid.VisibleColumns;

            if (visibleCols.Count == 0 || totalRows == 0)
            {
                e.HasMorePages = false;
                return;
            }

            // High quality vector text rendering
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int currentY = bounds.Top;

            // 1. Draw Page Title (on first page)
            if (_pageNumber == 1 && !string.IsNullOrEmpty(ReportTitle))
            {
                using var titleFont = new Font("Segoe UI", 14f, FontStyle.Bold);
                using var titleBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
                g.DrawString(ReportTitle, titleFont, titleBrush, bounds.Left, currentY);
                currentY += 32;
            }

            // 2. Draw Column Headers
            if (_pageNumber == 1 || RepeatHeadersOnEveryPage)
            {
                int headerY = currentY;
                int currentX = bounds.Left;

                // Scale columns proportionally to fit printable width
                int totalColW = 0;
                for (int i = 0; i < visibleCols.Count; i++) totalColW += visibleCols[i].Width;
                if (totalColW <= 0) totalColW = 1;
                float scale = (float)bounds.Width / totalColW;

                using var headerFont = new Font("Segoe UI", 9f, FontStyle.Bold);
                using var headerBgBrush = new SolidBrush(Color.FromArgb(241, 245, 249));
                using var headerTextBrush = new SolidBrush(Color.FromArgb(30, 41, 59));
                using var gridPen = new Pen(Color.FromArgb(203, 213, 225), 1f);

                for (int i = 0; i < visibleCols.Count; i++)
                {
                    var col = visibleCols[i];
                    int colW = (int)Math.Round(col.Width * scale);
                    if (i == visibleCols.Count - 1) colW = bounds.Right - currentX; // snap to right edge

                    var colRect = new Rectangle(currentX, headerY, colW, HeaderHeight);
                    g.FillRectangle(headerBgBrush, colRect);
                    g.DrawRectangle(gridPen, colRect);

                    var textRect = new Rectangle(colRect.Left + 4, colRect.Top + 3, colRect.Width - 8, colRect.Height - 6);
                    var sf = new StringFormat
                    {
                        Alignment = col.Alignment == CellAlignment.Right ? StringAlignment.Far :
                                    col.Alignment == CellAlignment.Center ? StringAlignment.Center : StringAlignment.Near,
                        LineAlignment = StringAlignment.Center,
                        Trimming = StringTrimming.EllipsisCharacter
                    };
                    g.DrawString(col.HeaderText, headerFont, headerTextBrush, textRect, sf);

                    currentX += colW;
                }

                currentY += HeaderHeight;
            }

            // 3. Draw Data Rows
            using (var cellFont = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (var rowTextBrush = new SolidBrush(Color.FromArgb(51, 65, 85)))
            using (var zebraBrush = new SolidBrush(Color.FromArgb(248, 250, 252)))
            using (var whiteBrush = new SolidBrush(Color.White))
            using (var gridPen = new Pen(Color.FromArgb(226, 232, 240), 1f))
            {
                int totalColW = 0;
                for (int i = 0; i < visibleCols.Count; i++) totalColW += visibleCols[i].Width;
                if (totalColW <= 0) totalColW = 1;
                float scale = (float)bounds.Width / totalColW;

                while (_currentPrintRow < totalRows && currentY + RowHeight <= bounds.Bottom - 25)
                {
                    int currentX = bounds.Left;
                    bool isZebra = ShowZebraRows && (_currentPrintRow % 2 == 1);
                    Brush bgBrush = isZebra ? zebraBrush : whiteBrush;

                    for (int c = 0; c < visibleCols.Count; c++)
                    {
                        var col = visibleCols[c];
                        int colW = (int)Math.Round(col.Width * scale);
                        if (c == visibleCols.Count - 1) colW = bounds.Right - currentX;

                        var cellRect = new Rectangle(currentX, currentY, colW, RowHeight);
                        g.FillRectangle(bgBrush, cellRect);
                        g.DrawRectangle(gridPen, cellRect);

                        string val = _grid.GetCellDisplayText(_currentPrintRow, c);
                        var textRect = new Rectangle(cellRect.Left + 4, cellRect.Top + 2, cellRect.Width - 8, cellRect.Height - 4);
                        var sf = new StringFormat
                        {
                            Alignment = col.Alignment == CellAlignment.Right ? StringAlignment.Far :
                                        col.Alignment == CellAlignment.Center ? StringAlignment.Center : StringAlignment.Near,
                            LineAlignment = StringAlignment.Center,
                            Trimming = StringTrimming.EllipsisCharacter
                        };
                        g.DrawString(val, cellFont, rowTextBrush, textRect, sf);

                        currentX += colW;
                    }

                    currentY += RowHeight;
                    _currentPrintRow++;
                }
            }

            // 4. Draw Page Footer ("Printed on YYYY-MM-DD   Page X of Y")
            using (var footerFont = new Font("Segoe UI", 7.5f, FontStyle.Italic))
            using (var footerBrush = new SolidBrush(Color.FromArgb(100, 116, 139)))
            {
                string leftInfo = $"Printed: {DateTime.Now:yyyy-MM-dd HH:mm}";
                string rightInfo = $"Page {_pageNumber}";
                g.DrawString(leftInfo, footerFont, footerBrush, bounds.Left, bounds.Bottom - 14);

                var sfRight = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(rightInfo, footerFont, footerBrush, bounds.Right, bounds.Bottom - 14, sfRight);
            }

            if (_currentPrintRow < totalRows)
            {
                _pageNumber++;
                e.HasMorePages = true;
            }
            else
            {
                e.HasMorePages = false;
            }
        }

        public void Dispose()
        {
            _printDocument?.Dispose();
            _printDocument = null;
        }
    }
}
