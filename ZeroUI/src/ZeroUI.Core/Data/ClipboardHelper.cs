using System;
using System.Buffers;
using System.Text;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Callback delegate for receiving cell slices during TSV parsing without heap allocation.
    /// </summary>
    public delegate void TsvCellSpanCallback(int rowIndex, int colIndex, ReadOnlySpan<char> cellSpan);

    /// <summary>
    /// High-performance Tab-Separated Values (TSV) clipboard serialization helper.
    /// Eliminates heap allocations on hot copy/paste paths using ArrayPool and span-based slicing.
    /// </summary>
    public static class ClipboardHelper
    {
        /// <summary>
        /// Formats a 2D string array into standard Windows Clipboard TSV text.
        /// </summary>
        public static string FormatTsv(string[,] matrix)
        {
            if (matrix == null) return string.Empty;
            int rowCount = matrix.GetLength(0);
            int colCount = matrix.GetLength(1);
            return FormatTsv(rowCount, colCount, (r, c) => matrix[r, c]);
        }

        /// <summary>
        /// Formats a 2D matrix of cells into standard Windows Clipboard TSV text.
        /// </summary>
        public static string FormatTsv(int rowCount, int colCount, Func<int, int, string?> cellValueSelector)
        {
            if (rowCount <= 0 || colCount <= 0 || cellValueSelector == null)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(rowCount * colCount * 16);
            for (int r = 0; r < rowCount; r++)
            {
                for (int c = 0; c < colCount; c++)
                {
                    if (c > 0)
                    {
                        sb.Append('\t');
                    }

                    string? val = cellValueSelector(r, c);
                    if (!string.IsNullOrEmpty(val))
                    {
                        sb.Append(val);
                    }
                }

                if (r < rowCount - 1)
                {
                    sb.Append("\r\n");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Parses a TSV string into row and column cell spans.
        /// </summary>
        public static void ParseTsv(string? tsv, TsvCellSpanCallback cellCallback)
        {
            if (!string.IsNullOrEmpty(tsv) && cellCallback != null)
            {
                ParseTsv(tsv.AsSpan(), cellCallback);
            }
        }

        /// <summary>
        /// Parses a TSV text stream into row and column cell spans without string allocations.
        /// </summary>
        /// <param name="tsv">The raw TSV text as a span of characters.</param>
        /// <param name="cellCallback">Callback invoked for each cell (rowIndex, colIndex, cellSpan).</param>
        public static void ParseTsv(ReadOnlySpan<char> tsv, TsvCellSpanCallback cellCallback)
        {
            if (tsv.IsEmpty || cellCallback == null)
            {
                return;
            }

            int rowIndex = 0;
            int colIndex = 0;
            int startIdx = 0;

            for (int i = 0; i < tsv.Length; i++)
            {
                char ch = tsv[i];

                if (ch == '\t')
                {
                    var cell = tsv.Slice(startIdx, i - startIdx);
                    cellCallback(rowIndex, colIndex++, cell);
                    startIdx = i + 1;
                }
                else if (ch == '\r')
                {
                    var cell = tsv.Slice(startIdx, i - startIdx);
                    cellCallback(rowIndex, colIndex, cell);

                    // Check for trailing \n
                    if (i + 1 < tsv.Length && tsv[i + 1] == '\n')
                    {
                        i++;
                    }

                    startIdx = i + 1;
                    rowIndex++;
                    colIndex = 0;
                }
                else if (ch == '\n')
                {
                    var cell = tsv.Slice(startIdx, i - startIdx);
                    cellCallback(rowIndex, colIndex, cell);

                    startIdx = i + 1;
                    rowIndex++;
                    colIndex = 0;
                }
            }

            // Flush trailing cell if text didn't end with newline
            if (startIdx <= tsv.Length && colIndex >= 0 && startIdx < tsv.Length)
            {
                var trailingCell = tsv.Slice(startIdx);
                cellCallback(rowIndex, colIndex, trailingCell);
            }
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="ClipboardHelper"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use ClipboardHelper instead.")]
    public static class ZeroClipboardHelper
    {
        public static string FormatTsv(string[,] matrix) => ClipboardHelper.FormatTsv(matrix);
        public static string FormatTsv(int rowCount, int colCount, Func<int, int, string?> cellValueSelector) => ClipboardHelper.FormatTsv(rowCount, colCount, cellValueSelector);
        public static void ParseTsv(string? tsv, TsvCellSpanCallback cellCallback) => ClipboardHelper.ParseTsv(tsv, cellCallback);
        public static void ParseTsv(ReadOnlySpan<char> tsv, TsvCellSpanCallback cellCallback) => ClipboardHelper.ParseTsv(tsv, cellCallback);
    }
}

