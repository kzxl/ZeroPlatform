using System;
using System.Collections.Generic;
using ZeroGeometry.Core.Spatial;

namespace ZeroGeometry.Core.PointCloud
{
    /// <summary>
    /// Represents a 3D point paired with its estimated surface normal and local curvature.
    /// </summary>
    public readonly struct PointNormal3D : IEquatable<PointNormal3D>
    {
        public Point3D Point { get; }
        public Point3D Normal { get; }
        public double Curvature { get; }

        public PointNormal3D(Point3D point, Point3D normal, double curvature)
        {
            Point = point;
            Normal = normal;
            Curvature = curvature;
        }

        public bool Equals(PointNormal3D other) =>
            Point.Equals(other.Point) && Normal.Equals(other.Normal) && Math.Abs(Curvature - other.Curvature) < 1e-6;

        public override bool Equals(object? obj) => obj is PointNormal3D other && Equals(other);
        public override int GetHashCode() => unchecked((Point.GetHashCode() * 397) ^ Normal.GetHashCode());
        public override string ToString() => $"Point: {Point}, Normal: {Normal}, Curvature: {Curvature:F4}";
    }

    /// <summary>
    /// Computes 3D surface normal vectors and local surface curvature for point clouds
    /// using k-nearest neighbor local covariance eigenanalysis (PCA).
    /// Pure C# with zero external dependencies.
    /// </summary>
    public static class NormalEstimator3D
    {
        /// <summary>
        /// Estimates surface normals for all points in the point cloud.
        /// </summary>
        /// <param name="points">Input 3D point cloud.</param>
        /// <param name="k">Number of nearest neighbors to construct local covariance (default 10, min 3).</param>
        /// <param name="viewpoint">Optional sensor/camera viewpoint to consistently orient normals towards.</param>
        /// <returns>List of PointNormal3D with unit normals and curvature estimates.</returns>
        public static List<PointNormal3D> EstimateNormals(
            IReadOnlyList<Point3D> points,
            int k = 10,
            Point3D? viewpoint = null)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (k < 3) k = 3;

            int count = points.Count;
            var result = new List<PointNormal3D>(count);
            if (count == 0) return result;

            var tree = new KdTree3D(points);

            for (int i = 0; i < count; i++)
            {
                var pt = points[i];
                var neighbors = tree.KNearestNeighbors(pt, k);

                if (neighbors.Count < 3)
                {
                    result.Add(new PointNormal3D(pt, new Point3D(0, 0, 1), 0.0));
                    continue;
                }

                // 1. Compute Centroid
                double meanX = 0, meanY = 0, meanZ = 0;
                int n = neighbors.Count;
                for (int j = 0; j < n; j++)
                {
                    var np = neighbors[j].point;
                    meanX += np.X;
                    meanY += np.Y;
                    meanZ += np.Z;
                }
                meanX /= n;
                meanY /= n;
                meanZ /= n;

                // 2. Compute 3x3 Covariance Matrix
                double cxx = 0, cxy = 0, cxz = 0;
                double cyy = 0, cyz = 0, czz = 0;

                for (int j = 0; j < n; j++)
                {
                    var np = neighbors[j].point;
                    double dx = np.X - meanX;
                    double dy = np.Y - meanY;
                    double dz = np.Z - meanZ;

                    cxx += dx * dx;
                    cxy += dx * dy;
                    cxz += dx * dz;
                    cyy += dy * dy;
                    cyz += dy * dz;
                    czz += dz * dz;
                }

                double invN = 1.0 / n;
                cxx *= invN; cxy *= invN; cxz *= invN;
                cyy *= invN; cyz *= invN; czz *= invN;

                // 3. Symmetric 3x3 Eigen Decomposition via Jacobi Rotations
                double[,] cov = new double[3, 3]
                {
                    { cxx, cxy, cxz },
                    { cxy, cyy, cyz },
                    { cxz, cyz, czz }
                };

                SolveSymmetric3x3Eigen(cov, out double[] eigenvalues, out double[,] eigenvectors);

                // Find index of smallest eigenvalue
                int minIdx = 0;
                if (eigenvalues[1] < eigenvalues[minIdx]) minIdx = 1;
                if (eigenvalues[2] < eigenvalues[minIdx]) minIdx = 2;

                double nx = eigenvectors[0, minIdx];
                double ny = eigenvectors[1, minIdx];
                double nz = eigenvectors[2, minIdx];

                double len = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (len > 1e-12)
                {
                    nx /= len;
                    ny /= len;
                    nz /= len;
                }
                else
                {
                    nx = 0; ny = 0; nz = 1;
                }

                // 4. Orient normal towards viewpoint if specified
                if (viewpoint.HasValue)
                {
                    var vp = viewpoint.Value;
                    double vx = vp.X - pt.X;
                    double vy = vp.Y - pt.Y;
                    double vz = vp.Z - pt.Z;

                    double dot = nx * vx + ny * vy + nz * vz;
                    if (dot < 0)
                    {
                        nx = -nx;
                        ny = -ny;
                        nz = -nz;
                    }
                }

                // 5. Curvature estimate: lambda_0 / (lambda_0 + lambda_1 + lambda_2)
                double sumLambda = eigenvalues[0] + eigenvalues[1] + eigenvalues[2];
                double curvature = sumLambda > 1e-12 ? eigenvalues[minIdx] / sumLambda : 0.0;
                if (curvature < 0) curvature = 0;

                result.Add(new PointNormal3D(pt, new Point3D(nx, ny, nz), curvature));
            }

