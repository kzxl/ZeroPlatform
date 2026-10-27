using System;

namespace ZeroGeometry.Core.Spatial
{
    public readonly struct Point2D : IEquatable<Point2D>
    {
        public double X { get; }
        public double Y { get; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public double DistanceSquaredTo(Point2D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return dx * dx + dy * dy;
        }

        public static Point2D operator +(Point2D a, Point2D b) => new Point2D(a.X + b.X, a.Y + b.Y);
        public static Point2D operator -(Point2D a, Point2D b) => new Point2D(a.X - b.X, a.Y - b.Y);
        public static Point2D operator *(Point2D a, double scalar) => new Point2D(a.X * scalar, a.Y * scalar);
        public static Point2D operator /(Point2D a, double scalar) => new Point2D(a.X / scalar, a.Y / scalar);

        public bool Equals(Point2D other) => Math.Abs(X - other.X) < 1e-12 && Math.Abs(Y - other.Y) < 1e-12;
        public override bool Equals(object? obj) => obj is Point2D other && Equals(other);
        public override int GetHashCode() => unchecked((X.GetHashCode() * 397) ^ Y.GetHashCode());
        public override string ToString() => $"({X:F3}, {Y:F3})";
    }

    public readonly struct Point3D : IEquatable<Point3D>
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public double LengthSquared => X * X + Y * Y + Z * Z;

        public Point3D Normalized()
        {
            double len = Length;
            return len > 1e-15 ? new Point3D(X / len, Y / len, Z / len) : new Point3D(0, 0, 0);
        }

        public double DistanceTo(Point3D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public double DistanceSquaredTo(Point3D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return dx * dx + dy * dy + dz * dz;
        }

        public double Dot(Point3D other) => X * other.X + Y * other.Y + Z * other.Z;

        public Point3D Cross(Point3D other)
        {
            return new Point3D(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }

        public static Point3D operator +(Point3D a, Point3D b) => new Point3D(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Point3D operator -(Point3D a, Point3D b) => new Point3D(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Point3D operator *(Point3D a, double scalar) => new Point3D(a.X * scalar, a.Y * scalar, a.Z * scalar);
        public static Point3D operator /(Point3D a, double scalar) => new Point3D(a.X / scalar, a.Y / scalar, a.Z / scalar);

        public bool Equals(Point3D other) => Math.Abs(X - other.X) < 1e-12 && Math.Abs(Y - other.Y) < 1e-12 && Math.Abs(Z - other.Z) < 1e-12;
        public override bool Equals(object? obj) => obj is Point3D other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
    }
}
