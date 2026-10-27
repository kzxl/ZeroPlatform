using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroOcr.Core.Models;

/// <summary>
/// Represents a contiguous horizontal or skewed line of recognized words.
/// </summary>
public sealed class OcrLine
{
    public string Text { get; }
    public IReadOnlyList<OcrWord> Words { get; }
    public OcrRect BoundingBox { get; }
    public float Confidence { get; }
    public float? AngleDegrees { get; }

    public OcrLine(
        string text,
        IReadOnlyList<OcrWord>? words = null,
        OcrRect? boundingBox = null,
        float? confidence = null,
        float? angleDegrees = null)
    {
        Words = words ?? Array.Empty<OcrWord>();
        Text = text ?? string.Join(" ", Words.Select(w => w.Text));
        AngleDegrees = angleDegrees;

        if (boundingBox.HasValue)
        {
            BoundingBox = boundingBox.Value;
        }
        else if (Words.Count > 0)
        {
            var rect = Words[0].BoundingBox;
            for (int i = 1; i < Words.Count; i++)
            {
                rect = rect.Union(Words[i].BoundingBox);
            }
            BoundingBox = rect;
        }
        else
        {
            BoundingBox = OcrRect.Empty;
        }

        if (confidence.HasValue)
        {
            Confidence = Math.Max(0.0f, Math.Min(1.0f, confidence.Value));
        }
        else if (Words.Count > 0)
        {
            float sum = 0f;
            for (int i = 0; i < Words.Count; i++)
                sum += Words[i].Confidence;
            Confidence = sum / Words.Count;
        }
        else
        {
            Confidence = 1.0f;
        }
    }

    public override string ToString() => $"Line: \"{Text}\" ({Words.Count} words, Bounds: {BoundingBox})";
}
