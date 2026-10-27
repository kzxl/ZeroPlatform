using System;

namespace ZeroMotion.Trajectory
{
    /// <summary>
    /// 7-Phase Jerk-Limited S-Curve Motion Profile Generator.
    /// Eliminates mechanical resonance, vibration, and gear backlash by constraining jerk (da/dt &lt;= J_max).
    /// </summary>
    public class SCurvePlanner
    {
        public double StartPosition { get; set; }
        public double TargetPosition { get; set; }
        public double StartVelocity { get; set; }
        public double EndVelocity { get; set; }
        public double MaxVelocity { get; set; }     // v_max > 0
        public double MaxAcceleration { get; set; } // a_max > 0
        public double MaxJerk { get; set; }         // j_max > 0

        // Calculated phase durations
        public double Tj1 { get; private set; } // Jerk ramp-up/down time in acceleration phase
        public double Ta { get; private set; }  // Constant acceleration time
        public double Tj2 { get; private set; } // Jerk ramp-up/down time in deceleration phase
        public double Tv { get; private set; }  // Constant velocity cruise time
        public double Td { get; private set; }  // Constant deceleration time
        public double CruiseVelocity { get; private set; }
        public double TotalDuration { get; private set; }

        private double _dir = 1.0;
        private double _v0Dir;
        private double _v1Dir;
        private double _j1;
        private double _j2;
        private bool _hasNonZeroBoundaryVel;

        public SCurvePlanner(double startPos, double targetPos, double maxVel, double maxAcc, double maxJerk)
            : this(startPos, targetPos, 0.0, 0.0, maxVel, maxAcc, maxJerk)
        {
        }

        public SCurvePlanner(
            double startPos,
            double targetPos,
            double startVel,
            double endVel,
            double maxVel,
            double maxAcc,
            double maxJerk)
        {
            if (maxVel <= 0) throw new ArgumentOutOfRangeException(nameof(maxVel), "Max velocity must be positive.");
            if (maxAcc <= 0) throw new ArgumentOutOfRangeException(nameof(maxAcc), "Max acceleration must be positive.");
            if (maxJerk <= 0) throw new ArgumentOutOfRangeException(nameof(maxJerk), "Max jerk must be positive.");

            StartPosition = startPos;
            TargetPosition = targetPos;
            StartVelocity = startVel;
            EndVelocity = endVel;
            MaxVelocity = maxVel;
            MaxAcceleration = maxAcc;
            MaxJerk = maxJerk;

            _hasNonZeroBoundaryVel = System.Math.Abs(startVel) > 1e-9 || System.Math.Abs(endVel) > 1e-9;
            if (_hasNonZeroBoundaryVel)
            {
                PlanArbitraryVelocity();
            }
            else
            {
                Plan();
            }
        }

        /// <summary>
        /// Plans a 7-segment jerk-limited S-Curve trajectory with arbitrary initial and final velocities (v0, v1)
        /// for online trajectory retargeting and continuous motion blending.
        /// </summary>
        public static SCurvePlanner PlanWithVelocity(
            double startPos,
            double endPos,
            double startVel,
            double endVel,
            double maxVel,
            double maxAcc,
            double maxJerk)
        {
            return new SCurvePlanner(startPos, endPos, startVel, endVel, maxVel, maxAcc, maxJerk);
        }

        private void Plan()
        {
            double distance = System.Math.Abs(TargetPosition - StartPosition);
            _dir = TargetPosition >= StartPosition ? 1.0 : -1.0;
            if (distance < 1e-9)
            {
                Tj1 = Ta = Tj2 = Tv = Td = CruiseVelocity = TotalDuration = 0.0;
                return;
            }

            double vMax = MaxVelocity;
            double aMax = MaxAcceleration;
            double jMax = MaxJerk;

            // Check if aMax is reached
            if (vMax * jMax < aMax * aMax)
            {
                // Acceleration limit cannot be reached; reduce aMax
                aMax = System.Math.Sqrt(vMax * jMax);
            }

            Tj1 = aMax / jMax;
            Ta = (vMax / aMax) - Tj1;
            Tj2 = Tj1;

            // Distance covered during acceleration phase
            double sAccel = vMax * (Ta + Tj1);

            if (distance >= 2.0 * sAccel)
            {
                // Full cruise velocity is reached
                Tv = (distance - 2.0 * sAccel) / vMax;
                Td = Ta;
                CruiseVelocity = vMax;
            }
            else
            {
                // Cruise phase does not exist (Tv = 0)
                Tv = 0.0;
                // Recompute actual peak velocity reached
                double vAct = System.Math.Min(vMax, System.Math.Sqrt(distance * aMax));

                Ta = (vAct / aMax) - Tj1;
                if (Ta < 0.0)
                {
                    Ta = 0.0;
                    Tj1 = System.Math.Pow(distance * 0.5 / jMax, 1.0 / 3.0);
                    Tj2 = Tj1;
                    CruiseVelocity = jMax * Tj1 * Tj1;
                }
                else
                {
                    CruiseVelocity = vAct;
                }
                Td = Ta;
            }

            TotalDuration = 4.0 * Tj1 + 2.0 * Ta + Tv;
        }

