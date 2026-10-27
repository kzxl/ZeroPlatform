using System;
using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Immutable 2D Axis-Aligned Bounding Box (AABB) with float precision.
    /// Platform-independent spatial primitive for scene nodes, viewport culling, and hit testing.
    /// </summary>
    public readonly struct SceneRect : IEquatable<SceneRect>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Width;
        public readonly float Height;

        public float Left => X;
        public float Top => Y;
        public float Right => X + Width;
        public float Bottom => Y + Height;

        public static readonly SceneRect Empty = new SceneRect(0f, 0f, 0f, 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SceneRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(float px, float py)
        {
            return px >= X && px <= Right && py >= Y && py <= Bottom;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IntersectsWith(in SceneRect other)
        {
            return X < other.Right && Right > other.X &&
                   Y < other.Bottom && Bottom > other.Y;
        }

        public bool Equals(SceneRect other) =>
            Math.Abs(X - other.X) < 1e-5f &&
            Math.Abs(Y - other.Y) < 1e-5f &&
            Math.Abs(Width - other.Width) < 1e-5f &&
            Math.Abs(Height - other.Height) < 1e-5f;

        public override bool Equals(object? obj) => obj is SceneRect other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + X.GetHashCode();
                hash = hash * 31 + Y.GetHashCode();
                hash = hash * 31 + Width.GetHashCode();
                hash = hash * 31 + Height.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"[X={X:F1}, Y={Y:F1}, W={Width:F1}, H={Height:F1}]";
    }
}
