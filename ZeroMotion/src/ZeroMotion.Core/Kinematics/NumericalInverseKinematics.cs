using System;
using ZeroMotion.Math;

namespace ZeroMotion.Kinematics
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
    /// Numerical Inverse Kinematics solver for multi-axis industrial robots.
    /// Implements Cyclic Coordinate Descent (CCD) and Damped Least Squares (DLS) algorithms.
    /// </summary>
    public static class NumericalInverseKinematics
    {
        /// <summary>
        /// Solves inverse kinematics iteratively using Cyclic Coordinate Descent (CCD) to reach a target 3D position.
        /// </summary>
        public static IkResult SolveCcd(
            RobotKinematics robot,
            Vec3 targetPosition,
            float[]? seedJoints = null,
            int maxIterations = 100,
            float toleranceMm = 1.0f,
            float damping = 0.75f)
        {
            if (robot == null) throw new ArgumentNullException(nameof(robot));
            int linkCount = robot.Links.Count;
            float[] joints = new float[linkCount];

            if (seedJoints != null)
            {
                for (int i = 0; i < System.Math.Min(seedJoints.Length, linkCount); i++)
                {
                    joints[i] = seedJoints[i];
                }
            }

            // Clamp seed joints to link limits
            for (int i = 0; i < linkCount; i++)
            {
                joints[i] = System.Math.Max(robot.Links[i].MinLimit, System.Math.Min(robot.Links[i].MaxLimit, joints[i]));
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

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // Iterate from end-effector link backwards to base link
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

                            float cosAngle = System.Math.Max(-1.0f, System.Math.Min(1.0f, Vec3.Dot(uEnd, uTarget)));
                            var cross = Vec3.Cross(uEnd, uTarget);
                            float sinAngle = Vec3.Dot(cross, jointAxis);

                            float deltaAngleRad = (float)System.Math.Atan2(sinAngle, cosAngle);
                            float deltaAngleDeg = deltaAngleRad * (float)(180.0 / System.Math.PI) * damping;

                            float newAngle = joints[i] + deltaAngleDeg;
                            newAngle = System.Math.Max(link.MinLimit, System.Math.Min(link.MaxLimit, newAngle));
                            joints[i] = newAngle;
                        }
                    }
                    else if (link.Type == JointType.Prismatic)
                    {
                        var toTarget = targetPosition - eePos;
                        float disp = Vec3.Dot(toTarget, jointAxis) * damping;
                        float newPos = joints[i] + disp;
                        newPos = System.Math.Max(link.MinLimit, System.Math.Min(link.MaxLimit, newPos));
                        joints[i] = newPos;
                    }
                }
            }

            fk = robot.ComputeForwardKinematics(joints);
            float finalError = Vec3.Distance(fk.EndEffectorPosition, targetPosition);

            return new IkResult
            {
                Success = finalError <= toleranceMm,
                JointAngles = joints,
                FinalErrorMm = finalError,
                IterationsUsed = maxIterations,
                ReachedPosition = fk.EndEffectorPosition
            };
        }
    }
}
