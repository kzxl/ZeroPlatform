using System;
using System.IO;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Sync;

namespace ZeroTwin3D.IO
{
    /// <summary>
    /// Pure C# glTF 2.0 and binary GLB 3D model loader delegating to the foundational Zero3D graphics engine.
    /// Supports automatic format discovery (.obj, .stl, .gltf, .glb).
    /// </summary>
    public static class GltfLoader
    {
        /// <summary>
        /// Automatically identifies and loads a 3D model (.obj, .stl, .gltf, .glb) from disk into a TwinNode.
        /// </summary>
        public static TwinNode LoadAuto(string filePath)
        {
            var sceneNode = Zero3D.IO.GltfLoader.LoadAuto(filePath);
            return ObjLoader.ToTwinNode(sceneNode);
        }

        /// <summary>
        /// Loads a binary GLB file from disk into a hierarchical <see cref="TwinNode"/>.
        /// </summary>
        public static TwinNode LoadGlbFromFile(string filePath)
        {
            var sceneNode = Zero3D.IO.GltfLoader.LoadGlbFromFile(filePath);
            return ObjLoader.ToTwinNode(sceneNode);
        }

        /// <summary>
        /// Loads a binary GLB byte array into a hierarchical <see cref="TwinNode"/>.
        /// </summary>
        public static TwinNode LoadGlb(byte[] glbBytes, string modelName = "GlbModel")
        {
            var sceneNode = Zero3D.IO.GltfLoader.LoadGlb(glbBytes, modelName);
            return ObjLoader.ToTwinNode(sceneNode);
        }

        /// <summary>
        /// Loads a glTF 2.0 text file from disk into a hierarchical <see cref="TwinNode"/>.
        /// </summary>
        public static TwinNode LoadGltfFromFile(string filePath)
        {
            var sceneNode = Zero3D.IO.GltfLoader.LoadGltfFromFile(filePath);
            return ObjLoader.ToTwinNode(sceneNode);
        }

        /// <summary>
        /// Parses a glTF 2.0 JSON string and associated binary buffer into a hierarchical <see cref="TwinNode"/>.
        /// </summary>
        public static TwinNode LoadGltf(string jsonText, byte[]? binBuffer = null, string modelName = "GltfModel")
        {
            var sceneNode = Zero3D.IO.GltfLoader.LoadGltf(jsonText, binBuffer, modelName);
            return ObjLoader.ToTwinNode(sceneNode);
        }
    }
}
