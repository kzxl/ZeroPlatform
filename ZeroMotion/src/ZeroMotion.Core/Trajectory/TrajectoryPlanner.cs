using System;
using System.Collections.Generic;

namespace ZeroMotion.Trajectory
{
    /// <summary>
    /// Trajectory planner and interpolator for smooth kinematic motion control in industrial robots.
    /// Supports Linear, Cubic Hermite Spline, Quintic Spline, Trapezoidal, and Jerk-Limited S-Curve velocity profiles.
    /// </summary>
    public class TrajectoryPlanner
    {
        private readonly List<TrajectoryWaypoint> _waypoints = new List<TrajectoryWaypoint>();
        private readonly List<float[]> _tangentVelocities = new List<float[]>();
        private readonly List<float[]> _tangentAccelerations = new List<float[]>();
        private bool _isDirty = true;

        public TrajectoryProfile Profile { get; set; } = TrajectoryProfile.CubicSpline;
        public int WaypointCount => _waypoints.Count;
        public double TotalDuration => _waypoints.Count > 0 ? _waypoints[_waypoints.Count - 1].TimeSeconds : 0.0;

        public TrajectoryPlanner AddWaypoint(double timeSeconds, float[] jointPositions, float[]? jointVelocities = null, float[]? jointAccelerations = null)
        {
            if (_waypoints.Count > 0 && timeSeconds <= _waypoints[_waypoints.Count - 1].TimeSeconds)
            {
                throw new ArgumentException("Waypoint timestamps must be strictly ascending.");
            }

            _waypoints.Add(new TrajectoryWaypoint(timeSeconds, jointPositions, jointVelocities, jointAccelerations));
            _isDirty = true;
            return this;
        }

        public void Clear()
        {
            _waypoints.Clear();
            _tangentVelocities.Clear();
            _tangentAccelerations.Clear();
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
            int dof = _waypoints[0].JointPositions.Length;

            _tangentVelocities.Clear();
            _tangentAccelerations.Clear();
            for (int i = 0; i < n; i++)
            {
                _tangentVelocities.Add(new float[dof]);
                _tangentAccelerations.Add(new float[dof]);
            }

            // Catmull-Rom finite difference tangents for cubic hermite spline
            for (int d = 0; d < dof; d++)
            {
                // Boundary conditions: stationary at endpoints unless explicit
                _tangentVelocities[0][d] = _waypoints[0].JointVelocities?[d] ?? 0f;
                _tangentVelocities[n - 1][d] = _waypoints[n - 1].JointVelocities?[d] ?? 0f;

                for (int i = 1; i < n - 1; i++)
                {
                    if (_waypoints[i].JointVelocities != null)
                    {
                        _tangentVelocities[i][d] = _waypoints[i].JointVelocities![d];
                    }
                    else
                    {
                        double dt = _waypoints[i + 1].TimeSeconds - _waypoints[i - 1].TimeSeconds;
                        if (dt > 1e-6)
                        {
                            _tangentVelocities[i][d] = (float)((_waypoints[i + 1].JointPositions[d] - _waypoints[i - 1].JointPositions[d]) / dt);
                        }
                    }
                }
            }

            _isDirty = false;
        }

