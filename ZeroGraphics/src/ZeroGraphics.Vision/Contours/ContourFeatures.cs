using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZeroGraphics.Vision.Contours
{
    /// <summary>
    /// Geometric analysis and polygon processing for extracted contour chains.
    /// Includes area, perimeter, centroid, Ramer-Douglas-Peucker polygon approximation, and point-in-polygon tests.
    /// </summary>
    public static class ContourFeatures
    {
        /// <summary>
        /// Computes the signed/unsigned polygonal area of a contour using Green's theorem (Shoelace formula).
        /// </summary>
        public static double ComputeArea(IReadOnlyList<Point> points, bool absolute = true)
        {
            if (points == null || points.Count < 3) return 0.0;

            double sum = 0.0;
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % n];
                sum += (double)p1.X * p2.Y - (double)p2.X * p1.Y;
            }

            double area = 0.5 * sum;
            return absolute ? Math.Abs(area) : area;
        }

        /// <summary>
        /// Computes the total perimeter length of a contour.
        /// </summary>
        public static double ComputePerimeter(IReadOnlyList<Point> points, bool closed = true)
        {
            if (points == null || points.Count < 2) return 0.0;

            double length = 0.0;
            int count = closed ? points.Count : points.Count - 1;

            for (int i = 0; i < count; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                double dx = p2.X - p1.X;
                double dy = p2.Y - p1.Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return length;
        }

        /// <summary>
        /// Computes the center of mass (centroid) of a polygonal contour.
        /// </summary>
        public static PointF ComputeCentroid(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count == 0) return PointF.Empty;
            if (points.Count < 3)
            {
                float sx = 0, sy = 0;
                for (int i = 0; i < points.Count; i++)
                {
                    sx += points[i].X;
                    sy += points[i].Y;
                }
                return new PointF(sx / points.Count, sy / points.Count);
            }

            double areaSum = 0.0;
            double cx = 0.0;
            double cy = 0.0;
            int n = points.Count;

            for (int i = 0; i < n; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % n];
                double cross = (double)p1.X * p2.Y - (double)p2.X * p1.Y;
                areaSum += cross;
                cx += (p1.X + p2.X) * cross;
                cy += (p1.Y + p2.Y) * cross;
            }

            double area = 0.5 * areaSum;
            if (Math.Abs(area) < 1e-6)
            {
                // Degenerate polygon: fallback to vertex mean
                float sx = 0, sy = 0;
                for (int i = 0; i < n; i++) { sx += points[i].X; sy += points[i].Y; }
                return new PointF(sx / n, sy / n);
            }

            double factor = 1.0 / (6.0 * area);
            return new PointF((float)(cx * factor), (float)(cy * factor));
        }

        /// <summary>
        /// Computes the axis-aligned bounding box enclosing all points in the contour.
        /// </summary>
        public static Rectangle ComputeBoundingBox(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count == 0) return Rectangle.Empty;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                if (p.X < minX) minX = p.X;
                if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y;
                if (p.Y > maxY) maxY = p.Y;
            }

            return new Rectangle(minX, minY, Math.Max(0, maxX - minX + 1), Math.Max(0, maxY - minY + 1));
        }

        /// <summary>
        /// Simplifies a polygon contour using the Ramer-Douglas-Peucker (RDP) algorithm with tolerance epsilon.
        /// </summary>
        public static List<Point> ApproximatePolygon(IReadOnlyList<Point> points, double epsilon, bool closed = true)
        {
            if (points == null || points.Count <= 2 || epsilon <= 0.0)
                return points != null ? new List<Point>(points) : new List<Point>();

            var result = new List<Point>();
            RdpRecursive(points, 0, points.Count - 1, epsilon, result);

            if (closed && result.Count >= 3)
            {
                // Ensure closure connection
                var first = result[0];
                var last = result[result.Count - 1];
                if (first.X != last.X || first.Y != last.Y)
                {
                    result.Add(first);
                }
            }

            return result;
        }

        private static void RdpRecursive(
            IReadOnlyList<Point> points,
            int startIdx,
            int endIdx,
            double epsilon,
            List<Point> outList)
        {
            if (startIdx >= endIdx) return;

            // Find point with maximum perpendicular distance to segment [startIdx, endIdx]
            double maxDist = 0.0;
            int maxIdx = startIdx;

            Point pStart = points[startIdx];
            Point pEnd = points[endIdx];

            for (int i = startIdx + 1; i < endIdx; i++)
            {
                double dist = PerpendicularDistance(points[i], pStart, pEnd);
                if (dist > maxDist)
                {
                    maxDist = dist;
                    maxIdx = i;
                }
            }

            if (maxDist > epsilon)
            {
                // Split and recurse
                RdpRecursive(points, startIdx, maxIdx, epsilon, outList);
                RdpRecursive(points, maxIdx, endIdx, epsilon, outList);
            }
            else
            {
                if (outList.Count == 0 || outList[outList.Count - 1] != pStart)
                {
                    outList.Add(pStart);
                }
                outList.Add(pEnd);
            }
        }

        private static double PerpendicularDistance(Point pt, Point lineStart, Point lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            double lenSq = dx * dx + dy * dy;

            if (lenSq < 1e-8)
            {
                double px = pt.X - lineStart.X;
                double py = pt.Y - lineStart.Y;
                return Math.Sqrt(px * px + py * py);
            }

            // Distance from point to line formula: |dy*x0 - dx*y0 + x2*y1 - y2*x1| / sqrt(dx^2 + dy^2)
            double num = Math.Abs(dy * pt.X - dx * pt.Y + lineEnd.X * lineStart.Y - lineEnd.Y * lineStart.X);
            return num / Math.Sqrt(lenSq);
        }

        /// <summary>
        /// Tests whether a coordinate (x, y) lies inside the polygon using the ray casting algorithm.
        /// </summary>
        public static bool ContainsPoint(IReadOnlyList<Point> points, double x, double y)
        {
            if (points == null || points.Count < 3) return false;

            bool inside = false;
            int n = points.Count;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = points[i];
                var pj = points[j];

                if (((pi.Y > y) != (pj.Y > y)) &&
                    (x < (double)(pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
