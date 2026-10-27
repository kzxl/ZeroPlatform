using System;
using System.Collections.Generic;

namespace ZeroTwin3D.Kinematics
{
    public enum TrajectoryProfile
    {
        Linear,
        CubicSpline,
        Trapezoidal
    }

    /// <summary>
    /// Waypoint definition for a multi-joint robot trajectory at a specific timestamp.
    /// </summary>
    public class TrajectoryWaypoint
    {
        public double TimeSeconds { get; }
        public float[] JointValues { get; }

        public TrajectoryWaypoint(double timeSeconds, float[] jointValues)
        {
            TimeSeconds = timeSeconds;
            JointValues = (float[])jointValues.Clone();
        }
    }

    /// <summary>
    /// Interpolated motion trajectory sample containing joint positions and velocities.
    /// </summary>
    public struct TrajectorySample
    {
        public double TimeSeconds;
        public float[] Positions;
        public float[] Velocities;

        public TrajectorySample(double timeSeconds, float[] positions, float[] velocities)
        {
            TimeSeconds = timeSeconds;
            Positions = positions;
            Velocities = velocities;
        }
    }

    /// <summary>
    /// Trajectory planner and interpolator for smooth kinematic motion simulation in 3D Digital Twins.
    /// Supports Linear, Cubic Hermite Spline, and Trapezoidal velocity profiles.
    /// </summary>
    public class TrajectoryPlanner
    {
        private readonly List<TrajectoryWaypoint> _waypoints = new List<TrajectoryWaypoint>();
        private readonly List<float[]> _tangentVelocities = new List<float[]>();
        private bool _isDirty = true;

        public TrajectoryProfile Profile { get; set; } = TrajectoryProfile.CubicSpline;
        public int WaypointCount => _waypoints.Count;

        public double TotalDuration => _waypoints.Count > 0 ? _waypoints[_waypoints.Count - 1].TimeSeconds : 0.0;

        public TrajectoryPlanner AddWaypoint(double timeSeconds, float[] jointValues)
        {
            if (_waypoints.Count > 0 && timeSeconds <= _waypoints[_waypoints.Count - 1].TimeSeconds)
            {
                throw new ArgumentException("Waypoint timestamps must be strictly ascending.");
            }

            _waypoints.Add(new TrajectoryWaypoint(timeSeconds, jointValues));
            _isDirty = true;
            return this;
        }

        public void Clear()
        {
            _waypoints.Clear();
            _tangentVelocities.Clear();
            _isDirty = true;
        }

        private void Prepare()
        {
            if (!_isDirty || _waypoints.Count < 2)
            {
                _isDirty = false;
                return;
            }

            int n = _waypoints.Count;
            int dof = _waypoints[0].JointValues.Length;

            _tangentVelocities.Clear();
            for (int i = 0; i < n; i++)
            {
                _tangentVelocities.Add(new float[dof]);
            }

            // Catmull-Rom finite difference tangents for cubic hermite spline
            for (int d = 0; d < dof; d++)
            {
                // Boundary conditions: stationary at endpoints
                _tangentVelocities[0][d] = 0f;
                _tangentVelocities[n - 1][d] = 0f;

                for (int i = 1; i < n - 1; i++)
                {
                    double dt = _waypoints[i + 1].TimeSeconds - _waypoints[i - 1].TimeSeconds;
                    if (dt > 1e-6)
                    {
                        float dp = _waypoints[i + 1].JointValues[d] - _waypoints[i - 1].JointValues[d];
                        _tangentVelocities[i][d] = (float)(dp / dt);
                    }
                }
            }

            _isDirty = false;
        }

        /// <summary>
        /// Evaluates the trajectory at specified time, returning interpolated joint positions and velocities.
        /// </summary>
        public TrajectorySample Evaluate(double timeSeconds)
        {
            if (_waypoints.Count == 0)
            {
                return new TrajectorySample(timeSeconds, Array.Empty<float>(), Array.Empty<float>());
            }

            int dof = _waypoints[0].JointValues.Length;
            if (_waypoints.Count == 1 || timeSeconds <= _waypoints[0].TimeSeconds)
            {
                return new TrajectorySample(timeSeconds, (float[])_waypoints[0].JointValues.Clone(), new float[dof]);
            }

            if (timeSeconds >= _waypoints[_waypoints.Count - 1].TimeSeconds)
            {
                return new TrajectorySample(timeSeconds, (float[])_waypoints[_waypoints.Count - 1].JointValues.Clone(), new float[dof]);
            }

            Prepare();

            // Find segment [i, i+1]
            int seg = 0;
            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                if (timeSeconds >= _waypoints[i].TimeSeconds && timeSeconds <= _waypoints[i + 1].TimeSeconds)
                {
                    seg = i;
                    break;
                }
            }

            var w0 = _waypoints[seg];
            var w1 = _waypoints[seg + 1];
            double dt = w1.TimeSeconds - w0.TimeSeconds;
            if (dt < 1e-6) dt = 1e-6;

            double u = (timeSeconds - w0.TimeSeconds) / dt;
            u = Math.Max(0.0, Math.Min(1.0, u));

            float[] pos = new float[dof];
            float[] vel = new float[dof];

            switch (Profile)
            {
                case TrajectoryProfile.Linear:
                    for (int d = 0; d < dof; d++)
                    {
                        pos[d] = (float)(w0.JointValues[d] + u * (w1.JointValues[d] - w0.JointValues[d]));
                        vel[d] = (float)((w1.JointValues[d] - w0.JointValues[d]) / dt);
                    }
                    break;

                case TrajectoryProfile.Trapezoidal:
                    // Smooth S-curve transition using smoothstep polynomial: 3u^2 - 2u^3
                    double s = u * u * (3.0 - 2.0 * u);
                    double ds = (6.0 * u - 6.0 * u * u) / dt;
                    for (int d = 0; d < dof; d++)
                    {
                        float delta = w1.JointValues[d] - w0.JointValues[d];
                        pos[d] = (float)(w0.JointValues[d] + s * delta);
                        vel[d] = (float)(ds * delta);
                    }
                    break;

                case TrajectoryProfile.CubicSpline:
                default:
                    // Cubic Hermite Spline basis functions
                    double u2 = u * u;
                    double u3 = u2 * u;

                    double h00 = 2.0 * u3 - 3.0 * u2 + 1.0;
                    double h10 = u3 - 2.0 * u2 + u;
                    double h01 = -2.0 * u3 + 3.0 * u2;
                    double h11 = u3 - u2;

                    double dh00 = (6.0 * u2 - 6.0 * u) / dt;
                    double dh10 = (3.0 * u2 - 4.0 * u + 1.0);
                    double dh01 = (-6.0 * u2 + 6.0 * u) / dt;
                    double dh11 = (3.0 * u2 - 2.0 * u);

                    var m0 = _tangentVelocities[seg];
                    var m1 = _tangentVelocities[seg + 1];

                    for (int d = 0; d < dof; d++)
                    {
                        pos[d] = (float)(h00 * w0.JointValues[d] +
                                         h10 * dt * m0[d] +
                                         h01 * w1.JointValues[d] +
                                         h11 * dt * m1[d]);

                        vel[d] = (float)(dh00 * w0.JointValues[d] +
                                         dh10 * m0[d] +
                                         dh01 * w1.JointValues[d] +
                                         dh11 * m1[d]);
                    }
                    break;
            }

            return new TrajectorySample(timeSeconds, pos, vel);
        }
    }
}
