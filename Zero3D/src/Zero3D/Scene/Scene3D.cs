using System;
using System.Collections.Generic;
using Zero3D.Camera;
using Zero3D.Lighting;
using Zero3D.Math;

namespace Zero3D.Scene
{
    /// <summary>
    /// Information resulting from a spatial raycast test against the scene.
    /// </summary>
    public struct RaycastHit
    {
        public bool HasHit;
        public SceneNode? Node;
        public Vec3 Point;
        public float Distance;

        public static RaycastHit None => new RaycastHit { HasHit = false, Distance = float.MaxValue };
    }

    /// <summary>
    /// Top-level 3D Scene container managing scene hierarchy, active camera, and illumination lights.
    /// </summary>
    public class Scene3D
    {
        public SceneNode Root { get; } = new SceneNode { Name = "Root" };
        public Camera3D Camera { get; set; } = new Camera3D();
        public List<Light3D> Lights { get; } = new List<Light3D>();

        public void Update()
        {
            Root.UpdateTransform(Mat4.Identity);
        }

        public SceneNode? FindNode(string name)
        {
            if (Root.Name == name) return Root;
            return Root.FindChild(name, true);
        }

        /// <summary>
        /// Performs bounding-box raycast intersection across all visible mesh nodes in the scene.
        /// Returns the closest intersected node.
        /// </summary>
        public RaycastHit Raycast(Ray3D ray)
        {
            Update();
            float minDistance = float.MaxValue;
            SceneNode? closestNode = null;

            void CheckNode(SceneNode node)
            {
                if (!node.IsVisible) return;

                if (node.Mesh != null && node.Mesh.Vertices.Count > 0)
                {
                    if (node.WorldBounds.Intersects(ray, out float hitDist))
                    {
                        if (hitDist < minDistance)
                        {
                            minDistance = hitDist;
                            closestNode = node;
                        }
                    }
                }

                for (int i = 0; i < node.Children.Count; i++)
                {
                    CheckNode(node.Children[i]);
                }
            }

            CheckNode(Root);

            if (closestNode != null)
            {
                return new RaycastHit
                {
                    HasHit = true,
                    Node = closestNode,
                    Distance = minDistance,
                    Point = ray.GetPoint(minDistance)
                };
            }

            return RaycastHit.None;
        }
    }
}
