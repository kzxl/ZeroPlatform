using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;

namespace ZeroGraphics.Vision.Contours
{
    /// <summary>
    /// Represents a vectorized geometric path consisting of continuous cubic Bézier curve segments.
    /// Provides SVG path data serialization ("M ... C ... Z") and full SVG document export.
    /// </summary>
    public sealed class VectorPath
    {
        public List<CubicBezier2D> Curves { get; }
        public bool IsClosed { get; set; }

        public VectorPath(bool isClosed = false)
        {
            Curves = new List<CubicBezier2D>();
            IsClosed = isClosed;
        }

        public VectorPath(IEnumerable<CubicBezier2D> curves, bool isClosed = false)
        {
            Curves = new List<CubicBezier2D>(curves);
            IsClosed = isClosed;
        }

        /// <summary>
        /// Generates the standard SVG path 'd' attribute string (e.g. "M x0 y0 C c1x c1y, c2x c2y, x1 y1 ... Z").
        /// </summary>
        public string ToSvgPathData(int decimals = 2)
        {
            if (Curves.Count == 0) return string.Empty;

            var sb = new StringBuilder(Curves.Count * 64);
            string fmt = "0." + new string('#', decimals);

            var first = Curves[0];
            sb.AppendFormat(CultureInfo.InvariantCulture, "M {0} {1}", first.P0.X.ToString(fmt, CultureInfo.InvariantCulture), first.P0.Y.ToString(fmt, CultureInfo.InvariantCulture));

            for (int i = 0; i < Curves.Count; i++)
            {
                var c = Curves[i];
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    " C {0} {1}, {2} {3}, {4} {5}",
                    c.P1.X.ToString(fmt, CultureInfo.InvariantCulture),
                    c.P1.Y.ToString(fmt, CultureInfo.InvariantCulture),
                    c.P2.X.ToString(fmt, CultureInfo.InvariantCulture),
                    c.P2.Y.ToString(fmt, CultureInfo.InvariantCulture),
                    c.P3.X.ToString(fmt, CultureInfo.InvariantCulture),
                    c.P3.Y.ToString(fmt, CultureInfo.InvariantCulture));
            }

