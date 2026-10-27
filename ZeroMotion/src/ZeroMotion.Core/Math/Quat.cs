using System;
using System.Globalization;

namespace ZeroMotion.Math
{
    /// <summary>
    /// Represents a unit quaternion (X, Y, Z, W) for 3D orientation and gimbal-lock-free spherical interpolation.
    /// </summary>
    public struct Quat : IEquatable<Quat>
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static Quat Identity => new Quat(0f, 0f, 0f, 1f);

        public static Quat FromAxisAngle(Vec3 axis, float radians)
        {
            var normAxis = axis.Normalize();
            float halfAngle = radians * 0.5f;
            float sin = (float)System.Math.Sin(halfAngle);
            float cos = (float)System.Math.Cos(halfAngle);
            return new Quat(normAxis.X * sin, normAxis.Y * sin, normAxis.Z * sin, cos);
        }

        public static Quat FromEuler(float yawRad, float pitchRad, float rollRad)
        {
            float cy = (float)System.Math.Cos(yawRad * 0.5f);
            float sy = (float)System.Math.Sin(yawRad * 0.5f);
            float cp = (float)System.Math.Cos(pitchRad * 0.5f);
            float sp = (float)System.Math.Sin(pitchRad * 0.5f);
            float cr = (float)System.Math.Cos(rollRad * 0.5f);
            float sr = (float)System.Math.Sin(rollRad * 0.5f);

            return new Quat(
                sr * cp * cy - cr * sp * sy,
                cr * sp * cy + sr * cp * sy,
                cr * cp * sy - sr * sp * cy,
                cr * cp * cy + sr * sp * sy
            );
        }

        public static Quat Slerp(Quat q1, Quat q2, float t)
        {
            float cosHalfTheta = q1.X * q2.X + q1.Y * q2.Y + q1.Z * q2.Z + q1.W * q2.W;

            if (cosHalfTheta < 0f)
            {
                q2 = new Quat(-q2.X, -q2.Y, -q2.Z, -q2.W);
                cosHalfTheta = -cosHalfTheta;
            }

            if (cosHalfTheta >= 0.995f)
            {
                // Linear fallback to avoid division by zero
                float x = q1.X + t * (q2.X - q1.X);
                float y = q1.Y + t * (q2.Y - q1.Y);
                float z = q1.Z + t * (q2.Z - q1.Z);
                float w = q1.W + t * (q2.W - q1.W);
                float invLen = 1f / (float)System.Math.Sqrt(x * x + y * y + z * z + w * w);
                return new Quat(x * invLen, y * invLen, z * invLen, w * invLen);
            }

            float halfTheta = (float)System.Math.Acos(cosHalfTheta);
            float sinHalfTheta = (float)System.Math.Sin(halfTheta);

            float r1 = (float)System.Math.Sin((1f - t) * halfTheta) / sinHalfTheta;
            float r2 = (float)System.Math.Sin(t * halfTheta) / sinHalfTheta;

            return new Quat(
                q1.X * r1 + q2.X * r2,
                q1.Y * r1 + q2.Y * r2,
                q1.Z * r1 + q2.Z * r2,
                q1.W * r1 + q2.W * r2
            );
        }

        public Mat4 ToRotationMatrix()
        {
            float xx = X * X;
            float yy = Y * Y;
            float zz = Z * Z;
            float xy = X * Y;
            float xz = X * Z;
            float yz = Y * Z;
            float wx = W * X;
            float wy = W * Y;
            float wz = W * Z;

            var m = Mat4.Identity;
            m.M11 = 1f - 2f * (yy + zz);
            m.M12 = 2f * (xy + wz);
            m.M13 = 2f * (xz - wy);

            m.M21 = 2f * (xy - wz);
            m.M22 = 1f - 2f * (xx + zz);
            m.M23 = 2f * (yz + wx);

            m.M31 = 2f * (xz + wy);
            m.M32 = 2f * (yz - wx);
            m.M33 = 1f - 2f * (xx + yy);
            return m;
        }

        public bool Equals(Quat other) =>
            System.Math.Abs(X - other.X) < 1e-6f &&
            System.Math.Abs(Y - other.Y) < 1e-6f &&
            System.Math.Abs(Z - other.Z) < 1e-6f &&
            System.Math.Abs(W - other.W) < 1e-6f;

        public override bool Equals(object? obj) => obj is Quat other && Equals(other);
        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode() ^ W.GetHashCode();
        public static bool operator ==(Quat a, Quat b) => a.Equals(b);
        public static bool operator !=(Quat a, Quat b) => !a.Equals(b);

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3}, {3:F3})", X, Y, Z, W);
    }
}
