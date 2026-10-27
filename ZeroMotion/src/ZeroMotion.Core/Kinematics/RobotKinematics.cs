using System;
using System.Collections.Generic;
using ZeroMotion.Math;

namespace ZeroMotion.Kinematics
{
    /// <summary>
    /// Multi-axis industrial robot kinematics engine.
    /// Computes forward kinematics, geometric Jacobians, and frame transforms for N-DOF manipulators.
    /// </summary>
    public class RobotKinematics
    {
        public List<DhLink> Links { get; } = new List<DhLink>();
        public Mat4 BaseTransform { get; set; } = Mat4.Identity;
        public Mat4 ToolTransform { get; set; } = Mat4.Identity; // TCP (Tool Center Point)

        public RobotKinematics()
        {
        }

        public RobotKinematics AddLink(DhLink link)
        {
            Links.Add(link);
            return this;
        }

        public KinematicsResult ComputeForwardKinematics(IReadOnlyList<float> jointValues)
        {
            if (jointValues == null) throw new ArgumentNullException(nameof(jointValues));

            var result = new KinematicsResult();
            var current = BaseTransform;

            result.JointTransforms.Add(current);
            result.JointPositions.Add(current.Translation);

            for (int i = 0; i < Links.Count; i++)
            {
                float val = (i < jointValues.Count) ? jointValues[i] : 0f;
                Mat4 localT = Links[i].ComputeTransform(val);
                current = localT * current;

                result.JointTransforms.Add(current);
                result.JointPositions.Add(current.Translation);
            }

            result.EndEffectorTransform = ToolTransform * current;
            return result;
        }

        /// <summary>
        /// Computes the 6xN Geometric Jacobian matrix J(q) relating joint velocities to end-effector linear & angular velocity.
        /// [v; omega] = J(q) * q_dot
        /// Returns float[6, N] array where rows 0..2 are linear velocity and rows 3..5 are angular velocity.
        /// </summary>
        public float[,] ComputeJacobian(IReadOnlyList<float> jointValues)
        {
            int n = Links.Count;
            var jacobian = new float[6, n];
            var fk = ComputeForwardKinematics(jointValues);
            var pe = fk.EndEffectorPosition;

            for (int i = 0; i < n; i++)
            {
                var t = fk.JointTransforms[i];
                var pi = t.Translation;
                var zi = new Vec3(t.M31, t.M32, t.M33).Normalize();
                if (zi.LengthSquared() < 1e-4f) zi = Vec3.UnitZ;

                if (Links[i].Type == JointType.Revolute)
                {
                    var jv = Vec3.Cross(zi, pe - pi);
                    jacobian[0, i] = jv.X;
                    jacobian[1, i] = jv.Y;
                    jacobian[2, i] = jv.Z;

                    jacobian[3, i] = zi.X;
                    jacobian[4, i] = zi.Y;
                    jacobian[5, i] = zi.Z;
                }
                else
                {
                    jacobian[0, i] = zi.X;
                    jacobian[1, i] = zi.Y;
                    jacobian[2, i] = zi.Z;

                    jacobian[3, i] = 0f;
                    jacobian[4, i] = 0f;
                    jacobian[5, i] = 0f;
                }
            }

            return jacobian;
        }

        /// <summary>
        /// Computes Yoshikawa's Manipulability Measure: w = sqrt(det(J * J^T)).
        /// Units are normalized to SI (meters/radians) to prevent numerical ill-conditioning.
        /// When w approaches 0, the robot is at or near a kinematic singularity.
        /// </summary>
        public float ComputeManipulability(IReadOnlyList<float> jointValues)
        {
            float[,] J = ComputeJacobian(jointValues);
            int n = Links.Count;
            double[,] A = new double[6, 6];

            for (int r = 0; r < 6; r++)
            {
                double scaleR = (r < 3) ? 0.001 : 1.0; // scale mm to meters for linear rows
                for (int c = 0; c < 6; c++)
                {
                    double scaleC = (c < 3) ? 0.001 : 1.0;
                    double sum = 0.0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += (J[r, k] * scaleR) * (J[c, k] * scaleC);
                    }
                    A[r, c] = sum;
                }
            }

