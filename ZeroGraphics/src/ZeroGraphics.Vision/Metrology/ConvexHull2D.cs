using System;
using System.Collections.Generic;
using ZeroGraphics.Vision.Matching;

namespace ZeroGraphics.Vision.Metrology
{
    /// <summary>
    /// Geometric algorithms for 2D Convex Hull and Minimum-Area Oriented Bounding Box (OBB) using Rotating Calipers.
    /// Essential for automated robotic pick-and-place orientation and workpiece outline analysis.
    /// </summary>
    public static class ConvexHull2D
    {
        /// <summary>
        /// Cross product of vectors OA and OB: (A.X - O.X) * (B.Y - O.Y) - (A.Y - O.Y) * (B.X - O.X).
        /// Positive if counter-clockwise turn, negative if clockwise turn, zero if collinear.
        /// </summary>
        private static double CrossProduct(VisionPoint2D o, VisionPoint2D a, VisionPoint2D b)
        {
            return (a.X - o.X) * (b.Y - o.Y) - (a.Y - o.Y) * (b.X - o.X);
        }

        /// <summary>
        /// Computes the 2D Convex Hull of a set of points using Andrew's Monotone Chain algorithm (O(N log N)).
        /// Returns the vertices of the convex hull in counter-clockwise order.
        /// </summary>
        public static VisionPoint2D[] ComputeConvexHull(IReadOnlyList<VisionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            int n = points.Count;
            if (n <= 1)
            {
                var single = new VisionPoint2D[n];
                for (int i = 0; i < n; i++) single[i] = points[i];
                return single;
            }

            // Copy and sort points lexicographically (by X, then by Y)
            var pts = new VisionPoint2D[n];
            for (int i = 0; i < n; i++) pts[i] = points[i];

            Array.Sort(pts, (p1, p2) =>
            {
                int cmp = p1.X.CompareTo(p2.X);
                return cmp != 0 ? cmp : p1.Y.CompareTo(p2.Y);
            });

            var hull = new VisionPoint2D[2 * n];
            int k = 0;

            // Build lower hull
            for (int i = 0; i < n; i++)
            {
                while (k >= 2 && CrossProduct(hull[k - 2], hull[k - 1], pts[i]) <= 1e-9)
                {
                    k--;
                }
                hull[k++] = pts[i];
            }

            // Build upper hull
            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && CrossProduct(hull[k - 2], hull[k - 1], pts[i]) <= 1e-9)
                {
                    k--;
                }
                hull[k++] = pts[i];
            }

            // Remove the duplicate last point (same as first point)
            int hullCount = Math.Max(1, k - 1);
            var result = new VisionPoint2D[hullCount];
            Array.Copy(hull, result, hullCount);
            return result;
        }

        /// <summary>
        /// Computes the Minimum-Area Oriented Bounding Box (OBB) enclosing the given points using Rotating Calipers.
        /// </summary>
        public static RotatedRect2D ComputeMinimumAreaBoundingBox(IReadOnlyList<VisionPoint2D> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            if (points.Count == 0)
                return new RotatedRect2D(0, 0, 0, 0, 0);

            if (points.Count == 1)
                return new RotatedRect2D(points[0].X, points[0].Y, 0, 0, 0);

            // 1. Compute convex hull
            var hull = ComputeConvexHull(points);
            int hCount = hull.Length;

            if (hCount == 2)
            {
                double dx = hull[1].X - hull[0].X;
                double dy = hull[1].Y - hull[0].Y;
                double len = Math.Sqrt(dx * dx + dy * dy);
                double angleDeg = Math.Atan2(dy, dx) * (180.0 / Math.PI);
                return new RotatedRect2D((hull[0].X + hull[1].X) * 0.5, (hull[0].Y + hull[1].Y) * 0.5, len, 0, angleDeg);
            }

            // 2. Rotating Calipers over hull edges
            double minArea = double.MaxValue;
            RotatedRect2D bestRect = default;

            for (int i = 0; i < hCount; i++)
            {
                VisionPoint2D p1 = hull[i];
                VisionPoint2D p2 = hull[(i + 1) % hCount];

                double edgeDx = p2.X - p1.X;
                double edgeDy = p2.Y - p1.Y;
                double edgeLen = Math.Sqrt(edgeDx * edgeDx + edgeDy * edgeDy);
                if (edgeLen < 1e-9) continue;

                // Unit direction vector along edge and normal vector
                double uX = edgeDx / edgeLen;
                double uY = edgeDy / edgeLen;
                double nX = -uY;
                double nY = uX;

                // Project all hull points onto (u, n) axes
                double minU = double.MaxValue, maxU = double.MinValue;
                double minN = double.MaxValue, maxN = double.MinValue;

                for (int j = 0; j < hCount; j++)
                {
                    double vx = hull[j].X - p1.X;
                    double vy = hull[j].Y - p1.Y;

                    double projU = vx * uX + vy * uY;
                    double projN = vx * nX + vy * nY;

                    if (projU < minU) minU = projU;
                    if (projU > maxU) maxU = projU;
                    if (projN < minN) minN = projN;
                    if (projN > maxN) maxN = projN;
                }

                double width = maxU - minU;
                double height = maxN - minN;
                double area = width * height;

                if (area < minArea)
                {
                    minArea = area;

                    // Center in world coordinates
                    double midU = (minU + maxU) * 0.5;
                    double midN = (minN + maxN) * 0.5;

                    double cx = p1.X + midU * uX + midN * nX;
                    double cy = p1.Y + midU * uY + midN * nY;

                    double angleDeg = Math.Atan2(uY, uX) * (180.0 / Math.PI);
                    // Normalize angle to [-90, 90]
                    while (angleDeg > 90.0) angleDeg -= 180.0;
                    while (angleDeg < -90.0) angleDeg += 180.0;

                    bestRect = new RotatedRect2D(cx, cy, width, height, angleDeg);
                }
            }

            return bestRect;
        }
    }
}