        private void PlanArbitraryVelocity()
        {
            double rawDelta = TargetPosition - StartPosition;
            _dir = rawDelta >= 0.0 ? 1.0 : -1.0;
            double h = System.Math.Abs(rawDelta);

            _v0Dir = _dir * StartVelocity;
            _v1Dir = _dir * EndVelocity;

            double vMax = System.Math.Max(MaxVelocity, System.Math.Max(System.Math.Abs(_v0Dir), System.Math.Abs(_v1Dir)));
            double aMax = MaxAcceleration;
            double jMax = MaxJerk;

            if (h < 1e-9 && System.Math.Abs(_v0Dir - _v1Dir) < 1e-9)
            {
                Tj1 = Ta = Tj2 = Tv = Td = TotalDuration = 0.0;
                CruiseVelocity = _v0Dir;
                return;
            }

            ComputePhaseParams(_v0Dir, vMax, aMax, jMax, out double tj1Max, out double taMax, out double j1Max, out double sAccMax);
            ComputePhaseParams(vMax, _v1Dir, aMax, jMax, out double tj2Max, out double tdMax, out double j2Max, out double sDecMax);

            if (sAccMax + sDecMax <= h)
            {
                // Full cruise velocity vMax is reached
                Tj1 = tj1Max;
                Ta = taMax;
                _j1 = j1Max;
                Tj2 = tj2Max;
                Td = tdMax;
                _j2 = j2Max;
                CruiseVelocity = vMax;
                Tv = vMax > 1e-12 ? (h - (sAccMax + sDecMax)) / vMax : 0.0;
            }
            else
            {
                // Cruise phase is zero (Tv = 0); find peak/valley velocity vLim via bisection
                Tv = 0.0;
                double vLow = -vMax;
                double vHigh = vMax;

                // Bisect on monotonically increasing displacement function S(vLim) = sAcc(v0, vLim) + sDec(vLim, v1)
                double vBest = 0.5 * (_v0Dir + _v1Dir);
                for (int iter = 0; iter < 64; iter++)
                {
                    double vMid = 0.5 * (vLow + vHigh);
                    ComputePhaseParams(_v0Dir, vMid, aMax, jMax, out _, out _, out _, out double sAcc);
                    ComputePhaseParams(vMid, _v1Dir, aMax, jMax, out _, out _, out _, out double sDec);
                    double sTot = sAcc + sDec;

                    if (sTot < h)
                    {
                        vLow = vMid;
                    }
                    else
                    {
                        vHigh = vMid;
                    }
                    vBest = vMid;
                }

                CruiseVelocity = vBest;
                ComputePhaseParams(_v0Dir, CruiseVelocity, aMax, jMax, out double tj1, out double ta, out double j1, out _);
                ComputePhaseParams(CruiseVelocity, _v1Dir, aMax, jMax, out double tj2, out double td, out double j2, out _);

                Tj1 = tj1;
                Ta = ta;
                _j1 = j1;
                Tj2 = tj2;
                Td = td;
                _j2 = j2;
            }

            TotalDuration = 2.0 * Tj1 + Ta + Tv + 2.0 * Tj2 + Td;
        }

        private static void ComputePhaseParams(
            double vStart,
            double vEnd,
            double aMax,
            double jMax,
            out double tj,
            out double tConstAcc,
            out double signedJerk,
            out double displacement)
        {
            double dv = vEnd - vStart;
            double absDv = System.Math.Abs(dv);
            if (absDv < 1e-12)
            {
                tj = 0.0;
                tConstAcc = 0.0;
                signedJerk = 0.0;
                displacement = 0.0;
                return;
            }

            signedJerk = dv >= 0.0 ? jMax : -jMax;
            if (absDv * jMax <= aMax * aMax)
            {
                tj = System.Math.Sqrt(absDv / jMax);
                tConstAcc = 0.0;
            }
            else
            {
                tj = aMax / jMax;
                tConstAcc = (absDv / aMax) - tj;
            }

            double duration = 2.0 * tj + tConstAcc;
            displacement = 0.5 * (vStart + vEnd) * duration;
        }

