using System;
using ZeroMotion.Math;

namespace ZeroMotion.Kinematics
{
    public enum JointType
    {
        Revolute,
        Prismatic
    }

    /// <summary>
    /// Denavit-Hartenberg (DH) kinematic link parameter specification for industrial robotic manipulators.
    /// Supports standard Denavit-Hartenberg homogenous transformations.
    /// </summary>
    public class DhLink
    {
        public string Name { get; set; } = string.Empty;
        public JointType Type { get; set; } = JointType.Revolute;
        public float ThetaOffsetDeg { get; set; }
        public float D { get; set; }        // Link offset along Z (m or mm)
        public float A { get; set; }        // Link length along X (m or mm)
        public float AlphaDeg { get; set; } // Link twist about X (degrees)

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

        public DhLink WithLimits(float min, float max)
        {
            MinLimit = min;
            MaxLimit = max;
            return this;
        }

        /// <summary>
        /// Computes the 4x4 homogenous transformation matrix for this link given the joint variable.
        /// RotZ(theta) * TransZ(d) * TransX(a) * RotX(alpha)
        /// </summary>
        public Mat4 ComputeTransform(float jointValue)
        {
            float theta = (Type == JointType.Revolute) ? (jointValue + ThetaOffsetDeg) : ThetaOffsetDeg;
            float d = (Type == JointType.Prismatic) ? (jointValue + D) : D;

            float thetaRad = theta * (float)System.Math.PI / 180.0f;
            float alphaRad = AlphaDeg * (float)System.Math.PI / 180.0f;

            float cosTheta = (float)System.Math.Cos(thetaRad);
            float sinTheta = (float)System.Math.Sin(thetaRad);
            float cosAlpha = (float)System.Math.Cos(alphaRad);
            float sinAlpha = (float)System.Math.Sin(alphaRad);

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
}
