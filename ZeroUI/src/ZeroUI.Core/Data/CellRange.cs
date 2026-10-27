using System;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Represents a 2D rectangular range of cells in a DataGrid for block selection and clipboard interop.
    /// </summary>
    public readonly struct CellRange : IEquatable<CellRange>
    {
        public readonly int StartRow;
        public readonly int StartColumn;
        public readonly int EndRow;
        public readonly int EndColumn;

        public CellRange(int startRow, int startCol, int endRow, int endCol)
        {
            StartRow = startRow;
            StartColumn = startCol;
            EndRow = endRow;
            EndColumn = endCol;
        }

        public int TopRow => Math.Min(StartRow, EndRow);
        public int BottomRow => Math.Max(StartRow, EndRow);
        public int LeftColumn => Math.Min(StartColumn, EndColumn);
        public int RightColumn => Math.Max(StartColumn, EndColumn);

        public int RowCount => Math.Abs(EndRow - StartRow) + 1;
        public int ColumnCount => Math.Abs(EndColumn - StartColumn) + 1;

        public bool Contains(int row, int col)
        {
            return row >= TopRow && row <= BottomRow && col >= LeftColumn && col <= RightColumn;
        }

        public bool IsEmpty => StartRow < 0 || StartColumn < 0;

        public static readonly CellRange Empty = new CellRange(-1, -1, -1, -1);

        public bool Equals(CellRange other) =>
            StartRow == other.StartRow && StartColumn == other.StartColumn &&
            EndRow == other.EndRow && EndColumn == other.EndColumn;

        public override bool Equals(object? obj) => obj is CellRange other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + StartRow;
                hash = hash * 31 + StartColumn;
                hash = hash * 31 + EndRow;
                hash = hash * 31 + EndColumn;
                return hash;
            }
        }

        public static bool operator ==(CellRange left, CellRange right) => left.Equals(right);
        public static bool operator !=(CellRange left, CellRange right) => !left.Equals(right);
    }
}
