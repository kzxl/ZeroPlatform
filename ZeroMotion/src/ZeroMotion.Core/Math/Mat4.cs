using System;

namespace ZeroMotion.Math
{
    /// <summary>
    /// Represents a 4x4 row-major transformation matrix for spatial kinematics and coordinate frames.
    /// </summary>
    public struct Mat4 : IEquatable<Mat4>
    {
        public float M11, M12, M13, M14;
        public float M21, M22, M23, M24;
        public float M31, M32, M33, M34;
        public float M41, M42, M43, M44;

        public static Mat4 Identity => new Mat4
        {
            M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f
        };

        public Vec3 Translation
        {
            get => new Vec3(M41, M42, M43);
            set
            {
                M41 = value.X;
                M42 = value.Y;
                M43 = value.Z;
            }
        }

        public static Mat4 CreateTranslation(float x, float y, float z)
        {
            var m = Identity;
            m.M41 = x;
            m.M42 = y;
            m.M43 = z;
            return m;
        }

        public static Mat4 CreateTranslation(Vec3 v) => CreateTranslation(v.X, v.Y, v.Z);

        public static Mat4 CreateRotationX(float radians)
        {
            var m = Identity;
            float cos = (float)System.Math.Cos(radians);
            float sin = (float)System.Math.Sin(radians);
            m.M22 = cos;
            m.M23 = sin;
            m.M32 = -sin;
            m.M33 = cos;
            return m;
        }

        public static Mat4 CreateRotationY(float radians)
        {
            var m = Identity;
            float cos = (float)System.Math.Cos(radians);
            float sin = (float)System.Math.Sin(radians);
            m.M11 = cos;
            m.M13 = -sin;
            m.M31 = sin;
            m.M33 = cos;
            return m;
        }

        public static Mat4 CreateRotationZ(float radians)
        {
            var m = Identity;
            float cos = (float)System.Math.Cos(radians);
            float sin = (float)System.Math.Sin(radians);
            m.M11 = cos;
            m.M12 = sin;
            m.M21 = -sin;
            m.M22 = cos;
            return m;
        }

        public static Mat4 operator *(Mat4 a, Mat4 b)
        {
            return new Mat4
            {
                M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
                M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
                M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
                M14 = a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,

                M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
                M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
                M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
                M24 = a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,

                M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
                M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
                M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
                M34 = a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,

                M41 = a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
                M42 = a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
                M43 = a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
                M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44,
            };
        }

        public Vec3 TransformPoint(Vec3 point)
        {
            return new Vec3(
                point.X * M11 + point.Y * M21 + point.Z * M31 + M41,
                point.X * M12 + point.Y * M22 + point.Z * M32 + M42,
                point.X * M13 + point.Y * M23 + point.Z * M33 + M43
            );
        }

        public Vec3 TransformVector(Vec3 vector)
        {
            return new Vec3(
                vector.X * M11 + vector.Y * M21 + vector.Z * M31,
                vector.X * M12 + vector.Y * M22 + vector.Z * M32,
                vector.X * M13 + vector.Y * M23 + vector.Z * M33
            );
        }

        /// <summary>
        /// Inverts an affine rigid transformation matrix (rotation R + translation T: inv(T) * inv(R) = [R^T, -R^T * T]).
        /// </summary>
        public Mat4 InvertRigid()
        {
            // Transpose rotation 3x3
            var res = Identity;
            res.M11 = M11; res.M12 = M21; res.M13 = M31;
            res.M21 = M12; res.M22 = M22; res.M23 = M32;
            res.M31 = M13; res.M32 = M23; res.M33 = M33;

            // -R^T * T
            res.M41 = -(M41 * res.M11 + M42 * res.M21 + M43 * res.M31);
            res.M42 = -(M41 * res.M12 + M42 * res.M22 + M43 * res.M32);
            res.M43 = -(M41 * res.M13 + M42 * res.M23 + M43 * res.M33);
            return res;
        }

        public bool Equals(Mat4 other) =>
            System.Math.Abs(M11 - other.M11) < 1e-6f &&
            System.Math.Abs(M22 - other.M22) < 1e-6f &&
            System.Math.Abs(M33 - other.M33) < 1e-6f &&
            System.Math.Abs(M44 - other.M44) < 1e-6f &&
            System.Math.Abs(M41 - other.M41) < 1e-6f &&
            System.Math.Abs(M42 - other.M42) < 1e-6f &&
            System.Math.Abs(M43 - other.M43) < 1e-6f;

        public override bool Equals(object? obj) => obj is Mat4 other && Equals(other);
        public override int GetHashCode() => M11.GetHashCode() ^ M22.GetHashCode() ^ M41.GetHashCode();
        public static bool operator ==(Mat4 a, Mat4 b) => a.Equals(b);
        public static bool operator !=(Mat4 a, Mat4 b) => !a.Equals(b);
    }
}