        /// <summary>
        /// Evaluates current position, velocity, and acceleration at time t (seconds).
        /// </summary>
        public (double Position, double Velocity, double Acceleration) Evaluate(double t)
        {
            if (TotalDuration < 1e-9)
            {
                return (TargetPosition, EndVelocity, 0.0);
            }

            if (_hasNonZeroBoundaryVel)
            {
                return EvaluateArbitraryVelocity(t);
            }

            double dir = TargetPosition >= StartPosition ? 1.0 : -1.0;
            double s0 = StartPosition;
            double j = MaxJerk;
            double a = MaxAcceleration;
            double v = MaxVelocity;

            t = System.Math.Max(0.0, System.Math.Min(TotalDuration, t));

            double t1 = Tj1;
            double t2 = t1 + Ta;
            double t3 = t2 + Tj1;
            double t4 = t3 + Tv;
            double t5 = t4 + Tj1;
            double t6 = t5 + Td;
            double t7 = TotalDuration;

            double pos, vel, acc;

            if (t <= t1)
            {
                // Phase 1: Jerk ramp-up
                acc = j * t;
                vel = 0.5 * j * t * t;
                pos = s0 + dir * (j * t * t * t / 6.0);
            }
            else if (t <= t2)
            {
                // Phase 2: Constant acceleration
                double dt = t - t1;
                double v1 = 0.5 * j * t1 * t1;
                double s1 = j * t1 * t1 * t1 / 6.0;

                acc = a;
                vel = v1 + a * dt;
                pos = s0 + dir * (s1 + v1 * dt + 0.5 * a * dt * dt);
            }
            else if (t <= t3)
            {
                // Phase 3: Jerk ramp-down to cruise
                double dt = t - t2;
                double v1 = 0.5 * j * t1 * t1;
                double v2 = v1 + a * Ta;
                double s1 = j * t1 * t1 * t1 / 6.0;
                double s2 = s1 + v1 * Ta + 0.5 * a * Ta * Ta;

                acc = a - j * dt;
                vel = v2 + a * dt - 0.5 * j * dt * dt;
                pos = s0 + dir * (s2 + v2 * dt + 0.5 * a * dt * dt - (j * dt * dt * dt / 6.0));
            }
            else if (t <= t4)
            {
                // Phase 4: Cruise velocity
                double dt = t - t3;
                double v3 = v;
                double s3 = 0.5 * (TargetPosition - StartPosition) - 0.5 * v * Tv;

                acc = 0.0;
                vel = v3;
                pos = s0 + dir * (System.Math.Abs(s3) + v3 * dt);
            }
            else if (t <= t5)
            {
                // Phase 5: Deceleration jerk ramp-up
                double dt = t - t4;
                acc = -j * dt;
                vel = v - 0.5 * j * dt * dt;
                pos = TargetPosition - dir * (v * (t7 - t) - (j * System.Math.Pow(t7 - t, 3) / 6.0));
            }
            else if (t <= t6)
            {
                // Phase 6: Constant deceleration
                double dt = t - t5;
                acc = -a;
                vel = v - 0.5 * j * Tj1 * Tj1 - a * dt;
                pos = TargetPosition - dir * (0.5 * a * (t7 - t) * (t7 - t));
            }
            else
            {
                // Phase 7: Deceleration jerk ramp-down to zero
                double dt = t7 - t;
                acc = -j * dt;
                vel = 0.5 * j * dt * dt;
                pos = TargetPosition - dir * (j * dt * dt * dt / 6.0);
            }

            return (pos, vel * dir, acc * dir);
        }

        private (double Position, double Velocity, double Acceleration) EvaluateArbitraryVelocity(double t)
        {
            t = System.Math.Max(0.0, System.Math.Min(TotalDuration, t));

            double[] durations = new double[] { Tj1, Ta, Tj1, Tv, Tj2, Td, Tj2 };
            double aPeak1 = _j1 * Tj1;
            double aPeak2 = _j2 * Tj2;

            double[] jerks = new double[] { _j1, 0.0, -_j1, 0.0, _j2, 0.0, -_j2 };
            double[] initAcc = new double[] { 0.0, aPeak1, aPeak1, 0.0, 0.0, aPeak2, aPeak2 };

            double curPos = 0.0;
            double curVel = _v0Dir;
            double elapsed = 0.0;

            for (int seg = 0; seg < 7; seg++)
            {
                double segDur = durations[seg];
                double dt = t - elapsed;

                if (dt <= segDur || seg == 6)
                {
                    dt = System.Math.Max(0.0, System.Math.Min(segDur, dt));
                    double a0 = initAcc[seg];
                    double j = jerks[seg];

                    double acc = a0 + j * dt;
                    double vel = curVel + a0 * dt + 0.5 * j * dt * dt;
                    double pos = curPos + curVel * dt + 0.5 * a0 * dt * dt + (j * dt * dt * dt) / 6.0;

                    return (StartPosition + _dir * pos, _dir * vel, _dir * acc);
                }

                // Advance state to end of segment
                double aSeg = initAcc[seg];
                double jSeg = jerks[seg];
                curPos += curVel * segDur + 0.5 * aSeg * segDur * segDur + (jSeg * segDur * segDur * segDur) / 6.0;
                curVel += aSeg * segDur + 0.5 * jSeg * segDur * segDur;
                elapsed += segDur;
            }

            return (TargetPosition, EndVelocity, 0.0);
        }
    }
}
