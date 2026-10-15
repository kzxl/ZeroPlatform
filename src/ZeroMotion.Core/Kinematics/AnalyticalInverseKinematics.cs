using System;
using ZeroMotion.Math;

namespace ZeroMotion.Kinematics
{
    /// <summary>
    /// Analytical closed-form Inverse Kinematics solver.
    /// Provides deterministic, sub-microsecond, exact solutions without numerical iterations for SCARA and spherical wrist manipulators.
    /// </summary>
    public static class AnalyticalInverseKinematics
    {
        public struct ScaraSolution
        {
            public bool IsReachable;
            public float Theta1Deg; // Shoulder
            public float Theta2Deg; // Elbow
            public float ZPrismatic; // Prismatic Z
            public float Theta4Deg;  // Tool Yaw
        }

        public struct WristSolution
        {
            public float Theta4Deg;
            public float Theta5Deg;
            public float Theta6Deg;
        }

        /// <summary>
        /// Solves exact closed-form inverse kinematics for a 4-Axis SCARA robot.
        /// </summary>
        /// <param name="l1">Length of link 1 (shoulder to elbow).</param>
        /// <param name="l2">Length of link 2 (elbow to wrist).</param>
        /// <param name="baseHeight">Base height along Z axis.</param>
        /// <param name="target">Target 3D position.</param>
        /// <param name="targetRollDeg">Desired tool roll angle in degrees.</param>
        /// <param name="elbowRight">True for elbow-right configuration, false for elbow-left.</param>
        public static ScaraSolution SolveScara(
            float l1, float l2, float baseHeight, Vec3 target, float targetRollDeg = 0f, bool elbowRight = true)
        {
            float x = target.X;
            float y = target.Y;
            float rSq = x * x + y * y;
            float r = (float)System.Math.Sqrt(rSq);

            // Reachability check
            if (r > (l1 + l2) || r < System.Math.Abs(l1 - l2))
            {
                return new ScaraSolution { IsReachable = false };
            }

            // Cosine rule for theta2
            float cosTheta2 = (rSq - l1 * l1 - l2 * l2) / (2f * l1 * l2);
            cosTheta2 = System.Math.Max(-1.0f, System.Math.Min(1.0f, cosTheta2));
            float sinTheta2 = (float)System.Math.Sqrt(System.Math.Max(0f, 1f - cosTheta2 * cosTheta2));
            if (!elbowRight) sinTheta2 = -sinTheta2;

            float theta2Rad = (float)System.Math.Atan2(sinTheta2, cosTheta2);

            // Theta1
            float beta = (float)System.Math.Atan2(y, x);
            float gamma = (float)System.Math.Atan2(l2 * sinTheta2, l1 + l2 * cosTheta2);
            float theta1Rad = beta - gamma;

            float theta1Deg = theta1Rad * 180f / (float)System.Math.PI;
            float theta2Deg = theta2Rad * 180f / (float)System.Math.PI;
            float zPrismatic = target.Z - baseHeight;
            float theta4Deg = targetRollDeg - (theta1Deg + theta2Deg);

            return new ScaraSolution
            {
                IsReachable = true,
                Theta1Deg = theta1Deg,
                Theta2Deg = theta2Deg,
                ZPrismatic = zPrismatic,
                Theta4Deg = theta4Deg
            };
        }

        /// <summary>
        /// Solves the orientation inverse kinematics for a 3-axis spherical wrist (Joints 4, 5, 6) given the wrist rotation matrix R_3_6.
        /// </summary>
        public static WristSolution SolveSphericalWrist(Mat4 r36)
        {
            float r13 = r36.M13;
            float r23 = r36.M23;
            float r33 = r36.M33;
            float r31 = r36.M31;
            float r32 = r36.M32;

            float theta5Rad = (float)System.Math.Atan2(System.Math.Sqrt(r13 * r13 + r23 * r23), r33);
            float theta4Rad, theta6Rad;

            if (System.Math.Abs(theta5Rad) > 1e-4f)
            {
                theta4Rad = (float)System.Math.Atan2(r23, r13);
                theta6Rad = (float)System.Math.Atan2(r32, -r31);
            }
            else
            {
                theta4Rad = 0f;
                theta6Rad = (float)System.Math.Atan2(-r36.M21, r36.M11);
            }

            return new WristSolution
            {
                Theta4Deg = theta4Rad * 180f / (float)System.Math.PI,
                Theta5Deg = theta5Rad * 180f / (float)System.Math.PI,
                Theta6Deg = theta6Rad * 180f / (float)System.Math.PI
            };
        }

