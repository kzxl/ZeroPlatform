using System;
using System.Linq;

namespace ZeroDocuments.Excel.Models
{
    /// <summary>
    /// Utility for parsing and converting Excel cell coordinates and range boundaries.
    /// </summary>
    public static class ExcelCellAddress
    {
        public const string DefaultRange = "A1:ZZ5000";

        /// <summary>
        /// Converts Excel column name to 1-based index (e.g., "A" -> 1, "Z" -> 26, "AA" -> 27).
        /// </summary>
        public static int ColumnNameToIndex(string? columnName)
        {
            if (string.IsNullOrEmpty(columnName)) return 1;
            columnName = columnName!.ToUpperInvariant();
            int sum = 0;
            for (int i = 0; i < columnName.Length; i++)
            {
                if (columnName[i] >= 'A' && columnName[i] <= 'Z')
                {
                    sum *= 26;
                    sum += (columnName[i] - 'A' + 1);
                }
            }
            return sum > 0 ? sum : 1;
        }

        /// <summary>
        /// Converts 1-based column index to Excel column name (e.g., 1 -> "A", 26 -> "Z", 27 -> "AA").
        /// </summary>
        public static string IndexToColumnName(int index)
        {
            if (index <= 0) return "A";
            string col = string.Empty;
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                col = (char)('A' + rem) + col;
                index = (index - rem) / 26;
            }
            return col;
        }

        /// <summary>
        /// Splits a cell reference like "BC123" into column name ("BC") and row index (123).
        /// </summary>
        public static bool TryParseCellReference(string cellRef, out string columnName, out int rowNumber)
        {
            columnName = "A";
            rowNumber = 1;
            if (string.IsNullOrWhiteSpace(cellRef)) return false;

            int letterCount = 0;
            while (letterCount < cellRef.Length && char.IsLetter(cellRef[letterCount]))
            {
                letterCount++;
            }

            if (letterCount == 0 || letterCount == cellRef.Length) return false;

            columnName = cellRef.Substring(0, letterCount).ToUpperInvariant();
            return int.TryParse(cellRef.Substring(letterCount), out rowNumber);
        }

        /// <summary>
        /// Parses a cell range string (e.g. "A1:D50") into bounding coordinates.
        /// </summary>
        public static void ParseCellRange(string? range, out string startCol, out int startRow, out string endCol, out int endRow)
        {
            startCol = "A";
            startRow = 1;
            endCol = "ZZ";
            endRow = 5000;

            if (string.IsNullOrWhiteSpace(range)) return;

            string[] parts = range!.Split(':');
            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                if (TryParseCellReference(parts[0].Trim(), out var col1, out var row1))
                {
                    startCol = col1;
                    startRow = row1;
                }
            }

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                if (TryParseCellReference(parts[1].Trim(), out var col2, out var row2))
                {
                    endCol = col2;
                    endRow = row2;
                }
            }
        }

        /// <summary>
        /// Extends a single-row header range (e.g. "D24:T24") into a full data range down to maxRows.
        /// </summary>
        public static string ConvertHeaderRangeToDataRange(string? headerRange, int maxRows = 5000)
        {
            if (string.IsNullOrWhiteSpace(headerRange)) return DefaultRange;

            string[] parts = headerRange!.Split(':');
            if (parts.Length != 2) return headerRange;

            string startCell = parts[0].Trim();
            string endCell = parts[1].Trim();

            if (TryParseCellReference(startCell, out var startCol, out var startRow) &&
                TryParseCellReference(endCell, out var endCol, out var endRow))
            {
                if (startRow == endRow)
                {
                    return $"{startCol}{startRow + 1}:{endCol}{startRow + maxRows}";
                }
            }

            return headerRange;
        }
    }
}
