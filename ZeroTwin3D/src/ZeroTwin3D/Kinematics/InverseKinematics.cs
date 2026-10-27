using System;
using System.Collections.Generic;
using ZeroTwin3D.Engine;

namespace ZeroTwin3D.Kinematics
{
    /// <summary>
    /// Result of an Inverse Kinematics calculation.
    /// </summary>
    public class IkResult
    {
        public bool Success { get; set; }
        public float[] JointAngles { get; set; } = Array.Empty<float>();
        public float FinalErrorMm { get; set; }
        public int IterationsUsed { get; set; }
        public Vec3 ReachedPosition { get; set; }
    }

    /// <summary>
    /// Cyclic Coordinate Descent (CCD) Inverse Kinematics solver for multi-axis industrial robots.
    /// Supports N-DOF articulated manipulators with revolute and prismatic joint limit constraints.
    /// </summary>
    public static class NumericalInverseKinematics
    {
        /// <summary>
        /// Solves inverse kinematics iteratively to reach target 3D spatial position within specified tolerance.
        /// </summary>
        public static IkResult SolveCcd(
            RobotKinematics robot,
            Vec3 targetPosition,
            float[]? seedJoints = null,
            int maxIterations = 100,
            float toleranceMm = 1.0f,
            float damping = 0.75f)
        {
            int linkCount = robot.Links.Count;
            float[] joints = new float[linkCount];

            if (seedJoints != null)
            {
                for (int i = 0; i < Math.Min(seedJoints.Length, linkCount); i++)
                {
                    joints[i] = seedJoints[i];
                }
            }

            // Clamp seed joints to link limits
            for (int i = 0; i < linkCount; i++)
            {
                joints[i] = Math.Max(robot.Links[i].MinLimit, Math.Min(robot.Links[i].MaxLimit, joints[i]));
            }

            var fk = robot.ComputeForwardKinematics(joints);
            float error = Vec3.Distance(fk.EndEffectorPosition, targetPosition);
            if (error <= toleranceMm)
            {
                return new IkResult
                {
                    Success = true,
                    JointAngles = joints,
                    FinalErrorMm = error,
                    IterationsUsed = 0,
                    ReachedPosition = fk.EndEffectorPosition
                };
            }

            int iter = 0;
            for (iter = 0; iter < maxIterations; iter++)
            {
                // Iterate from end effector link backwards to base link
                for (int i = linkCount - 1; i >= 0; i--)
                {
                    var link = robot.Links[i];
                    fk = robot.ComputeForwardKinematics(joints);
                    var eePos = fk.EndEffectorPosition;

                    if (Vec3.Distance(eePos, targetPosition) <= toleranceMm)
                    {
                        return new IkResult
                        {
                            Success = true,
                            JointAngles = joints,
                            FinalErrorMm = Vec3.Distance(eePos, targetPosition),
                            IterationsUsed = iter + 1,
                            ReachedPosition = eePos
                        };
                    }

                    // Joint transform before link i
                    var jointTransform = fk.JointTransforms[i];
                    var jointPos = jointTransform.Translation;

                    // Joint Z-axis in world coordinates
                    var jointAxis = new Vec3(jointTransform.M31, jointTransform.M32, jointTransform.M33).Normalize();
                    if (jointAxis.LengthSquared() < 1e-4f)
                    {
                        jointAxis = Vec3.UnitZ;
                    }

                    if (link.Type == JointType.Revolute)
                    {
                        var toEnd = eePos - jointPos;
                        var toTarget = targetPosition - jointPos;

                        // Project onto plane perpendicular to joint rotation axis
                        var projEnd = toEnd - (jointAxis * Vec3.Dot(toEnd, jointAxis));
                        var projTarget = toTarget - (jointAxis * Vec3.Dot(toTarget, jointAxis));

                        float lenEnd = projEnd.Length();
                        float lenTarget = projTarget.Length();

                        if (lenEnd > 1e-4f && lenTarget > 1e-4f)
                        {
                            var uEnd = projEnd / lenEnd;
                            var uTarget = projTarget / lenTarget;

                            float cosAngle = Math.Max(-1.0f, Math.Min(1.0f, Vec3.Dot(uEnd, uTarget)));
                            var cross = Vec3.Cross(uEnd, uTarget);
                            float sinAngle = Vec3.Dot(cross, jointAxis);

                            float deltaAngleRad = (float)Math.Atan2(sinAngle, cosAngle);
                            float deltaAngleDeg = deltaAngleRad * (float)(180.0 / Math.PI) * damping;

                            float newAngle = joints[i] + deltaAngleDeg;
                            newAngle = Math.Max(link.MinLimit, Math.Min(link.MaxLimit, newAngle));
                            joints[i] = newAngle;
                        }
                    }
                    else if (link.Type == JointType.Prismatic)
                    {
                        var toEnd = eePos - jointPos;
                        var toTarget = targetPosition - jointPos;
                        float deltaD = Vec3.Dot(toTarget - toEnd, jointAxis) * damping;

                        float newD = joints[i] + deltaD;
                        newD = Math.Max(link.MinLimit, Math.Min(link.MaxLimit, newD));
                        joints[i] = newD;
                    }
                }
            }

            fk = robot.ComputeForwardKinematics(joints);
            error = Vec3.Distance(fk.EndEffectorPosition, targetPosition);

            return new IkResult
            {
                Success = error <= toleranceMm,
                JointAngles = joints,
                FinalErrorMm = error,
                IterationsUsed = maxIterations,
                ReachedPosition = fk.EndEffectorPosition
            };
        }
    }
}
