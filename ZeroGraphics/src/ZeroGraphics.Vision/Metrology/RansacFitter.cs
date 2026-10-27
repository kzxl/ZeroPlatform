using System;
using System.Collections.Generic;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// Result structure for RANSAC fitting operations containing the best model and inlier statistics.
    /// </summary>
    public readonly struct RansacResult<TModel>
    {
        public TModel Model { get; }
        public int InlierCount { get; }
        public int TotalCount { get; }
        public double InlierRatio => TotalCount > 0 ? (double)InlierCount / TotalCount : 0.0;
        public IReadOnlyList<VisionPoint2D> Inliers { get; }

        public RansacResult(TModel model, int inlierCount, int totalCount, IReadOnlyList<VisionPoint2D> inliers)
        {
            Model = model;
            InlierCount = inlierCount;
            TotalCount = totalCount;
            Inliers = inliers;
        }
    }

    /// <summary>
    /// Random Sample Consensus (RANSAC) robust estimator for machine vision edge and metrology fitting.
    /// Eliminates noise, burrs, dust, and scratch outliers from caliper edge samples.
    /// </summary>
    public static class RansacFitter
    {
        /// <summary>
        /// Robustly fits a 2D line to noisy points by rejecting outliers using RANSAC followed by Total Least Squares.
        /// </summary>
        /// <param name="points">Set of detected edge points.</param>
        /// <param name="distanceThreshold">Maximum perpendicular distance to be considered an inlier.</param>
        /// <param name="maxIterations">Maximum number of RANSAC iterations.</param>
        /// <param name="minInlierRatio">Minimum required ratio of inliers to accept the model.</param>
        /// <param name="seed">Optional random seed for deterministic reproduction.</param>
        public static RansacResult<FittedLine2D> FitLineRansac(
            IReadOnlyList<VisionPoint2D> points,
            double distanceThreshold = 2.0,
            int maxIterations = 100,
            double minInlierRatio = 0.5,
            int? seed = null)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            int n = points.Count;
            if (n < 2) throw new ArgumentException("At least 2 points are required for line fitting.", nameof(points));

            var rng = seed.HasValue ? new Random(seed.Value) : new Random(42);
            int bestInlierCount = 0;
            var bestInliers = new List<VisionPoint2D>();
            var tempInliers = new List<VisionPoint2D>(n);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // Sample 2 random distinct points
                int idx1 = rng.Next(n);
                int idx2 = rng.Next(n);
                if (idx1 == idx2) continue;

                VisionPoint2D p1 = points[idx1];
                VisionPoint2D p2 = points[idx2];

                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-6) continue;

                // Line equation in normalized form: A*x + B*y + C = 0
                double a = -dy / len;
                double b = dx / len;
                double c = -(a * p1.X + b * p1.Y);

                // Count inliers
                tempInliers.Clear();
                for (int i = 0; i < n; i++)
                {
                    double dist = Math.Abs(a * points[i].X + b * points[i].Y + c);
                    if (dist <= distanceThreshold)
                    {
                        tempInliers.Add(points[i]);
                    }
                }

                if (tempInliers.Count > bestInlierCount)
                {
                    bestInlierCount = tempInliers.Count;
                    bestInliers.Clear();
                    bestInliers.AddRange(tempInliers);

                    // Early exit if vast majority are inliers
                    if ((double)bestInlierCount / n > 0.95)
                        break;
                }
            }

            if (bestInliers.Count < 2 || ((double)bestInliers.Count / n) < minInlierRatio)
            {
                // Fallback to standard Total Least Squares across all points if RANSAC didn't meet ratio
                var fallbackLine = GeometryFitters.FitLine(points);
                return new RansacResult<FittedLine2D>(fallbackLine, bestInliers.Count, n, bestInliers);
            }

            // Refine model with Total Least Squares using only inliers
            var refinedLine = GeometryFitters.FitLine(bestInliers);
            return new RansacResult<FittedLine2D>(refinedLine, bestInliers.Count, n, bestInliers);
        }

        /// <summary>
        /// Robustly fits a 2D circle to noisy points by rejecting outliers using RANSAC followed by Taubin fitting.
        /// </summary>
        /// <param name="points">Set of detected edge points.</param>
        /// <param name="distanceThreshold">Maximum radial distance deviation to be considered an inlier.</param>
        /// <param name="maxIterations">Maximum number of RANSAC iterations.</param>
        /// <param name="minInlierRatio">Minimum required ratio of inliers to accept the model.</param>
        /// <param name="seed">Optional random seed for deterministic reproduction.</param>
        public static RansacResult<FittedCircle2D> FitCircleRansac(
            IReadOnlyList<VisionPoint2D> points,
            double distanceThreshold = 2.0,
            int maxIterations = 150,
            double minInlierRatio = 0.5,
            int? seed = null)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            int n = points.Count;
            if (n < 3) throw new ArgumentException("At least 3 points are required for circle fitting.", nameof(points));

            var rng = seed.HasValue ? new Random(seed.Value) : new Random(42);
            int bestInlierCount = 0;
            double bestSqErr = double.MaxValue;
            var bestInliers = new List<VisionPoint2D>();
            var tempInliers = new List<VisionPoint2D>(n);

            for (int iter = 0; iter < maxIterations; iter++)
            {
                // Sample 3 random distinct points
                int idx1 = rng.Next(n);
                int idx2 = rng.Next(n);
                int idx3 = rng.Next(n);
                if (idx1 == idx2 || idx2 == idx3 || idx1 == idx3) continue;

                VisionPoint2D p1 = points[idx1];
                VisionPoint2D p2 = points[idx2];
                VisionPoint2D p3 = points[idx3];

                // Ensure samples are not clustered too close together (degeneracy check)
                double d12 = (p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y);
                double d23 = (p2.X - p3.X) * (p2.X - p3.X) + (p2.Y - p3.Y) * (p2.Y - p3.Y);
                double d31 = (p3.X - p1.X) * (p3.X - p1.X) + (p3.Y - p1.Y) * (p3.Y - p1.Y);
                if (d12 < 25.0 || d23 < 25.0 || d31 < 25.0) continue;

                // Exact 3-point circle calculation
                double d = 2.0 * (p1.X * (p2.Y - p3.Y) + p2.X * (p3.Y - p1.Y) + p3.X * (p1.Y - p2.Y));
                if (Math.Abs(d) < 1e-6) continue; // Collinear points

                double p1Sq = p1.X * p1.X + p1.Y * p1.Y;
                double p2Sq = p2.X * p2.X + p2.Y * p2.Y;
                double p3Sq = p3.X * p3.X + p3.Y * p3.Y;

                double cx = (p1Sq * (p2.Y - p3.Y) + p2Sq * (p3.Y - p1.Y) + p3Sq * (p1.Y - p2.Y)) / d;
                double cy = (p1Sq * (p3.X - p2.X) + p2Sq * (p1.X - p3.X) + p3Sq * (p2.X - p1.X)) / d;
                double r = Math.Sqrt((p1.X - cx) * (p1.X - cx) + (p1.Y - cy) * (p1.Y - cy));

                tempInliers.Clear();
                double currentSqErr = 0.0;
                for (int i = 0; i < n; i++)
                {
                    double dist = Math.Abs(Math.Sqrt((points[i].X - cx) * (points[i].X - cx) + (points[i].Y - cy) * (points[i].Y - cy)) - r);
                    if (dist <= distanceThreshold)
                    {
                        tempInliers.Add(points[i]);
                        currentSqErr += dist * dist;
                    }
                }

                if (tempInliers.Count > bestInlierCount || (tempInliers.Count == bestInlierCount && currentSqErr < bestSqErr))
                {
                    bestInlierCount = tempInliers.Count;
                    bestSqErr = currentSqErr;
                    bestInliers.Clear();
                    bestInliers.AddRange(tempInliers);

                    if ((double)bestInlierCount / n > 0.95)
                        break;
                }
            }

            if (bestInliers.Count < 3 || ((double)bestInliers.Count / n) < minInlierRatio)
            {
                var fallbackCircle = GeometryFitters.FitCircle(points);
                return new RansacResult<FittedCircle2D>(fallbackCircle, bestInliers.Count, n, bestInliers);
            }

            var refinedCircle = GeometryFitters.FitCircle(bestInliers);
            return new RansacResult<FittedCircle2D>(refinedCircle, bestInliers.Count, n, bestInliers);
        }
    }
}
