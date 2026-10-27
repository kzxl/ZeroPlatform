using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroTwin3D.Engine;
using ZeroTwin3D.IO;
using ZeroTwin3D.Sync;

namespace ZeroTwin3D.Tests
{
    public class ModelLoaderTests
    {
        [Fact]
        public void ObjLoader_LoadsMultiGroupAndTriangulatesQuads()
        {
            string objContent = @"# Multi-group Wavefront OBJ test
v 0.0 0.0 0.0
v 1.0 0.0 0.0
v 1.0 1.0 0.0
v 0.0 1.0 0.0
vn 0.0 0.0 1.0
vt 0.0 0.0
vt 1.0 0.0
vt 1.0 1.0
vt 0.0 1.0

g FaceFront
f 1/1/1 2/2/1 3/3/1 4/4/1

g FaceTop
v 0.0 1.0 1.0
v 1.0 1.0 1.0
f 4/1/1 3/2/1 6/3/1 5/4/1
";

            var root = ObjLoader.Load(objContent, "TestMachineModel");

            Assert.NotNull(root);
            Assert.Equal("TestMachineModel", root.Name);
            Assert.Equal(2, root.Children.Count);

            var frontGroup = root.Children[0];
            Assert.Equal("FaceFront", frontGroup.Name);
            Assert.NotNull(frontGroup.Mesh);
            // Quad triangulates to 2 triangles = 6 indices
            Assert.Equal(6, frontGroup.Mesh.Indices.Count);
            Assert.Equal(4, frontGroup.Mesh.Vertices.Count);
            Assert.Equal(0f, frontGroup.Mesh.BoundingBox.Min.X);
            Assert.Equal(1f, frontGroup.Mesh.BoundingBox.Max.X);

            var topGroup = root.Children[1];
            Assert.Equal("FaceTop", topGroup.Name);
            Assert.NotNull(topGroup.Mesh);
            Assert.Equal(6, topGroup.Mesh.Indices.Count);
        }