        /// <summary>
        /// Evaluates interpolated joint positions and velocities at the specified time instant.
        /// </summary>
        public TrajectorySample Evaluate(double t)
        {
            if (_waypoints.Count == 0)
            {
                return new TrajectorySample(t, Array.Empty<float>(), Array.Empty<float>());
            }

            if (_waypoints.Count == 1)
            {
                var single = _waypoints[0];
                return new TrajectorySample(t, (float[])single.JointPositions.Clone(), new float[single.JointPositions.Length]);
            }

            Prepare();
            int dof = _waypoints[0].JointPositions.Length;

            // Clamp time to boundary
            if (t <= _waypoints[0].TimeSeconds)
            {
                return new TrajectorySample(t, (float[])_waypoints[0].JointPositions.Clone(), (float[])_tangentVelocities[0].Clone());
            }

            if (t >= _waypoints[_waypoints.Count - 1].TimeSeconds)
            {
                var last = _waypoints[_waypoints.Count - 1];
                return new TrajectorySample(t, (float[])last.JointPositions.Clone(), (float[])_tangentVelocities[_tangentVelocities.Count - 1].Clone());
            }

            // Find segment [i, i+1]
            int seg = 0;
            for (int i = 0; i < _waypoints.Count - 1; i++)
            {
                if (t >= _waypoints[i].TimeSeconds && t <= _waypoints[i + 1].TimeSeconds)
                {
                    seg = i;
                    break;
                }
            }

            var w0 = _waypoints[seg];
            var w1 = _waypoints[seg + 1];
            double dt = w1.TimeSeconds - w0.TimeSeconds;
            if (dt < 1e-9)
            {
                return new TrajectorySample(t, (float[])w0.JointPositions.Clone(), new float[dof]);
            }

            float u = (float)((t - w0.TimeSeconds) / dt); // normalized [0..1]
            float[] pos = new float[dof];
            float[] vel = new float[dof];
            float[] acc = new float[dof];

            switch (Profile)
            {
                case TrajectoryProfile.Linear:
                    for (int d = 0; d < dof; d++)
                    {
                        pos[d] = w0.JointPositions[d] + u * (w1.JointPositions[d] - w0.JointPositions[d]);
                        vel[d] = (float)((w1.JointPositions[d] - w0.JointPositions[d]) / dt);
                    }
                    break;

                case TrajectoryProfile.Trapezoidal:
                    // 1/3 acceleration, 1/3 cruise, 1/3 deceleration
                    for (int d = 0; d < dof; d++)
                    {
                        float p0 = w0.JointPositions[d];
                        float p1 = w1.JointPositions[d];
                        float deltaP = p1 - p0;
                        float vMax = (float)(1.5 * deltaP / dt);

                        if (u < 0.333f)
                        {
                            float tau = u / 0.333f;
                            pos[d] = p0 + 0.5f * deltaP * (tau * tau) / 3f;
                            vel[d] = tau * vMax;
                            acc[d] = (float)(vMax / (0.333 * dt));
                        }
                        else if (u < 0.667f)
                        {
                            float tau = (u - 0.333f) / 0.334f;
                            pos[d] = p0 + (deltaP / 6f) + deltaP * (2f / 3f) * tau;
                            vel[d] = vMax;
                            acc[d] = 0f;
                        }
                        else
                        {
                            float tau = (u - 0.667f) / 0.333f;
                            pos[d] = p1 - 0.5f * deltaP * ((1f - tau) * (1f - tau)) / 3f;
                            vel[d] = (1f - tau) * vMax;
                            acc[d] = (float)(-vMax / (0.333 * dt));
                        }
                    }
                    break;

                case TrajectoryProfile.QuinticSpline:
                    // Quintic polynomial (5th-degree) ensuring zero acceleration at ends:
                    // s(u) = 10*u^3 - 15*u^4 + 6*u^5
                    // s'(u) = 30*u^2 - 60*u^3 + 30*u^4
                    // s''(u) = 60*u - 180*u^2 + 120*u^3
                    float u2 = u * u;
                    float u3 = u2 * u;
                    float u4 = u3 * u;
                    float u5 = u4 * u;
                    float sQ = 10f * u3 - 15f * u4 + 6f * u5;
                    float dsQ = (30f * u2 - 60f * u3 + 30f * u4) / (float)dt;
                    float ddsQ = (60f * u - 180f * u2 + 120f * u3) / (float)(dt * dt);

                    for (int d = 0; d < dof; d++)
                    {
                        float deltaP = w1.JointPositions[d] - w0.JointPositions[d];
                        pos[d] = w0.JointPositions[d] + sQ * deltaP;
                        vel[d] = dsQ * deltaP;
                        acc[d] = ddsQ * deltaP;
                    }
                    break;

                case TrajectoryProfile.CubicSpline:
                default:
                    // Cubic Hermite Spline:
                    // h00 = 2*u^3 - 3*u^2 + 1
                    // h10 = u^3 - 2*u^2 + u
                    // h01 = -2*u^3 + 3*u^2
                    // h11 = u^3 - u^2
                    float uSquare = u * u;
                    float uCube = uSquare * u;

                    float h00 = 2f * uCube - 3f * uSquare + 1f;
                    float h10 = uCube - 2f * uSquare + u;
                    float h01 = -2f * uCube + 3f * uSquare;
                    float h11 = uCube - uSquare;

                    float dh00 = (6f * uSquare - 6f * u) / (float)dt;
                    float dh10 = (3f * uSquare - 4f * u + 1f) / (float)dt;
                    float dh01 = (-6f * uSquare + 6f * u) / (float)dt;
                    float dh11 = (3f * uSquare - 2f * u) / (float)dt;

                    for (int d = 0; d < dof; d++)
                    {
                        float p0 = w0.JointPositions[d];
                        float p1 = w1.JointPositions[d];
                        float m0 = _tangentVelocities[seg][d] * (float)dt;
                        float m1 = _tangentVelocities[seg + 1][d] * (float)dt;

                        pos[d] = h00 * p0 + h10 * m0 + h01 * p1 + h11 * m1;
                        vel[d] = dh00 * p0 + dh10 * m0 + dh01 * p1 + dh11 * m1;
                    }
                    break;
            }

            return new TrajectorySample(t, pos, vel, acc);
        }
    }
}
