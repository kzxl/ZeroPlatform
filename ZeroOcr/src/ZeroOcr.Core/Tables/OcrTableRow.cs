using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroOcr.Core.Tables;

/// <summary>
/// Represents a row of cells within an extracted table grid.
/// </summary>
public sealed class OcrTableRow
{
    public int RowIndex { get; }
    public IReadOnlyList<OcrTableCell> Cells { get; }
    public bool IsHeaderRow { get; }
    public int CellCount => Cells.Count;

    public OcrTableRow(int rowIndex, IReadOnlyList<OcrTableCell> cells, bool isHeaderRow = false)
    {
        RowIndex = rowIndex;
        Cells = cells ?? Array.Empty<OcrTableCell>();
        IsHeaderRow = isHeaderRow;
    }

    /// <summary>
    /// Gets the cell at the specified column index, or null if empty.
    /// </summary>
    public OcrTableCell? this[int columnIndex]
    {
        get
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i].ColumnIndex == columnIndex)
                    return Cells[i];
            }
            return null;
        }
    }

    public override string ToString() => $"Row {RowIndex} ({Cells.Count} cells): " + string.Join(" | ", Cells.Select(c => c.Text));
}
