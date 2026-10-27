using System;

namespace ZeroOcr.Core.Models;

/// <summary>
/// Represents an oriented 4-vertex polygon (quadrilateral) for rotated or perspective-distorted text.
/// Order: TopLeft, TopRight, BottomRight, BottomLeft.
/// </summary>
public readonly struct OcrQuad : IEquatable<OcrQuad>
{
    public OcrPoint TopLeft { get; }
    public OcrPoint TopRight { get; }
    public OcrPoint BottomRight { get; }
    public OcrPoint BottomLeft { get; }

    public OcrQuad(OcrPoint topLeft, OcrPoint topRight, OcrPoint bottomRight, OcrPoint bottomLeft)
    {
        TopLeft = topLeft;
        TopRight = topRight;
        BottomRight = bottomRight;
        BottomLeft = bottomLeft;
    }

    public static OcrQuad FromRect(OcrRect rect)
    {
        return new OcrQuad(
            new OcrPoint(rect.Left, rect.Top),
            new OcrPoint(rect.Right, rect.Top),
            new OcrPoint(rect.Right, rect.Bottom),
            new OcrPoint(rect.Left, rect.Bottom)
        );
    }

    public OcrRect GetBoundingRect()
    {
        float minX = Math.Min(Math.Min(TopLeft.X, TopRight.X), Math.Min(BottomRight.X, BottomLeft.X));
        float maxX = Math.Max(Math.Max(TopLeft.X, TopRight.X), Math.Max(BottomRight.X, BottomLeft.X));
        float minY = Math.Min(Math.Min(TopLeft.Y, TopRight.Y), Math.Min(BottomRight.Y, BottomLeft.Y));
        float maxY = Math.Max(Math.Max(TopLeft.Y, TopRight.Y), Math.Max(BottomRight.Y, BottomLeft.Y));

        return new OcrRect(minX, minY, maxX - minX, maxY - minY);
    }

    public bool Equals(OcrQuad other) =>
        TopLeft == other.TopLeft &&
        TopRight == other.TopRight &&
        BottomRight == other.BottomRight &&
        BottomLeft == other.BottomLeft;

    public override bool Equals(object? obj) =>
        obj is OcrQuad other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = TopLeft.GetHashCode();
            hash = (hash * 397) ^ TopRight.GetHashCode();
            hash = (hash * 397) ^ BottomRight.GetHashCode();
            hash = (hash * 397) ^ BottomLeft.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(OcrQuad left, OcrQuad right) => left.Equals(right);
    public static bool operator !=(OcrQuad left, OcrQuad right) => !left.Equals(right);

    public override string ToString() => $"Quad[{TopLeft}, {TopRight}, {BottomRight}, {BottomLeft}]";
}
