using System;
using ZeroGeometry.Core.Spatial;

namespace ZeroScan3D.Core.Camera
{
    /// <summary>
    /// Represents a 3D point with an associated RGB color.
    /// </summary>
    public readonly struct ColoredPoint3D : IEquatable<ColoredPoint3D>
    {
        public Point3D Position { get; }
        public RgbColor Color { get; }

        public ColoredPoint3D(Point3D position, RgbColor color)
        {
            Position = position;
            Color = color;
        }

        public ColoredPoint3D(double x, double y, double z, byte r, byte g, byte b)
        {
            Position = new Point3D(x, y, z);
            Color = new RgbColor(r, g, b);
        }

        public bool Equals(ColoredPoint3D other) => Position.Equals(other.Position) && Color.Equals(other.Color);
        public override bool Equals(object? obj) => obj is ColoredPoint3D other && Equals(other);
        public override int GetHashCode() => unchecked((Position.GetHashCode() * 397) ^ Color.GetHashCode());
        public override string ToString() => $"{Position} {Color}";
    }
}