        public struct Arm3DofSolution
        {
            public bool IsReachable;
            public float Theta1Deg;
            public float Theta2Deg;
            public float Theta3Deg;
        }

        public struct SixDofJointSolution
        {
            public float Theta1Deg;
            public float Theta2Deg;
            public float Theta3Deg;
            public float Theta4Deg;
            public float Theta5Deg;
            public float Theta6Deg;

            public float[] ToArray() => new float[] { Theta1Deg, Theta2Deg, Theta3Deg, Theta4Deg, Theta5Deg, Theta6Deg };
        }

        public struct SixDofIkResult
        {
            public bool IsReachable;
            public Vec3 WristCenter;
            public SixDofJointSolution NonFlipped;
            public SixDofJointSolution Flipped;
        }

        public struct DeltaIkSolution
        {
            public bool IsReachable;
            public float Theta1Deg;
            public float Theta2Deg;
            public float Theta3Deg;
        }

        public struct DeltaFkSolution
        {
            public bool IsValid;
            public Vec3 Position;
        }

        /// <summary>
        /// Solves closed-form 3-DOF anthropomorphic positioning arm IK (Joints 1, 2, 3) for a given wrist center position.
        /// </summary>
        public static Arm3DofSolution SolveArm3Dof(
            float d1,
            float a1,
            float a2,
            float d4,
            Vec3 wristCenter,
            bool elbowUp = true)
        {
            double wx = wristCenter.X;
            double wy = wristCenter.Y;
            double wz = wristCenter.Z;

            double theta1Rad = (wx * wx + wy * wy > 1e-12) ? System.Math.Atan2(wy, wx) : 0.0;
            double r = System.Math.Sqrt(wx * wx + wy * wy) - a1;
            double z = wz - d1;

            double distSq = r * r + z * z;
            double dist = System.Math.Sqrt(distSq);

            if (dist > (a2 + d4) + 1e-4 || dist < System.Math.Abs(a2 - d4) - 1e-4)
            {
                return new Arm3DofSolution { IsReachable = false };
            }

            double cosTheta3 = (distSq - (double)a2 * a2 - (double)d4 * d4) / (2.0 * a2 * d4);
            cosTheta3 = System.Math.Max(-1.0, System.Math.Min(1.0, cosTheta3));
            double sinTheta3 = System.Math.Sqrt(System.Math.Max(0.0, 1.0 - cosTheta3 * cosTheta3));
            if (elbowUp)
            {
                sinTheta3 = -sinTheta3;
            }

            double theta3Rad = System.Math.Atan2(sinTheta3, cosTheta3);
            double theta2Rad = System.Math.Atan2(z, r) - System.Math.Atan2(d4 * sinTheta3, a2 + d4 * cosTheta3);

            const double RadToDeg = 180.0 / System.Math.PI;
            return new Arm3DofSolution
            {
                IsReachable = true,
                Theta1Deg = (float)(theta1Rad * RadToDeg),
                Theta2Deg = (float)(theta2Rad * RadToDeg),
                Theta3Deg = (float)(theta3Rad * RadToDeg)
            };
        }

