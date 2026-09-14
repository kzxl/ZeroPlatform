using System;
using System.Collections.Generic;
using ZeroTwin3D.Engine;

namespace ZeroTwin3D.Kinematics
{
    public enum JointType
    {
        Revolute,
        Prismatic
    }

    /// <summary>
    /// Denavit-Hartenberg (DH) kinematic link parameter specification.
    /// </summary>
    public class DhLink
    {
        public string Name { get; set; } = string.Empty;
        public JointType Type { get; set; } = JointType.Revolute;
        public float ThetaOffsetDeg { get; set; }
        public float D { get; set; }        // Link offset along Z
        public float A { get; set; }        // Link length along X
        public float AlphaDeg { get; set; } // Link twist about X

        public float MinLimit { get; set; } = -180f;
        public float MaxLimit { get; set; } = 180f;

        public DhLink(float thetaOffsetDeg, float d, float a, float alphaDeg, string name = "")
        {
            ThetaOffsetDeg = thetaOffsetDeg;
            D = d;
            A = a;
            AlphaDeg = alphaDeg;
            Name = name;
        }

        /// <summary>
        /// Computes the 4x4 homogenous transformation matrix for this link given the joint variable.
        /// </summary>
        public Mat4 ComputeTransform(float jointValue)
        {
            float theta = (Type == JointType.Revolute) ? (jointValue + ThetaOffsetDeg) : ThetaOffsetDeg;
            float d = (Type == JointType.Prismatic) ? (jointValue + D) : D;

            float thetaRad = theta * (float)Math.PI / 180.0f;
            float alphaRad = AlphaDeg * (float)Math.PI / 180.0f;

            float cosTheta = (float)Math.Cos(thetaRad);
            float sinTheta = (float)Math.Sin(thetaRad);
            float cosAlpha = (float)Math.Cos(alphaRad);
            float sinAlpha = (float)Math.Sin(alphaRad);

            return new Mat4
            {
                M11 = cosTheta,
                M12 = sinTheta * cosAlpha,
                M13 = sinTheta * sinAlpha,
                M14 = 0f,

                M21 = -sinTheta,
                M22 = cosTheta * cosAlpha,
                M23 = cosTheta * sinAlpha,
                M24 = 0f,

                M31 = 0f,
                M32 = -sinAlpha,
                M33 = cosAlpha,
                M34 = 0f,

                M41 = A * cosTheta,
                M42 = A * sinTheta,
                M43 = d,
                M44 = 1f
            };
        }
    }

    public class KinematicsResult
    {
        public List<Mat4> JointTransforms { get; } = new List<Mat4>();
        public List<Vec3> JointPositions { get; } = new List<Vec3>();
        public Mat4 EndEffectorTransform { get; set; }
        public Vec3 EndEffectorPosition => EndEffectorTransform.Translation;
    }

    /// <summary>
    /// Kinematics engine for multi-axis industrial robots (6-DOF articulated arms and SCARA manipulators).
    /// </summary>
    public class RobotKinematics
    {
        public List<DhLink> Links { get; } = new List<DhLink>();
        public Mat4 BaseTransform { get; set; } = Mat4.Identity;

        public RobotKinematics()
        {
        }

        public RobotKinematics AddLink(DhLink link)
        {
            Links.Add(link);
            return this;
        }

        /// <summary>
        /// Solves Forward Kinematics (FK) for the robot given joint values.
        /// </summary>
        public KinematicsResult ComputeForwardKinematics(IReadOnlyList<float> jointValues)
        {
            var result = new KinematicsResult();
            Mat4 current = BaseTransform;

            result.JointPositions.Add(current.Translation);
            result.JointTransforms.Add(current);

            for (int i = 0; i < Links.Count; i++)
            {
                float val = (i < jointValues.Count) ? jointValues[i] : 0f;
                Mat4 localT = Links[i].ComputeTransform(val);
                current = localT * current;

                result.JointTransforms.Add(current);
                result.JointPositions.Add(current.Translation);
            }

            result.EndEffectorTransform = current;
            return result;
        }

        /// <summary>
        /// Factory method for a standard 6-Axis Articulated Industrial Robot (KUKA/ABB style).
        /// </summary>
        public static RobotKinematics CreateStandard6AxisRobot(float d1 = 400f, float a1 = 150f, float a2 = 450f, float d4 = 400f, float d6 = 100f)
        {
            var robot = new RobotKinematics();
            robot.AddLink(new DhLink(0f, d1, a1, 90f, "Joint1_BaseYaw"));
            robot.AddLink(new DhLink(0f, 0f, a2, 0f, "Joint2_ShoulderPitch"));
            robot.AddLink(new DhLink(0f, 0f, 0f, 90f, "Joint3_ElbowPitch"));
            robot.AddLink(new DhLink(0f, d4, 0f, -90f, "Joint4_WristRoll"));
            robot.AddLink(new DhLink(0f, 0f, 0f, 90f, "Joint5_WristPitch"));
            robot.AddLink(new DhLink(0f, d6, 0f, 0f, "Joint6_FlangeRoll"));
            return robot;
        }

        /// <summary>
        /// Factory method for a standard 4-Axis SCARA Industrial Robot.
        /// </summary>
        public static RobotKinematics CreateStandardScaraRobot(float l1 = 300f, float l2 = 250f, float baseHeight = 200f)
        {
            var robot = new RobotKinematics();
            robot.AddLink(new DhLink(0f, baseHeight, l1, 0f, "Joint1_Arm1Yaw"));
            robot.AddLink(new DhLink(0f, 0f, l2, 0f, "Joint2_Arm2Yaw"));
            robot.AddLink(new DhLink(0f, 0f, 0f, 0f, "Joint3_ZPrismatic") { Type = JointType.Prismatic, MinLimit = -200f, MaxLimit = 0f });
            robot.AddLink(new DhLink(0f, 0f, 0f, 0f, "Joint4_ToolYaw"));
            return robot;
        }

        /// <summary>
        /// Solves analytical Inverse Kinematics for a 4-Axis SCARA Robot.
        /// </summary>
        public static bool SolveScaraIk(float l1, float l2, Vec3 targetPos, out float theta1Deg, out float theta2Deg, out float zPrismatic, bool elbowRight = true)
        {
            theta1Deg = 0f;
            theta2Deg = 0f;
            zPrismatic = targetPos.Z;

            float x = targetPos.X;
            float y = targetPos.Y;
            float rSq = x * x + y * y;
            float r = (float)Math.Sqrt(rSq);

            // Reachability check
            if (r > (l1 + l2) || r < Math.Abs(l1 - l2))
            {
                return false;
            }

            // Cosine rule for theta2
            float cosTheta2 = (rSq - l1 * l1 - l2 * l2) / (2f * l1 * l2);
            cosTheta2 = Math.Max(-1.0f, Math.Min(1.0f, cosTheta2));
            float sinTheta2 = (float)Math.Sqrt(Math.Max(0f, 1f - cosTheta2 * cosTheta2));
            if (!elbowRight) sinTheta2 = -sinTheta2;

            float theta2Rad = (float)Math.Atan2(sinTheta2, cosTheta2);

            // Theta1
            float beta = (float)Math.Atan2(y, x);
            float gamma = (float)Math.Atan2(l2 * sinTheta2, l1 + l2 * cosTheta2);
            float theta1Rad = beta - gamma;

            theta1Deg = theta1Rad * 180f / (float)Math.PI;
            theta2Deg = theta2Rad * 180f / (float)Math.PI;
            return true;
        }
    }
}
