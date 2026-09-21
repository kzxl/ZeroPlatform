using System;
using Zero3D.Math;
using Zero3D.Materials;

namespace Zero3D.Mesh
{
    /// <summary>
    /// Standard 3D vertex structure containing spatial position, surface normal, texture coordinate, and color.
    /// </summary>
    public struct Vertex3D
    {
        public Vec3 Position;
        public Vec3 Normal;
        public Vec2 Uv;
        public ColorRgb Color;

        public Vertex3D(Vec3 position, Vec3 normal, Vec2 uv)
        {
            Position = position;
            Normal = normal;
            Uv = uv;
            Color = ColorRgb.White;
        }

        public Vertex3D(Vec3 position, Vec3 normal, Vec2 uv, ColorRgb color)
        {
            Position = position;
            Normal = normal;
            Uv = uv;
            Color = color;
        }

        public override string ToString() => $"Pos:{Position} Norm:{Normal} UV:({Uv.U:F2},{Uv.V:F2})";
    }

    /// <summary>
    /// Sub-division of a mesh mapped to a specific material.
    /// </summary>
    public class SubMesh
    {
        public string Name { get; set; } = string.Empty;
        public int IndexStart { get; set; }
        public int IndexCount { get; set; }
        public int MaterialIndex { get; set; }

        public SubMesh() { }

        public SubMesh(string name, int indexStart, int indexCount, int materialIndex = 0)
        {
            Name = name;
            IndexStart = indexStart;
            IndexCount = indexCount;
            MaterialIndex = materialIndex;
        }
    }
}
