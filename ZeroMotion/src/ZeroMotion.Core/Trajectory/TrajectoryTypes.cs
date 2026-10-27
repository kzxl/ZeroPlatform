using System;

namespace ZeroMotion.Trajectory
{
    public enum TrajectoryProfile
    {
        Linear,
        CubicSpline,
        QuinticSpline,
        Trapezoidal,
        SCurve
    }

    /// <summary>
    /// Multi-joint robot trajectory waypoint at a specific timestamp.
    /// </summary>
    public class TrajectoryWaypoint
    {
        public double TimeSeconds { get; }
        public float[] JointPositions { get; }
        public float[]? JointVelocities { get; }
        public float[]? JointAccelerations { get; }

        public TrajectoryWaypoint(double timeSeconds, float[] jointPositions, float[]? jointVelocities = null, float[]? jointAccelerations = null)
        {
            TimeSeconds = timeSeconds;
            JointPositions = (float[])jointPositions.Clone();
            if (jointVelocities != null) JointVelocities = (float[])jointVelocities.Clone();
            if (jointAccelerations != null) JointAccelerations = (float[])jointAccelerations.Clone();
        }
    }

    /// <summary>
    /// Sampled motion trajectory state containing joint positions, velocities, accelerations, and jerk.
    /// </summary>
    public struct TrajectorySample
    {
        public double TimeSeconds;
        public float[] Positions;
        public float[] Velocities;
        public float[] Accelerations;

        public TrajectorySample(double timeSeconds, float[] positions, float[] velocities, float[]? accelerations = null)
        {
            TimeSeconds = timeSeconds;
            Positions = positions;
            Velocities = velocities;
            Accelerations = accelerations ?? new float[positions.Length];
        }
    }
}
