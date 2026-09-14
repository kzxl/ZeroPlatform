using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ZeroReports.Pdf;
using ZeroReports.Thermal;

namespace ZeroReports.Dom
{
    /// <summary>
    /// Document Object Model for structured industrial reports, manifests, invoices, and lab results.
    /// Supports multi-target rendering (PDF 1.4 Vector, Thermal ZPL/TSPL, and HTML preview).
    /// </summary>
    public class ReportDocument
    {
        public string Title { get; set; } = "Document";
        public string Subtitle { get; set; } = string.Empty;
        public float PageWidth { get; set; } = 595.28f; // A4
        public float PageHeight { get; set; } = 841.89f; // A4
        public float MarginLeft { get; set; } = 36f; // 0.5 inch
        public float MarginTop { get; set; } = 36f;
        public float MarginRight { get; set; } = 36f;
        public float MarginBottom { get; set; } = 36f;

        public List<ReportSection> Sections { get; } = new List<ReportSection>();
        public Dictionary<string, string> Metadata { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public ReportDocument()
        {
        }

        public ReportDocument(string title)
        {
            Title = title;
        }

        public ReportSection AddSection(string name = "")
        {
            var section = new ReportSection { Name = name };
            Sections.Add(section);
            return section;
        }

        /// <summary>
        /// Renders this report to a vector PDF 1.4 byte array.
        /// </summary>
        public byte[] RenderToPdf()
        {
            using (var doc = new PdfDocument())
            {
                var page = doc.AddPage(PageWidth, PageHeight);

                float currentY = MarginTop;
                float usableWidth = PageWidth - MarginLeft - MarginRight;

                // Title
                if (!string.IsNullOrEmpty(Title))
                {
                    page.SetFillColor(0.1f, 0.15f, 0.25f);
                    page.DrawText(Title, MarginLeft, currentY + 18, fontSize: 18);
                    currentY += 28;
                }

                // Subtitle
                if (!string.IsNullOrEmpty(Subtitle))
                {
                    page.SetFillColor(0.4f, 0.4f, 0.45f);
                    page.DrawText(Subtitle, MarginLeft, currentY + 11, fontSize: 11);
                    currentY += 18;
                }

                // Divider line
                page.SetStrokeColor(0.8f, 0.8f, 0.85f);
                page.SetLineWidth(1f);
                page.DrawLine(MarginLeft, currentY, PageWidth - MarginRight, currentY);
                currentY += 15;

                // Render sections
                foreach (var section in Sections)
                {
                    if (!string.IsNullOrEmpty(section.Name))
                    {
                        page.SetFillColor(0.2f, 0.3f, 0.5f);
                        page.DrawText(section.Name, MarginLeft, currentY + 12, fontSize: 13);
                        currentY += 20;
                    }

                    foreach (var element in section.Elements)
                    {
                        if (element is TextBlockElement tb)
                        {
                            page.SetFillColor(0f, 0f, 0f);
                            page.DrawText(tb.Text, MarginLeft + tb.Indent, currentY + tb.FontSize, fontSize: tb.FontSize);
                            currentY += tb.FontSize + 6;
                        }
                        else if (element is KeyValueGridElement kv)
                        {
                            foreach (var pair in kv.Pairs)
                            {
                                page.SetFillColor(0.35f, 0.35f, 0.35f);
                                page.DrawText(pair.Key + ":", MarginLeft + 10, currentY + 10, fontSize: 10);
                                page.SetFillColor(0f, 0f, 0f);
                                page.DrawText(pair.Value, MarginLeft + 160, currentY + 10, fontSize: 10);
                                currentY += 16;
                            }
                            currentY += 6;
                        }
                        else if (element is TableElement tbl)
                        {
                            currentY = RenderTableToPdf(page, tbl, MarginLeft, currentY, usableWidth);
                            currentY += 15;
                        }
                        else if (element is SpacerElement sp)
                        {
                            currentY += sp.Height;
                        }
                    }
                }

                return doc.ToByteArray();
            }
        }

        private float RenderTableToPdf(PdfPage page, TableElement tbl, float startX, float startY, float width)
        {
            if (tbl.Columns.Count == 0) return startY;

            float currentY = startY;
            float rowHeight = 20f;
            float colWidth = width / tbl.Columns.Count;

            // Draw Header
            page.SetFillColor(0.92f, 0.94f, 0.97f);
            page.DrawRectangle(startX, currentY, width, rowHeight, fill: true, stroke: false);

            page.SetFillColor(0.1f, 0.1f, 0.2f);
            for (int i = 0; i < tbl.Columns.Count; i++)
            {
                page.DrawText(tbl.Columns[i], startX + (i * colWidth) + 4, currentY + 13, fontSize: 10);
            }
            currentY += rowHeight;

            // Header underline
            page.SetStrokeColor(0.7f, 0.75f, 0.8f);
            page.SetLineWidth(1f);
            page.DrawLine(startX, currentY, startX + width, currentY);

            // Draw Rows
            for (int r = 0; r < tbl.Rows.Count; r++)
            {
                var row = tbl.Rows[r];

                // Alternate row highlight
                if (r % 2 == 1)
                {
                    page.SetFillColor(0.98f, 0.98f, 0.99f);
                    page.DrawRectangle(startX, currentY, width, rowHeight, fill: true, stroke: false);
                }

                page.SetFillColor(0.1f, 0.1f, 0.1f);
                for (int c = 0; c < Math.Min(row.Count, tbl.Columns.Count); c++)
                {
                    page.DrawText(row[c], startX + (c * colWidth) + 4, currentY + 13, fontSize: 9);
                }

                currentY += rowHeight;
                // Bottom border
                page.SetStrokeColor(0.9f, 0.9f, 0.92f);
                page.DrawLine(startX, currentY, startX + width, currentY);
            }

            return currentY;
        }

        /// <summary>
        /// Converts simple labels/manifests in this document to a ZPL II string.
        /// </summary>
        public string RenderToZpl()
        {
            var zpl = new ZplEncoder();
            int y = 40;

            if (!string.IsNullOrEmpty(Title))
            {
                zpl.DrawText(40, y, Title, fontHeight: 34, fontWidth: 34);
                y += 50;
            }

            foreach (var section in Sections)
            {
                foreach (var el in section.Elements)
                {
                    if (el is TextBlockElement tb)
                    {
                        zpl.DrawText(40, y, tb.Text, fontHeight: (int)tb.FontSize * 2, fontWidth: (int)tb.FontSize * 2);
                        y += 35;
                    }
                    else if (el is KeyValueGridElement kv)
                    {
                        foreach (var pair in kv.Pairs)
                        {
                            zpl.DrawText(40, y, $"{pair.Key}: {pair.Value}", fontHeight: 24, fontWidth: 24);
                            y += 30;
                        }
                    }
                }
            }

            zpl.EndLabel();
            return zpl.ToZplString();
        }
    }

