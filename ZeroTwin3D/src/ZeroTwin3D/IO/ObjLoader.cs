using System;
using System.IO;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Sync;

namespace ZeroTwin3D.IO
{
    /// <summary>
    /// Pure C# Wavefront OBJ 3D model loader delegating to the foundational Zero3D graphics engine.
    /// </summary>
    public static class ObjLoader
    {
        /// <summary>
        /// Loads a Wavefront OBJ file from disk into a hierarchical <see cref="TwinNode"/>.
        /// </summary>
        public static TwinNode LoadFromFile(string filePath)
        {
            var sceneNode = Zero3D.IO.ObjLoader.LoadFromFile(filePath);
            return ToTwinNode(sceneNode);
        }

        /// <summary>
        /// Loads Wavefront OBJ ASCII content into a hierarchical <see cref="TwinNode"/>.
        /// </summary>
        public static TwinNode Load(string objText, string nodeName = "ObjModel")
        {
            var sceneNode = Zero3D.IO.ObjLoader.Load(objText, nodeName);
            return ToTwinNode(sceneNode);
        }

        internal static TwinNode ToTwinNode(Zero3D.Scene.SceneNode node)
        {
            var twinNode = new TwinNode
            {
                Name = node.Name,
                LocalTransform = (Mat4)node.LocalTransform,
                IsVisible = node.IsVisible,
                CastShadow = node.CastShadow
            };

            if (node.Mesh != null && node.Mesh.Vertices.Count > 0)
            {
                var mesh = new Mesh3D();
                for (int i = 0; i < node.Mesh.Vertices.Count; i++)
                {
                    var v = node.Mesh.Vertices[i];
                    mesh.Vertices.Add(new Vertex3D(v.Position, v.Normal, v.Uv));
                }
                mesh.Indices.AddRange(node.Mesh.Indices);
                mesh.ComputeBounds();
                twinNode.Mesh = mesh;
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                twinNode.AddChild(ToTwinNode(node.Children[i]));
            }

            return twinNode;
        }
    }
}
