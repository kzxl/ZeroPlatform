using System;
using System.Collections.Generic;
using System.Numerics;
using ZeroGeometry.Core.Spatial;
using ZeroTensor.Core;

namespace ZeroGeometry.Core.PointCloud
{
    public sealed class IcpResult
    {
        public double[,] Rotation { get; } // 3x3
        public Point3D Translation { get; }
        public double FitnessRms { get; }
        public int Iterations { get; }
        public bool Converged { get; }
        public PointCloud3D AlignedCloud { get; }

        public IcpResult(
            double[,] rotation,
            Point3D translation,
            double fitnessRms,
            int iterations,
            bool converged,
            PointCloud3D alignedCloud)
        {
            Rotation = rotation;
            Translation = translation;
            FitnessRms = fitnessRms;
            Iterations = iterations;
            Converged = converged;
            AlignedCloud = alignedCloud;
        }
    }

    /// <summary>
    /// Iterative Closest Point (ICP) algorithms (Point-to-Point SVD &amp; Point-to-Plane se(3) Gauss-Newton)
    /// for rigid 3D point cloud registration.
    /// </summary>
    public static class IcpRegistration
    {
        public static IcpResult Align(
            PointCloud3D source,
            PointCloud3D target,
            int maxIterations = 50,
            double tolerance = 1e-5,
            double maxCorrespondenceDistance = double.PositiveInfinity)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (source.Count == 0 || target.Count == 0)
                throw new ArgumentException("Source and target point clouds must not be empty.");

            var targetTree = new KdTree3D(target.Points);

            // Accumulated transformation: R_total = I, t_total = 0
            double[,] R_total = new double[3, 3]
            {
                { 1, 0, 0 },
                { 0, 1, 0 },
                { 0, 0, 1 }
            };
            Point3D t_total = new Point3D(0, 0, 0);

            var currentSourcePoints = new Point3D[source.Count];
            for (int i = 0; i < source.Count; i++) currentSourcePoints[i] = source[i];

            double prevRms = double.MaxValue;
            double currentRms = double.MaxValue;
            bool converged = false;
            int iter = 0;

