using System;
using System.Collections.Generic;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Kinematics;

namespace ZeroTwin3D.Sync
{
    /// <summary>
    /// Node in the 3D Digital Twin scene graph hierarchy.
    /// </summary>
    public class TwinNode
    {
        public string Name { get; set; } = string.Empty;
        public Mat4 LocalTransform { get; set; } = Mat4.Identity;
        public Mat4 WorldTransform { get; internal set; } = Mat4.Identity;

        public TwinNode? Parent { get; internal set; }
        public List<TwinNode> Children { get; } = new List<TwinNode>();
        public Mesh3D? Mesh { get; set; }

        public Aabb3D WorldBounds
        {
            get
            {
                if (Mesh == null)
                {
                    var p = WorldTransform.Translation;
                    return new Aabb3D(p, p);
                }

                // Transform local bounding box corners into world space
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

                foreach (var c in corners)
                {
                    var w = WorldTransform.TransformPoint(c);
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

        public TwinNode AddChild(TwinNode child)
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        public void UpdateTransform(Mat4 parentWorld)
        {
            WorldTransform = LocalTransform * parentWorld;
            foreach (var c in Children)
            {
                c.UpdateTransform(WorldTransform);
            }
        }
    }

    /// <summary>
    /// Digital Twin 3D Scene container maintaining hierarchical spatial trees and safety zones.
    /// </summary>
    public class TwinScene
    {
        public TwinNode Root { get; } = new TwinNode { Name = "Root" };
        public List<Aabb3D> SafetyZones { get; } = new List<Aabb3D>();

        public void Update()
        {
            Root.UpdateTransform(Mat4.Identity);
        }

        public TwinNode? FindNode(string name)
        {
            return FindRecursive(Root, name);
        }

        private TwinNode? FindRecursive(TwinNode current, string name)
        {
            if (string.Equals(current.Name, name, StringComparison.OrdinalIgnoreCase))
                return current;

            foreach (var child in current.Children)
            {
                var found = FindRecursive(child, name);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Checks if any node in the scene breaches a designated safety exclusion zone.
        /// </summary>
        public bool CheckSafetyViolation(out string violatingNode, out Aabb3D breachedZone)
        {
            violatingNode = string.Empty;
            breachedZone = default;

            foreach (var zone in SafetyZones)
            {
                if (CheckNodeViolationRecursive(Root, zone, out violatingNode))
                {
                    breachedZone = zone;
                    return true;
                }
            }

            return false;
        }

        private bool CheckNodeViolationRecursive(TwinNode current, Aabb3D zone, out string violatingNode)
        {
            violatingNode = string.Empty;
            if (current != Root && current.WorldBounds.Intersects(zone))
            {
                violatingNode = current.Name;
                return true;
            }

            foreach (var child in current.Children)
            {
                if (CheckNodeViolationRecursive(child, zone, out violatingNode))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Synchronizes physical machine telemetry (PLC registers, robot joint angles) into the 3D Digital Twin.
    /// </summary>
    public class TwinTelemetryBridge
    {
        public TwinScene Scene { get; }
        public RobotKinematics? Robot { get; set; }
        public TwinNode? RobotEndEffectorNode { get; set; }

        public event Action<string, Aabb3D>? OnSafetyBreach;

        public TwinTelemetryBridge(TwinScene scene)
        {
            Scene = scene;
        }

        /// <summary>
        /// Applies physical robot joint telemetry, recalculates kinematics, and verifies workcell safety.
        /// </summary>
        public void ApplyRobotJointTelemetry(IReadOnlyList<float> jointAnglesDeg)
        {
            if (Robot == null || RobotEndEffectorNode == null) return;

            var result = Robot.ComputeForwardKinematics(jointAnglesDeg);
            RobotEndEffectorNode.LocalTransform = result.EndEffectorTransform;

            Scene.Update();

            if (Scene.CheckSafetyViolation(out string node, out var zone))
            {
                OnSafetyBreach?.Invoke(node, zone);
            }
        }
    }
}
