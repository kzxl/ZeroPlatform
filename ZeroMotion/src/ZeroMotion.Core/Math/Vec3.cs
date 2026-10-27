using System;
using System.Globalization;

namespace ZeroMotion.Math
{
    /// <summary>
    /// Represents a 3D single-precision floating point vector for spatial robotics kinematics.
    /// </summary>
    public struct Vec3 : IEquatable<Vec3>
    {
        public float X;
        public float Y;
        public float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 Zero => new Vec3(0f, 0f, 0f);
        public static Vec3 One => new Vec3(1f, 1f, 1f);
        public static Vec3 UnitX => new Vec3(1f, 0f, 0f);
        public static Vec3 UnitY => new Vec3(0f, 1f, 0f);
        public static Vec3 UnitZ => new Vec3(0f, 0f, 1f);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator -(Vec3 a) => new Vec3(-a.X, -a.Y, -a.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator *(float s, Vec3 a) => new Vec3(a.X * s, a.Y * s, a.Z * s);
        public static Vec3 operator /(Vec3 a, float s) => new Vec3(a.X / s, a.Y / s, a.Z / s);

        public float LengthSquared() => X * X + Y * Y + Z * Z;
        public float Length() => (float)System.Math.Sqrt(LengthSquared());

        public Vec3 Normalize()
        {
            float len = Length();
            return (len > 1e-6f) ? this / len : Zero;
        }

        public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

        public static float Distance(Vec3 a, Vec3 b) => (a - b).Length();
        public static float DistanceSquared(Vec3 a, Vec3 b) => (a - b).LengthSquared();

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;

        public bool Equals(Vec3 other) =>
            System.Math.Abs(X - other.X) < 1e-6f &&
            System.Math.Abs(Y - other.Y) < 1e-6f &&
            System.Math.Abs(Z - other.Z) < 1e-6f;

        public override bool Equals(object? obj) => obj is Vec3 other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2);
        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);
        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3})", X, Y, Z);
    }
}
