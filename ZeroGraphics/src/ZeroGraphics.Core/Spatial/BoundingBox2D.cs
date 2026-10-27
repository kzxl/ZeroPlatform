using System;

namespace ZeroGraphics.Core.Spatial
{
    /// <summary>
    /// Represents a high-performance, 2D axis-aligned bounding box (AABB) in single-precision floating point.
    /// Used for spatial indexing, viewport culling, and layout calculations across warehouse, factory, and CAD diagrams.
    /// </summary>
    public struct BoundingBox2D : IEquatable<BoundingBox2D>
    {
        public float MinX;
        public float MinY;
        public float MaxX;
        public float MaxY;

        public float Width => MaxX - MinX;
        public float Height => MaxY - MinY;
        public float CenterX => (MinX + MaxX) * 0.5f;
        public float CenterY => (MinY + MaxY) * 0.5f;

        public bool IsEmpty => MinX >= MaxX || MinY >= MaxY;

        public static BoundingBox2D Empty => new BoundingBox2D(0, 0, 0, 0);

        public BoundingBox2D(float minX, float minY, float maxX, float maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public static BoundingBox2D FromRect(float x, float y, float width, float height)
        {
            return new BoundingBox2D(x, y, x + width, y + height);
        }

        public bool Contains(float x, float y)
        {
            return x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
        }

        public bool Contains(BoundingBox2D other)
        {
            return other.MinX >= MinX && other.MaxX <= MaxX &&
                   other.MinY >= MinY && other.MaxY <= MaxY;
        }

        public bool Intersects(BoundingBox2D other)
        {
            return MinX <= other.MaxX && MaxX >= other.MinX &&
                   MinY <= other.MaxY && MaxY >= other.MinY;
        }

        public BoundingBox2D Union(BoundingBox2D other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;

            return new BoundingBox2D(
                System.Math.Min(MinX, other.MinX),
                System.Math.Min(MinY, other.MinY),
                System.Math.Max(MaxX, other.MaxX),
                System.Math.Max(MaxY, other.MaxY)
            );
        }

        public BoundingBox2D Inflate(float dx, float dy)
        {
            return new BoundingBox2D(MinX - dx, MinY - dy, MaxX + dx, MaxY + dy);
        }

        public bool Equals(BoundingBox2D other)
        {
            return MinX.Equals(other.MinX) && MinY.Equals(other.MinY) &&
                   MaxX.Equals(other.MaxX) && MaxY.Equals(other.MaxY);
        }

        public override bool Equals(object? obj) => obj is BoundingBox2D other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = MinX.GetHashCode();
                hash = (hash * 397) ^ MinY.GetHashCode();
                hash = (hash * 397) ^ MaxX.GetHashCode();
                hash = (hash * 397) ^ MaxY.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(BoundingBox2D left, BoundingBox2D right) => left.Equals(right);
        public static bool operator !=(BoundingBox2D left, BoundingBox2D right) => !left.Equals(right);

        public override string ToString() => $"[{MinX:F2}, {MinY:F2}] -> [{MaxX:F2}, {MaxY:F2}] ({Width:F2}x{Height:F2})";
    }
}
