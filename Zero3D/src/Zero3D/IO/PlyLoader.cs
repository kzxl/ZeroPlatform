using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Zero3D.Materials;
using Zero3D.Math;
using Zero3D.Mesh;
using Zero3D.Scene;

namespace Zero3D.IO
{
    /// <summary>
    /// Pure C# Stanford Polygon File Format (.PLY) loader and exporter.
    /// Supports both ASCII and Binary Little-Endian formats for 3D Meshes and dense Point Clouds.
    /// </summary>
    public static class PlyLoader
    {
        public static SceneNode LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"PLY file not found: {filePath}", filePath);

            byte[] bytes = File.ReadAllBytes(filePath);
            string name = Path.GetFileNameWithoutExtension(filePath);

            var mesh = LoadMesh(bytes, name);
            if (mesh.Vertices.Count > 0)
            {
                return new SceneNode { Name = name, Mesh = mesh };
            }

            var pc = LoadPointCloud(bytes, name);
            var node = new SceneNode { Name = name, UserData = pc };
            return node;
        }

        public static Mesh3D LoadMesh(byte[] bytes, string meshName = "PlyMesh")
        {
            var header = ParseHeader(bytes, out int bodyOffset);
            var mesh = new Mesh3D { Name = meshName };

            if (header.IsAscii)
            {
                LoadAscii(bytes, bodyOffset, header, mesh, null);
            }
            else
            {
                LoadBinary(bytes, bodyOffset, header, mesh, null);
            }

            mesh.ComputeBounds();
            return mesh;
        }

        public static PointCloud3D LoadPointCloud(byte[] bytes, string pcName = "PlyPointCloud")
        {
            var header = ParseHeader(bytes, out int bodyOffset);
            var pc = new PointCloud3D { Name = pcName };

            if (header.IsAscii)
            {
                LoadAscii(bytes, bodyOffset, header, null, pc);
            }
            else
            {
                LoadBinary(bytes, bodyOffset, header, null, pc);
            }

            pc.ComputeBounds();
            return pc;
        }

        private class PlyHeader
        {
            public bool IsAscii;
            public int VertexCount;
            public int FaceCount;
            public List<string> VertexProperties = new List<string>();
        }

        private static PlyHeader ParseHeader(byte[] bytes, out int bodyOffset)
        {
            var header = new PlyHeader();
            using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.ASCII))
            {
                string? line;
                int currentOffset = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    currentOffset += Encoding.ASCII.GetByteCount(line) + 1; // approximate
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    string[] parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    if (parts[0] == "format" && parts.Length >= 2)
                    {
                        header.IsAscii = parts[1].StartsWith("ascii", StringComparison.OrdinalIgnoreCase);
                    }
                    else if (parts[0] == "element" && parts.Length >= 3)
                    {
                        if (parts[1] == "vertex") header.VertexCount = int.Parse(parts[2]);
                        else if (parts[1] == "face") header.FaceCount = int.Parse(parts[2]);
                    }
                    else if (parts[0] == "property" && parts.Length >= 3)
                    {
                        header.VertexProperties.Add(parts[parts.Length - 1].ToLowerInvariant());
                    }
                    else if (parts[0] == "end_header")
                    {
                        break;
                    }
                }
            }

            // Find exact byte offset of "end_header\n"
            string asciiPrefix = Encoding.ASCII.GetString(bytes, 0, System.Math.Min(bytes.Length, 4096));
            int endIdx = asciiPrefix.IndexOf("end_header", StringComparison.Ordinal);
            if (endIdx < 0) throw new InvalidDataException("Invalid PLY: missing end_header");

            int newline = asciiPrefix.IndexOf('\n', endIdx);
            bodyOffset = newline >= 0 ? newline + 1 : endIdx + 11;

            return header;
        }

        private static void LoadAscii(byte[] bytes, int bodyOffset, PlyHeader header, Mesh3D? mesh, PointCloud3D? pc)
        {
            string text = Encoding.ASCII.GetString(bytes, bodyOffset, bytes.Length - bodyOffset);
            using (var reader = new StringReader(text))
            {
                int posX = header.VertexProperties.IndexOf("x");
                int posY = header.VertexProperties.IndexOf("y");
                int posZ = header.VertexProperties.IndexOf("z");
                int colR = header.VertexProperties.IndexOf("red");
                int colG = header.VertexProperties.IndexOf("green");
                int colB = header.VertexProperties.IndexOf("blue");
                int normX = header.VertexProperties.IndexOf("nx");
                int normY = header.VertexProperties.IndexOf("ny");
                int normZ = header.VertexProperties.IndexOf("nz");

                if (posX < 0) posX = 0;
                if (posY < 0) posY = 1;
                if (posZ < 0) posZ = 2;

                // Read vertices
                for (int i = 0; i < header.VertexCount; i++)
                {
                    string? line = reader.ReadLine();
                    if (line == null) break;
                    line = line.Trim();
                    if (line.Length == 0) continue;

                    string[] tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (tokens.Length <= posZ) continue;

                    float x = float.Parse(tokens[posX], CultureInfo.InvariantCulture);
                    float y = float.Parse(tokens[posY], CultureInfo.InvariantCulture);
                    float z = float.Parse(tokens[posZ], CultureInfo.InvariantCulture);
                    var pos = new Vec3(x, y, z);

                    var col = ColorRgb.White;
                    if (colR >= 0 && colR < tokens.Length &&
                        colG >= 0 && colG < tokens.Length &&
                        colB >= 0 && colB < tokens.Length)
                    {
                        float r = float.Parse(tokens[colR], CultureInfo.InvariantCulture) / 255f;
                        float g = float.Parse(tokens[colG], CultureInfo.InvariantCulture) / 255f;
                        float b = float.Parse(tokens[colB], CultureInfo.InvariantCulture) / 255f;
                        col = new ColorRgb(r, g, b);
                    }

                    var norm = Vec3.UnitY;
                    if (normX >= 0 && normZ < tokens.Length)
                    {
                        norm = new Vec3(
                            float.Parse(tokens[normX], CultureInfo.InvariantCulture),
                            float.Parse(tokens[normY], CultureInfo.InvariantCulture),
                            float.Parse(tokens[normZ], CultureInfo.InvariantCulture)
                        ).Normalize();
                    }

                    if (mesh != null)
                    {
                        mesh.Vertices.Add(new Vertex3D(pos, norm, Vec2.Zero, col));
                    }
                    if (pc != null)
                    {
                        pc.Add(new Point3D(pos, col, norm));
                    }
                }

                // Read faces
                if (mesh != null)
                {
                    for (int i = 0; i < header.FaceCount; i++)
                    {
                        string? line = reader.ReadLine();
                        if (line == null) break;
                        line = line.Trim();
                        if (line.Length == 0) continue;

                        string[] tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length < 4) continue;

                        int count = int.Parse(tokens[0]);
                        int idx0 = int.Parse(tokens[1]);
                        int prev = int.Parse(tokens[2]);

                        for (int f = 3; f <= count; f++)
                        {
                            int curr = int.Parse(tokens[f]);
                            mesh.Indices.Add(idx0);
                            mesh.Indices.Add(prev);
                            mesh.Indices.Add(curr);
                            prev = curr;
                        }
                    }
                }
            }
        }

        private static void LoadBinary(byte[] bytes, int bodyOffset, PlyHeader header, Mesh3D? mesh, PointCloud3D? pc)
        {
            using (var ms = new MemoryStream(bytes, bodyOffset, bytes.Length - bodyOffset))
            using (var reader = new BinaryReader(ms))
            {
                // Each vertex typically has 3 floats (12 bytes)
                for (int i = 0; i < header.VertexCount; i++)
                {
                    if (ms.Position + 12 > ms.Length) break;

                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    var pos = new Vec3(x, y, z);

                    if (mesh != null)
                    {
                        mesh.Vertices.Add(new Vertex3D(pos, Vec3.UnitY, Vec2.Zero));
                    }
                    if (pc != null)
                    {
                        pc.Add(new Point3D(pos));
                    }
                }

                if (mesh != null && header.FaceCount > 0)
                {
                    for (int i = 0; i < header.FaceCount; i++)
                    {
                        if (ms.Position >= ms.Length) break;
                        byte count = reader.ReadByte();
                        if (count < 3 || ms.Position + count * 4 > ms.Length) break;

                        int idx0 = reader.ReadInt32();
                        int prev = reader.ReadInt32();

                        for (int f = 2; f < count; f++)
                        {
                            int curr = reader.ReadInt32();
                            mesh.Indices.Add(idx0);
                            mesh.Indices.Add(prev);
                            mesh.Indices.Add(curr);
                            prev = curr;
                        }
                    }
                }
            }
        }
    }
}
