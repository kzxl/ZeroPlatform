using System;
using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Scene
{
    /// <summary>
    /// Immutable 2D point with float precision for scene graph and routing calculations.
    /// Platform-independent primitive without dependencies on System.Drawing or System.Windows.
    /// </summary>
    public readonly struct ScenePoint : IEquatable<ScenePoint>
    {
        public readonly float X;
        public readonly float Y;

        public static readonly ScenePoint Zero = new ScenePoint(0f, 0f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ScenePoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float DistanceTo(in ScenePoint other)
        {
            float dx = X - other.X;
            float dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ManhattanDistanceTo(in ScenePoint other)
        {
            return Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        }

        public bool Equals(ScenePoint other) =>
            Math.Abs(X - other.X) < 1e-5f && Math.Abs(Y - other.Y) < 1e-5f;

        public override bool Equals(object? obj) => obj is ScenePoint other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X.GetHashCode() * 397) ^ Y.GetHashCode();
            }
        }

        public static bool operator ==(ScenePoint left, ScenePoint right) => left.Equals(right);
        public static bool operator !=(ScenePoint left, ScenePoint right) => !left.Equals(right);

        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }
}
