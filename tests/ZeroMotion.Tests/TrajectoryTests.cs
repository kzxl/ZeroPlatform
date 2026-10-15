using System;
using Xunit;
using ZeroMotion.Trajectory;

namespace ZeroMotion.Tests
{
    public class TrajectoryTests
    {
        [Fact]
        public void TrajectoryPlanner_InterpolatesWaypointsContinuously()
        {
            var planner = new TrajectoryPlanner { Profile = TrajectoryProfile.CubicSpline };
            planner.AddWaypoint(0.0, new float[] { 0f, 0f, 0f });
            planner.AddWaypoint(1.0, new float[] { 45f, -30f, 90f });
            planner.AddWaypoint(2.0, new float[] { 90f, 0f, 180f });

            Assert.Equal(3, planner.WaypointCount);
            Assert.Equal(2.0, planner.TotalDuration);

            // At t = 0
            var s0 = planner.Evaluate(0.0);
            Assert.Equal(0f, s0.Positions[0], 2);
            Assert.Equal(0f, s0.Velocities[0], 2);

            // At t = 1.0 (exact waypoint 1)
            var s1 = planner.Evaluate(1.0);
            Assert.Equal(45f, s1.Positions[0], 2);
            Assert.Equal(-30f, s1.Positions[1], 2);
            Assert.Equal(90f, s1.Positions[2], 2);

            // At t = 2.0 (end)
            var sEnd = planner.Evaluate(2.0);
            Assert.Equal(90f, sEnd.Positions[0], 2);
            Assert.Equal(0f, sEnd.Velocities[0], 2);
        }

        [Fact]
        public void TrajectoryPlanner_QuinticSplineEnsuresSmoothDerivatives()
        {
            var planner = new TrajectoryPlanner { Profile = TrajectoryProfile.QuinticSpline };
            planner.AddWaypoint(0.0, new float[] { 10f, 20f });
            planner.AddWaypoint(1.0, new float[] { 100f, 200f });

            var s0 = planner.Evaluate(0.0);
            Assert.Equal(10f, s0.Positions[0], 2);
            Assert.Equal(0f, s0.Velocities[0], 2);
            Assert.Equal(0f, s0.Accelerations[0], 2);

            var sMid = planner.Evaluate(0.5);
            Assert.Equal(55f, sMid.Positions[0], 1); // Exact midpoint by symmetry
            Assert.True(sMid.Velocities[0] > 0f);

            var s1 = planner.Evaluate(1.0);
            Assert.Equal(100f, s1.Positions[0], 2);
            Assert.Equal(0f, s1.Velocities[0], 2);
            Assert.Equal(0f, s1.Accelerations[0], 2);
        }

        [Fact]
        public void SCurvePlanner_RespectsVelocityAndJerkConstraints()
        {
            double startPos = 0.0;
            double targetPos = 500.0; // 500 mm move
            double maxVel = 200.0;    // 200 mm/s
            double maxAcc = 500.0;    // 500 mm/s^2
            double maxJerk = 2000.0;  // 2000 mm/s^3

            var scurve = new SCurvePlanner(startPos, targetPos, maxVel, maxAcc, maxJerk);
            Assert.True(scurve.TotalDuration > 0.0);

            // Sample across 100 time points
            int steps = 100;
            double dt = scurve.TotalDuration / steps;

            double maxObservedVel = 0.0;
            double maxObservedAcc = 0.0;

            for (int i = 0; i <= steps; i++)
            {
                double t = i * dt;
                var (pos, vel, acc) = scurve.Evaluate(t);

                if (i == 0)
                {
                    Assert.Equal(startPos, pos, 2);
                    Assert.Equal(0.0, vel, 2);
                }
                else if (i == steps)
                {
                    Assert.Equal(targetPos, pos, 2);
                    Assert.Equal(0.0, vel, 2);
                }

                maxObservedVel = System.Math.Max(maxObservedVel, System.Math.Abs(vel));
                maxObservedAcc = System.Math.Max(maxObservedAcc, System.Math.Abs(acc));
            }

            // Verify physical constraints
            Assert.True(maxObservedVel <= maxVel + 1e-3, $"Observed vel {maxObservedVel} exceeded limit {maxVel}");
            Assert.True(maxObservedAcc <= maxAcc + 1e-3, $"Observed acc {maxObservedAcc} exceeded limit {maxAcc}");
        }