            if (IsClosed)
            {
                sb.Append(" Z");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exports the path as a complete, standalone SVG XML string.
        /// </summary>
        public string ToSvg(int width, int height, string stroke = "#1E88E5", string fill = "none", float strokeWidth = 1.5f)
        {
            string pathData = ToSvgPathData();
            return string.Format(
                CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{0}\" height=\"{1}\" viewBox=\"0 0 {0} {1}\">\n" +
                "  <path d=\"{2}\" stroke=\"{3}\" fill=\"{4}\" stroke-width=\"{5}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />\n" +
                "</svg>",
                width, height, pathData, stroke, fill, strokeWidth.ToString("0.##", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// High-performance Philip J. Schneider curve fitting engine (Graphic Gems I).
    /// Converts discrete raster polygon contours into smooth, minimal cubic Bézier curves within sub-pixel error tolerance.
    /// </summary>
    public static class BezierCurveFitter
    {
        /// <summary>
        /// Fits an optimal sequence of cubic Bézier curves to a sequence of discrete 2D points.
        /// </summary>
        /// <param name="points">Ordered points along the contour.</param>
        /// <param name="errorTolerance">Maximum allowed Euclidean distance (pixels) between sample points and fitted curves.</param>
        /// <param name="maxReparameterizations">Maximum Newton-Raphson iteration passes per curve segment before splitting.</param>
        /// <returns>A VectorPath containing fitted cubic Bézier segments.</returns>
        public static VectorPath Fit(IReadOnlyList<PointF> points, double errorTolerance = 1.0, int maxReparameterizations = 4)
        {
            if (points == null || points.Count == 0) return new VectorPath();
            if (points.Count == 1)
            {
                var pt = points[0];
                var single = new CubicBezier2D(pt, pt, pt, pt);
                return new VectorPath(new[] { single });
            }

            // Detect if closed
            float dx = points[points.Count - 1].X - points[0].X;
            float dy = points[points.Count - 1].Y - points[0].Y;
            bool isClosed = (dx * dx + dy * dy) < 0.25f && points.Count >= 3;

            var curves = new List<CubicBezier2D>();

            PointF tHat1 = ComputeLeftTangent(points, 0);
            PointF tHat2 = ComputeRightTangent(points, points.Count - 1);

            FitCubicRecursive(points, 0, points.Count - 1, tHat1, tHat2, (float)errorTolerance, maxReparameterizations, curves);

            return new VectorPath(curves, isClosed);
        }

        /// <summary>
        /// Fits Bézier curves to integer Point contours (e.g. from ContourTracer).
        /// </summary>
        public static VectorPath Fit(IReadOnlyList<Point> points, double errorTolerance = 1.0, int maxReparameterizations = 4)
        {
            if (points == null || points.Count == 0) return new VectorPath();

            var floatPoints = new PointF[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                floatPoints[i] = new PointF(points[i].X, points[i].Y);
            }

            return Fit(floatPoints, errorTolerance, maxReparameterizations);
        }

        private static void FitCubicRecursive(
            IReadOnlyList<PointF> points,
            int first,
            int last,
            PointF tHat1,
            PointF tHat2,
            float errorTolerance,
            int maxReparameterizations,
            List<CubicBezier2D> resultCurves)
        {
            int nPoints = last - first + 1;
            if (nPoints <= 2)
            {
                // 2 points: fit straight line as cubic bezier
                PointF p0 = points[first];
                PointF p3 = points[last];
                float dist = (float)Math.Sqrt((p3.X - p0.X) * (p3.X - p0.X) + (p3.Y - p0.Y) * (p3.Y - p0.Y)) / 3.0f;
                PointF p1 = new PointF(p0.X + tHat1.X * dist, p0.Y + tHat1.Y * dist);
                PointF p2 = new PointF(p3.X + tHat2.X * dist, p3.Y + tHat2.Y * dist);
                resultCurves.Add(new CubicBezier2D(p0, p1, p2, p3));
                return;
            }

            // 1. Parameterize points using chord length
            float[] u = ChordLengthParameterize(points, first, last);

            // 2. Generate initial least-squares fitted curve
            CubicBezier2D bezier = GenerateBezier(points, first, last, u, tHat1, tHat2);

            // 3. Find point of maximum error
            float maxError = ComputeMaxError(points, first, last, bezier, u, out int splitPoint);

            if (maxError <= errorTolerance)
            {
                resultCurves.Add(bezier);
                return;
            }

            // 4. Try Newton-Raphson re-parameterization if error is not exceedingly large
            if (maxError < errorTolerance * 4.0f && maxReparameterizations > 0)
            {
                for (int iter = 0; iter < maxReparameterizations; iter++)
                {
                    float[] uPrime = Reparameterize(points, first, last, u, bezier);
                    bezier = GenerateBezier(points, first, last, uPrime, tHat1, tHat2);
                    maxError = ComputeMaxError(points, first, last, bezier, uPrime, out splitPoint);

                    if (maxError <= errorTolerance)
                    {
                        resultCurves.Add(bezier);
                        return;
                    }

                    u = uPrime;
                }
            }

            // 5. Subdivision: split at worst fitting point
            if (splitPoint <= first) splitPoint = first + 1;
            if (splitPoint >= last) splitPoint = last - 1;

            PointF tHatCenter = ComputeCenterTangent(points, splitPoint);
            PointF tHatCenterOpposite = new PointF(-tHatCenter.X, -tHatCenter.Y);

            FitCubicRecursive(points, first, splitPoint, tHat1, tHatCenter, errorTolerance, maxReparameterizations, resultCurves);
            FitCubicRecursive(points, splitPoint, last, tHatCenterOpposite, tHat2, errorTolerance, maxReparameterizations, resultCurves);
        }

        private static CubicBezier2D GenerateBezier(
            IReadOnlyList<PointF> points,
            int first,
            int last,
            float[] u,
            PointF tHat1,
            PointF tHat2)
        {
            PointF p0 = points[first];
            PointF p3 = points[last];

            float c00 = 0.0f, c01 = 0.0f, c11 = 0.0f;
            float x0 = 0.0f, x1 = 0.0f;

            int n = last - first + 1;
            for (int i = 0; i < n; i++)
            {
                float t = u[i];
                float omt = 1.0f - t;

                // Bernstein basis
                float b0 = omt * omt * omt;
                float b1 = 3.0f * t * omt * omt;
                float b2 = 3.0f * t * t * omt;
                float b3 = t * t * t;

                PointF a1 = new PointF(tHat1.X * b1, tHat1.Y * b1);
                PointF a2 = new PointF(tHat2.X * b2, tHat2.Y * b2);

                c00 += a1.X * a1.X + a1.Y * a1.Y;
                c01 += a1.X * a2.X + a1.Y * a2.Y;
                c11 += a2.X * a2.X + a2.Y * a2.Y;

                PointF pt = points[first + i];
                float rx = pt.X - (p0.X * (b0 + b1) + p3.X * (b2 + b3));
                float ry = pt.Y - (p0.Y * (b0 + b1) + p3.Y * (b2 + b3));

                x0 += a1.X * rx + a1.Y * ry;
                x1 += a2.X * rx + a2.Y * ry;
            }

            float det = c00 * c11 - c01 * c01;
            float alpha1, alpha2;

            if (Math.Abs(det) > 1e-6f)
            {
                alpha1 = (x0 * c11 - x1 * c01) / det;
                alpha2 = (c00 * x1 - c01 * x0) / det;
            }
            else
            {
                float chord = (float)Math.Sqrt((p3.X - p0.X) * (p3.X - p0.X) + (p3.Y - p0.Y) * (p3.Y - p0.Y));
                alpha1 = chord / 3.0f;
                alpha2 = chord / 3.0f;
            }

            // Fallback for negative alphas (which would loop backward)
            if (alpha1 < 1e-4f || alpha2 < 1e-4f)
            {
                float chord = (float)Math.Sqrt((p3.X - p0.X) * (p3.X - p0.X) + (p3.Y - p0.Y) * (p3.Y - p0.Y));
                alpha1 = chord / 3.0f;
                alpha2 = chord / 3.0f;
            }

            PointF p1 = new PointF(p0.X + tHat1.X * alpha1, p0.Y + tHat1.Y * alpha1);
            PointF p2 = new PointF(p3.X + tHat2.X * alpha2, p3.Y + tHat2.Y * alpha2);

            return new CubicBezier2D(p0, p1, p2, p3);
        }

        private static float[] Reparameterize(
            IReadOnlyList<PointF> points,
            int first,
            int last,
            float[] u,
            CubicBezier2D bezier)
        {
            int n = last - first + 1;
            float[] uPrime = new float[n];

            for (int i = 0; i < n; i++)
            {
                uPrime[i] = NewtonRaphsonRootFind(bezier, points[first + i], u[i]);
            }

            return uPrime;
        }

        private static float NewtonRaphsonRootFind(CubicBezier2D bezier, PointF p, float u)
        {
            PointF q = bezier.Evaluate(u);
            PointF qPrime = bezier.Derivative(u);
            PointF qPrime2 = bezier.SecondDerivative(u);

            float diffX = q.X - p.X;
            float diffY = q.Y - p.Y;

            float num = diffX * qPrime.X + diffY * qPrime.Y;
            float denom = qPrime.X * qPrime.X + qPrime.Y * qPrime.Y + diffX * qPrime2.X + diffY * qPrime2.Y;

            if (Math.Abs(denom) < 1e-6f) return u;

            float uNext = u - num / denom;
            if (uNext < 0.0f) return 0.0f;
            if (uNext > 1.0f) return 1.0f;
            return uNext;
        }

        private static float ComputeMaxError(
            IReadOnlyList<PointF> points,
            int first,
            int last,
            CubicBezier2D bezier,
            float[] u,
            out int splitPoint)
        {
            splitPoint = (first + last) / 2;
            float maxDistSq = 0.0f;

            int n = last - first + 1;
            for (int i = 1; i < n - 1; i++)
            {
                PointF ptOnCurve = bezier.Evaluate(u[i]);
                PointF actualPt = points[first + i];

                float dx = ptOnCurve.X - actualPt.X;
                float dy = ptOnCurve.Y - actualPt.Y;
                float distSq = dx * dx + dy * dy;

                if (distSq >= maxDistSq)
                {
                    maxDistSq = distSq;
                    splitPoint = first + i;
                }
            }

            return (float)Math.Sqrt(maxDistSq);
        }

        private static float[] ChordLengthParameterize(IReadOnlyList<PointF> points, int first, int last)
        {
            int n = last - first + 1;
            float[] u = new float[n];
            u[0] = 0.0f;

            for (int i = 1; i < n; i++)
            {
                float dx = points[first + i].X - points[first + i - 1].X;
                float dy = points[first + i].Y - points[first + i - 1].Y;
                u[i] = u[i - 1] + (float)Math.Sqrt(dx * dx + dy * dy);
            }

            float totalLength = u[n - 1];
            if (totalLength > 1e-6f)
            {
                for (int i = 1; i < n; i++)
                {
                    u[i] /= totalLength;
                }
            }

            return u;
        }

        private static PointF ComputeLeftTangent(IReadOnlyList<PointF> points, int index)
        {
            PointF p0 = points[index];
            PointF p1 = points[Math.Min(index + 1, points.Count - 1)];
            return Normalize(new PointF(p1.X - p0.X, p1.Y - p0.Y));
        }

        private static PointF ComputeRightTangent(IReadOnlyList<PointF> points, int index)
        {
            PointF p0 = points[Math.Max(index - 1, 0)];
            PointF p1 = points[index];
            return Normalize(new PointF(p0.X - p1.X, p0.Y - p1.Y));
        }

        private static PointF ComputeCenterTangent(IReadOnlyList<PointF> points, int index)
        {
            PointF pPrev = points[Math.Max(index - 1, 0)];
            PointF pNext = points[Math.Min(index + 1, points.Count - 1)];
            return Normalize(new PointF(pNext.X - pPrev.X, pNext.Y - pPrev.Y));
        }

        private static PointF Normalize(PointF v)
        {
            float len = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (len > 1e-6f)
            {
                return new PointF(v.X / len, v.Y / len);
            }
            return new PointF(1.0f, 0.0f);
        }
    }
}
