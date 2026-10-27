using System;

namespace ZeroOcr.Core.Models;

/// <summary>
/// Represents a 2D coordinate point with floating point precision.
/// </summary>
public readonly struct OcrPoint : IEquatable<OcrPoint>
{
    public float X { get; }
    public float Y { get; }

    public static OcrPoint Zero => new(0f, 0f);

    public OcrPoint(float x, float y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(OcrPoint other) =>
        Math.Abs(X - other.X) < 1e-4f && Math.Abs(Y - other.Y) < 1e-4f;

    public override bool Equals(object? obj) =>
        obj is OcrPoint other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            return (X.GetHashCode() * 397) ^ Y.GetHashCode();
        }
    }

    public static bool operator ==(OcrPoint left, OcrPoint right) => left.Equals(right);
    public static bool operator !=(OcrPoint left, OcrPoint right) => !left.Equals(right);

    public override string ToString() => $"({X:F1}, {Y:F1})";
}