            double det = ComputeDeterminant6x6(A);
            return det > 0.0 ? (float)System.Math.Sqrt(det) : 0f;
        }

        /// <summary>
        /// Solves differential inverse kinematics using Damped Least Squares (Levenberg-Marquardt):
        /// delta_q = J^T * (J * J^T + lambda^2 * I)^(-1) * delta_x
        /// Robust against singularities, preventing joint velocity divergence.
        /// </summary>
        public float[] SolveDampedLeastSquaresIK(
            IReadOnlyList<float> currentJoints,
            Vec3 targetDeltaLinear,
            Vec3 targetDeltaAngular,
            float dampingLambda = 0.05f)
        {
            float[,] J = ComputeJacobian(currentJoints);
            int n = Links.Count;

            // Compute A = J * J^T + lambda^2 * I (6x6) in double precision
            double[,] A = new double[6, 7]; // augmented with rhs
            double lambdaSq = (double)dampingLambda * dampingLambda;

            for (int r = 0; r < 6; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    double sum = (r == c) ? lambdaSq : 0.0;
                    for (int k = 0; k < n; k++)
                    {
                        sum += (double)J[r, k] * J[c, k];
                    }
                    A[r, c] = sum;
                }
            }

            // RHS: delta_x = [linear; angular]
            A[0, 6] = targetDeltaLinear.X;
            A[1, 6] = targetDeltaLinear.Y;
            A[2, 6] = targetDeltaLinear.Z;
            A[3, 6] = targetDeltaAngular.X;
            A[4, 6] = targetDeltaAngular.Y;
            A[5, 6] = targetDeltaAngular.Z;

            // Solve (J * J^T + lambda^2 * I) * y = delta_x via Gaussian elimination
            double[] y = SolveLinearSystem6(A);

            // delta_q = J^T * y
            float[] deltaQ = new float[n];
            for (int j = 0; j < n; j++)
            {
                double sum = 0.0;
                for (int r = 0; r < 6; r++)
                {
                    sum += (double)J[r, j] * y[r];
                }
                deltaQ[j] = (float)sum;
            }

