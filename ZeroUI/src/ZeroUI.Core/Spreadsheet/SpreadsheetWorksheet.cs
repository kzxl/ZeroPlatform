using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Spreadsheet
{
    /// <summary>
    /// Represents a sparse 2D tabular worksheet storing cells, column widths, row heights, and freeze pane boundaries.
    /// Uses 64-bit composite keys for O(1) cell lookup and zero allocation on empty cells.
    /// </summary>
    public sealed class SpreadsheetWorksheet
    {
        public string Title { get; set; } = "Sheet1";

        public int RowCount { get; set; } = 100;
        public int ColumnCount { get; set; } = 26;

        public double DefaultColumnWidth { get; set; } = 80.0;
        public double DefaultRowHeight { get; set; } = 22.0;

        public int FrozenRows { get; set; } = 0;
        public int FrozenColumns { get; set; } = 0;

        // Sparse cell dictionary: key = ((long)row << 32) | (uint)col
        private readonly Dictionary<long, SpreadsheetCell> _cells = new Dictionary<long, SpreadsheetCell>();

        // Custom dimension overrides
        private readonly Dictionary<int, double> _columnWidths = new Dictionary<int, double>();
        private readonly Dictionary<int, double> _rowHeights = new Dictionary<int, double>();

        public SpreadsheetWorksheet(string title = "Sheet1", int rowCount = 100, int columnCount = 26)
        {
            Title = title;
            RowCount = Math.Max(1, rowCount);
            ColumnCount = Math.Max(1, columnCount);
        }

        private static long MakeKey(int row, int col) => ((long)row << 32) | (uint)col;

        public SpreadsheetCell? GetCell(int row, int col)
        {
            long key = MakeKey(row, col);
            if (_cells.TryGetValue(key, out var cell))
                return cell;
            return null;
        }

        public SpreadsheetCell? GetCell(CellAddress address) => GetCell(address.Row, address.Column);

        public SpreadsheetCell GetOrCreateCell(int row, int col)
        {
            long key = MakeKey(row, col);
            if (!_cells.TryGetValue(key, out var cell))
            {
                cell = new SpreadsheetCell(row, col);
                _cells[key] = cell;

                if (row >= RowCount) RowCount = row + 1;
                if (col >= ColumnCount) ColumnCount = col + 1;
            }
            return cell;
        }

        public SpreadsheetCell GetOrCreateCell(CellAddress address) => GetOrCreateCell(address.Row, address.Column);

        public void SetValue(int row, int col, string? rawValue)
        {
            var cell = GetOrCreateCell(row, col);
            cell.RawValue = rawValue;
            cell.Error = null;

            if (cell.HasFormula)
            {
                // Defer to formula engine
            }
            else
            {
                // Attempt numeric parse
                if (double.TryParse(rawValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double num))
                {
                    cell.EvaluatedValue = num;
                }
                else
                {
                    cell.EvaluatedValue = rawValue;
                }
            }
        }

        public void SetValue(string addressText, string? rawValue)
        {
            if (CellAddress.TryParse(addressText, out var addr))
            {
                SetValue(addr.Row, addr.Column, rawValue);
            }
        }

        public void ClearCell(int row, int col)
        {
            long key = MakeKey(row, col);
            _cells.Remove(key);
        }

        public IEnumerable<SpreadsheetCell> GetPopulatedCells() => _cells.Values;

        public double GetColumnWidth(int col)
        {
            if (_columnWidths.TryGetValue(col, out double w))
                return w;
            return DefaultColumnWidth;
        }

        public void SetColumnWidth(int col, double width)
        {
            _columnWidths[col] = Math.Max(20.0, Math.Min(500.0, width));
        }

        public double GetRowHeight(int row)
        {
            if (_rowHeights.TryGetValue(row, out double h))
                return h;
            return DefaultRowHeight;
        }

        public void SetRowHeight(int row, double height)
        {
            _rowHeights[row] = Math.Max(14.0, Math.Min(200.0, height));
        }

        /// <summary>
        /// Recalculates all formulas across the worksheet.
        /// </summary>
        public void RecalculateAllFormulas()
        {
            SpreadsheetFormulaEngine.RecalculateWorksheet(this);
        }
    }
}
