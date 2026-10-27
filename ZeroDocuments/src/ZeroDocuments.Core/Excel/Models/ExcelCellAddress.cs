using System;
using System.Linq;

namespace ZeroDocuments.Excel.Models
{
    /// <summary>
    /// Utility for parsing and converting Excel cell coordinates and range boundaries.
    /// </summary>
    public static class ExcelCellAddress
    {
        public const string DefaultRange = "A1:XFD1048576";

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
        /// Splits a cell reference like "BC123" or "$BC$123" into column name ("BC") and row index (123).
        /// </summary>
        public static bool TryParseCellReference(string cellRef, out string columnName, out int rowNumber)
        {
            columnName = "A";
            rowNumber = 1;
            if (string.IsNullOrWhiteSpace(cellRef)) return false;

            if (cellRef.IndexOf('$') >= 0)
            {
                cellRef = cellRef.Replace("$", "");
            }

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
        /// Parses a cell range string (e.g. "A1:D50", "$A$1:$D$50", "A:D", "1:10") into bounding coordinates.
        /// </summary>
        public static void ParseCellRange(string? range, out string startCol, out int startRow, out string endCol, out int endRow)
        {
            startCol = "A";
            startRow = 1;
            endCol = "XFD";
            endRow = 1048576;

            if (string.IsNullOrWhiteSpace(range)) return;

            string[] parts = range!.Split(':');
            if (parts.Length == 1 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                string p = parts[0].Trim();
                if (TryParseCellReference(p, out var col, out var row))
                {
                    startCol = col;
                    startRow = row;
                    endCol = col;
                    endRow = row;
                }
                return;
            }

            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
            {
                string p0 = parts[0].Trim();
                if (TryParseCellReference(p0, out var col1, out var row1))
                {
                    startCol = col1;
                    startRow = row1;
                }
                else if (IsAllLetters(p0))
                {
                    startCol = p0.Replace("$", "").ToUpperInvariant();
                    startRow = 1;
                }
                else if (int.TryParse(p0.Replace("$", ""), out int r0))
                {
                    startCol = "A";
                    startRow = r0;
                }
            }

            if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                string p1 = parts[1].Trim();
                if (TryParseCellReference(p1, out var col2, out var row2))
                {
                    endCol = col2;
                    endRow = row2;
                }
                else if (IsAllLetters(p1))
                {
                    endCol = p1.Replace("$", "").ToUpperInvariant();
                    endRow = 1048576;
                }
                else if (int.TryParse(p1.Replace("$", ""), out int r1))
                {
                    endCol = "XFD";
                    endRow = r1;
                }
            }
        }

        private static bool IsAllLetters(string s)
        {
            if (s.IndexOf('$') >= 0) s = s.Replace("$", "");
            if (string.IsNullOrEmpty(s) || s.Length > 3) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsLetter(s[i])) return false;
            }
            return true;
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
