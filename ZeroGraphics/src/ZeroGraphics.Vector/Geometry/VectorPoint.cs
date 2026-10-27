using System;

namespace ZeroGraphics.Vector.Geometry
{
    /// <summary>
    /// Pure, decoupled 2D point representation for high-performance vector graphics.
    /// </summary>
    public readonly struct VectorPoint : IEquatable<VectorPoint>
    {
        public float X { get; }
        public float Y { get; }

        public static VectorPoint Zero => new VectorPoint(0.0f, 0.0f);

        public VectorPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float LengthSquared => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(X * X + Y * Y);

        public VectorPoint Normalized()
        {
            float len = Length;
            if (len > 1e-6f)
            {
                return new VectorPoint(X / len, Y / len);
            }
            return Zero;
        }

        public static VectorPoint operator +(VectorPoint a, VectorPoint b) => new VectorPoint(a.X + b.X, a.Y + b.Y);
        public static VectorPoint operator -(VectorPoint a, VectorPoint b) => new VectorPoint(a.X - b.X, a.Y - b.Y);
        public static VectorPoint operator *(VectorPoint a, float scalar) => new VectorPoint(a.X * scalar, a.Y * scalar);
        public static VectorPoint operator *(float scalar, VectorPoint a) => new VectorPoint(a.X * scalar, a.Y * scalar);
        public static VectorPoint operator /(VectorPoint a, float scalar) => new VectorPoint(a.X / scalar, a.Y / scalar);

        public static float Dot(VectorPoint a, VectorPoint b) => a.X * b.X + a.Y * b.Y;
        public static float Cross(VectorPoint a, VectorPoint b) => a.X * b.Y - a.Y * b.X;

        public bool Equals(VectorPoint other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is VectorPoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(VectorPoint left, VectorPoint right) => left.Equals(right);
        public static bool operator !=(VectorPoint left, VectorPoint right) => !left.Equals(right);

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}
