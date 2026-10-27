using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Tables;

/// <summary>
/// Represents a structured tabular matrix extracted from an OCR document.
/// Provides native bidirectional exports to System.Data.DataTable, Markdown, CSV, and 2D string matrices.
/// </summary>
public sealed class OcrTable
{
    public int TableIndex { get; }
    public OcrRect BoundingBox { get; }
    public IReadOnlyList<OcrTableRow> Rows { get; }
    public int RowCount => Rows.Count;
    public int ColumnCount { get; }

    public OcrTable(
        int tableIndex,
        OcrRect boundingBox,
        IReadOnlyList<OcrTableRow> rows,
        int columnCount)
    {
        TableIndex = tableIndex;
        BoundingBox = boundingBox;
        Rows = rows ?? Array.Empty<OcrTableRow>();
        ColumnCount = Math.Max(0, columnCount);
    }

    /// <summary>
    /// Gets the cell located at the specified (row, col) coordinates, or null if cell is empty.
    /// </summary>
    public OcrTableCell? this[int rowIndex, int columnIndex]
    {
        get
        {
            if (rowIndex < 0 || rowIndex >= Rows.Count) return null;
            return Rows[rowIndex][columnIndex];
        }
    }

    /// <summary>
    /// Converts the extracted OCR table directly into a standard ADO.NET DataTable.
    /// </summary>
    /// <param name="firstRowIsHeader">Whether to use the first row's cell texts as column headers.</param>
    public DataTable ToDataTable(bool firstRowIsHeader = true)
    {
        var dt = new DataTable($"OcrTable_{TableIndex}");
        if (RowCount == 0 || ColumnCount == 0) return dt;

        int startDataRow = 0;
        var colNames = new List<string>(ColumnCount);

        if (firstRowIsHeader && RowCount > 0)
        {
            var headerRow = Rows[0];
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int c = 0; c < ColumnCount; c++)
            {
                string headerText = headerRow[c]?.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(headerText))
                {
                    headerText = $"Column_{c + 1}";
                }

                // Ensure unique column names in DataTable
                string uniqueName = headerText;
                int suffix = 2;
                while (usedNames.Contains(uniqueName))
                {
                    uniqueName = $"{headerText}_{suffix++}";
                }

                usedNames.Add(uniqueName);
                colNames.Add(uniqueName);
                dt.Columns.Add(uniqueName, typeof(string));
            }

            startDataRow = 1;
        }
        else
        {
            for (int c = 0; c < ColumnCount; c++)
            {
                string colName = $"Column_{c + 1}";
                colNames.Add(colName);
                dt.Columns.Add(colName, typeof(string));
            }
        }

        // Populate data rows
        for (int r = startDataRow; r < RowCount; r++)
        {
            var dataRow = dt.NewRow();
            var tableRow = Rows[r];

            for (int c = 0; c < ColumnCount; c++)
            {
                var cell = tableRow[c];
                dataRow[c] = cell?.Text ?? string.Empty;
            }

            dt.Rows.Add(dataRow);
        }

        return dt;
    }

    /// <summary>
    /// Formats the table into a clean GitHub-flavored Markdown table.
    /// </summary>
    public string ToMarkdown()
    {
        if (RowCount == 0 || ColumnCount == 0) return string.Empty;

        var sb = new StringBuilder();

        // 1. Headers
        sb.Append("|");
        var headerRow = Rows[0];
        for (int c = 0; c < ColumnCount; c++)
        {
            string txt = headerRow[c]?.Text?.Trim() ?? $"Col {c + 1}";
            sb.Append(" ").Append(txt.Replace("|", "\\|")).Append(" |");
        }
        sb.AppendLine();

        // 2. Delimiter row
        sb.Append("|");
        for (int c = 0; c < ColumnCount; c++)
        {
            sb.Append(" :--- |");
        }
        sb.AppendLine();

        // 3. Body rows
        for (int r = 1; r < RowCount; r++)
        {
            var row = Rows[r];
            sb.Append("|");
            for (int c = 0; c < ColumnCount; c++)
            {
                string val = row[c]?.Text?.Trim() ?? string.Empty;
                sb.Append(" ").Append(val.Replace("|", "\\|")).Append(" |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Formats the table into an RFC 4180 compliant CSV string.
    /// </summary>
    public string ToCsv(char delimiter = ',')
    {
        if (RowCount == 0 || ColumnCount == 0) return string.Empty;

        var sb = new StringBuilder();

        for (int r = 0; r < RowCount; r++)
        {
            var row = Rows[r];
            for (int c = 0; c < ColumnCount; c++)
            {
                if (c > 0) sb.Append(delimiter);

                string val = row[c]?.Text ?? string.Empty;
                bool needsEscape = val.IndexOf(delimiter) >= 0 || val.IndexOf('"') >= 0 || val.IndexOf('\n') >= 0 || val.IndexOf('\r') >= 0;

                if (needsEscape)
                {
                    sb.Append('"').Append(val.Replace("\"", "\"\"")).Append('"');
                }
                else
                {
                    sb.Append(val);
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports cell contents into a 2D string array [rows, columns].
    /// </summary>
    public string[,] ToGrid()
    {
        var grid = new string[RowCount, ColumnCount];
        for (int r = 0; r < RowCount; r++)
        {
            var row = Rows[r];
            for (int c = 0; c < ColumnCount; c++)
            {
                grid[r, c] = row[c]?.Text ?? string.Empty;
            }
        }
        return grid;
    }
}
