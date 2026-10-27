using System;
using System.Runtime.InteropServices;

namespace ZeroGraphics.Vector.Tessellation
{
    /// <summary>
    /// GPU-ready 2D vector vertex layout: 2D Position (X, Y), UV Texture Coordinates (U, V), and 32-bit Color.
    /// Packed into exactly 20 bytes for maximum cache efficiency and direct RHI VertexBuffer mapping.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VectorVertex : IEquatable<VectorVertex>
    {
        public float X;
        public float Y;
        public float U;
        public float V;
        public uint Color;

        public VectorVertex(float x, float y, float u, float v, uint color)
        {
            X = x;
            Y = y;
            U = u;
            V = v;
            Color = color;
        }

        public VectorVertex(float x, float y, uint color)
            : this(x, y, 0.0f, 0.0f, color)
        {
        }

        public bool Equals(VectorVertex other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && U.Equals(other.U) && V.Equals(other.V) && Color == other.Color;

        public override bool Equals(object? obj) => obj is VectorVertex other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ U.GetHashCode();
                hash = (hash * 397) ^ V.GetHashCode();
                hash = (hash * 397) ^ (int)Color;
                return hash;
            }
        }

        public static bool operator ==(VectorVertex left, VectorVertex right) => left.Equals(right);
        public static bool operator !=(VectorVertex left, VectorVertex right) => !left.Equals(right);
    }
}
