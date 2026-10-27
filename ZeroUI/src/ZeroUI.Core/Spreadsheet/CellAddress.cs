using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroUI.Core.Spreadsheet
{
    /// <summary>
    /// Represents a zero-based (Row, Column) spreadsheet cell coordinate (e.g. A1 = 0,0; B12 = 11,1).
    /// </summary>
    public readonly struct CellAddress : IEquatable<CellAddress>, IComparable<CellAddress>
    {
        public int Row { get; }
        public int Column { get; }

        public CellAddress(int row, int column)
        {
            Row = Math.Max(0, row);
            Column = Math.Max(0, column);
        }

        public string Name => ToAddress(Row, Column);

        public override string ToString() => Name;

        public static string ColumnIndexToLetters(int col)
        {
            if (col < 0) col = 0;
            StringBuilder sb = new StringBuilder();
            col++;
            while (col > 0)
            {
                int rem = (col - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                col = (col - rem) / 26;
            }
            return sb.ToString();
        }

        public static int LettersToColumnIndex(string letters)
        {
            if (string.IsNullOrEmpty(letters)) return 0;
            int col = 0;
            for (int i = 0; i < letters.Length; i++)
            {
                char c = char.ToUpperInvariant(letters[i]);
                if (c >= 'A' && c <= 'Z')
                {
                    col = col * 26 + (c - 'A' + 1);
                }
            }
            return Math.Max(0, col - 1);
        }

        public static string ToAddress(int row, int col)
        {
            return $"{ColumnIndexToLetters(col)}{row + 1}";
        }

        public static bool TryParse(string? text, out CellAddress address)
        {
            address = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text!.Trim();
            int splitIdx = 0;
            while (splitIdx < text.Length && char.IsLetter(text[splitIdx]))
            {
                splitIdx++;
            }

            if (splitIdx == 0 || splitIdx >= text.Length) return false;

            string colLetters = text.Substring(0, splitIdx);
            string rowDigits = text.Substring(splitIdx);

            if (int.TryParse(rowDigits, out int rowNum) && rowNum >= 1)
            {
                int col = LettersToColumnIndex(colLetters);
                address = new CellAddress(rowNum - 1, col);
                return true;
            }

            return false;
        }

        public static CellAddress Parse(string text)
        {
            if (TryParse(text, out var addr))
                return addr;
            throw new FormatException($"Invalid cell address '{text}'. Expected format like 'A1' or 'C25'.");
        }

        public bool Equals(CellAddress other) => Row == other.Row && Column == other.Column;
        public override bool Equals(object? obj) => obj is CellAddress other && Equals(other);
        public override int GetHashCode() => (Row * 397) ^ Column;
        public static bool operator ==(CellAddress left, CellAddress right) => left.Equals(right);
        public static bool operator !=(CellAddress left, CellAddress right) => !left.Equals(right);

        public int CompareTo(CellAddress other)
        {
            int r = Row.CompareTo(other.Row);
            return r != 0 ? r : Column.CompareTo(other.Column);
        }
    }

    /// <summary>
    /// Represents a 2D rectangular range of cells (e.g. A1:B10).
    /// </summary>
    public readonly struct CellRange : IEquatable<CellRange>
    {
        public CellAddress Start { get; }
        public CellAddress End { get; }

        public int MinRow => Math.Min(Start.Row, End.Row);
        public int MaxRow => Math.Max(Start.Row, End.Row);
        public int MinColumn => Math.Min(Start.Column, End.Column);
        public int MaxColumn => Math.Max(Start.Column, End.Column);

        public int RowCount => MaxRow - MinRow + 1;
        public int ColumnCount => MaxColumn - MinColumn + 1;
        public int TotalCells => RowCount * ColumnCount;

        public CellRange(CellAddress start, CellAddress end)
        {
            Start = start;
            End = end;
        }

        public CellRange(int startRow, int startCol, int endRow, int endCol)
        {
            Start = new CellAddress(startRow, startCol);
            End = new CellAddress(endRow, endCol);
        }

        public bool Contains(CellAddress addr)
        {
            return addr.Row >= MinRow && addr.Row <= MaxRow &&
                   addr.Column >= MinColumn && addr.Column <= MaxColumn;
        }

        public IEnumerable<CellAddress> GetAddresses()
        {
            int minR = MinRow;
            int maxR = MaxRow;
            int minC = MinColumn;
            int maxC = MaxColumn;

            for (int r = minR; r <= maxR; r++)
            {
                for (int c = minC; c <= maxC; c++)
                {
                    yield return new CellAddress(r, c);
                }
            }
        }

        public static bool TryParse(string? text, out CellRange range)
        {
            range = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text!.Trim();
            int colonIdx = text.IndexOf(':');

            if (colonIdx > 0)
            {
                string first = text.Substring(0, colonIdx);
                string second = text.Substring(colonIdx + 1);

                if (CellAddress.TryParse(first, out var a1) && CellAddress.TryParse(second, out var a2))
                {
                    range = new CellRange(a1, a2);
                    return true;
                }
            }
            else if (CellAddress.TryParse(text, out var single))
            {
                range = new CellRange(single, single);
                return true;
            }

            return false;
        }

        public static CellRange Parse(string text)
        {
            if (TryParse(text, out var range))
                return range;
            throw new FormatException($"Invalid cell range '{text}'. Expected format like 'A1:B10'.");
        }

        public override string ToString() => $"{Start.Name}:{End.Name}";

        public bool Equals(CellRange other) => Start == other.Start && End == other.End;
        public override bool Equals(object? obj) => obj is CellRange other && Equals(other);
        public override int GetHashCode() => (Start.GetHashCode() * 397) ^ End.GetHashCode();
        public static bool operator ==(CellRange left, CellRange right) => left.Equals(right);
        public static bool operator !=(CellRange left, CellRange right) => !left.Equals(right);
    }
}