            for (iter = 0; iter < maxIterations; iter++)
            {
                // Step 1: Find correspondences
                var matchedSource = new List<Point3D>(source.Count);
                var matchedTarget = new List<Point3D>(source.Count);
                double sumDistSq = 0.0;

                for (int i = 0; i < currentSourcePoints.Length; i++)
                {
                    var p = currentSourcePoints[i];
                    if (targetTree.NearestNeighbor(p, out var q, out double distSq, out _))
                    {
                        if (distSq <= maxCorrespondenceDistance * maxCorrespondenceDistance)
                        {
                            matchedSource.Add(p);
                            matchedTarget.Add(q);
                            sumDistSq += distSq;
                        }
                    }
                }

                if (matchedSource.Count < 3)
                    break; // Too few matches

                currentRms = Math.Sqrt(sumDistSq / matchedSource.Count);
                if (Math.Abs(prevRms - currentRms) < tolerance)
                {
                    converged = true;
                    break;
                }
                prevRms = currentRms;

                // Step 2: Compute centroids
                double meanPx = 0, meanPy = 0, meanPz = 0;
                double meanQx = 0, meanQy = 0, meanQz = 0;
                int n = matchedSource.Count;

                for (int i = 0; i < n; i++)
                {
                    meanPx += matchedSource[i].X;
                    meanPy += matchedSource[i].Y;
                    meanPz += matchedSource[i].Z;

                    meanQx += matchedTarget[i].X;
                    meanQy += matchedTarget[i].Y;
                    meanQz += matchedTarget[i].Z;
                }

                var pBar = new Point3D(meanPx / n, meanPy / n, meanPz / n);
                var qBar = new Point3D(meanQx / n, meanQy / n, meanQz / n);

                // Step 3: Compute cross-covariance matrix H = sum((p - pBar) * (q - qBar)^T)
                var H = Tensor.Zeros<float>(3, 3);
                for (int i = 0; i < n; i++)
                {
                    float px = (float)(matchedSource[i].X - pBar.X);
                    float py = (float)(matchedSource[i].Y - pBar.Y);
                    float pz = (float)(matchedSource[i].Z - pBar.Z);

                    float qx = (float)(matchedTarget[i].X - qBar.X);
                    float qy = (float)(matchedTarget[i].Y - qBar.Y);
                    float qz = (float)(matchedTarget[i].Z - qBar.Z);

                    H[0, 0] += px * qx; H[0, 1] += px * qy; H[0, 2] += px * qz;
                    H[1, 0] += py * qx; H[1, 1] += py * qy; H[1, 2] += py * qz;
                    H[2, 0] += pz * qx; H[2, 1] += pz * qy; H[2, 2] += pz * qz;
                }

                // Step 4: SVD(H) = U * S * V^T
                TensorDecompositions.SVD(H, out var U, out var _, out var Vt);
                var V = Vt.Transpose(0, 1);

                // R_step = V * U^T
                var Ut = U.Transpose(0, 1);
                var R_step_tensor = TensorBlas.MatMul(V, Ut);

                // Check reflection: det(R)
                float det = R_step_tensor[0, 0] * (R_step_tensor[1, 1] * R_step_tensor[2, 2] - R_step_tensor[1, 2] * R_step_tensor[2, 1]) -
                            R_step_tensor[0, 1] * (R_step_tensor[1, 0] * R_step_tensor[2, 2] - R_step_tensor[1, 2] * R_step_tensor[2, 0]) +
                            R_step_tensor[0, 2] * (R_step_tensor[1, 0] * R_step_tensor[2, 1] - R_step_tensor[1, 1] * R_step_tensor[2, 0]);

                if (det < 0f)
                {
                    // Negate 3rd column of V
                    V[0, 2] = -V[0, 2];
                    V[1, 2] = -V[1, 2];
                    V[2, 2] = -V[2, 2];
                    R_step_tensor = TensorBlas.MatMul(V, Ut);
                }

                double[,] R_step = new double[3, 3];
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        R_step[r, c] = R_step_tensor[r, c];

                // t_step = qBar - R_step * pBar
                double t_stepX = qBar.X - (R_step[0, 0] * pBar.X + R_step[0, 1] * pBar.Y + R_step[0, 2] * pBar.Z);
                double t_stepY = qBar.Y - (R_step[1, 0] * pBar.X + R_step[1, 1] * pBar.Y + R_step[1, 2] * pBar.Z);
                double t_stepZ = qBar.Z - (R_step[2, 0] * pBar.X + R_step[2, 1] * pBar.Y + R_step[2, 2] * pBar.Z);
                var t_step = new Point3D(t_stepX, t_stepY, t_stepZ);

                // Step 5: Update current source points
                for (int i = 0; i < currentSourcePoints.Length; i++)
                {
                    var p = currentSourcePoints[i];
                    double nx = R_step[0, 0] * p.X + R_step[0, 1] * p.Y + R_step[0, 2] * p.Z + t_step.X;
                    double ny = R_step[1, 0] * p.X + R_step[1, 1] * p.Y + R_step[1, 2] * p.Z + t_step.Y;
                    double nz = R_step[2, 0] * p.X + R_step[2, 1] * p.Y + R_step[2, 2] * p.Z + t_step.Z;
                    currentSourcePoints[i] = new Point3D(nx, ny, nz);
                }

                // Step 6: Accumulate total transformation
                // R_total = R_step * R_total
                // t_total = R_step * t_total + t_step
                double[,] newR = new double[3, 3];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        newR[r, c] = R_step[r, 0] * R_total[0, c] +
                                     R_step[r, 1] * R_total[1, c] +
                                     R_step[r, 2] * R_total[2, c];
                    }
                }
                R_total = newR;