        /// <summary>
        /// Solves unified 6-DOF Spherical-Wrist Analytical Inverse Kinematics via Pieper's kinematic decoupling:
        /// 1. Decouples wrist center p_wc = p_ee - d6 * R_ee * z_hat
        /// 2. Solves positioning arm (theta1, theta2, theta3) via SolveArm3Dof
        /// 3. Computes R_36 = R_03^T * R_ee
        /// 4. Extracts both non-flipped (theta5 &gt;= 0) and flipped (theta5 &lt; 0) spherical wrist solutions.
        /// </summary>
        public static SixDofIkResult Solve6DofSphericalWrist(
            float d1,
            float a1,
            float a2,
            float d4,
            float d6,
            Vec3 targetPosition,
            Mat4 targetOrientation,
            bool elbowUp = true)
        {
            // Tool approach vector z_ee is column 3 of R_ee
            Vec3 zEe = new Vec3(targetOrientation.M13, targetOrientation.M23, targetOrientation.M33);
            if (zEe.LengthSquared() > 1e-6f)
            {
                zEe = zEe.Normalize();
            }
            else
            {
                zEe = Vec3.UnitZ;
            }

            Vec3 wristCenter = targetPosition - zEe * d6;

            Arm3DofSolution arm = SolveArm3Dof(d1, a1, a2, d4, wristCenter, elbowUp);
            if (!arm.IsReachable)
            {
                return new SixDofIkResult
                {
                    IsReachable = false,
                    WristCenter = wristCenter
                };
            }

            Mat4 r03 = ComputeArmRotationR03(arm.Theta1Deg, arm.Theta2Deg, arm.Theta3Deg);
            Mat4 r03Transpose = TransposeRotation3x3(r03);
            Mat4 r36 = MultiplyRotation3x3(r03Transpose, targetOrientation);

            WristSolution nonFlippedWrist = SolveSphericalWrist(r36);

            WristSolution flippedWrist = new WristSolution
            {
                Theta4Deg = WrapAngleDeg(nonFlippedWrist.Theta4Deg + 180f),
                Theta5Deg = -nonFlippedWrist.Theta5Deg,
                Theta6Deg = WrapAngleDeg(nonFlippedWrist.Theta6Deg + 180f)
            };

            return new SixDofIkResult
            {
                IsReachable = true,
                WristCenter = wristCenter,
                NonFlipped = new SixDofJointSolution
                {
                    Theta1Deg = arm.Theta1Deg,
                    Theta2Deg = arm.Theta2Deg,
                    Theta3Deg = arm.Theta3Deg,
                    Theta4Deg = nonFlippedWrist.Theta4Deg,
                    Theta5Deg = nonFlippedWrist.Theta5Deg,
                    Theta6Deg = nonFlippedWrist.Theta6Deg
                },
                Flipped = new SixDofJointSolution
                {
                    Theta1Deg = arm.Theta1Deg,
                    Theta2Deg = arm.Theta2Deg,
                    Theta3Deg = arm.Theta3Deg,
                    Theta4Deg = flippedWrist.Theta4Deg,
                    Theta5Deg = flippedWrist.Theta5Deg,
                    Theta6Deg = flippedWrist.Theta6Deg
                }
            };
        }

        /// <summary>
        /// Computes exact forward kinematics (end-effector position and 3x3 orientation in Mat4)
        /// for the 6-DOF spherical-wrist manipulator model paired with Solve6DofSphericalWrist.
        /// </summary>
        public static (Vec3 Position, Mat4 Orientation, Vec3 WristCenter) Compute6DofSphericalWristFk(
            float d1,
            float a1,
            float a2,
            float d4,
            float d6,
            SixDofJointSolution joints)
        {
            const double DegToRad = System.Math.PI / 180.0;
            double t1 = joints.Theta1Deg * DegToRad;
            double t2 = joints.Theta2Deg * DegToRad;
            double t23 = (joints.Theta2Deg + joints.Theta3Deg) * DegToRad;

            double r = a1 + a2 * System.Math.Cos(t2) + d4 * System.Math.Cos(t23);
            double z = d1 + a2 * System.Math.Sin(t2) + d4 * System.Math.Sin(t23);

            Vec3 wristCenter = new Vec3(
                (float)(r * System.Math.Cos(t1)),
                (float)(r * System.Math.Sin(t1)),
                (float)z);

            Mat4 r03 = ComputeArmRotationR03(joints.Theta1Deg, joints.Theta2Deg, joints.Theta3Deg);
            Mat4 r36 = ComputeSphericalWristRotationR36(joints.Theta4Deg, joints.Theta5Deg, joints.Theta6Deg);
            Mat4 rEe = MultiplyRotation3x3(r03, r36);

            Vec3 zEe = new Vec3(rEe.M13, rEe.M23, rEe.M33);
            Vec3 eePos = wristCenter + zEe * d6;
            rEe.Translation = eePos;

            return (eePos, rEe, wristCenter);
        }

