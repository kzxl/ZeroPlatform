using System;

namespace ZeroOcr.Core.Models;

/// <summary>
/// Represents a single recognized word with bounding box and recognition confidence.
/// </summary>
public sealed class OcrWord
{
    public string Text { get; }
    public OcrRect BoundingBox { get; }
    public OcrQuad? Polygon { get; }
    public float Confidence { get; }

    public OcrWord(string text, OcrRect boundingBox, float confidence = 1.0f, OcrQuad? polygon = null)
    {
        Text = text ?? string.Empty;
        BoundingBox = boundingBox;
        Confidence = Math.Max(0.0f, Math.Min(1.0f, confidence));
        Polygon = polygon;
    }

    public override string ToString() => $"'{Text}' (Conf: {Confidence:P0}, Bounds: {BoundingBox})";
}