                double newTx = R_step[0, 0] * t_total.X + R_step[0, 1] * t_total.Y + R_step[0, 2] * t_total.Z + t_step.X;
                double newTy = R_step[1, 0] * t_total.X + R_step[1, 1] * t_total.Y + R_step[1, 2] * t_total.Z + t_step.Y;
                double newTz = R_step[2, 0] * t_total.X + R_step[2, 1] * t_total.Y + R_step[2, 2] * t_total.Z + t_step.Z;
                t_total = new Point3D(newTx, newTy, newTz);
            }

            var alignedCloud = new PointCloud3D(currentSourcePoints);
            return new IcpResult(R_total, t_total, currentRms, iter, converged, alignedCloud);
        }

        /// <summary>
        /// Point-to-Plane ICP in se(3) using ReadOnlySpan&lt;Vector3&gt; inputs.
        /// Minimizes sum_i w_i * ((omega x p_i + t + p_i - q_i) . n_i)^2 via a 6x6 symmetric LDL^T solver
        /// with Huber robust loss weighting and Rodrigues SE(3) exponential map updates.
        /// </summary>
        public static IcpResult RegisterPointToPlane(
            ReadOnlySpan<Vector3> source,
            ReadOnlySpan<Vector3> target,
            ReadOnlySpan<Vector3> targetNormals = default,
            int maxIterations = 50,
            double tolerance = 1e-6,
            double maxCorrespondenceDistance = double.PositiveInfinity,
            double huberDelta = 0.5)
        {
            if (source.Length == 0 || target.Length == 0)
                throw new ArgumentException("Source and target point spans must not be empty.");

            var srcPoints = new Point3D[source.Length];
            for (int i = 0; i < source.Length; i++)
                srcPoints[i] = new Point3D(source[i].X, source[i].Y, source[i].Z);

            var tgtPoints = new Point3D[target.Length];
            for (int i = 0; i < target.Length; i++)
                tgtPoints[i] = new Point3D(target[i].X, target[i].Y, target[i].Z);

            Point3D[]? normals = null;
            if (!targetNormals.IsEmpty && targetNormals.Length == target.Length)
            {
                normals = new Point3D[targetNormals.Length];
                for (int i = 0; i < targetNormals.Length; i++)
                {
                    normals[i] = new Point3D(targetNormals[i].X, targetNormals[i].Y, targetNormals[i].Z).Normalized();
                }
            }

            return RegisterPointToPlane(
                new PointCloud3D(srcPoints),
                new PointCloud3D(tgtPoints),
                normals,
                maxIterations,
                tolerance,
                maxCorrespondenceDistance,
                huberDelta);
        }

        /// <summary>
        /// Point-to-Plane ICP in se(3) for PointCloud3D.
        /// Automatically estimates target normals via PCA (NormalEstimator3D) when targetNormals is null.
        /// </summary>
        public static IcpResult RegisterPointToPlane(
            PointCloud3D source,
            PointCloud3D target,
            IReadOnlyList<Point3D>? targetNormals = null,
            int maxIterations = 50,
            double tolerance = 1e-6,
            double maxCorrespondenceDistance = double.PositiveInfinity,
            double huberDelta = 0.5)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (source.Count == 0 || target.Count == 0)
                throw new ArgumentException("Source and target point clouds must not be empty.");

            Point3D[] normals = new Point3D[target.Count];
            if (targetNormals != null && targetNormals.Count == target.Count)
            {
                for (int i = 0; i < target.Count; i++)
                {
                    var n = targetNormals[i].Normalized();
                    normals[i] = n.LengthSquared > 1e-12 ? n : new Point3D(0, 0, 1);
                }
            }
            else
            {
                var estimated = NormalEstimator3D.EstimateNormals(target.Points, k: Math.Min(10, Math.Max(3, target.Count)));
                for (int i = 0; i < target.Count; i++)
                {
                    normals[i] = estimated[i].Normal;
                }
            }

            var targetTree = new KdTree3D(target.Points);

            double[,] R_total = new double[3, 3]
            {
                { 1, 0, 0 },
                { 0, 1, 0 },
                { 0, 0, 1 }
            };
            Point3D t_total = new Point3D(0, 0, 0);

            var currentSourcePoints = new Point3D[source.Count];
            for (int i = 0; i < source.Count; i++) currentSourcePoints[i] = source[i];

            double prevRms = double.MaxValue;
            double currentRms = double.MaxValue;
            bool converged = false;
            int iter = 0;

            double maxDistSq = double.IsPositiveInfinity(maxCorrespondenceDistance)
                ? double.PositiveInfinity
                : maxCorrespondenceDistance * maxCorrespondenceDistance;

            double[,] A = new double[6, 6];
            double[] b = new double[6];
            double[] aRow = new double[6];

            for (iter = 0; iter < maxIterations; iter++)
            {
                Array.Clear(A, 0, 36);
                Array.Clear(b, 0, 6);

                double sumPlaneErrSq = 0.0;
                double sumPointDistSq = 0.0;
                int matchCount = 0;

                for (int i = 0; i < currentSourcePoints.Length; i++)
                {
                    var p = currentSourcePoints[i];
                    if (!targetTree.NearestNeighbor(p, out var q, out double distSq, out int qIdx) || distSq > maxDistSq)
                        continue;

                    var n = normals[qIdx];
                    double rx = p.X - q.X;
                    double ry = p.Y - q.Y;
                    double rz = p.Z - q.Z;

                    // Point-to-plane residual r_i = (p_i - q_i) . n_i
                    double residual = rx * n.X + ry * n.Y + rz * n.Z;
                    sumPlaneErrSq += residual * residual;
                    sumPointDistSq += distSq;
                    matchCount++;

                    // Huber robust weight
                    double absRes = Math.Abs(residual);
                    double weight = (huberDelta > 0.0 && absRes > huberDelta) ? (huberDelta / absRes) : 1.0;

                    // c_i = p_i x n_i
                    aRow[0] = p.Y * n.Z - p.Z * n.Y;
                    aRow[1] = p.Z * n.X - p.X * n.Z;
                    aRow[2] = p.X * n.Y - p.Y * n.X;
                    aRow[3] = n.X;
                    aRow[4] = n.Y;
                    aRow[5] = n.Z;

                    for (int r = 0; r < 6; r++)
                    {
                        double wa = weight * aRow[r];
                        b[r] -= wa * residual;
                        for (int c = r; c < 6; c++)
                        {
                            A[r, c] += wa * aRow[c];
                        }
                    }
                }

                if (matchCount < 3)
                    break;

                // Mirror symmetric lower triangle and add Levenberg-Marquardt damping for rank-deficient planes
                for (int r = 0; r < 6; r++)
                {
                    A[r, r] += 1e-6;
                    for (int c = 0; c < r; c++)
                    {
                        A[r, c] = A[c, r];
                    }
                }

                currentRms = Math.Sqrt(sumPointDistSq / matchCount);
                double planeRms = Math.Sqrt(sumPlaneErrSq / matchCount);

                if (planeRms < tolerance || Math.Abs(prevRms - currentRms) < tolerance * 0.1)
                {
                    converged = true;
                    break;
                }
                prevRms = currentRms;

                // Solve 6x6 symmetric positive-definite system A * xi = b via LDL^T decomposition
                double[] xi = SolveSymmetric6x6Ldlt(A, b);

                double stepNormSq = 0.0;
                for (int k = 0; k < 6; k++) stepNormSq += xi[k] * xi[k];

                // Map twist xi = [omega_x, omega_y, omega_z, t_x, t_y, t_z] to SE(3) via Rodrigues formula
                double[,] R_step = ExpMapSo3(xi[0], xi[1], xi[2]);
                var t_step = new Point3D(xi[3], xi[4], xi[5]);

                for (int i = 0; i < currentSourcePoints.Length; i++)
                {
                    var p = currentSourcePoints[i];
                    double nx = R_step[0, 0] * p.X + R_step[0, 1] * p.Y + R_step[0, 2] * p.Z + t_step.X;
                    double ny = R_step[1, 0] * p.X + R_step[1, 1] * p.Y + R_step[1, 2] * p.Z + t_step.Y;
                    double nz = R_step[2, 0] * p.X + R_step[2, 1] * p.Y + R_step[2, 2] * p.Z + t_step.Z;
                    currentSourcePoints[i] = new Point3D(nx, ny, nz);
                }

                double[,] newR = new double[3, 3];
                for (int r = 0; r < 3; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        newR[r, c] = R_step[r, 0] * R_total[0, c] +
                                     R_step[r, 1] * R_total[1, c] +
                                     R_step[r, 2] * R_total[2, c];
                    }
                }
                R_total = newR;

                double newTx = R_step[0, 0] * t_total.X + R_step[0, 1] * t_total.Y + R_step[0, 2] * t_total.Z + t_step.X;
                double newTy = R_step[1, 0] * t_total.X + R_step[1, 1] * t_total.Y + R_step[1, 2] * t_total.Z + t_step.Y;
                double newTz = R_step[2, 0] * t_total.X + R_step[2, 1] * t_total.Y + R_step[2, 2] * t_total.Z + t_step.Z;
                t_total = new Point3D(newTx, newTy, newTz);

                if (Math.Sqrt(stepNormSq) < tolerance)
                {
                    converged = true;
                    iter++;
                    break;
                }
            }

            // Re-evaluate final point-to-point RMS
            double finalDistSq = 0.0;
            int finalMatches = 0;
            for (int i = 0; i < currentSourcePoints.Length; i++)
            {
                if (targetTree.NearestNeighbor(currentSourcePoints[i], out _, out double dSq, out _) && dSq <= maxDistSq)
                {
                    finalDistSq += dSq;
                    finalMatches++;
                }
            }
            if (finalMatches > 0)
            {
                currentRms = Math.Sqrt(finalDistSq / finalMatches);
            }

            return new IcpResult(R_total, t_total, currentRms, iter, converged, new PointCloud3D(currentSourcePoints));
        }

        private static double[] SolveSymmetric6x6Ldlt(double[,] A, double[] b)
        {
            double[,] L = new double[6, 6];
            double[] D = new double[6];

            for (int i = 0; i < 6; i++)
            {
                L[i, i] = 1.0;
                for (int j = 0; j < i; j++)
                {
                    double sum = A[i, j];
                    for (int k = 0; k < j; k++)
                    {
                        sum -= L[i, k] * L[j, k] * D[k];
                    }
                    L[i, j] = Math.Abs(D[j]) > 1e-15 ? sum / D[j] : 0.0;
                }

                double diag = A[i, i];
                for (int k = 0; k < i; k++)
                {
                    diag -= L[i, k] * L[i, k] * D[k];
                }
                D[i] = Math.Abs(diag) > 1e-15 ? diag : 1e-15;
            }

            // Forward substitution: L * y = b
            double[] y = new double[6];
            for (int i = 0; i < 6; i++)
            {
                double sum = b[i];
                for (int k = 0; k < i; k++)
                {
                    sum -= L[i, k] * y[k];
                }
                y[i] = sum;
            }

            // Diagonal scaling: D * z = y
            double[] z = new double[6];
            for (int i = 0; i < 6; i++)
            {
                z[i] = y[i] / D[i];
            }

            // Back substitution: L^T * x = z
            double[] x = new double[6];
            for (int i = 5; i >= 0; i--)
            {
                double sum = z[i];
                for (int k = i + 1; k < 6; k++)
                {
                    sum -= L[k, i] * x[k];
                }
                x[i] = sum;
            }

            return x;
        }

        private static double[,] ExpMapSo3(double wx, double wy, double wz)
        {
            double thetaSq = wx * wx + wy * wy + wz * wz;
            double theta = Math.Sqrt(thetaSq);

            if (theta < 1e-12)
            {
                return new double[3, 3]
                {
                    {  1.0, -wz,   wy },
                    {  wz,   1.0, -wx },
                    { -wy,   wx,   1.0 }
                };
            }

            double ux = wx / theta;
            double uy = wy / theta;
            double uz = wz / theta;

            double s = Math.Sin(theta);
            double c = 1.0 - Math.Cos(theta);

            return new double[3, 3]
            {
                { 1.0 - c * (uy * uy + uz * uz), -s * uz + c * ux * uy,          s * uy + c * ux * uz },
                {  s * uz + c * ux * uy,          1.0 - c * (ux * ux + uz * uz), -s * ux + c * uy * uz },
                { -s * uy + c * ux * uz,          s * ux + c * uy * uz,          1.0 - c * (ux * ux + uy * uy) }
            };
        }
    }
}