    public class ReportSection
    {
        public string Name { get; set; } = string.Empty;
        public List<IReportElement> Elements { get; } = new List<IReportElement>();

        public ReportSection AddText(string text, float fontSize = 11f, float indent = 0f)
        {
            Elements.Add(new TextBlockElement { Text = text, FontSize = fontSize, Indent = indent });
            return this;
        }

        public ReportSection AddKeyValue(string key, string value)
        {
            var grid = Elements.Find(e => e is KeyValueGridElement) as KeyValueGridElement;
            if (grid == null)
            {
                grid = new KeyValueGridElement();
                Elements.Add(grid);
            }
            grid.Pairs.Add(new KeyValuePair<string, string>(key, value));
            return this;
        }

        public ReportSection AddTable(Action<TableElement> configure)
        {
            var table = new TableElement();
            configure(table);
            Elements.Add(table);
            return this;
        }

        public ReportSection AddSpacer(float height = 10f)
        {
            Elements.Add(new SpacerElement { Height = height });
            return this;
        }
    }

    public interface IReportElement { }

    public class TextBlockElement : IReportElement
    {
        public string Text { get; set; } = string.Empty;
        public float FontSize { get; set; } = 11f;
        public float Indent { get; set; } = 0f;
    }

    public class KeyValueGridElement : IReportElement
    {
        public List<KeyValuePair<string, string>> Pairs { get; } = new List<KeyValuePair<string, string>>();
    }

    public class TableElement : IReportElement
    {
        public List<string> Columns { get; } = new List<string>();
        public List<List<string>> Rows { get; } = new List<List<string>>();

        public TableElement AddColumn(string name)
        {
            Columns.Add(name);
            return this;
        }

        public TableElement AddRow(params string[] values)
        {
            Rows.Add(new List<string>(values));
            return this;
        }
    }

    public class SpacerElement : IReportElement
    {
        public float Height { get; set; } = 10f;
    }
}
