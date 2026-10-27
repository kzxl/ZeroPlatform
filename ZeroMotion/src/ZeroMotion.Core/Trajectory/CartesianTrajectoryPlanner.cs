using System;
using ZeroMotion.Math;

namespace ZeroMotion.Trajectory
{
    /// <summary>
    /// Represents a 6-DOF Cartesian waypoint consisting of 3D position and orientation quaternion.
    /// </summary>
    public readonly struct CartesianWaypoint
    {
        public Vec3 Position { get; }
        public Quat Orientation { get; }
        public Vec3 LinearVelocity { get; }
        public double Progress { get; } // 0.0 to 1.0

        public CartesianWaypoint(Vec3 position, Quat orientation, Vec3 linearVelocity, double progress)
        {
            Position = position;
            Orientation = orientation;
            LinearVelocity = linearVelocity;
            Progress = progress;
        }

        public Mat4 ToTransformMatrix()
        {
            var mat = Orientation.ToRotationMatrix();
            mat.Translation = Position;
            return mat;
        }
    }

    /// <summary>
    /// Cartesian Space Trajectory Planner (MoveL - Linear Interpolation).
    /// Generates straight-line paths in 3D Cartesian workspace coupled with
    /// gimbal-lock-free Spherical Linear Interpolation (SLERP) for orientation.
    /// Smooth motion is governed by a 7-phase jerk-limited S-curve along the path parameter.
    /// </summary>
    public class CartesianTrajectoryPlanner
    {
        public Vec3 StartPosition { get; }
        public Quat StartOrientation { get; }
        public Vec3 TargetPosition { get; }
        public Quat TargetOrientation { get; }

        public double LinearDistance { get; }
        public double TotalDuration => _sCurve?.TotalDuration ?? 0.0;

        private readonly SCurvePlanner? _sCurve;
        private readonly Vec3 _directionUnit;

        public CartesianTrajectoryPlanner(
            Vec3 startPos,
            Quat startRot,
            Vec3 targetPos,
            Quat targetRot,
            double maxLinearVel,
            double maxLinearAcc,
            double maxLinearJerk)
        {
            StartPosition = startPos;
            StartOrientation = startRot;
            TargetPosition = targetPos;
            TargetOrientation = targetRot;

            var delta = targetPos - startPos;
            LinearDistance = delta.Length();

            if (LinearDistance > 1e-7f)
            {
                _directionUnit = delta.Normalize();
                _sCurve = new SCurvePlanner(0.0, LinearDistance, maxLinearVel, maxLinearAcc, maxLinearJerk);
            }
            else
            {
                _directionUnit = Vec3.Zero;
                _sCurve = null;
            }
        }

        /// <summary>
        /// Evaluates the Cartesian waypoint at time t (seconds).
        /// </summary>
        public CartesianWaypoint Evaluate(double t)
        {
            if (_sCurve == null || LinearDistance < 1e-7)
            {
                return new CartesianWaypoint(TargetPosition, TargetOrientation, Vec3.Zero, 1.0);
            }

            var (pos, vel, _) = _sCurve.Evaluate(t);
            float progress = (float)System.Math.Max(0.0, System.Math.Min(1.0, pos / LinearDistance));

            var currentPos = StartPosition + _directionUnit * (float)pos;
            var currentRot = Quat.Slerp(StartOrientation, TargetOrientation, progress);
            var linearVel = _directionUnit * (float)vel;

            return new CartesianWaypoint(currentPos, currentRot, linearVel, progress);
        }
    }
}
