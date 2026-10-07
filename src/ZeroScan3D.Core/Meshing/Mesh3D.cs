using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ZeroGeometry.Core.Spatial;
using ZeroScan3D.Core.Camera;

namespace ZeroScan3D.Core.Meshing
{
    /// <summary>
    /// Represents an indexed 3D triangle mesh with vertices, normals, and colors.
    /// </summary>
    public sealed class Mesh3D
    {
        private readonly List<Point3D> _vertices;
        private readonly List<int> _indices;
        private readonly List<Point3D>? _normals;
        private readonly List<RgbColor>? _colors;

        public IReadOnlyList<Point3D> Vertices => _vertices;
        public IReadOnlyList<int> Indices => _indices;
        public IReadOnlyList<Point3D>? Normals => _normals;
        public IReadOnlyList<RgbColor>? Colors => _colors;

        public int VertexCount => _vertices.Count;
        public int TriangleCount => _indices.Count / 3;

        public Mesh3D(
            List<Point3D> vertices,
            List<int> indices,
            List<Point3D>? normals = null,
            List<RgbColor>? colors = null)
        {
            _vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            _indices = indices ?? throw new ArgumentNullException(nameof(indices));
            _normals = normals;
            _colors = colors;
        }

        public Mesh3D()
        {
            _vertices = new List<Point3D>();
            _indices = new List<int>();
            _normals = new List<Point3D>();
            _colors = new List<RgbColor>();
        }

        /// <summary>
        /// Exports the mesh to a standard Wavefront (.obj) file format.
        /// </summary>
        public void ExportObj(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var sb = new StringBuilder();
            sb.AppendLine("# ZeroScan3D Wavefront OBJ Exporter");
            sb.AppendLine($"# Vertices: {_vertices.Count}, Triangles: {TriangleCount}");

            var culture = CultureInfo.InvariantCulture;

            // Vertices (with optional RGB vertex color)
            for (int i = 0; i < _vertices.Count; i++)
            {
                var v = _vertices[i];
                if (_colors != null && i < _colors.Count)
                {
                    var c = _colors[i];
                    sb.AppendLine(string.Format(culture, "v {0:F6} {1:F6} {2:F6} {3:F4} {4:F4} {5:F4}",
                        v.X, v.Y, v.Z, c.R / 255.0, c.G / 255.0, c.B / 255.0));
                }
                else
                {
                    sb.AppendLine(string.Format(culture, "v {0:F6} {1:F6} {2:F6}", v.X, v.Y, v.Z));
                }
            }

            // Normals
            bool hasNormals = _normals != null && _normals.Count == _vertices.Count;
            if (hasNormals)
            {
                for (int i = 0; i < _normals!.Count; i++)
                {
                    var n = _normals[i];
                    sb.AppendLine(string.Format(culture, "vn {0:F6} {1:F6} {2:F6}", n.X, n.Y, n.Z));
                }
            }

            // Faces (1-indexed in OBJ format)
            for (int i = 0; i < _indices.Count; i += 3)
            {
                int i0 = _indices[i] + 1;
                int i1 = _indices[i + 1] + 1;
                int i2 = _indices[i + 2] + 1;

                if (hasNormals)
                {
                    sb.AppendLine($"f {i0}//{i0} {i1}//{i1} {i2}//{i2}");
                }
                else
                {
                    sb.AppendLine($"f {i0} {i1} {i2}");
                }
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Exports the mesh to a standard Stanford (.ply) ASCII file format.
        /// </summary>
        public void ExportPly(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));

            var sb = new StringBuilder();
            sb.AppendLine("ply");
            sb.AppendLine("format ascii 1.0");
            sb.AppendLine("comment ZeroScan3D PLY Exporter");
            sb.AppendLine($"element vertex {_vertices.Count}");
            sb.AppendLine("property float x");
            sb.AppendLine("property float y");
            sb.AppendLine("property float z");

            bool hasNormals = _normals != null && _normals.Count == _vertices.Count;
            if (hasNormals)
            {
                sb.AppendLine("property float nx");
                sb.AppendLine("property float ny");
                sb.AppendLine("property float nz");
            }

            bool hasColors = _colors != null && _colors.Count == _vertices.Count;
            if (hasColors)
            {
                sb.AppendLine("property uchar red");
                sb.AppendLine("property uchar green");
                sb.AppendLine("property uchar blue");
            }

            sb.AppendLine($"element face {TriangleCount}");
            sb.AppendLine("property list uchar int vertex_indices");
            sb.AppendLine("end_header");

            var culture = CultureInfo.InvariantCulture;

            for (int i = 0; i < _vertices.Count; i++)
            {
                var v = _vertices[i];
                sb.Append(string.Format(culture, "{0:F6} {1:F6} {2:F6}", v.X, v.Y, v.Z));

                if (hasNormals)
                {
                    var n = _normals![i];
                    sb.Append(string.Format(culture, " {0:F6} {1:F6} {2:F6}", n.X, n.Y, n.Z));
                }

                if (hasColors)
                {
                    var c = _colors![i];
                    sb.Append($" {c.R} {c.G} {c.B}");
                }

                sb.AppendLine();
            }

            for (int i = 0; i < _indices.Count; i += 3)
            {
                sb.AppendLine($"3 {_indices[i]} {_indices[i + 1]} {_indices[i + 2]}");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}