            return result;
        }

        private static void SolveSymmetric3x3Eigen(double[,] A, out double[] eigenvalues, out double[,] V)
        {
            // Initializes V as 3x3 Identity matrix
            V = new double[3, 3]
            {
                { 1, 0, 0 },
                { 0, 1, 0 },
                { 0, 0, 1 }
            };

            double[,] D = (double[,])A.Clone();

            const int maxIterations = 50;
            const double eps = 1e-15;

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // Find maximum off-diagonal element
                int p = 0, q = 1;
                double maxOff = Math.Abs(D[0, 1]);
                if (Math.Abs(D[0, 2]) > maxOff) { p = 0; q = 2; maxOff = Math.Abs(D[0, 2]); }
                if (Math.Abs(D[1, 2]) > maxOff) { p = 1; q = 2; maxOff = Math.Abs(D[1, 2]); }

                if (maxOff < eps) break;

                // Jacobi rotation for (p, q)
                double app = D[p, p];
                double aqq = D[q, q];
                double apq = D[p, q];

                double theta = (aqq - app) / (2.0 * apq);
                double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                if (double.IsNaN(t)) t = 0;

                double c = 1.0 / Math.Sqrt(t * t + 1.0);
                double s = t * c;

                // Update D
                D[p, p] -= t * apq;
                D[q, q] += t * apq;
                D[p, q] = 0;
                D[q, p] = 0;

                for (int r = 0; r < 3; r++)
                {
                    if (r != p && r != q)
                    {
                        double arp = D[r, p];
                        double arq = D[r, q];
                        D[r, p] = c * arp - s * arq;
                        D[p, r] = D[r, p];
                        D[r, q] = s * arp + c * arq;
                        D[q, r] = D[r, q];
                    }
                }

                // Update eigenvectors V
                for (int r = 0; r < 3; r++)
                {
                    double vrp = V[r, p];
                    double vrq = V[r, q];
                    V[r, p] = c * vrp - s * vrq;
                    V[r, q] = s * vrp + c * vrq;
                }
            }

            eigenvalues = new double[] { Math.Max(0.0, D[0, 0]), Math.Max(0.0, D[1, 1]), Math.Max(0.0, D[2, 2]) };
        }
    }
}