        [Fact]
        public void GltfLoader_LoadsSyntheticGlbBinary()
        {
            // Build a valid minimal GLB 2.0 file containing 1 triangle
            // 3 vertices: (0,0,0), (1,0,0), (0,1,0) -> 3 * 3 * 4 = 36 bytes float
            // 3 indices: 0, 1, 2 -> 3 * 2 = 6 bytes ushort (padded to 8 bytes for 4-byte alignment)

            byte[] binData = new byte[44]; // 36 + 8
            // Vertex 0: 0, 0, 0
            // Vertex 1: 1, 0, 0
            Buffer.BlockCopy(BitConverter.GetBytes(1.0f), 0, binData, 12, 4);
            // Vertex 2: 0, 1, 0
            Buffer.BlockCopy(BitConverter.GetBytes(1.0f), 0, binData, 28, 4);

            // Indices: 0, 1, 2 (ushort) at offset 36
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)0), 0, binData, 36, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)1), 0, binData, 38, 2);
            Buffer.BlockCopy(BitConverter.GetBytes((ushort)2), 0, binData, 40, 2);

            string json = @"{
  ""asset"": { ""version"": ""2.0"" },
  ""buffers"": [{ ""byteLength"": 44 }],
  ""bufferViews"": [
    { ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": 36, ""target"": 34962 },
    { ""buffer"": 0, ""byteOffset"": 36, ""byteLength"": 6, ""target"": 34963 }
  ],
  ""accessors"": [
    { ""bufferView"": 0, ""byteOffset"": 0, ""componentType"": 5126, ""count"": 3, ""type"": ""VEC3"", ""max"": [1.0, 1.0, 0.0], ""min"": [0.0, 0.0, 0.0] },
    { ""bufferView"": 1, ""byteOffset"": 0, ""componentType"": 5123, ""count"": 3, ""type"": ""SCALAR"", ""max"": [2], ""min"": [0] }
  ],
  ""meshes"": [
    {
      ""primitives"": [
        {
          ""attributes"": { ""POSITION"": 0 },
          ""indices"": 1
        }
      ]
    }
  ],
  ""nodes"": [
    {
      ""name"": ""TriangleNode"",
      ""mesh"": 0,
      ""translation"": [5.0, 10.0, 15.0]
    }
  ],
  ""scenes"": [{ ""nodes"": [0] }],
  ""scene"": 0
}";

            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            // GLB requires chunks to be 4-byte aligned
            int jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
            int jsonChunkLength = jsonBytes.Length + jsonPadding;

            int binPadding = (4 - (binData.Length % 4)) % 4;
            int binChunkLength = binData.Length + binPadding;

            int totalLength = 12 + (8 + jsonChunkLength) + (8 + binChunkLength);
            byte[] glb = new byte[totalLength];

            using (var ms = new MemoryStream(glb))
            using (var bw = new BinaryWriter(ms))
            {
                // 12-byte header
                bw.Write((uint)0x46546C67); // magic 'glTF'
                bw.Write((uint)2);          // version 2
                bw.Write((uint)totalLength);

                // Chunk 0: JSON
                bw.Write((uint)jsonChunkLength);
                bw.Write((uint)0x4E4F534A); // 'JSON'
                bw.Write(jsonBytes);
                for (int i = 0; i < jsonPadding; i++) bw.Write((byte)0x20); // space padding

                // Chunk 1: BIN
                bw.Write((uint)binChunkLength);
                bw.Write((uint)0x004E4942); // 'BIN\0'
                bw.Write(binData);
                for (int i = 0; i < binPadding; i++) bw.Write((byte)0x00);
            }

            var root = GltfLoader.LoadGlb(glb, "GlbTestModel");

            Assert.NotNull(root);
            Assert.Equal("GlbTestModel", root.Name);
            Assert.Single(root.Children);

            var child = root.Children[0];
            Assert.Equal("TriangleNode", child.Name);
            Assert.NotNull(child.Mesh);
            Assert.Equal(3, child.Mesh.Vertices.Count);
            Assert.Equal(3, child.Mesh.Indices.Count);

            // Transform check
            Assert.Equal(5.0f, child.LocalTransform.Translation.X);
            Assert.Equal(10.0f, child.LocalTransform.Translation.Y);
            Assert.Equal(15.0f, child.LocalTransform.Translation.Z);

            // Mesh bounds check
            Assert.Equal(0.0f, child.Mesh.BoundingBox.Min.X);
            Assert.Equal(1.0f, child.Mesh.BoundingBox.Max.X);
            Assert.Equal(1.0f, child.Mesh.BoundingBox.Max.Y);
        }

        [Fact]
        public void GltfLoader_DecodesBase64DataUriMesh()
        {
            // Simple triangle positions in float (36 bytes):
            // (0,0,0), (2,0,0), (0,3,0)
            byte[] posBytes = new byte[36];
            Buffer.BlockCopy(BitConverter.GetBytes(2.0f), 0, posBytes, 12, 4); // x=2 at v1
            Buffer.BlockCopy(BitConverter.GetBytes(3.0f), 0, posBytes, 28, 4); // y=3 at v2

            string base64 = Convert.ToBase64String(posBytes);

            string json = $@"{{
  ""buffers"": [
    {{ ""byteLength"": 36, ""uri"": ""data:application/octet-stream;base64,{base64}"" }}
  ],
  ""bufferViews"": [
    {{ ""buffer"": 0, ""byteOffset"": 0, ""byteLength"": 36 }}
  ],
  ""accessors"": [
    {{ ""bufferView"": 0, ""byteOffset"": 0, ""componentType"": 5126, ""count"": 3, ""type"": ""VEC3"" }}
  ],
  ""meshes"": [
    {{
      ""primitives"": [
        {{ ""attributes"": {{ ""POSITION"": 0 }} }}
      ]
    }}
  ],
  ""nodes"": [
    {{ ""name"": ""Base64MeshNode"", ""mesh"": 0 }}
  ]
}}";

            var root = GltfLoader.LoadGltf(json, modelName: "Base64Test");

            Assert.NotNull(root);
            Assert.Single(root.Children);
            var meshNode = root.Children[0];
            Assert.NotNull(meshNode.Mesh);
            Assert.Equal(3, meshNode.Mesh.Vertices.Count);
            Assert.Equal(0f, meshNode.Mesh.BoundingBox.Min.X);
            Assert.Equal(2f, meshNode.Mesh.BoundingBox.Max.X);
            Assert.Equal(3f, meshNode.Mesh.BoundingBox.Max.Y);
        }

        [Fact]
        public void GltfLoader_LoadAuto_DetectsFileTypesCorrectly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZeroTwin3D_AutoLoadTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // Write OBJ
                string objPath = Path.Combine(tempDir, "sample.obj");
                File.WriteAllText(objPath, "v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n");

                var objNode = GltfLoader.LoadAuto(objPath);
                Assert.NotNull(objNode);
                Assert.NotNull(objNode.Mesh);
                Assert.Equal(3, objNode.Mesh.Vertices.Count);

                // Write invalid path throws FileNotFoundException
                Assert.Throws<FileNotFoundException>(() => GltfLoader.LoadAuto(Path.Combine(tempDir, "non_existent.glb")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
