using System;
using System.Collections.Generic;

namespace ZeroReports.Engine.Table
{
    [Flags]
    public enum CellBorders
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        All = Left | Top | Right | Bottom
    }

    /// <summary>
    /// Represents an individual cell within a ReportTable row, equivalent to DevExpress XRTableCell.
    /// Supports ColSpan, explicit borders, formatting, background fills, and data binding.
    /// </summary>
    public class ReportCell
    {
        public string Text { get; set; } = string.Empty;
        public string DataField { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public int ColSpan { get; set; } = 1;

        public float FontSize { get; set; } = 10f;
        public bool IsBold { get; set; }
        public ReportTextAlignment Alignment { get; set; } = ReportTextAlignment.Left;
        public (float R, float G, float B) TextColor { get; set; } = (0f, 0f, 0f);
        public (float R, float G, float B)? BackgroundColor { get; set; }

        public CellBorders Borders { get; set; } = CellBorders.All;
        public (float R, float G, float B) BorderColor { get; set; } = (0.8f, 0.8f, 0.85f);
        public float BorderWidth { get; set; } = 0.5f;

        public List<ZeroReports.Engine.Formatting.ConditionalFormattingRule> FormattingRules { get; } = new List<ZeroReports.Engine.Formatting.ConditionalFormattingRule>();

        public ReportCell() { }

        public ReportCell(string text, int colSpan = 1)
        {
            Text = text;
            ColSpan = colSpan;
        }
    }

    /// <summary>
    /// Represents a horizontal row within a ReportTable, equivalent to DevExpress XRTableRow.
    /// </summary>
    public class ReportRow
    {
        public float Height { get; set; } = 22f;
        public List<ReportCell> Cells { get; } = new List<ReportCell>();

        public ReportRow(float height = 22f)
        {
            Height = height;
        }

        public ReportCell AddCell(string text = "", int colSpan = 1)
        {
            var cell = new ReportCell(text, colSpan);
            Cells.Add(cell);
            return cell;
        }

        public ReportCell AddCell(
            string text,
            int colSpan,
            bool isBold,
            ReportTextAlignment align,
            (float R, float G, float B)? bgColor = null)
        {
            var cell = new ReportCell(text, colSpan)
            {
                IsBold = isBold,
                Alignment = align,
                BackgroundColor = bgColor
            };
            Cells.Add(cell);
            return cell;
        }
    }

    /// <summary>
    /// Structured table control equivalent to DevExpress XRTable.
    /// Guarantees exact column alignment across Header, Detail, and Footer bands with ColSpan support.
    /// </summary>
    public class ReportTable : ReportControl
    {
        public List<float> ColumnWidths { get; } = new List<float>();
        public List<ReportRow> Rows { get; } = new List<ReportRow>();

        public ReportTable() { }

        public ReportTable(float left, float top, float width)
        {
            Left = left;
            Top = top;
            Width = width;
        }

        /// <summary>
        /// Sets fixed column widths in points.
        /// </summary>
        public ReportTable SetColumns(params float[] widths)
        {
            ColumnWidths.Clear();
            float total = 0f;
            foreach (var w in widths)
            {
                ColumnWidths.Add(w);
                total += w;
            }
            Width = total;
            return this;
        }

        /// <summary>
        /// Sets proportional column widths distributing total table width according to ratios.
        /// </summary>
        public ReportTable SetProportionalColumns(float totalWidth, params float[] ratios)
        {
            ColumnWidths.Clear();
            float sumRatio = 0f;
            foreach (var r in ratios) sumRatio += r;
            if (sumRatio <= 0f) sumRatio = 1f;

            Width = totalWidth;
            foreach (var r in ratios)
            {
                ColumnWidths.Add((r / sumRatio) * totalWidth);
            }
            return this;
        }

        public ReportRow AddRow(float height = 22f)
        {
            var row = new ReportRow(height);
            Rows.Add(row);
            return row;
        }

        /// <summary>
        /// Calculates total height of all rows in the table.
        /// </summary>
        public float CalculateTotalHeight()
        {
            float total = 0f;
            foreach (var r in Rows) total += r.Height;
            return total;
        }
    }
}
