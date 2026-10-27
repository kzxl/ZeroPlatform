using System;

namespace ZeroOcr.Core.Models;

/// <summary>
/// Represents an axis-aligned bounding rectangle for OCR tokens.
/// </summary>
public readonly struct OcrRect : IEquatable<OcrRect>
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
    public float Area => Width * Height;
    public bool IsEmpty => Width <= 0f || Height <= 0f;

    public static OcrRect Empty => new(0f, 0f, 0f, 0f);

    public OcrRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = Math.Max(0f, width);
        Height = Math.Max(0f, height);
    }

    public bool Contains(float px, float py) =>
        px >= Left && px <= Right && py >= Top && py <= Bottom;

    public bool Contains(OcrPoint point) => Contains(point.X, point.Y);

    public bool Contains(OcrRect other) =>
        Left <= other.Left && Right >= other.Right &&
        Top <= other.Top && Bottom >= other.Bottom;

    public bool IntersectsWith(OcrRect other) =>
        !(Left > other.Right || Right < other.Left ||
          Top > other.Bottom || Bottom < other.Top);

    public OcrRect Union(OcrRect other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;

        float minX = Math.Min(Left, other.Left);
        float minY = Math.Min(Top, other.Top);
        float maxX = Math.Max(Right, other.Right);
        float maxY = Math.Max(Bottom, other.Bottom);

        return new OcrRect(minX, minY, maxX - minX, maxY - minY);
    }

    public bool Equals(OcrRect other) =>
        Math.Abs(X - other.X) < 1e-4f &&
        Math.Abs(Y - other.Y) < 1e-4f &&
        Math.Abs(Width - other.Width) < 1e-4f &&
        Math.Abs(Height - other.Height) < 1e-4f;

    public override bool Equals(object? obj) =>
        obj is OcrRect other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = X.GetHashCode();
            hash = (hash * 397) ^ Y.GetHashCode();
            hash = (hash * 397) ^ Width.GetHashCode();
            hash = (hash * 397) ^ Height.GetHashCode();
            return hash;
        }
    }

    public static bool operator ==(OcrRect left, OcrRect right) => left.Equals(right);
    public static bool operator !=(OcrRect left, OcrRect right) => !left.Equals(right);

    public override string ToString() => $"[X={X:F1}, Y={Y:F1}, W={Width:F1}, H={Height:F1}]";
}
