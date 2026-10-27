using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using ZeroMotion.Math;

namespace ZeroMotion.Urdf
{
    /// <summary>
    /// Pure C# Unified Robot Description Format (URDF) XML Parser.
    /// Extracts kinematic tree topology, joint limits, axes, and frame offsets.
    /// </summary>
    public static class UrdfParser
    {
        public static UrdfRobot ParseFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"URDF robot file not found: {filePath}", filePath);

            string xml = File.ReadAllText(filePath);
            return Parse(xml);
        }

        public static UrdfRobot Parse(string urdfXml)
        {
            if (string.IsNullOrEmpty(urdfXml))
                throw new ArgumentNullException(nameof(urdfXml));

            var doc = XDocument.Parse(urdfXml);
            var rootEl = doc.Root;
            if (rootEl == null || !rootEl.Name.LocalName.Equals("robot", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Root element must be <robot>.");
            }

            var robot = new UrdfRobot
            {
                Name = rootEl.Attribute("name")?.Value ?? "Robot"
            };

            // 1. Parse Links
            foreach (var linkEl in rootEl.Elements("link"))
            {
                string linkName = linkEl.Attribute("name")?.Value ?? $"Link_{robot.Links.Count}";
                var link = new UrdfLink { Name = linkName };

                var inertialEl = linkEl.Element("inertial");
                if (inertialEl != null)
                {
                    var massEl = inertialEl.Element("mass");
                    if (massEl != null && float.TryParse(massEl.Attribute("value")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float m))
                    {
                        link.MassKg = m;
                    }
                    var originEl = inertialEl.Element("origin");
                    if (originEl != null)
                    {
                        link.CenterOfMass = ParseVec3(originEl.Attribute("xyz")?.Value);
                    }
                }

                var visualEl = linkEl.Element("visual");
                if (visualEl != null)
                {
                    var originEl = visualEl.Element("origin");
                    if (originEl != null)
                    {
                        link.VisualOrigin = ParseVec3(originEl.Attribute("xyz")?.Value);
                        link.VisualRpyRad = ParseVec3(originEl.Attribute("rpy")?.Value);
                    }
                    var geomEl = visualEl.Element("geometry");
                    if (geomEl != null)
                    {
                        var meshEl = geomEl.Element("mesh");
                        if (meshEl != null)
                        {
                            link.MeshResourcePath = meshEl.Attribute("filename")?.Value;
                        }
                    }
                }

                robot.Links[linkName] = link;
            }

            // 2. Parse Joints
            var childLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var jointEl in rootEl.Elements("joint"))
            {
                string jointName = jointEl.Attribute("name")?.Value ?? $"Joint_{robot.Joints.Count}";
                string jointType = jointEl.Attribute("type")?.Value ?? "revolute";
                string parentName = jointEl.Element("parent")?.Attribute("link")?.Value ?? string.Empty;
                string childName = jointEl.Element("child")?.Attribute("link")?.Value ?? string.Empty;

                var joint = new UrdfJoint
                {
                    Name = jointName,
                    Type = jointType,
                    ParentLink = parentName,
                    ChildLink = childName
                };

                childLinks.Add(childName);

                var originEl = jointEl.Element("origin");
                if (originEl != null)
                {
                    joint.OriginTranslation = ParseVec3(originEl.Attribute("xyz")?.Value);
                    joint.OriginRpyRad = ParseVec3(originEl.Attribute("rpy")?.Value);
                }

                var axisEl = jointEl.Element("axis");
                if (axisEl != null)
                {
                    joint.Axis = ParseVec3(axisEl.Attribute("xyz")?.Value, Vec3.UnitZ).Normalize();
                }

                var limitEl = jointEl.Element("limit");
                if (limitEl != null)
                {
                    if (float.TryParse(limitEl.Attribute("lower")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float lower))
                        joint.LowerLimit = lower;
                    if (float.TryParse(limitEl.Attribute("upper")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float upper))
                        joint.UpperLimit = upper;
                    if (float.TryParse(limitEl.Attribute("velocity")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float vel))
                        joint.MaxVelocity = vel;
                    if (float.TryParse(limitEl.Attribute("effort")?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float eff))
                        joint.MaxEffort = eff;
                }

                robot.Joints.Add(joint);
            }

            // Determine root link (the link that is never a child)
            foreach (var linkName in robot.Links.Keys)
            {
                if (!childLinks.Contains(linkName))
                {
                    robot.RootLinkName = linkName;
                    break;
                }
            }

            return robot;
        }

        private static Vec3 ParseVec3(string? raw, Vec3 defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
            var parts = raw!.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3) return defaultValue;

            float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
            float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
            float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
            return new Vec3(x, y, z);
        }
    }
}