        [Fact]
        public void MultiAxisSCurvePlanner_SynchronizesArrivalTimes()
        {
            // 3-axis motion (e.g. X: 300mm, Y: 100mm, Z: 50mm)
            var start = new double[] { 0, 0, 0 };
            var target = new double[] { 300, 100, 50 };
            var maxVel = new double[] { 150, 150, 150 };
            var maxAcc = new double[] { 400, 400, 400 };
            var maxJerk = new double[] { 2000, 2000, 2000 };

            var multi = new MultiAxisSCurvePlanner(start, target, maxVel, maxAcc, maxJerk);
            Assert.True(multi.SynchronizedDuration > 0.0);

            // Sample at completion time
            var (posEnd, velEnd, accEnd) = multi.Evaluate(multi.SynchronizedDuration);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(target[i], posEnd[i], 1);
                Assert.Equal(0.0, velEnd[i], 1);
            }

            // Sample halfway: all axes should be in progress
            var (posMid, velMid, _) = multi.Evaluate(multi.SynchronizedDuration * 0.5);
            for (int i = 0; i < 3; i++)
            {
                Assert.True(posMid[i] > start[i] && posMid[i] < target[i]);
                Assert.True(velMid[i] > 0.0);
            }
        }

        [Fact]
        public void CartesianTrajectoryPlanner_InterpolatesPositionAndSlerp()
        {
            var p0 = new ZeroMotion.Math.Vec3(100f, 200f, 300f);
            var q0 = ZeroMotion.Math.Quat.Identity;
            var p1 = new ZeroMotion.Math.Vec3(400f, 200f, 600f);
            var q1 = ZeroMotion.Math.Quat.FromEuler(0f, (float)System.Math.PI * 0.5f, 0f);

            var planner = new CartesianTrajectoryPlanner(p0, q0, p1, q1, maxLinearVel: 200, maxLinearAcc: 500, maxLinearJerk: 2000);
            Assert.True(planner.TotalDuration > 0.0);
            Assert.True(planner.LinearDistance > 0.0);

            // Start
            var w0 = planner.Evaluate(0.0);
            Assert.Equal(p0.X, w0.Position.X, 2);
            Assert.Equal(0.0, w0.Progress, 2);

            // End
            var wEnd = planner.Evaluate(planner.TotalDuration);
            Assert.Equal(p1.X, wEnd.Position.X, 2);
            Assert.Equal(p1.Z, wEnd.Position.Z, 2);
            Assert.Equal(1.0, wEnd.Progress, 2);
        }

        [Fact]
        public void SCurvePlanner_PlanWithVelocity_HandlesNonZeroBoundaryVelocitiesAccurately()
        {
            double startPos = 50.0;
            double endPos = 450.0;
            double startVel = 60.0;
            double endVel = 90.0;
            double maxVel = 200.0;
            double maxAcc = 500.0;
            double maxJerk = 2000.0;

            var scurve = SCurvePlanner.PlanWithVelocity(startPos, endPos, startVel, endVel, maxVel, maxAcc, maxJerk);
            Assert.True(scurve.TotalDuration > 0.0);

            var (p0, v0, a0) = scurve.Evaluate(0.0);
            Assert.Equal(startPos, p0, 2);
            Assert.Equal(startVel, v0, 2);
            Assert.Equal(0.0, a0, 2);

            var (pEnd, vEnd, aEnd) = scurve.Evaluate(scurve.TotalDuration);
            Assert.Equal(endPos, pEnd, 2);
            Assert.Equal(endVel, vEnd, 2);
            Assert.Equal(0.0, aEnd, 2);

            // Also test short distance without cruise phase (Tv == 0) and non-zero boundary velocities
            var shortCurve = SCurvePlanner.PlanWithVelocity(0.0, 25.0, 40.0, 20.0, 200.0, 400.0, 2000.0);
            Assert.True(shortCurve.TotalDuration > 0.0);
            Assert.Equal(0.0, shortCurve.Tv, 4);

            var (sp0, sv0, _) = shortCurve.Evaluate(0.0);
            var (sp1, sv1, sa1) = shortCurve.Evaluate(shortCurve.TotalDuration);
            Assert.Equal(0.0, sp0, 2);
            Assert.Equal(40.0, sv0, 2);
            Assert.Equal(25.0, sp1, 2);
            Assert.Equal(20.0, sv1, 2);
            Assert.Equal(0.0, sa1, 2);
        }
    }
}
