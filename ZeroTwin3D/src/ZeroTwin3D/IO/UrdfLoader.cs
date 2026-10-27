using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Zero3D.Mesh;
using ZeroTwin3D.Engine;
using ZeroTwin3D.Kinematics;
using ZeroTwin3D.Sync;
using Mesh3D = ZeroTwin3D.Engine.Mesh3D;
using Vec3 = ZeroTwin3D.Engine.Vec3;
using Vertex3D = ZeroTwin3D.Engine.Vertex3D;

namespace ZeroTwin3D.IO
{
    /// <summary>
    /// Model container resulting from a loaded URDF robot specification.
    /// Contains both the visual 3D scene hierarchy and the kinematic linkage chain.
    /// </summary>
    public class UrdfRobotModel
    {
        public string Name { get; set; } = "Robot";
        public TwinNode RootNode { get; set; } = null!;
        public Dictionary<string, TwinNode> Links { get; } = new Dictionary<string, TwinNode>(StringComparer.OrdinalIgnoreCase);
        public List<UrdfJointInfo> Joints { get; } = new List<UrdfJointInfo>();
        public RobotKinematics Kinematics { get; } = new RobotKinematics();
    }

    public class UrdfJointInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "revolute";
        public string ParentLink { get; set; } = string.Empty;
        public string ChildLink { get; set; } = string.Empty;
        public Vec3 OriginTranslation { get; set; }
        public Vec3 OriginRpyRad { get; set; }
        public Vec3 Axis { get; set; } = Vec3.UnitZ;
        public float LowerLimitRad { get; set; } = -(float)System.Math.PI;
        public float UpperLimitRad { get; set; } = (float)System.Math.PI;
    }

    /// <summary>
    /// Pure C# Unified Robot Description Format (URDF) XML Parser for ROS industrial robots.
    /// Automatically builds hierarchical TwinNode visual trees and maps kinematic linkages.
    /// </summary>
    public static class UrdfLoader
    {
        public static UrdfRobotModel LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"URDF robot file not found: {filePath}", filePath);

            string xml = File.ReadAllText(filePath);
            return Load(xml, Path.GetDirectoryName(filePath) ?? string.Empty);
        }

        public static UrdfRobotModel Load(string urdfXml, string baseDir = "")
        {
            if (string.IsNullOrEmpty(urdfXml))
                throw new ArgumentNullException(nameof(urdfXml));

            var doc = XDocument.Parse(urdfXml);
            var rootEl = doc.Root;
            if (rootEl == null || !rootEl.Name.LocalName.Equals("robot", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Root element must be <robot>.");
            }

            var robotModel = new UrdfRobotModel
            {
                Name = rootEl.Attribute("name")?.Value ?? "Robot"
            };

            // 1. Parse all links
            foreach (var linkEl in rootEl.Elements("link"))
            {
                string linkName = linkEl.Attribute("name")?.Value ?? $"Link_{robotModel.Links.Count}";
                var node = new TwinNode { Name = linkName };

                var visualEl = linkEl.Element("visual");
                if (visualEl != null)
                {
                    var geomEl = visualEl.Element("geometry");
                    if (geomEl != null)
                    {
                        node.Mesh = ParseGeometryMesh(geomEl, baseDir);
                    }

                    var originEl = visualEl.Element("origin");
                    if (originEl != null)
                    {
                        node.LocalTransform = ParseOriginTransform(originEl);
                    }
                }

                robotModel.Links[linkName] = node;
            }

            // 2. Parse all joints & establish hierarchy
            var childLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var jointEl in rootEl.Elements("joint"))
            {
                string jointName = jointEl.Attribute("name")?.Value ?? "Joint";
                string jointType = jointEl.Attribute("type")?.Value?.ToLowerInvariant() ?? "revolute";

                string parentLink = jointEl.Element("parent")?.Attribute("link")?.Value ?? string.Empty;
                string childLink = jointEl.Element("child")?.Attribute("link")?.Value ?? string.Empty;

                var jointInfo = new UrdfJointInfo
                {
                    Name = jointName,
                    Type = jointType,
                    ParentLink = parentLink,
                    ChildLink = childLink
                };

                var originEl = jointEl.Element("origin");
                if (originEl != null)
                {
                    ParseOriginValues(originEl, out var trans, out var rpy);
                    jointInfo.OriginTranslation = trans;
                    jointInfo.OriginRpyRad = rpy;
                }

                var axisEl = jointEl.Element("axis");
                if (axisEl != null && axisEl.Attribute("xyz") != null)
                {
                    jointInfo.Axis = ParseVec3(axisEl.Attribute("xyz")!.Value);
                }

                var limitEl = jointEl.Element("limit");
                if (limitEl != null)
                {
                    if (float.TryParse(limitEl.Attribute("lower")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float lower))
                        jointInfo.LowerLimitRad = lower;
                    if (float.TryParse(limitEl.Attribute("upper")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float upper))
                        jointInfo.UpperLimitRad = upper;
                }

                robotModel.Joints.Add(jointInfo);

                // Build parent-child relationship in TwinNode graph
                if (robotModel.Links.TryGetValue(parentLink, out var parentNode) &&
                    robotModel.Links.TryGetValue(childLink, out var childNode))
                {
                    Mat4 jointT = CreateRpyTransform(jointInfo.OriginTranslation, jointInfo.OriginRpyRad);
                    childNode.LocalTransform = jointT;
                    parentNode.AddChild(childNode);
                    childLinks.Add(childLink);
                }

                // Add to RobotKinematics chain
                if (jointType == "revolute" || jointType == "continuous")
                {
                    float d = jointInfo.OriginTranslation.Z;
                    float a = jointInfo.OriginTranslation.X;
                    float alphaDeg = jointInfo.OriginRpyRad.X * 180f / (float)System.Math.PI;

                    var dhLink = new DhLink(0f, d, a, alphaDeg, jointName)
                    {
                        MinLimit = jointInfo.LowerLimitRad * 180f / (float)System.Math.PI,
                        MaxLimit = jointInfo.UpperLimitRad * 180f / (float)System.Math.PI
                    };
                    robotModel.Kinematics.AddLink(dhLink);
                }
            }

            // 3. Identify root node (a link not referenced as a child of any joint)
            TwinNode? root = null;
            foreach (var kvp in robotModel.Links)
            {
                if (!childLinks.Contains(kvp.Key))
                {
                    root = kvp.Value;
                    break;
                }
            }

            robotModel.RootNode = root ?? (robotModel.Links.Count > 0 ? new List<TwinNode>(robotModel.Links.Values)[0] : new TwinNode { Name = "Root" });
            return robotModel;
        }

        private static Mesh3D? ParseGeometryMesh(XElement geomEl, string baseDir)
        {
            var box = geomEl.Element("box");
            if (box != null && box.Attribute("size") != null)
            {
                var s = ParseVec3(box.Attribute("size")!.Value);
                var primitive = MeshPrimitives.CreateBox(s.X, s.Y, s.Z);
                return ConvertZero3DMesh(primitive);
            }

            var cyl = geomEl.Element("cylinder");
            if (cyl != null)
            {
                float r = float.Parse(cyl.Attribute("radius")?.Value ?? "1", CultureInfo.InvariantCulture);
                float l = float.Parse(cyl.Attribute("length")?.Value ?? "1", CultureInfo.InvariantCulture);
                var primitive = MeshPrimitives.CreateCylinder(r, l);
                return ConvertZero3DMesh(primitive);
            }

            var sphere = geomEl.Element("sphere");
            if (sphere != null)
            {
                float r = float.Parse(sphere.Attribute("radius")?.Value ?? "1", CultureInfo.InvariantCulture);
                var primitive = MeshPrimitives.CreateSphere(r);
                return ConvertZero3DMesh(primitive);
            }

            var meshEl = geomEl.Element("mesh");
            if (meshEl != null && meshEl.Attribute("filename") != null)
            {
                string filename = meshEl.Attribute("filename")!.Value;
                if (filename.StartsWith("package://", StringComparison.OrdinalIgnoreCase))
                {
                    filename = filename.Substring("package://".Length);
                }

                string fullPath = Path.Combine(baseDir, filename);
                if (File.Exists(fullPath))
                {
                    var loadedNode = GltfLoader.LoadAuto(fullPath);
                    if (loadedNode.Mesh != null) return loadedNode.Mesh;
                }
            }

            return null;
        }

        private static Mesh3D ConvertZero3DMesh(Zero3D.Mesh.Mesh3D source)
        {
            var m = new Mesh3D();
            for (int i = 0; i < source.Vertices.Count; i++)
            {
                var v = source.Vertices[i];
                m.Vertices.Add(new Vertex3D(v.Position, v.Normal, v.Uv));
            }
            m.Indices.AddRange(source.Indices);
            m.ComputeBounds();
            return m;
        }

        private static Mat4 ParseOriginTransform(XElement originEl)
        {
            ParseOriginValues(originEl, out var trans, out var rpy);
            return CreateRpyTransform(trans, rpy);
        }

        private static void ParseOriginValues(XElement originEl, out Vec3 trans, out Vec3 rpy)
        {
            trans = Vec3.Zero;
            rpy = Vec3.Zero;

            if (originEl.Attribute("xyz") != null)
            {
                trans = ParseVec3(originEl.Attribute("xyz")!.Value);
            }
            if (originEl.Attribute("rpy") != null)
            {
                rpy = ParseVec3(originEl.Attribute("rpy")!.Value);
            }
        }

        private static Mat4 CreateRpyTransform(Vec3 translation, Vec3 rpyRad)
        {
            var rotX = Mat4.CreateRotationX(rpyRad.X);
            var rotY = Mat4.CreateRotationY(rpyRad.Y);
            var rotZ = Mat4.CreateRotationZ(rpyRad.Z);
            var trans = Mat4.CreateTranslation(translation);

            // Roll-Pitch-Yaw convention: Rz * Ry * Rx * Translation
            return rotZ * rotY * rotX * trans;
        }

        private static Vec3 ParseVec3(string text)
        {
            var tokens = text.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 3)
            {
                return new Vec3(
                    float.Parse(tokens[0], CultureInfo.InvariantCulture),
                    float.Parse(tokens[1], CultureInfo.InvariantCulture),
                    float.Parse(tokens[2], CultureInfo.InvariantCulture)
                );
            }
            return Vec3.Zero;
        }
    }
}