        /// <summary>
        /// Solves closed-form Inverse Kinematics for a 3-DOF Clavel Delta parallel robot.
        /// Arms 1, 2, 3 are arranged symmetrically at 0, 120, and 240 degrees around the Z axis.
        /// </summary>
        /// <param name="baseRadius">Radius from fixed base center to actuated hip joints.</param>
        /// <param name="effectorRadius">Radius from moving platform center to ankle joints.</param>
        /// <param name="upperArmLength">Length of actuated upper arms (biceps).</param>
        /// <param name="forearmLength">Length of passive parallelogram forearms.</param>
        /// <param name="targetPosition">Target 3D position of moving platform (typically Z &lt; 0).</param>
        public static DeltaIkSolution SolveDeltaIk(
            float baseRadius,
            float effectorRadius,
            float upperArmLength,
            float forearmLength,
            Vec3 targetPosition)
        {
            double rOffset = (double)baseRadius - effectorRadius;
            double lu = upperArmLength;
            double lf = forearmLength;

            double[] azimuthRad = new double[]
            {
                0.0,
                2.0 * System.Math.PI / 3.0,
                4.0 * System.Math.PI / 3.0
            };

            float[] thetasDeg = new float[3];

            for (int i = 0; i < 3; i++)
            {
                double phi = azimuthRad[i];
                double cosPhi = System.Math.Cos(phi);
                double sinPhi = System.Math.Sin(phi);

                // Rotate target into arm i's radial XZ plane
                double xLocal = targetPosition.X * cosPhi + targetPosition.Y * sinPhi;
                double yLocal = -targetPosition.X * sinPhi + targetPosition.Y * cosPhi;
                double zLocal = targetPosition.Z;

                double x0 = xLocal - rOffset;
                double z0 = zLocal;

                double rProjSq = lf * lf - yLocal * yLocal;
                if (rProjSq < 0.0)
                {
                    return new DeltaIkSolution { IsReachable = false };
                }

                double rProj = System.Math.Sqrt(rProjSq);
                double dSq = x0 * x0 + z0 * z0;
                double d = System.Math.Sqrt(dSq);

                if (d < 1e-9 || d > lu + rProj + 1e-5 || d < System.Math.Abs(lu - rProj) - 1e-5)
                {
                    return new DeltaIkSolution { IsReachable = false };
                }

                // Circle-circle intersection between upper arm circle (radius lu at origin)
                // and projected forearm circle (radius rProj at (x0, z0))
                double a = (lu * lu - rProjSq + dSq) / (2.0 * d);
                double hSq = System.Math.Max(0.0, lu * lu - a * a);
                double h = System.Math.Sqrt(hSq);

                double um = a * x0 / d;
                double wm = a * z0 / d;

                // Two candidate elbow positions (u, w) = (lu * cos(theta), -lu * sin(theta))
                double u1 = um + h * z0 / d;
                double w1 = wm - h * x0 / d;

                double u2 = um - h * z0 / d;
                double w2 = wm + h * x0 / d;

                // Select outward-knee configuration (maximum radial extension u)
                double u = (u1 >= u2) ? u1 : u2;
                double w = (u1 >= u2) ? w1 : w2;

                double thetaRad = System.Math.Atan2(-w, u);
                thetasDeg[i] = (float)(thetaRad * 180.0 / System.Math.PI);
            }

            return new DeltaIkSolution
            {
                IsReachable = true,
                Theta1Deg = thetasDeg[0],
                Theta2Deg = thetasDeg[1],
                Theta3Deg = thetasDeg[2]
            };
        }

        /// <summary>
        /// Solves closed-form Forward Kinematics for a 3-DOF Clavel Delta parallel robot
        /// via analytical intersection of three spheres centered at the virtual elbow joints.
        /// </summary>
        public static DeltaFkSolution SolveDeltaFk(
            float baseRadius,
            float effectorRadius,
            float upperArmLength,
            float forearmLength,
            float theta1Deg,
            float theta2Deg,
            float theta3Deg)
        {
            const double DegToRad = System.Math.PI / 180.0;
            double rOffset = (double)baseRadius - effectorRadius;
            double lu = upperArmLength;
            double lf = forearmLength;

            double[] thetas = new double[]
            {
                theta1Deg * DegToRad,
                theta2Deg * DegToRad,
                theta3Deg * DegToRad
            };

            double[] azimuthRad = new double[]
            {
                0.0,
                2.0 * System.Math.PI / 3.0,
                4.0 * System.Math.PI / 3.0
            };

            double[] cx = new double[3];
            double[] cy = new double[3];
            double[] cz = new double[3];
            double[] w = new double[3];

            for (int i = 0; i < 3; i++)
            {
                double ri = rOffset + lu * System.Math.Cos(thetas[i]);
                cx[i] = ri * System.Math.Cos(azimuthRad[i]);
                cy[i] = ri * System.Math.Sin(azimuthRad[i]);
                cz[i] = -lu * System.Math.Sin(thetas[i]);
                w[i] = cx[i] * cx[i] + cy[i] * cy[i] + cz[i] * cz[i];
            }

            // Subtract sphere 0 from sphere 1 and sphere 2:
            // m11 * x + m12 * y = rhs1 - d1z * z
            // m21 * x + m22 * y = rhs2 - d2z * z
            double m11 = 2.0 * (cx[1] - cx[0]);
            double m12 = 2.0 * (cy[1] - cy[0]);
            double d1z = 2.0 * (cz[1] - cz[0]);
            double rhs1 = w[1] - w[0];

            double m21 = 2.0 * (cx[2] - cx[0]);
            double m22 = 2.0 * (cy[2] - cy[0]);
            double d2z = 2.0 * (cz[2] - cz[0]);
            double rhs2 = w[2] - w[0];

            double det = m11 * m22 - m12 * m21;
            if (System.Math.Abs(det) < 1e-12)
            {
                return new DeltaFkSolution { IsValid = false };
            }

            // Express x = a1 * z + b1, y = a2 * z + b2
            double a1 = (d2z * m12 - d1z * m22) / det;
            double b1 = (rhs1 * m22 - rhs2 * m12) / det;

            double a2 = (d1z * m21 - d2z * m11) / det;
            double b2 = (rhs2 * m11 - rhs1 * m21) / det;

            // Substitute into sphere 0: (x - cx0)^2 + (y - cy0)^2 + (z - cz0)^2 = lf^2
            double qa = a1 * a1 + a2 * a2 + 1.0;
            double qb = 2.0 * (a1 * (b1 - cx[0]) + a2 * (b2 - cy[0]) - cz[0]);
            double qc = (b1 - cx[0]) * (b1 - cx[0]) + (b2 - cy[0]) * (b2 - cy[0]) + cz[0] * cz[0] - lf * lf;

            double disc = qb * qb - 4.0 * qa * qc;
            if (disc < -1e-6)
            {
                return new DeltaFkSolution { IsValid = false };
            }

            double sqrtDisc = System.Math.Sqrt(System.Math.Max(0.0, disc));
            // Lower workspace root (z < 0 below the fixed base)
            double z = (-qb - sqrtDisc) / (2.0 * qa);
            double x = a1 * z + b1;
            double y = a2 * z + b2;

            return new DeltaFkSolution
            {
                IsValid = true,
                Position = new Vec3((float)x, (float)y, (float)z)
            };
        }

