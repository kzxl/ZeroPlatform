using System;
using System.Collections.Generic;
using ZeroMotion.Kinematics;
using ZeroMotion.Math;

namespace ZeroMotion.Urdf
{
    public class UrdfLink
    {
        public string Name { get; set; } = string.Empty;
        public float MassKg { get; set; }
        public Vec3 CenterOfMass { get; set; } = Vec3.Zero;
        public Vec3 VisualOrigin { get; set; } = Vec3.Zero;
        public Vec3 VisualRpyRad { get; set; } = Vec3.Zero;
        public string? MeshResourcePath { get; set; }
    }

    public class UrdfJoint
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "revolute"; // revolute, continuous, prismatic, fixed
        public string ParentLink { get; set; } = string.Empty;
        public string ChildLink { get; set; } = string.Empty;
        public Vec3 OriginTranslation { get; set; } = Vec3.Zero;
        public Vec3 OriginRpyRad { get; set; } = Vec3.Zero;
        public Vec3 Axis { get; set; } = Vec3.UnitZ;
        public float LowerLimit { get; set; } = -(float)System.Math.PI;
        public float UpperLimit { get; set; } = (float)System.Math.PI;
        public float MaxVelocity { get; set; } = 10f;
        public float MaxEffort { get; set; } = 100f;
    }

    public class UrdfRobot
    {
        public string Name { get; set; } = "Robot";
        public string RootLinkName { get; set; } = string.Empty;
        public Dictionary<string, UrdfLink> Links { get; } = new Dictionary<string, UrdfLink>(StringComparer.OrdinalIgnoreCase);
        public List<UrdfJoint> Joints { get; } = new List<UrdfJoint>();

        /// <summary>
        /// Converts the kinematic tree of revolute and prismatic joints into DH links.
        /// </summary>
        public RobotKinematics ToRobotKinematics()
        {
            var robot = new RobotKinematics();

            foreach (var joint in Joints)
            {
                if (joint.Type.Equals("fixed", StringComparison.OrdinalIgnoreCase)) continue;

                var jType = joint.Type.Equals("prismatic", StringComparison.OrdinalIgnoreCase)
                    ? JointType.Prismatic
                    : JointType.Revolute;

                float d = joint.OriginTranslation.Z;
                float a = joint.OriginTranslation.X;
                float alphaDeg = joint.OriginRpyRad.X * 180f / (float)System.Math.PI;
                float thetaOffsetDeg = joint.OriginRpyRad.Z * 180f / (float)System.Math.PI;

                float minLim = joint.LowerLimit * 180f / (float)System.Math.PI;
                float maxLim = joint.UpperLimit * 180f / (float)System.Math.PI;

                var link = new DhLink(thetaOffsetDeg, d, a, alphaDeg, joint.Name)
                {
                    Type = jType,
                    MinLimit = minLim,
                    MaxLimit = maxLim
                };

                robot.AddLink(link);
            }

            return robot;
        }
    }
}
