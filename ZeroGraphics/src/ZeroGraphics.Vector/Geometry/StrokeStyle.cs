using System;

namespace ZeroGraphics.Vector.Geometry
{
    /// <summary>
    /// Configures the stroke geometry: line width, end caps, corner joins, miter limit, and dash pattern.
    /// </summary>
    public readonly struct StrokeStyle : IEquatable<StrokeStyle>
    {
        public float Width { get; }
        public LineCap Cap { get; }
        public LineJoin Join { get; }
        public float MiterLimit { get; }
        public float[]? DashArray { get; }
        public float DashOffset { get; }

        public StrokeStyle(
            float width,
            LineCap cap = LineCap.Butt,
            LineJoin join = LineJoin.Miter,
            float miterLimit = 4.0f,
            float[]? dashArray = null,
            float dashOffset = 0.0f)
        {
            Width = width > 0.0f ? width : 1.0f;
            Cap = cap;
            Join = join;
            MiterLimit = miterLimit > 0.0f ? miterLimit : 4.0f;
            DashArray = dashArray;
            DashOffset = dashOffset;
        }

        public static StrokeStyle Default => new StrokeStyle(1.0f);

        public bool Equals(StrokeStyle other) =>
            Width.Equals(other.Width) &&
            Cap == other.Cap &&
            Join == other.Join &&
            MiterLimit.Equals(other.MiterLimit) &&
            DashOffset.Equals(other.DashOffset);

        public override bool Equals(object? obj) => obj is StrokeStyle other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Width.GetHashCode();
                hash = (hash * 397) ^ (int)Cap;
                hash = (hash * 397) ^ (int)Join;
                hash = (hash * 397) ^ MiterLimit.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(StrokeStyle left, StrokeStyle right) => left.Equals(right);
        public static bool operator !=(StrokeStyle left, StrokeStyle right) => !left.Equals(right);
    }
}
