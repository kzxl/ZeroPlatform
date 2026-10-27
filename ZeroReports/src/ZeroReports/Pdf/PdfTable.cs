using System;
using System.Collections.Generic;

namespace ZeroReports.Pdf
{
    public enum PdfTextAlignment
    {
        Left,
        Center,
        Right
    }

    public class PdfTableColumn
    {
        public string Title { get; set; }
        public float Width { get; set; }
        public PdfTextAlignment Alignment { get; set; }

        public PdfTableColumn(string title, float width, PdfTextAlignment alignment = PdfTextAlignment.Left)
        {
            Title = title;
            Width = width;
            Alignment = alignment;
        }
    }

    public class PdfTableRow
    {
        public List<string> Cells { get; } = new List<string>();

        public PdfTableRow(IEnumerable<string> cells)
        {
            Cells.AddRange(cells);
        }
    }

    /// <summary>
    /// Declarative industrial PDF table generator with multi-page auto-pagination,
    /// column alignments, alternating row striping, cell borders, and repeat headers.
    /// </summary>
    public class PdfTable
    {
        private readonly List<PdfTableColumn> _columns = new List<PdfTableColumn>();
        private readonly List<PdfTableRow> _rows = new List<PdfTableRow>();

        public IReadOnlyList<PdfTableColumn> Columns => _columns;
        public IReadOnlyList<PdfTableRow> Rows => _rows;

        public float HeaderHeight { get; set; } = 24f;
        public float RowHeight { get; set; } = 20f;
        public float CellPadding { get; set; } = 5f;

        public (float R, float G, float B) HeaderFillColor { get; set; } = (0.15f, 0.25f, 0.40f);
        public (float R, float G, float B) HeaderTextColor { get; set; } = (1.0f, 1.0f, 1.0f);
        public (float R, float G, float B) AlternateRowColor { get; set; } = (0.95f, 0.96f, 0.98f);
        public (float R, float G, float B) BorderColor { get; set; } = (0.80f, 0.82f, 0.86f);
        public (float R, float G, float B) TextColor { get; set; } = (0.10f, 0.10f, 0.10f);

        public float BorderWidth { get; set; } = 0.5f;
        public float HeaderFontSize { get; set; } = 10f;
        public float BodyFontSize { get; set; } = 9f;

        public PdfTable AddColumn(string title, float width, PdfTextAlignment alignment = PdfTextAlignment.Left)
        {
            _columns.Add(new PdfTableColumn(title, width, alignment));
            return this;
        }

        public PdfTable AddRow(params object?[] cells)
        {
            var stringCells = new List<string>(cells.Length);
            foreach (var c in cells)
            {
                stringCells.Add(c?.ToString() ?? string.Empty);
            }
            _rows.Add(new PdfTableRow(stringCells));
            return this;
        }

        /// <summary>
        /// Renders table onto the PDF document starting at (startX, startY).
        /// Automatically inserts new pages when content overflows the page height.
        /// Returns the final Y position after the last row.
        /// </summary>
        public float Render(PdfDocument document, float startX, float startY, float bottomMargin = 40f)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (_columns.Count == 0) return startY;

            float totalWidth = 0;
            foreach (var col in _columns)
            {
                totalWidth += col.Width;
            }

            PdfPage currentPage = document.Pages.Count > 0 ? document.Pages[document.Pages.Count - 1] : document.AddPage();
            float currentY = startY;

            // Draw initial header
            DrawHeader(currentPage, startX, currentY, totalWidth);
            currentY += HeaderHeight;

            for (int r = 0; r < _rows.Count; r++)
            {
                // Check if row overflows page
                if (currentY + RowHeight > currentPage.Height - bottomMargin)
                {
                    // Add new page with same dimensions
                    currentPage = document.AddPage(currentPage.Width, currentPage.Height);
                    currentY = startY;

                    // Repeat header row on new page
                    DrawHeader(currentPage, startX, currentY, totalWidth);
                    currentY += HeaderHeight;
                }

                // Alternating row background
                if (r % 2 == 1)
                {
                    currentPage.SetFillColor(AlternateRowColor.R, AlternateRowColor.G, AlternateRowColor.B);
                    currentPage.DrawRectangle(startX, currentY, totalWidth, RowHeight, fill: true, stroke: false);
                }

                // Cell borders
                currentPage.SetStrokeColor(BorderColor.R, BorderColor.G, BorderColor.B);
                currentPage.SetLineWidth(BorderWidth);
                currentPage.DrawRectangle(startX, currentY, totalWidth, RowHeight, fill: false, stroke: true);

                // Cell contents
                var rowData = _rows[r];
                float colX = startX;

                for (int c = 0; c < _columns.Count; c++)
                {
                    var col = _columns[c];
                    string cellText = c < rowData.Cells.Count ? rowData.Cells[c] : string.Empty;

                    // Draw vertical column divider if not first column
                    if (c > 0)
                    {
                        currentPage.DrawLine(colX, currentY, colX, currentY + RowHeight);
                    }

                    // Render text
                    currentPage.SetFillColor(TextColor.R, TextColor.G, TextColor.B);
                    float textX = CalculateTextX(colX, col.Width, cellText, col.Alignment);
                    float textY = currentY + (RowHeight / 2) + 3f; // vertically centered baseline
                    currentPage.DrawText(cellText, textX, textY, fontSize: BodyFontSize, font: "F1");

                    colX += col.Width;
                }

                currentY += RowHeight;
            }

            return currentY;
        }

        private void DrawHeader(PdfPage page, float startX, float startY, float totalWidth)
        {
            // Header background
            page.SetFillColor(HeaderFillColor.R, HeaderFillColor.G, HeaderFillColor.B);
            page.DrawRectangle(startX, startY, totalWidth, HeaderHeight, fill: true, stroke: false);

            // Header border
            page.SetStrokeColor(BorderColor.R, BorderColor.G, BorderColor.B);
            page.SetLineWidth(BorderWidth * 1.5f);
            page.DrawRectangle(startX, startY, totalWidth, HeaderHeight, fill: false, stroke: true);

            float colX = startX;
            for (int c = 0; c < _columns.Count; c++)
            {
                var col = _columns[c];

                if (c > 0)
                {
                    page.DrawLine(colX, startY, colX, startY + HeaderHeight);
                }

                page.SetFillColor(HeaderTextColor.R, HeaderTextColor.G, HeaderTextColor.B);
                float textX = CalculateTextX(colX, col.Width, col.Title, col.Alignment);
                float textY = startY + (HeaderHeight / 2) + 3.5f;

                // Use Bold Font F2 for Headers
                page.DrawText(col.Title, textX, textY, fontSize: HeaderFontSize, font: "F2");

                colX += col.Width;
            }
        }

        private float CalculateTextX(float colStartX, float colWidth, string text, PdfTextAlignment alignment)
        {
            // Estimate text width roughly based on 0.5 * fontSize per char
            float approxTextWidth = text.Length * (BodyFontSize * 0.5f);

            switch (alignment)
            {
                case PdfTextAlignment.Center:
                    float centerX = colStartX + (colWidth / 2) - (approxTextWidth / 2);
                    return centerX > colStartX + CellPadding ? centerX : colStartX + CellPadding;

                case PdfTextAlignment.Right:
                    float rightX = colStartX + colWidth - approxTextWidth - CellPadding;
                    return rightX > colStartX + CellPadding ? rightX : colStartX + CellPadding;

                case PdfTextAlignment.Left:
                default:
                    return colStartX + CellPadding;
            }
        }
    }
}
