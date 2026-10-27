using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ZeroTwin3D.Engine
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
        public float Length() => (float)Math.Sqrt(LengthSquared());

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

        public static implicit operator Zero3D.Math.Vec3(Vec3 v) => new Zero3D.Math.Vec3(v.X, v.Y, v.Z);
        public static implicit operator Vec3(Zero3D.Math.Vec3 v) => new Vec3(v.X, v.Y, v.Z);

        public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
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

        public static implicit operator Zero3D.Math.Vec2(Vec2 v) => new Zero3D.Math.Vec2(v.U, v.V);
        public static implicit operator Vec2(Zero3D.Math.Vec2 v) => new Vec2(v.U, v.V);
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

        public static Mat4 CreateRotationX(float angleRad)
        {
            var m = Identity;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            m.M22 = cos;  m.M23 = sin;
            m.M32 = -sin; m.M33 = cos;
            return m;
        }

        public static Mat4 CreateRotationY(float angleRad)
        {
            var m = Identity;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            m.M11 = cos; m.M13 = -sin;
            m.M31 = sin; m.M33 = cos;
            return m;
        }

        public static Mat4 CreateRotationZ(float angleRad)
        {
            var m = Identity;
            float cos = (float)Math.Cos(angleRad);
            float sin = (float)Math.Sin(angleRad);
            m.M11 = cos;  m.M12 = sin;
            m.M21 = -sin; m.M22 = cos;
            return m;
        }

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
            if (fovYRad <= 0f || fovYRad >= Math.PI) throw new ArgumentOutOfRangeException(nameof(fovYRad));
            if (aspectRatio <= 0f) throw new ArgumentOutOfRangeException(nameof(aspectRatio));
            if (nearZ <= 0f || farZ <= nearZ) throw new ArgumentOutOfRangeException(nameof(nearZ));

            float tanHalfFov = (float)Math.Tan(fovYRad * 0.5);
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
                M44 = a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44
            };
        }

        public Vec3 TransformPoint(Vec3 v)
        {
            return new Vec3(
                v.X * M11 + v.Y * M21 + v.Z * M31 + M41,
                v.X * M12 + v.Y * M22 + v.Z * M32 + M42,
                v.X * M13 + v.Y * M23 + v.Z * M33 + M43
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

        public static implicit operator Zero3D.Math.Mat4(Mat4 m) => new Zero3D.Math.Mat4
        {
            M11 = m.M11, M12 = m.M12, M13 = m.M13, M14 = m.M14,
            M21 = m.M21, M22 = m.M22, M23 = m.M23, M24 = m.M24,
            M31 = m.M31, M32 = m.M32, M33 = m.M33, M34 = m.M34,
            M41 = m.M41, M42 = m.M42, M43 = m.M43, M44 = m.M44
        };

        public static implicit operator Mat4(Zero3D.Math.Mat4 m) => new Mat4
        {
            M11 = m.M11, M12 = m.M12, M13 = m.M13, M14 = m.M14,
            M21 = m.M21, M22 = m.M22, M23 = m.M23, M24 = m.M24,
            M31 = m.M31, M32 = m.M32, M33 = m.M33, M34 = m.M34,
            M41 = m.M41, M42 = m.M42, M43 = m.M43, M44 = m.M44
        };
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

        public static implicit operator Zero3D.Math.Plane3D(Plane3D p) => new Zero3D.Math.Plane3D(p.Normal, p.D);
        public static implicit operator Plane3D(Zero3D.Math.Plane3D p) => new Plane3D(p.Normal, p.D);
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

        public static implicit operator Zero3D.Math.Aabb3D(Aabb3D a) => new Zero3D.Math.Aabb3D(a.Min, a.Max);
        public static implicit operator Aabb3D(Zero3D.Math.Aabb3D a) => new Aabb3D(a.Min, a.Max);
    }

    public struct Vertex3D
    {
        public Vec3 Position;
        public Vec3 Normal;
        public Vec2 Uv;

        public Vertex3D(Vec3 pos, Vec3 normal, Vec2 uv)
        {
            Position = pos;
            Normal = normal;
            Uv = uv;
        }
    }

    /// <summary>
    /// Lightweight 3D triangle mesh structure with bounds calculation and OBJ / STL parsers.
    /// </summary>
    public class Mesh3D
    {
        public List<Vertex3D> Vertices { get; } = new List<Vertex3D>();
        public List<int> Indices { get; } = new List<int>();
        public Aabb3D BoundingBox { get; private set; }

        public void ComputeBounds()
        {
            if (Vertices.Count == 0)
            {
                BoundingBox = default;
                return;
            }

            var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);

            foreach (var v in Vertices)
            {
                if (v.Position.X < min.X) min.X = v.Position.X;
                if (v.Position.Y < min.Y) min.Y = v.Position.Y;
                if (v.Position.Z < min.Z) min.Z = v.Position.Z;

                if (v.Position.X > max.X) max.X = v.Position.X;
                if (v.Position.Y > max.Y) max.Y = v.Position.Y;
                if (v.Position.Z > max.Z) max.Z = v.Position.Z;
            }

            BoundingBox = new Aabb3D(min, max);
        }

        /// <summary>
        /// Parses a Wavefront OBJ ASCII file content into a Mesh3D by delegating to Zero3D.IO.ObjLoader.
        /// </summary>
        public static Mesh3D ParseObj(string objText)
        {
            var m = Zero3D.IO.ObjLoader.LoadMesh(objText);
            var mesh = new Mesh3D();
            for (int i = 0; i < m.Vertices.Count; i++)
            {
                var v = m.Vertices[i];
                mesh.Vertices.Add(new Vertex3D(v.Position, v.Normal, v.Uv));
            }
            mesh.Indices.AddRange(m.Indices);
            mesh.ComputeBounds();
            return mesh;
        }

        /// <summary>
        /// Parses an STL binary byte stream into a Mesh3D by delegating to Zero3D.IO.StlLoader.
        /// </summary>
        public static Mesh3D ParseBinaryStl(byte[] stlBytes)
        {
            var m = Zero3D.IO.StlLoader.LoadBinary(stlBytes);
            var mesh = new Mesh3D();
            for (int i = 0; i < m.Vertices.Count; i++)
            {
                var v = m.Vertices[i];
                mesh.Vertices.Add(new Vertex3D(v.Position, v.Normal, v.Uv));
            }
            mesh.Indices.AddRange(m.Indices);
            mesh.ComputeBounds();
            return mesh;
        }
    }
}

