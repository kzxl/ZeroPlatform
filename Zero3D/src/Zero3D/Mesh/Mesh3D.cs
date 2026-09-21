using System;
using System.Collections.Generic;
using Zero3D.Math;
using Zero3D.Materials;

namespace Zero3D.Mesh
{
    /// <summary>
    /// Lightweight, high-performance 3D triangle mesh representation with submesh support,
    /// bounding box computation, vertex normal calculation, and spatial transformation.
    /// </summary>
    public class Mesh3D
    {
        public string Name { get; set; } = "Mesh";
        public List<Vertex3D> Vertices { get; } = new List<Vertex3D>();
        public List<int> Indices { get; } = new List<int>();
        public List<SubMesh> SubMeshes { get; } = new List<SubMesh>();
        public Aabb3D BoundingBox { get; private set; }

        public int TriangleCount => Indices.Count > 0 ? Indices.Count / 3 : Vertices.Count / 3;

        /// <summary>
        /// Recalculates the Axis-Aligned Bounding Box (AABB) enclosing all vertices.
        /// </summary>
        public void ComputeBounds()
        {
            if (Vertices.Count == 0)
            {
                BoundingBox = default;
                return;
            }

            var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);

            for (int i = 0; i < Vertices.Count; i++)
            {
                var p = Vertices[i].Position;
                if (p.X < min.X) min.X = p.X;
                if (p.Y < min.Y) min.Y = p.Y;
                if (p.Z < min.Z) min.Z = p.Z;

                if (p.X > max.X) max.X = p.X;
                if (p.Y > max.Y) max.Y = p.Y;
                if (p.Z > max.Z) max.Z = p.Z;
            }

            BoundingBox = new Aabb3D(min, max);
        }

        /// <summary>
        /// Computes smooth averaged surface normals based on triangle geometry.
        /// </summary>
        public void ComputeNormals()
        {
            if (Vertices.Count == 0) return;

            var accumulatedNormals = new Vec3[Vertices.Count];

            if (Indices.Count >= 3)
            {
                for (int i = 0; i < Indices.Count; i += 3)
                {
                    int i0 = Indices[i];
                    int i1 = Indices[i + 1];
                    int i2 = Indices[i + 2];

                    if (i0 < 0 || i0 >= Vertices.Count ||
                        i1 < 0 || i1 >= Vertices.Count ||
                        i2 < 0 || i2 >= Vertices.Count)
                        continue;

                    var p0 = Vertices[i0].Position;
                    var p1 = Vertices[i1].Position;
                    var p2 = Vertices[i2].Position;

                    var faceNormal = Vec3.Cross(p1 - p0, p2 - p0);
                    accumulatedNormals[i0] = accumulatedNormals[i0] + faceNormal;
                    accumulatedNormals[i1] = accumulatedNormals[i1] + faceNormal;
                    accumulatedNormals[i2] = accumulatedNormals[i2] + faceNormal;
                }
            }
            else
            {
                for (int i = 0; i < Vertices.Count; i += 3)
                {
                    if (i + 2 >= Vertices.Count) break;
                    var p0 = Vertices[i].Position;
                    var p1 = Vertices[i + 1];
                    var p2 = Vertices[i + 2];

                    var faceNormal = Vec3.Cross(p1.Position - p0, p2.Position - p0).Normalize();
                    accumulatedNormals[i] = faceNormal;
                    accumulatedNormals[i + 1] = faceNormal;
                    accumulatedNormals[i + 2] = faceNormal;
                }
            }

            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                v.Normal = accumulatedNormals[i].Normalize();
                Vertices[i] = v;
            }
        }

        /// <summary>
        /// Transforms all vertex positions and normals in place by a 4x4 matrix.
        /// </summary>
        public void Transform(Mat4 matrix)
        {
            for (int i = 0; i < Vertices.Count; i++)
            {
                var v = Vertices[i];
                v.Position = matrix.TransformPoint(v.Position);
                v.Normal = matrix.TransformVector(v.Normal).Normalize();
                Vertices[i] = v;
            }
            ComputeBounds();
        }

        /// <summary>
        /// Creates a deep copy clone of this mesh.
        /// </summary>
        public Mesh3D Clone()
        {
            var clone = new Mesh3D
            {
                Name = this.Name + "_Clone"
            };
            clone.Vertices.AddRange(this.Vertices);
            clone.Indices.AddRange(this.Indices);
            foreach (var sub in this.SubMeshes)
            {
                clone.SubMeshes.Add(new SubMesh(sub.Name, sub.IndexStart, sub.IndexCount, sub.MaterialIndex));
            }
            clone.BoundingBox = this.BoundingBox;
            return clone;
        }

        /// <summary>
        /// Parses a Wavefront OBJ ASCII string into a Mesh3D.
        /// </summary>
        public static Mesh3D ParseObj(string objText) => IO.ObjLoader.LoadMesh(objText);

        /// <summary>
        /// Parses an STL binary byte stream into a Mesh3D.
        /// </summary>
        public static Mesh3D ParseBinaryStl(byte[] stlBytes) => IO.StlLoader.LoadBinary(stlBytes);
    }
}
