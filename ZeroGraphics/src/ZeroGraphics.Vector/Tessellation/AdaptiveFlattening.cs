using System;
using System.Collections.Generic;
using ZeroGraphics.Vector.Geometry;

namespace ZeroGraphics.Vector.Tessellation
{
    /// <summary>
    /// High-speed adaptive curve flattening engine.
    /// Decomposes quadratic and cubic Bézier curves in a Path2D into contiguous piecewise linear polylines
    /// with guaranteed sub-pixel error tolerance.
    /// </summary>
    public static class AdaptiveFlattening
    {
        private const int MaxRecursionDepth = 12;

        /// <summary>
        /// Flattens a Path2D into a list of linear subpaths (polylines).
        /// </summary>
        /// <param name="path">Source vector path.</param>
        /// <param name="tolerance">Maximum permissible chord-to-curve deviation in pixels.</param>
        /// <returns>List of continuous linear polyline chains.</returns>
        public static List<List<VectorPoint>> Flatten(Path2D path, float tolerance = 0.5f)
        {
            if (path == null || path.VerbCount == 0) return new List<List<VectorPoint>>();
            if (tolerance < 0.05f) tolerance = 0.05f;

            var subpaths = new List<List<VectorPoint>>();
            List<VectorPoint>? currentSubpath = null;

            VectorPoint currentPoint = VectorPoint.Zero;
            VectorPoint startPoint = VectorPoint.Zero;

            int ptIdx = 0;
            var verbs = path.Verbs;
            var points = path.Points;

            for (int i = 0; i < verbs.Count; i++)
            {
                var verb = verbs[i];
                switch (verb)
                {
                    case PathVerb.MoveTo:
                        currentPoint = points[ptIdx++];
                        startPoint = currentPoint;
                        currentSubpath = new List<VectorPoint> { currentPoint };
                        subpaths.Add(currentSubpath);
                        break;

                    case PathVerb.LineTo:
                        if (currentSubpath == null)
                        {
                            currentSubpath = new List<VectorPoint> { currentPoint };
                            subpaths.Add(currentSubpath);
                        }
                        currentPoint = points[ptIdx++];
                        currentSubpath.Add(currentPoint);
                        break;

                    case PathVerb.QuadTo:
                        if (currentSubpath == null)
                        {
                            currentSubpath = new List<VectorPoint> { currentPoint };
                            subpaths.Add(currentSubpath);
                        }
                        var qCtrl = points[ptIdx++];
                        var qEnd = points[ptIdx++];
                        FlattenQuadratic(currentPoint, qCtrl, qEnd, tolerance, 0, currentSubpath);
                        currentPoint = qEnd;
                        break;

                    case PathVerb.CubicTo:
                        if (currentSubpath == null)
                        {
                            currentSubpath = new List<VectorPoint> { currentPoint };
                            subpaths.Add(currentSubpath);
                        }
                        var c1 = points[ptIdx++];
                        var c2 = points[ptIdx++];
                        var cEnd = points[ptIdx++];
                        FlattenCubic(currentPoint, c1, c2, cEnd, tolerance, 0, currentSubpath);
                        currentPoint = cEnd;
                        break;

                    case PathVerb.Close:
                        if (currentSubpath != null && currentSubpath.Count > 1)
                        {
                            if ((currentPoint - startPoint).LengthSquared > 1e-4f)
                            {
                                currentSubpath.Add(startPoint);
                            }
                        }
                        currentPoint = startPoint;
                        break;
                }
            }

            return subpaths;
        }

        private static void FlattenQuadratic(
            VectorPoint p0, VectorPoint p1, VectorPoint p2,
            float tolerance, int depth, List<VectorPoint> dest)
        {
            // Midpoint deviation: dist(p1, (p0 + p2)/2) / 2
            VectorPoint midChord = (p0 + p2) * 0.5f;
            float deviation = (p1 - midChord).Length * 0.5f;

            if (deviation <= tolerance || depth >= MaxRecursionDepth)
            {
                dest.Add(p2);
                return;
            }

            // De Casteljau subdivision at t = 0.5
            VectorPoint p01 = (p0 + p1) * 0.5f;
            VectorPoint p12 = (p1 + p2) * 0.5f;
            VectorPoint p012 = (p01 + p12) * 0.5f;

            FlattenQuadratic(p0, p01, p012, tolerance, depth + 1, dest);
            FlattenQuadratic(p012, p12, p2, tolerance, depth + 1, dest);
        }

        private static void FlattenCubic(
            VectorPoint p0, VectorPoint p1, VectorPoint p2, VectorPoint p3,
            float tolerance, int depth, List<VectorPoint> dest)
        {
            // Check distance of control points p1 and p2 from chord (p0, p3)
            float d1 = PointToLineDistanceSquared(p1, p0, p3);
            float d2 = PointToLineDistanceSquared(p2, p0, p3);
            float maxDeviationSq = Math.Max(d1, d2);

            if (maxDeviationSq <= tolerance * tolerance || depth >= MaxRecursionDepth)
            {
                dest.Add(p3);
                return;
            }

            // De Casteljau subdivision at t = 0.5
            VectorPoint p01 = (p0 + p1) * 0.5f;
            VectorPoint p12 = (p1 + p2) * 0.5f;
            VectorPoint p23 = (p2 + p3) * 0.5f;

            VectorPoint p012 = (p01 + p12) * 0.5f;
            VectorPoint p123 = (p12 + p23) * 0.5f;

            VectorPoint p0123 = (p012 + p123) * 0.5f;

            FlattenCubic(p0, p01, p012, p0123, tolerance, depth + 1, dest);
            FlattenCubic(p0123, p123, p23, p3, tolerance, depth + 1, dest);
        }

        private static float PointToLineDistanceSquared(VectorPoint p, VectorPoint a, VectorPoint b)
        {
            float dx = b.X - a.X;
            float dy = b.Y - a.Y;
            float lenSq = dx * dx + dy * dy;

            if (lenSq < 1e-6f)
            {
                return (p - a).LengthSquared;
            }

            float t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lenSq;
            if (t < 0.0f) t = 0.0f;
            else if (t > 1.0f) t = 1.0f;

            VectorPoint proj = new VectorPoint(a.X + t * dx, a.Y + t * dy);
            return (p - proj).LengthSquared;
        }
    }
}
