using System;
using System.Collections.Generic;
using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Tables;

/// <summary>
/// Represents a single cell within an extracted table grid.
/// </summary>
public sealed class OcrTableCell
{
    public int RowIndex { get; }
    public int ColumnIndex { get; }
    public int RowSpan { get; }
    public int ColSpan { get; }
    public string Text { get; }
    public float Confidence { get; }
    public OcrRect BoundingBox { get; }
    public IReadOnlyList<OcrWord> Words { get; }
    public bool IsHeader { get; }

    public OcrTableCell(
        int rowIndex,
        int columnIndex,
        string text,
        OcrRect boundingBox,
        float confidence = 1.0f,
        int rowSpan = 1,
        int colSpan = 1,
        IReadOnlyList<OcrWord>? words = null,
        bool isHeader = false)
    {
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
        Text = text ?? string.Empty;
        BoundingBox = boundingBox;
        Confidence = confidence;
        RowSpan = Math.Max(1, rowSpan);
        ColSpan = Math.Max(1, colSpan);
        Words = words ?? Array.Empty<OcrWord>();
        IsHeader = isHeader;
    }

    public override string ToString() => $"[{RowIndex},{ColumnIndex}]: \"{Text}\" ({BoundingBox})";
}
