using ZeroOcr.Core.Models;

namespace ZeroOcr.Core.Analysis;

/// <summary>
/// Relative spatial direction of the value with respect to the key label.
/// </summary>
public enum SpatialDirection
{
    /// <summary>
    /// Value is located horizontally to the right of the key.
    /// </summary>
    Right = 1,

    /// <summary>
    /// Value is located vertically below the key.
    /// </summary>
    Below = 2
}

/// <summary>
/// Represents an extracted Key-Value pair from OCR layout analysis.
/// </summary>
public sealed class OcrKeyValuePair
{
    public string KeyText { get; }
    public string ValueText { get; }
    public OcrRect KeyBounds { get; }
    public OcrRect ValueBounds { get; }
    public OcrRect TotalBounds { get; }
    public float Confidence { get; }
    public SpatialDirection Direction { get; }

    public OcrKeyValuePair(
        string keyText,
        string valueText,
        OcrRect keyBounds,
        OcrRect valueBounds,
        float confidence,
        SpatialDirection direction)
    {
        KeyText = keyText ?? string.Empty;
        ValueText = valueText ?? string.Empty;
        KeyBounds = keyBounds;
        ValueBounds = valueBounds;
        TotalBounds = keyBounds.Union(valueBounds);
        Confidence = confidence;
        Direction = direction;
    }

    public override string ToString() => $"[{Direction}] \"{KeyText}\": \"{ValueText}\" (Conf: {Confidence:P0})";
}
