using System;
using System.Drawing;

namespace ZeroGraphics.Vision.Contours
{
    /// <summary>
    /// Represents a 2D cubic Bézier curve defined by four control points (P0, P1, P2, P3).
    /// Provides Bernstein polynomial evaluation, de Casteljau subdivision, arc length, and bounding box.
    /// </summary>
    public readonly struct CubicBezier2D : IEquatable<CubicBezier2D>
    {
        public PointF P0 { get; }
        public PointF P1 { get; }
        public PointF P2 { get; }
        public PointF P3 { get; }

        public CubicBezier2D(PointF p0, PointF p1, PointF p2, PointF p3)
        {
            P0 = p0;
            P1 = p1;
            P2 = p2;
            P3 = p3;
        }

        public CubicBezier2D(float x0, float y0, float x1, float y1, float x2, float y2, float x3, float y3)
            : this(new PointF(x0, y0), new PointF(x1, y1), new PointF(x2, y2), new PointF(x3, y3))
        {
        }

        /// <summary>
        /// Evaluates the curve point B(t) at normalized parameter t in [0, 1].
        /// Uses Horner's form of the cubic Bernstein polynomial:
        /// B(t) = (1-t)^3 * P0 + 3*(1-t)^2 * t * P1 + 3*(1-t) * t^2 * P2 + t^3 * P3.
        /// </summary>
        public PointF Evaluate(float t)
        {
            float u = 1.0f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            float x = uuu * P0.X + 3.0f * uu * t * P1.X + 3.0f * u * tt * P2.X + ttt * P3.X;
            float y = uuu * P0.Y + 3.0f * uu * t * P1.Y + 3.0f * u * tt * P2.Y + ttt * P3.Y;

            return new PointF(x, y);
        }

        /// <summary>
        /// Evaluates the first derivative (tangent vector) B'(t) at parameter t in [0, 1].
        /// B'(t) = 3*(1-t)^2 * (P1 - P0) + 6*(1-t)*t * (P2 - P1) + 3*t^2 * (P3 - P2).
        /// </summary>
        public PointF Derivative(float t)
        {
            float u = 1.0f - t;
            float c0 = 3.0f * u * u;
            float c1 = 6.0f * u * t;
            float c2 = 3.0f * t * t;

            float dx = c0 * (P1.X - P0.X) + c1 * (P2.X - P1.X) + c2 * (P3.X - P2.X);
            float dy = c0 * (P1.Y - P0.Y) + c1 * (P2.Y - P1.Y) + c2 * (P3.Y - P2.Y);

            return new PointF(dx, dy);
        }

        /// <summary>
        /// Evaluates the second derivative B''(t) at parameter t in [0, 1].
        /// B''(t) = 6*(1-t) * (P2 - 2*P1 + P0) + 6*t * (P3 - 2*P2 + P1).
        /// </summary>
        public PointF SecondDerivative(float t)
        {
            float u = 1.0f - t;
            float c0 = 6.0f * u;
            float c1 = 6.0f * t;

            float ddx = c0 * (P2.X - 2.0f * P1.X + P0.X) + c1 * (P3.X - 2.0f * P2.X + P1.X);
            float ddy = c0 * (P2.Y - 2.0f * P1.Y + P0.Y) + c1 * (P3.Y - 2.0f * P2.Y + P1.Y);

            return new PointF(ddx, ddy);
        }

        /// <summary>
        /// Splits this curve into two sub-curves at parameter t using de Casteljau's algorithm.
        /// </summary>
        public void Split(float t, out CubicBezier2D left, out CubicBezier2D right)
        {
            // Level 1
            PointF p01 = Lerp(P0, P1, t);
            PointF p12 = Lerp(P1, P2, t);
            PointF p23 = Lerp(P2, P3, t);

            // Level 2
            PointF p012 = Lerp(p01, p12, t);
            PointF p123 = Lerp(p12, p23, t);

            // Level 3
            PointF p0123 = Lerp(p012, p123, t);

            left = new CubicBezier2D(P0, p01, p012, p0123);
            right = new CubicBezier2D(p0123, p123, p23, P3);
        }

        /// <summary>
        /// Estimates the arc length of the curve by numerical chord integration with the specified number of steps.
        /// </summary>
        public float EstimateLength(int steps = 16)
        {
            if (steps < 1) steps = 1;
            float length = 0.0f;
            PointF prev = P0;
            float stepSize = 1.0f / steps;

            for (int i = 1; i <= steps; i++)
            {
                float t = i * stepSize;
                PointF curr = Evaluate(t);
                float dx = curr.X - prev.X;
                float dy = curr.Y - prev.Y;
                length += (float)Math.Sqrt(dx * dx + dy * dy);
                prev = curr;
            }

            return length;
        }

        /// <summary>
        /// Computes the bounding box enclosing the control polygon (which guarantees encloses the curve).
        /// </summary>
        public RectangleF GetBoundingBox()
        {
            float minX = Math.Min(Math.Min(P0.X, P1.X), Math.Min(P2.X, P3.X));
            float minY = Math.Min(Math.Min(P0.Y, P1.Y), Math.Min(P2.Y, P3.Y));
            float maxX = Math.Max(Math.Max(P0.X, P1.X), Math.Max(P2.X, P3.X));
            float maxY = Math.Max(Math.Max(P0.Y, P1.Y), Math.Max(P2.Y, P3.Y));

            return new RectangleF(minX, minY, Math.Max(0.0f, maxX - minX), Math.Max(0.0f, maxY - minY));
        }

        private static PointF Lerp(PointF a, PointF b, float t)
        {
            return new PointF(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        }

        public bool Equals(CubicBezier2D other) =>
            P0.Equals(other.P0) && P1.Equals(other.P1) && P2.Equals(other.P2) && P3.Equals(other.P3);

        public override bool Equals(object? obj) => obj is CubicBezier2D other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = P0.GetHashCode();
                hash = (hash * 397) ^ P1.GetHashCode();
                hash = (hash * 397) ^ P2.GetHashCode();
                hash = (hash * 397) ^ P3.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(CubicBezier2D left, CubicBezier2D right) => left.Equals(right);
        public static bool operator !=(CubicBezier2D left, CubicBezier2D right) => !left.Equals(right);

        public override string ToString() => $"CubicBezier2D(P0={P0}, P1={P1}, P2={P2}, P3={P3})";
    }
}
