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

        public Vec3 Translation => new Vec3(M41, M42, M43);
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
        /// Parses a Wavefront OBJ ASCII file content into a Mesh3D.
        /// </summary>
        public static Mesh3D ParseObj(string objText)
        {
            var mesh = new Mesh3D();
            var positions = new List<Vec3>();
            var normals = new List<Vec3>();
            var uvs = new List<Vec2>();

            using (var reader = new StringReader(objText))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;

                    string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    switch (parts[0])
                    {
                        case "v" when parts.Length >= 4:
                            positions.Add(new Vec3(
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture)
                            ));
                            break;

                        case "vn" when parts.Length >= 4:
                            normals.Add(new Vec3(
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture),
                                float.Parse(parts[3], CultureInfo.InvariantCulture)
                            ));
                            break;

                        case "vt" when parts.Length >= 3:
                            uvs.Add(new Vec2(
                                float.Parse(parts[1], CultureInfo.InvariantCulture),
                                float.Parse(parts[2], CultureInfo.InvariantCulture)
                            ));
                            break;

                        case "f" when parts.Length >= 4:
                            // Face vertices (at least 3)
                            int firstIdx = AddFaceVertex(mesh, parts[1], positions, normals, uvs);
                            int prevIdx = AddFaceVertex(mesh, parts[2], positions, normals, uvs);

                            for (int i = 3; i < parts.Length; i++)
                            {
                                int currIdx = AddFaceVertex(mesh, parts[i], positions, normals, uvs);
                                mesh.Indices.Add(firstIdx);
                                mesh.Indices.Add(prevIdx);
                                mesh.Indices.Add(currIdx);
                                prevIdx = currIdx;
                            }
                            break;
                    }
                }
            }

            mesh.ComputeBounds();
            return mesh;
        }

        private static int AddFaceVertex(Mesh3D mesh, string faceToken, List<Vec3> positions, List<Vec3> normals, List<Vec2> uvs)
        {
            string[] sub = faceToken.Split('/');
            int pIdx = int.Parse(sub[0]) - 1;
            Vec3 pos = (pIdx >= 0 && pIdx < positions.Count) ? positions[pIdx] : Vec3.Zero;

            Vec2 uv = Vec2.Zero;
            if (sub.Length > 1 && !string.IsNullOrEmpty(sub[1]))
            {
                int uvIdx = int.Parse(sub[1]) - 1;
                if (uvIdx >= 0 && uvIdx < uvs.Count) uv = uvs[uvIdx];
            }

            Vec3 norm = Vec3.UnitY;
            if (sub.Length > 2 && !string.IsNullOrEmpty(sub[2]))
            {
                int nIdx = int.Parse(sub[2]) - 1;
                if (nIdx >= 0 && nIdx < normals.Count) norm = normals[nIdx];
            }

            mesh.Vertices.Add(new Vertex3D(pos, norm, uv));
            return mesh.Vertices.Count - 1;
        }

        /// <summary>
        /// Parses an STL binary byte stream into a Mesh3D.
        /// </summary>
        public static Mesh3D ParseBinaryStl(byte[] stlBytes)
        {
            if (stlBytes.Length < 84) throw new InvalidDataException("Invalid binary STL size");

            var mesh = new Mesh3D();
            using (var ms = new MemoryStream(stlBytes))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadBytes(80); // 80-byte header
                uint numTriangles = reader.ReadUInt32();

                for (uint i = 0; i < numTriangles; i++)
                {
                    if (ms.Position + 50 > ms.Length) break;

                    float nx = reader.ReadSingle();
                    float ny = reader.ReadSingle();
                    float nz = reader.ReadSingle();
                    var normal = new Vec3(nx, ny, nz);

                    var v1 = new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    var v2 = new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    var v3 = new Vec3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());

                    reader.ReadUInt16(); // Attribute byte count

                    int idx = mesh.Vertices.Count;
                    mesh.Vertices.Add(new Vertex3D(v1, normal, default));
                    mesh.Vertices.Add(new Vertex3D(v2, normal, default));
                    mesh.Vertices.Add(new Vertex3D(v3, normal, default));

                    mesh.Indices.Add(idx);
                    mesh.Indices.Add(idx + 1);
                    mesh.Indices.Add(idx + 2);
                }
            }

            mesh.ComputeBounds();
            return mesh;
        }
    }
}