            return deltaQ;
        }

        private static double ComputeDeterminant6x6(double[,] m)
        {
            double[,] a = (double[,])m.Clone();
            double det = 1.0;

            for (int i = 0; i < 6; i++)
            {
                int pivot = i;
                for (int j = i + 1; j < 6; j++)
                {
                    if (System.Math.Abs(a[j, i]) > System.Math.Abs(a[pivot, i]))
                    {
                        pivot = j;
                    }
                }

                if (System.Math.Abs(a[pivot, i]) < 1e-15) return 0.0;

                if (pivot != i)
                {
                    for (int k = 0; k < 6; k++)
                    {
                        double tmp = a[i, k];
                        a[i, k] = a[pivot, k];
                        a[pivot, k] = tmp;
                    }
                    det = -det;
                }

                det *= a[i, i];

                for (int j = i + 1; j < 6; j++)
                {
                    double factor = a[j, i] / a[i, i];
                    for (int k = i; k < 6; k++)
                    {
                        a[j, k] -= factor * a[i, k];
                    }
                }
            }

            return det;
        }

        private static double[] SolveLinearSystem6(double[,] a)
        {
            for (int i = 0; i < 6; i++)
            {
                int pivot = i;
                for (int j = i + 1; j < 6; j++)
                {
                    if (System.Math.Abs(a[j, i]) > System.Math.Abs(a[pivot, i])) pivot = j;
                }

                if (pivot != i)
                {
                    for (int k = 0; k <= 6; k++)
                    {
                        double tmp = a[i, k];
                        a[i, k] = a[pivot, k];
                        a[pivot, k] = tmp;
                    }
                }

                double div = a[i, i];
                if (System.Math.Abs(div) < 1e-12) div = 1e-12;

                for (int j = i + 1; j < 6; j++)
                {
                    double factor = a[j, i] / div;
                    for (int k = i; k <= 6; k++)
                    {
                        a[j, k] -= factor * a[i, k];
                    }
                }
            }

            double[] x = new double[6];
            for (int i = 5; i >= 0; i--)
            {
                double sum = a[i, 6];
                for (int j = i + 1; j < 6; j++)
                {
                    sum -= a[i, j] * x[j];
                }
                double div = a[i, i];
                x[i] = System.Math.Abs(div) > 1e-12 ? sum / div : 0.0;
            }

            return x;
        }

        /// <summary>
        /// Creates a standard 6-axis articulated industrial robot manipulator (e.g. FANUC / ABB / KUKA kinematics).
        /// </summary>
        public static RobotKinematics CreateStandard6AxisRobot(float d1 = 400f, float a1 = 150f, float a2 = 450f, float d4 = 400f, float d6 = 100f)
        {
            var robot = new RobotKinematics();
            robot.AddLink(new DhLink(0f, d1, a1, 90f, "Joint1_BaseYaw").WithLimits(-170f, 170f));
            robot.AddLink(new DhLink(0f, 0f, a2, 0f, "Joint2_ShoulderPitch").WithLimits(-100f, 135f));
            robot.AddLink(new DhLink(0f, 0f, 0f, 90f, "Joint3_ElbowPitch").WithLimits(-200f, 65f));
            robot.AddLink(new DhLink(0f, d4, 0f, -90f, "Joint4_WristRoll").WithLimits(-190f, 190f));
            robot.AddLink(new DhLink(0f, 0f, 0f, 90f, "Joint5_WristPitch").WithLimits(-125f, 125f));
            robot.AddLink(new DhLink(0f, d6, 0f, 0f, "Joint6_FlangeRoll").WithLimits(-360f, 360f));
            return robot;
        }

        /// <summary>
        /// Creates a 4-DOF SCARA (Selective Compliance Articulated Robot Arm) manipulator.
        /// </summary>
        public static RobotKinematics CreateStandardScaraRobot(float l1 = 300f, float l2 = 250f, float baseHeight = 200f)
        {
            var robot = new RobotKinematics();
            robot.AddLink(new DhLink(0f, baseHeight, l1, 0f, "Joint1_Arm1Yaw").WithLimits(-135f, 135f));
            robot.AddLink(new DhLink(0f, 0f, l2, 0f, "Joint2_Arm2Yaw").WithLimits(-150f, 150f));
            var prismaticLink = new DhLink(0f, 0f, 0f, 0f, "Joint3_ZPrismatic")
            {
                Type = JointType.Prismatic,
                MinLimit = -200f,
                MaxLimit = 0f
            };
            robot.AddLink(prismaticLink);
            robot.AddLink(new DhLink(0f, 0f, 0f, 0f, "Joint4_ToolYaw").WithLimits(-360f, 360f));
            return robot;
        }

        /// <summary>
        /// Creates a 3-Axis Cartesian (Gantry) Robot.
        /// </summary>
        public static RobotKinematics CreateCartesianRobot(float xMax = 1000f, float yMax = 1000f, float zMax = 500f)
        {
            var robot = new RobotKinematics();
            var jX = new DhLink(0f, 0f, 0f, -90f, "Axis_X") { Type = JointType.Prismatic, MinLimit = 0f, MaxLimit = xMax };
            var jY = new DhLink(-90f, 0f, 0f, -90f, "Axis_Y") { Type = JointType.Prismatic, MinLimit = 0f, MaxLimit = yMax };
            var jZ = new DhLink(0f, 0f, 0f, 0f, "Axis_Z") { Type = JointType.Prismatic, MinLimit = 0f, MaxLimit = zMax };
            robot.AddLink(jX).AddLink(jY).AddLink(jZ);
            return robot;
        }
    }
}
