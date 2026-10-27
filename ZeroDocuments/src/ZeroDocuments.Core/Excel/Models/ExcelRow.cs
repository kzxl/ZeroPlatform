using System.Collections.Generic;

namespace ZeroDocuments.Excel.Models
{
    public class ExcelCell
    {
        public string Address { get; set; } = string.Empty;
        public int RowIndex { get; set; }
        public int ColumnIndex { get; set; }
        public string? Value { get; set; }

        public override string ToString() => $"{Address}: {Value}";
    }

    public class ExcelRow
    {
        public int RowNumber { get; set; }
        public Dictionary<int, string?> Cells { get; } = new Dictionary<int, string?>();
        public IEnumerable<int> PopulatedColumns => Cells.Keys;

        public string? this[int columnIndex]
        {
            get => Cells.TryGetValue(columnIndex, out var val) ? val : null;
            set => Cells[columnIndex] = value;
        }

        public string? this[string columnName]
        {
            get => this[ExcelCellAddress.ColumnNameToIndex(columnName)];
            set => this[ExcelCellAddress.ColumnNameToIndex(columnName)] = value;
        }
    }
}
