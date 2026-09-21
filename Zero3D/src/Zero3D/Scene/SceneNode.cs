using System;
using System.Collections.Generic;
using Zero3D.Math;
using Zero3D.Mesh;
using Zero3D.Materials;

namespace Zero3D.Scene
{
    /// <summary>
    /// Hierarchical Scene Node in the 3D scene graph, maintaining spatial transformations,
    /// mesh attachments, materials, and world-space bounds.
    /// </summary>
    public class SceneNode
    {
        public string Name { get; set; } = string.Empty;
        public Mat4 LocalTransform { get; set; } = Mat4.Identity;
        public Mat4 WorldTransform { get; internal set; } = Mat4.Identity;

        public SceneNode? Parent { get; internal set; }
        public List<SceneNode> Children { get; } = new List<SceneNode>();
        public Mesh3D? Mesh { get; set; }
        public Material3D? Material { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool CastShadow { get; set; } = true;
        public object? UserData { get; set; }

        public Aabb3D WorldBounds
        {
            get
            {
                if (Mesh == null || Mesh.Vertices.Count == 0)
                {
                    var p = WorldTransform.Translation;
                    return new Aabb3D(p, p);
                }

                var box = Mesh.BoundingBox;
                var corners = new[]
                {
                    new Vec3(box.Min.X, box.Min.Y, box.Min.Z),
                    new Vec3(box.Max.X, box.Min.Y, box.Min.Z),
                    new Vec3(box.Min.X, box.Max.Y, box.Min.Z),
                    new Vec3(box.Max.X, box.Max.Y, box.Min.Z),
                    new Vec3(box.Min.X, box.Min.Y, box.Max.Z),
                    new Vec3(box.Max.X, box.Min.Y, box.Max.Z),
                    new Vec3(box.Min.X, box.Max.Y, box.Max.Z),
                    new Vec3(box.Max.X, box.Max.Y, box.Max.Z),
                };

                var min = new Vec3(float.MaxValue, float.MaxValue, float.MaxValue);
                var max = new Vec3(float.MinValue, float.MinValue, float.MinValue);

                for (int i = 0; i < corners.Length; i++)
                {
                    var w = WorldTransform.TransformPoint(corners[i]);
                    if (w.X < min.X) min.X = w.X;
                    if (w.Y < min.Y) min.Y = w.Y;
                    if (w.Z < min.Z) min.Z = w.Z;

                    if (w.X > max.X) max.X = w.X;
                    if (w.Y > max.Y) max.Y = w.Y;
                    if (w.Z > max.Z) max.Z = w.Z;
                }

                return new Aabb3D(min, max);
            }
        }

        public SceneNode AddChild(SceneNode child)
        {
            if (child.Parent != null)
            {
                child.Parent.Children.Remove(child);
            }
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        public bool RemoveChild(SceneNode child)
        {
            if (Children.Remove(child))
            {
                child.Parent = null;
                return true;
            }
            return false;
        }

        public void UpdateTransform(Mat4 parentWorld)
        {
            WorldTransform = LocalTransform * parentWorld;
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].UpdateTransform(WorldTransform);
            }
        }

        public SceneNode? FindChild(string name, bool recursive = true)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i].Name == name) return Children[i];
                if (recursive)
                {
                    var found = Children[i].FindChild(name, true);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}
