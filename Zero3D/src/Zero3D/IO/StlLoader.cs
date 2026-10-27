using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Zero3D.Math;
using Zero3D.Mesh;

namespace Zero3D.IO
{
    /// <summary>
    /// Pure C# Stereolithography (STL) 3D Model Loader and Exporter.
    /// Supports both Binary STL and ASCII STL formats commonly used in 3D Printing, CAD/CAM, and Rapid Prototyping.
    /// </summary>
    public static class StlLoader
    {
        /// <summary>
        /// Loads an STL model from disk file auto-detecting binary or ASCII format.
        /// </summary>
        public static Mesh3D LoadFromFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"STL model file not found: {filePath}", filePath);

            byte[] bytes = File.ReadAllBytes(filePath);
            string name = Path.GetFileNameWithoutExtension(filePath);
            return Load(bytes, name);
        }

        /// <summary>
        /// Loads an STL model from raw byte array auto-detecting format.
        /// </summary>
        public static Mesh3D Load(byte[] bytes, string meshName = "StlModel")
        {
            if (bytes == null || bytes.Length < 15)
                throw new InvalidDataException("Invalid STL file data: payload too short.");

            // Check if ASCII or Binary
            if (IsAsciiStl(bytes))
            {
                string asciiText = Encoding.ASCII.GetString(bytes);
                return LoadAscii(asciiText, meshName);
            }

            return LoadBinary(bytes, meshName);
        }

        private static bool IsAsciiStl(byte[] bytes)
        {
            // ASCII STL must start with "solid"
            if (bytes.Length < 6) return false;
            if (bytes[0] != 's' || bytes[1] != 'o' || bytes[2] != 'l' || bytes[3] != 'i' || bytes[4] != 'd')
                return false;

            // Check if there are binary null bytes in the first 256 bytes
            int checkLen = System.Math.Min(bytes.Length, 256);
            for (int i = 5; i < checkLen; i++)
            {
                if (bytes[i] == 0) return false; // Binary files may contain "solid" in their 80-byte header
            }

            return true;
        }

        /// <summary>
        /// Parses ASCII STL string content into a Mesh3D.
        /// </summary>
        public static Mesh3D LoadAscii(string stlText, string meshName = "StlModel")
        {
            var mesh = new Mesh3D { Name = meshName };
            var triangleVertices = new List<Vec3>();
            Vec3 currentNormal = Vec3.UnitY;

            using (var reader = new StringReader(stlText))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    if (parts[0].Equals("endfacet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (triangleVertices.Count >= 3)
                        {
                            int startIdx = mesh.Vertices.Count;
                            mesh.Vertices.Add(new Vertex3D(triangleVertices[0], currentNormal, default));
                            mesh.Vertices.Add(new Vertex3D(triangleVertices[1], currentNormal, default));
                            mesh.Vertices.Add(new Vertex3D(triangleVertices[2], currentNormal, default));

                            mesh.Indices.Add(startIdx);
                            mesh.Indices.Add(startIdx + 1);
                            mesh.Indices.Add(startIdx + 2);
                        }
                    }
                    else if (parts[0].Equals("facet", StringComparison.OrdinalIgnoreCase) &&
                        parts.Length >= 5 && parts[1].Equals("normal", StringComparison.OrdinalIgnoreCase))
                    {
                        float nx = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float ny = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        float nz = float.Parse(parts[4], CultureInfo.InvariantCulture);
                        currentNormal = new Vec3(nx, ny, nz);
                        triangleVertices.Clear();
                    }
                    else if (parts[0].Equals("vertex", StringComparison.OrdinalIgnoreCase) && parts.Length >= 4)
                    {
                        float x = float.Parse(parts[1], CultureInfo.InvariantCulture);
                        float y = float.Parse(parts[2], CultureInfo.InvariantCulture);
                        float z = float.Parse(parts[3], CultureInfo.InvariantCulture);
                        triangleVertices.Add(new Vec3(x, y, z));
                    }
                }
            }

            mesh.ComputeBounds();
            return mesh;
        }

        /// <summary>
        /// Parses Binary STL byte array into a Mesh3D.
        /// </summary>
        public static Mesh3D LoadBinary(byte[] bytes, string meshName = "StlModel")
        {
            if (bytes.Length < 84)
                throw new InvalidDataException("Invalid binary STL size: header must be at least 84 bytes.");

            var mesh = new Mesh3D { Name = meshName };

            using (var ms = new MemoryStream(bytes))
            using (var reader = new BinaryReader(ms))
            {
                reader.ReadBytes(80); // 80-byte header
                uint numTriangles = reader.ReadUInt32();

                // Reserve capacity
                int expectedVertices = (int)System.Math.Min(numTriangles * 3, 500000);
                mesh.Vertices.Capacity = expectedVertices;
                mesh.Indices.Capacity = expectedVertices;

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

        /// <summary>
        /// Serializes a Mesh3D to Binary STL format.
        /// </summary>
        public static byte[] SaveToBinary(Mesh3D mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));

            int triCount = mesh.TriangleCount;
            int totalBytes = 84 + triCount * 50;

            byte[] buffer = new byte[totalBytes];
            using (var ms = new MemoryStream(buffer))
            using (var writer = new BinaryWriter(ms))
            {
                // 80-byte header
                byte[] header = Encoding.ASCII.GetBytes("Zero3D Pure C# Binary STL Export");
                writer.Write(header);
                writer.Write(new byte[80 - header.Length]);

                // Triangle count
                writer.Write((uint)triCount);

                bool hasIndices = mesh.Indices.Count >= 3;
                int count = hasIndices ? mesh.Indices.Count : mesh.Vertices.Count;

                for (int i = 0; i < count; i += 3)
                {
                    Vertex3D v1 = hasIndices ? mesh.Vertices[mesh.Indices[i]] : mesh.Vertices[i];
                    Vertex3D v2 = hasIndices ? mesh.Vertices[mesh.Indices[i + 1]] : mesh.Vertices[i + 1];
                    Vertex3D v3 = hasIndices ? mesh.Vertices[mesh.Indices[i + 2]] : mesh.Vertices[i + 2];

                    Vec3 normal = v1.Normal;
                    if (normal.LengthSquared() < 1e-4f)
                    {
                        normal = Vec3.Cross(v2.Position - v1.Position, v3.Position - v1.Position).Normalize();
                    }

                    writer.Write(normal.X);
                    writer.Write(normal.Y);
                    writer.Write(normal.Z);

                    writer.Write(v1.Position.X);
                    writer.Write(v1.Position.Y);
                    writer.Write(v1.Position.Z);

                    writer.Write(v2.Position.X);
                    writer.Write(v2.Position.Y);
                    writer.Write(v2.Position.Z);

                    writer.Write(v3.Position.X);
                    writer.Write(v3.Position.Y);
                    writer.Write(v3.Position.Z);

                    writer.Write((ushort)0); // Attribute byte count
                }
            }

            return buffer;
        }
    }
}
