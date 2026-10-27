using System;
using System.Collections.Generic;

namespace ZeroMotion.Trajectory
{
    /// <summary>
    /// Multi-Axis Synchronized Jerk-Limited S-Curve Motion Profile Generator.
    /// Synchronizes N axes so that all axes start, accelerate, cruise, decelerate,
    /// and reach their respective target positions at the exact same moment (T_sync).
    /// Prevents jagged toolpaths, path distortion, and mechanical strain in multi-DOF manipulators.
    /// </summary>
    public class MultiAxisSCurvePlanner
    {
        public int AxisCount { get; }
        public double[] StartPositions { get; }
        public double[] TargetPositions { get; }
        public double[] MaxVelocities { get; }
        public double[] MaxAccelerations { get; }
        public double[] MaxJerks { get; }

        public double SynchronizedDuration { get; private set; }
        public SCurvePlanner?[] AxisPlanners { get; }

        public MultiAxisSCurvePlanner(
            double[] startPositions,
            double[] targetPositions,
            double[] maxVelocities,
            double[] maxAccelerations,
            double[] maxJerks)
        {
            if (startPositions == null) throw new ArgumentNullException(nameof(startPositions));
            if (targetPositions == null) throw new ArgumentNullException(nameof(targetPositions));
            if (maxVelocities == null) throw new ArgumentNullException(nameof(maxVelocities));
            if (maxAccelerations == null) throw new ArgumentNullException(nameof(maxAccelerations));
            if (maxJerks == null) throw new ArgumentNullException(nameof(maxJerks));

            int count = startPositions.Length;
            if (targetPositions.Length != count || maxVelocities.Length != count ||
                maxAccelerations.Length != count || maxJerks.Length != count)
            {
                throw new ArgumentException("All input arrays must have the same length (AxisCount).");
            }

            AxisCount = count;
            StartPositions = (double[])startPositions.Clone();
            TargetPositions = (double[])targetPositions.Clone();
            MaxVelocities = (double[])maxVelocities.Clone();
            MaxAccelerations = (double[])maxAccelerations.Clone();
            MaxJerks = (double[])maxJerks.Clone();
            AxisPlanners = new SCurvePlanner?[count];

            PlanSynchronized();
        }

        private void PlanSynchronized()
        {
            if (AxisCount == 0)
            {
                SynchronizedDuration = 0;
                return;
            }

            // Step 1: Calculate standalone S-Curve profile for each axis to find bottleneck duration
            double maxDuration = 0.0;
            var standaloneDurations = new double[AxisCount];

            for (int i = 0; i < AxisCount; i++)
            {
                double dist = System.Math.Abs(TargetPositions[i] - StartPositions[i]);
                if (dist > 1e-9)
                {
                    var p = new SCurvePlanner(StartPositions[i], TargetPositions[i], MaxVelocities[i], MaxAccelerations[i], MaxJerks[i]);
                    standaloneDurations[i] = p.TotalDuration;
                    if (p.TotalDuration > maxDuration)
                    {
                        maxDuration = p.TotalDuration;
                    }
                }
                else
                {
                    standaloneDurations[i] = 0.0;
                }
            }

            SynchronizedDuration = maxDuration;
            if (SynchronizedDuration < 1e-9)
            {
                return;
            }

            // Step 2: Time-scale all faster axes to match the bottleneck duration T_sync
            for (int i = 0; i < AxisCount; i++)
            {
                double dist = System.Math.Abs(TargetPositions[i] - StartPositions[i]);
                if (dist <= 1e-9 || standaloneDurations[i] <= 1e-9)
                {
                    AxisPlanners[i] = null;
                    continue;
                }

                double alpha = standaloneDurations[i] / SynchronizedDuration;
                if (alpha >= 0.999999)
                {
                    // Bottleneck axis — uses native limits
                    AxisPlanners[i] = new SCurvePlanner(StartPositions[i], TargetPositions[i], MaxVelocities[i], MaxAccelerations[i], MaxJerks[i]);
                }
                else
                {
                    // Scale limits down: v' = v * alpha, a' = a * alpha^2, j' = j * alpha^3
                    double scaledV = System.Math.Max(1e-6, MaxVelocities[i] * alpha);
                    double scaledA = System.Math.Max(1e-6, MaxAccelerations[i] * alpha * alpha);
                    double scaledJ = System.Math.Max(1e-6, MaxJerks[i] * alpha * alpha * alpha);

                    AxisPlanners[i] = new SCurvePlanner(StartPositions[i], TargetPositions[i], scaledV, scaledA, scaledJ);
                }
            }
        }

        /// <summary>
        /// Evaluates all synchronized axes at time t (seconds).
        /// Returns arrays of positions, velocities, and accelerations for all axes.
        /// </summary>
        public (double[] Positions, double[] Velocities, double[] Accelerations) Evaluate(double t)
        {
            var pos = new double[AxisCount];
            var vel = new double[AxisCount];
            var acc = new double[AxisCount];

            for (int i = 0; i < AxisCount; i++)
            {
                var planner = AxisPlanners[i];
                if (planner == null)
                {
                    pos[i] = StartPositions[i];
                    vel[i] = 0.0;
                    acc[i] = 0.0;
                }
                else
                {
                    var sample = planner.Evaluate(t);
                    pos[i] = sample.Position;
                    vel[i] = sample.Velocity;
                    acc[i] = sample.Acceleration;
                }
            }

            return (pos, vel, acc);
        }
    }
}
