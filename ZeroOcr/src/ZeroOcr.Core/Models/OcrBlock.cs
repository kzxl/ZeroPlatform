using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroOcr.Core.Models;

/// <summary>
/// Represents a cohesive spatial block/paragraph of text containing one or more lines.
/// </summary>
public sealed class OcrBlock
{
    public IReadOnlyList<OcrLine> Lines { get; }
    public OcrRect BoundingBox { get; }
    public string Text { get; }

    public OcrBlock(IReadOnlyList<OcrLine> lines, OcrRect? boundingBox = null)
    {
        Lines = lines ?? Array.Empty<OcrLine>();
        Text = string.Join(Environment.NewLine, Lines.Select(l => l.Text));

        if (boundingBox.HasValue)
        {
            BoundingBox = boundingBox.Value;
        }
        else if (Lines.Count > 0)
        {
            var rect = Lines[0].BoundingBox;
            for (int i = 1; i < Lines.Count; i++)
            {
                rect = rect.Union(Lines[i].BoundingBox);
            }
            BoundingBox = rect;
        }
        else
        {
            BoundingBox = OcrRect.Empty;
        }
    }

    public override string ToString() => $"Block: {Lines.Count} lines, Bounds: {BoundingBox}";
}
