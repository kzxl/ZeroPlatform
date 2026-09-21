using System;
using System.Globalization;

namespace Zero3D.Math
{
    public struct Vec3
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

        public static Vec3 Zero => new Vec3(0, 0, 0);
        public static Vec3 One => new Vec3(1, 1, 1);
        public static Vec3 UnitX => new Vec3(1, 0, 0);
        public static Vec3 UnitY => new Vec3(0, 1, 0);
        public static Vec3 UnitZ => new Vec3(0, 0, 1);

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, float s) => new Vec3(a.X * s, a.Y * s, a.Z * s);
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

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t) => a + (b - a) * t;

        public override string ToString() => $"({X.ToString("F3", CultureInfo.InvariantCulture)}, {Y.ToString("F3", CultureInfo.InvariantCulture)}, {Z.ToString("F3", CultureInfo.InvariantCulture)})";
    }

    public struct Vec2
    {
        public float U;
        public float V;

        public Vec2(float u, float v)
        {
            U = u;
            V = v;
        }

        public static Vec2 Zero => new Vec2(0f, 0f);
    }

    public struct Mat4
    {
        public float M11, M12, M13, M14;
        public float M21, M22, M23, M24;
        public float M31, M32, M33, M34;
        public float M41, M42, M43, M44;

        public static Mat4 Identity => new Mat4
        {
            M11 = 1f, M22 = 1f, M33 = 1f, M44 = 1f
        };

        public static Mat4 CreateTranslation(float x, float y, float z)
        {
            var m = Identity;
            m.M41 = x;
            m.M42 = y;
            m.M43 = z;
            return m;
        }

        public static Mat4 CreateTranslation(Vec3 v) => CreateTranslation(v.X, v.Y, v.Z);

        public static Mat4 CreateScale(float x, float y, float z)
        {
            var m = Identity;
            m.M11 = x;
            m.M22 = y;
            m.M33 = z;
            return m;
        }

        public static Mat4 CreateScale(Vec3 s) => CreateScale(s.X, s.Y, s.Z);

        public static Mat4 CreateRotationX(float angleRad)
        {
            var m = Identity;
            float cos = (float)System.Math.Cos(angleRad);
            float sin = (float)System.Math.Sin(angleRad);
            m.M22 = cos;  m.M23 = sin;
            m.M32 = -sin; m.M33 = cos;
            return m;
        }

        public static Mat4 CreateRotationY(float angleRad)
        {
            var m = Identity;
            float cos = (float)System.Math.Cos(angleRad);
            float sin = (float)System.Math.Sin(angleRad);
            m.M11 = cos; m.M13 = -sin;
            m.M31 = sin; m.M33 = cos;
            return m;
        }

        public static Mat4 CreateRotationZ(float angleRad)
        {
            var m = Identity;
            float cos = (float)System.Math.Cos(angleRad);
            float sin = (float)System.Math.Sin(angleRad);
            m.M11 = cos;  m.M12 = sin;
            m.M21 = -sin; m.M22 = cos;
            return m;
        }

        public static Mat4 CreateFromQuaternion(Quat q) => CreateFromQuaternion(q.X, q.Y, q.Z, q.W);

        public static Mat4 CreateFromQuaternion(float x, float y, float z, float w)
        {
            float xx = x * x;
            float yy = y * y;
            float zz = z * z;
            float xy = x * y;
            float xz = x * z;
            float yz = y * z;
            float wx = w * x;
            float wy = w * y;
            float wz = w * z;

            var m = Identity;
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

        public static Mat4 CreateLookAt(Vec3 eye, Vec3 target, Vec3 up)
        {
            var zaxis = (eye - target).Normalize();
            var xaxis = Vec3.Cross(up, zaxis).Normalize();
            var yaxis = Vec3.Cross(zaxis, xaxis);

            var m = Identity;
            m.M11 = xaxis.X; m.M12 = yaxis.X; m.M13 = zaxis.X; m.M14 = 0f;
            m.M21 = xaxis.Y; m.M22 = yaxis.Y; m.M23 = zaxis.Y; m.M24 = 0f;
            m.M31 = xaxis.Z; m.M32 = yaxis.Z; m.M33 = zaxis.Z; m.M34 = 0f;

            m.M41 = -Vec3.Dot(xaxis, eye);
            m.M42 = -Vec3.Dot(yaxis, eye);
            m.M43 = -Vec3.Dot(zaxis, eye);
            m.M44 = 1f;
            return m;
        }

        public static Mat4 CreatePerspectiveFieldOfView(float fovYRad, float aspectRatio, float nearZ, float farZ)
        {
            if (fovYRad <= 0f || fovYRad >= System.Math.PI) throw new ArgumentOutOfRangeException(nameof(fovYRad));
            if (aspectRatio <= 0f) throw new ArgumentOutOfRangeException(nameof(aspectRatio));
            if (nearZ <= 0f || farZ <= nearZ) throw new ArgumentOutOfRangeException(nameof(nearZ));

            float tanHalfFov = (float)System.Math.Tan(fovYRad * 0.5);
            var m = new Mat4();
            m.M11 = 1f / (aspectRatio * tanHalfFov);
            m.M22 = 1f / tanHalfFov;
            m.M33 = farZ / (nearZ - farZ);
            m.M34 = -1f;
            m.M43 = (nearZ * farZ) / (nearZ - farZ);
            return m;
        }

        public static Mat4 CreateOrthographic(float width, float height, float nearZ, float farZ)
        {
            var m = new Mat4();
            m.M11 = 2f / width;
            m.M22 = 2f / height;
            m.M33 = 1f / (nearZ - farZ);
            m.M43 = nearZ / (nearZ - farZ);
            m.M44 = 1f;
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

        public Vec3 TransformPoint(Vec3 p)
        {
            float w = p.X * M14 + p.Y * M24 + p.Z * M34 + M44;
            if (System.Math.Abs(w) < 1e-7f) w = 1f;
            return new Vec3(
                (p.X * M11 + p.Y * M21 + p.Z * M31 + M41) / w,
                (p.X * M12 + p.Y * M22 + p.Z * M32 + M42) / w,
                (p.X * M13 + p.Y * M23 + p.Z * M33 + M43) / w
            );
        }

        public Vec3 TransformVector(Vec3 v)
        {
            return new Vec3(
                v.X * M11 + v.Y * M21 + v.Z * M31,
                v.X * M12 + v.Y * M22 + v.Z * M32,
                v.X * M13 + v.Y * M23 + v.Z * M33
            );
        }
        public Vec3 Translation => new Vec3(M41, M42, M43);
    }

    public struct Quat
    {
        public float X;
        public float Y;
        public float Z;
        public float W;

        public Quat(float x, float y, float z, float w)
        {
            X = x; Y = y; Z = z; W = w;
        }

        public static Quat Identity => new Quat(0f, 0f, 0f, 1f);

        public static Quat FromAxisAngle(Vec3 axis, float angleRad)
        {
            float halfAngle = angleRad * 0.5f;
            float sin = (float)System.Math.Sin(halfAngle);
            var a = axis.Normalize();
            return new Quat(a.X * sin, a.Y * sin, a.Z * sin, (float)System.Math.Cos(halfAngle));
        }

        public static Quat operator *(Quat a, Quat b)
        {
            return new Quat(
                a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
                a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
                a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
                a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z
            );
        }

        public Quat Normalize()
        {
            float len = (float)System.Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
            return len > 1e-6f ? new Quat(X / len, Y / len, Z / len, W / len) : Identity;
        }
    }

    public struct Ray3D
    {
        public Vec3 Origin;
        public Vec3 Direction;

        public Ray3D(Vec3 origin, Vec3 direction)
        {
            Origin = origin;
            Direction = direction.Normalize();
        }

        public Vec3 GetPoint(float distance) => Origin + Direction * distance;
    }

    public struct Plane3D
    {
        public Vec3 Normal;
        public float D;

        public Plane3D(Vec3 normal, float d)
        {
            Normal = normal;
            D = d;
        }

        public Plane3D(float a, float b, float c, float d)
        {
            Normal = new Vec3(a, b, c);
            D = d;
        }

        public Plane3D Normalize()
        {
            float len = Normal.Length();
            if (len > 1e-6f)
            {
                return new Plane3D(Normal / len, D / len);
            }
            return this;
        }

        public float DistanceToPoint(Vec3 p) => Vec3.Dot(Normal, p) + D;
    }

    public struct Aabb3D
    {
        public Vec3 Min;
        public Vec3 Max;

        public Aabb3D(Vec3 min, Vec3 max)
        {
            Min = min;
            Max = max;
        }

        public Vec3 Center => (Min + Max) * 0.5f;
        public Vec3 Size => Max - Min;

        public static Aabb3D Empty => new Aabb3D(
            new Vec3(float.MaxValue, float.MaxValue, float.MaxValue),
            new Vec3(float.MinValue, float.MinValue, float.MinValue)
        );

        public void Encapsulate(Vec3 p)
        {
            Min.X = System.Math.Min(Min.X, p.X);
            Min.Y = System.Math.Min(Min.Y, p.Y);
            Min.Z = System.Math.Min(Min.Z, p.Z);

            Max.X = System.Math.Max(Max.X, p.X);
            Max.Y = System.Math.Max(Max.Y, p.Y);
            Max.Z = System.Math.Max(Max.Z, p.Z);
        }

        public bool Intersects(Aabb3D other)
        {
            return (Min.X <= other.Max.X && Max.X >= other.Min.X) &&
                   (Min.Y <= other.Max.Y && Max.Y >= other.Min.Y) &&
                   (Min.Z <= other.Max.Z && Max.Z >= other.Min.Z);
        }

        public bool Contains(Vec3 point)
        {
            return (point.X >= Min.X && point.X <= Max.X) &&
                   (point.Y >= Min.Y && point.Y <= Max.Y) &&
                   (point.Z >= Min.Z && point.Z <= Max.Z);
        }

        public bool Intersects(Ray3D ray, out float distance)
        {
            distance = 0f;
            float tmin = (Min.X - ray.Origin.X) / (System.Math.Abs(ray.Direction.X) > 1e-7f ? ray.Direction.X : 1e-7f);
            float tmax = (Max.X - ray.Origin.X) / (System.Math.Abs(ray.Direction.X) > 1e-7f ? ray.Direction.X : 1e-7f);
            if (tmin > tmax) { float temp = tmin; tmin = tmax; tmax = temp; }

            float tymin = (Min.Y - ray.Origin.Y) / (System.Math.Abs(ray.Direction.Y) > 1e-7f ? ray.Direction.Y : 1e-7f);
            float tymax = (Max.Y - ray.Origin.Y) / (System.Math.Abs(ray.Direction.Y) > 1e-7f ? ray.Direction.Y : 1e-7f);
            if (tymin > tymax) { float temp = tymin; tymin = tymax; tymax = temp; }

            if ((tmin > tymax) || (tymin > tmax)) return false;
            if (tymin > tmin) tmin = tymin;
            if (tymax < tmax) tmax = tymax;

            float tzmin = (Min.Z - ray.Origin.Z) / (System.Math.Abs(ray.Direction.Z) > 1e-7f ? ray.Direction.Z : 1e-7f);
            float tzmax = (Max.Z - ray.Origin.Z) / (System.Math.Abs(ray.Direction.Z) > 1e-7f ? ray.Direction.Z : 1e-7f);
            if (tzmin > tzmax) { float temp = tzmin; tzmin = tzmax; tzmax = temp; }

            if ((tmin > tzmax) || (tzmin > tmax)) return false;
            if (tzmin > tmin) tmin = tzmin;
            if (tzmax < tmax) tmax = tzmax;

            if (tmax < 0f) return false;
            distance = tmin > 0f ? tmin : tmax;
            return true;
        }
    }

    /// <summary>
    /// Type alias for Aabb3D to maintain backward compatibility.
    /// </summary>
    public struct AABB
    {
        private Aabb3D _inner;

        public Vec3 Min { get => _inner.Min; set => _inner.Min = value; }
        public Vec3 Max { get => _inner.Max; set => _inner.Max = value; }
        public Vec3 Center => _inner.Center;
        public Vec3 Size => _inner.Size;

        public AABB(Vec3 min, Vec3 max) => _inner = new Aabb3D(min, max);
        public static AABB Empty => new AABB(Aabb3D.Empty.Min, Aabb3D.Empty.Max);

        public void Encapsulate(Vec3 p) => _inner.Encapsulate(p);
        public bool Intersects(AABB other) => _inner.Intersects(new Aabb3D(other.Min, other.Max));
        public bool Contains(Vec3 point) => _inner.Contains(point);
        public bool Intersects(Ray3D ray, out float distance) => _inner.Intersects(ray, out distance);

        public static implicit operator Aabb3D(AABB aabb) => aabb._inner;
        public static implicit operator AABB(Aabb3D aabb) => new AABB(aabb.Min, aabb.Max);
    }
}