        private static Mat4 ComputeArmRotationR03(float theta1Deg, float theta2Deg, float theta3Deg)
        {
            const double DegToRad = System.Math.PI / 180.0;
            double t1 = theta1Deg * DegToRad;
            double t23 = (theta2Deg + theta3Deg) * DegToRad;

            float c1 = (float)System.Math.Cos(t1);
            float s1 = (float)System.Math.Sin(t1);
            float c23 = (float)System.Math.Cos(t23);
            float s23 = (float)System.Math.Sin(t23);

            var m = Mat4.Identity;
            m.M11 = c1 * c23; m.M12 = -s1; m.M13 = c1 * s23;
            m.M21 = s1 * c23; m.M22 = c1;  m.M23 = s1 * s23;
            m.M31 = -s23;     m.M32 = 0f;  m.M33 = c23;
            return m;
        }

        private static Mat4 ComputeSphericalWristRotationR36(float theta4Deg, float theta5Deg, float theta6Deg)
        {
            const double DegToRad = System.Math.PI / 180.0;
            double t4 = theta4Deg * DegToRad;
            double t5 = theta5Deg * DegToRad;
            double t6 = theta6Deg * DegToRad;

            float c4 = (float)System.Math.Cos(t4), s4 = (float)System.Math.Sin(t4);
            float c5 = (float)System.Math.Cos(t5), s5 = (float)System.Math.Sin(t5);
            float c6 = (float)System.Math.Cos(t6), s6 = (float)System.Math.Sin(t6);

            var m = Mat4.Identity;
            m.M11 = c4 * c5 * c6 - s4 * s6;
            m.M12 = -c4 * c5 * s6 - s4 * c6;
            m.M13 = c4 * s5;

            m.M21 = s4 * c5 * c6 + c4 * s6;
            m.M22 = -s4 * c5 * s6 + c4 * c6;
            m.M23 = s4 * s5;

            m.M31 = -s5 * c6;
            m.M32 = s5 * s6;
            m.M33 = c5;
            return m;
        }

        private static Mat4 TransposeRotation3x3(Mat4 r)
        {
            var t = Mat4.Identity;
            t.M11 = r.M11; t.M12 = r.M21; t.M13 = r.M31;
            t.M21 = r.M12; t.M22 = r.M22; t.M23 = r.M32;
            t.M31 = r.M13; t.M32 = r.M23; t.M33 = r.M33;
            return t;
        }

        private static Mat4 MultiplyRotation3x3(Mat4 a, Mat4 b)
        {
            var res = Mat4.Identity;
            res.M11 = a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31;
            res.M12 = a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32;
            res.M13 = a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33;

            res.M21 = a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31;
            res.M22 = a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32;
            res.M23 = a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33;

            res.M31 = a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31;
            res.M32 = a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32;
            res.M33 = a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33;
            return res;
        }

        private static float WrapAngleDeg(float angleDeg)
        {
            float wrapped = angleDeg % 360f;
            if (wrapped > 180f) wrapped -= 360f;
            if (wrapped <= -180f) wrapped += 360f;
            return wrapped;
        }
    }
}
