#nullable enable
using System;
using System.Collections.Generic;

namespace ZeroGeometry.Core.Projective
{
    /// <summary>
    /// 2D Homography Projective Transformations, Hartley-Normalized Direct Linear Transformation (DLT),
    /// and Robust RANSAC Estimation.
    /// Provides pure mathematical 3x3 projective geometry routines.
    /// </summary>
    public static class Homography2D
    {
        public readonly struct Point2D
        {
            public readonly double X;
            public readonly double Y;

            public Point2D(double x, double y)
            {
                X = x;
                Y = y;
            }

            public override string ToString() => $"({X:F3}, {Y:F3})";
        }

        /// <summary>
        /// Applies 3x3 homography matrix H (row-major 9 elements) to point (x, y) with perspective division.
        /// </summary>
        public static Point2D Apply(double[] h, double x, double y)
        {
            if (h == null || h.Length < 9) throw new ArgumentException("Homography matrix must have 9 elements.", nameof(h));
            double X = h[0] * x + h[1] * y + h[2];
            double Y = h[3] * x + h[4] * y + h[5];
            double W = h[6] * x + h[7] * y + h[8];
            if (Math.Abs(W) < 1e-12) W = 1e-12;
            return new Point2D(X / W, Y / W);
        }

        /// <summary>
        /// Estimates 3x3 homography matrix H such that H * src ~ dst (minimum 4 point correspondences).
        /// Uses Hartley isotropic normalization and SVD/Gauss elimination on normal equations (A^T A).
        /// Returns null if degenerate or fewer than 4 points.
        /// </summary>
        public static double[]? EstimateDlt(IReadOnlyList<Point2D> src, IReadOnlyList<Point2D> dst)
        {
            if (src == null || dst == null) return null;
            int n = Math.Min(src.Count, dst.Count);
            if (n < 4) return null;

            // Hartley isotropic normalization for numerical stability
            if (!Normalize(src, n, out double[] T1, out Point2D[]? s) ||
                !Normalize(dst, n, out double[] T2, out Point2D[]? d) ||
                s == null || d == null)
            {
                return null;
            }

            var ata = new double[8 * 8];
            var atb = new double[8];
            var row = new double[8];

            for (int i = 0; i < n; i++)
            {
                double x = s[i].X, y = s[i].Y, u = d[i].X, v = d[i].Y;

                // Equation for u
                row[0] = x; row[1] = y; row[2] = 1; row[3] = 0; row[4] = 0; row[5] = 0;
                row[6] = -u * x; row[7] = -u * y;
                Accumulate(ata, atb, row, u);

                // Equation for v
                row[0] = 0; row[1] = 0; row[2] = 0; row[3] = x; row[4] = y; row[5] = 1;
                row[6] = -v * x; row[7] = -v * y;
                Accumulate(ata, atb, row, v);
            }

            double[]? hsol = Solve8(ata, atb);
            if (hsol == null) return null;

            double[] hN =
            {
                hsol[0], hsol[1], hsol[2],
                hsol[3], hsol[4], hsol[5],
                hsol[6], hsol[7], 1.0,
            };

            // Denormalize: H = T2^-1 * Hn * T1
            double[] t2inv = Invert3x3(T2);
            double[] H = Mul3x3(Mul3x3(t2inv, hN), T1);

            // Normalize so H[8] = 1.0
            if (Math.Abs(H[8]) > 1e-12)
            {
                for (int i = 0; i < 9; i++) H[i] /= H[8];
            }
            return H;
        }

        /// <summary>
        /// Robust RANSAC homography estimation with outlier rejection.
        /// </summary>
        public static double[]? EstimateRansac(
            IReadOnlyList<Point2D> src,
            IReadOnlyList<Point2D> dst,
            out bool[] inliers,
            double threshold = 3.0,
            int iterations = 500,
            int seed = 12345)
        {
            if (src == null || dst == null)
            {
                inliers = Array.Empty<bool>();
                return null;
            }
            int n = Math.Min(src.Count, dst.Count);
            inliers = new bool[n];
            if (n < 4) return null;

            var rng = new Random(seed);
            double[]? bestH = null;
            int bestCount = -1;
            double thr2 = threshold * threshold;
            var pick = new int[4];

            for (int it = 0; it < iterations; it++)
            {
                if (!Pick4(rng, n, pick)) continue;
                var s4 = new Point2D[4];
                var d4 = new Point2D[4];
                for (int i = 0; i < 4; i++)
                {
                    s4[i] = src[pick[i]];
                    d4[i] = dst[pick[i]];
                }
                var H = EstimateDlt(s4, d4);
                if (H == null) continue;

                int count = 0;
                for (int i = 0; i < n; i++)
                {
                    var p = Apply(H, src[i].X, src[i].Y);
                    double dx = p.X - dst[i].X;
                    double dy = p.Y - dst[i].Y;
                    if (dx * dx + dy * dy <= thr2) count++;
                }

                if (count > bestCount)
                {
                    bestCount = count;
                    bestH = H;
                    if (count == n) break;
                }
            }

            if (bestH == null || bestCount < 4) return null;

            // Refit on all inliers
            var inSrc = new List<Point2D>(bestCount);
            var inDst = new List<Point2D>(bestCount);
            for (int i = 0; i < n; i++)
            {
                var p = Apply(bestH, src[i].X, src[i].Y);
                double dx = p.X - dst[i].X;
                double dy = p.Y - dst[i].Y;
                bool ok = (dx * dx + dy * dy <= thr2);
                inliers[i] = ok;
                if (ok)
                {
                    inSrc.Add(src[i]);
                    inDst.Add(dst[i]);
                }
            }

            return EstimateDlt(inSrc, inDst) ?? bestH;
        }

        private static bool Normalize(IReadOnlyList<Point2D> pts, int n, out double[] T, out Point2D[]? norm)
        {
            double cx = 0, cy = 0;
            for (int i = 0; i < n; i++)
            {
                cx += pts[i].X;
                cy += pts[i].Y;
            }
            cx /= n;
            cy /= n;

            double distSum = 0;
            for (int i = 0; i < n; i++)
            {
                double dx = pts[i].X - cx;
                double dy = pts[i].Y - cy;
                distSum += Math.Sqrt(dx * dx + dy * dy);
            }
            if (distSum < 1e-9)
            {
                T = new double[9];
                norm = null;
                return false;
            }

            double scale = Math.Sqrt(2.0) * n / distSum;
            T = new double[]
            {
                scale, 0, -scale * cx,
                0, scale, -scale * cy,
                0, 0, 1,
            };

            norm = new Point2D[n];
            for (int i = 0; i < n; i++)
            {
                norm[i] = new Point2D((pts[i].X - cx) * scale, (pts[i].Y - cy) * scale);
            }
            return true;
        }

        private static void Accumulate(double[] ata, double[] atb, double[] r, double b)
        {
            for (int i = 0; i < 8; i++)
            {
                double ri = r[i];
                atb[i] += ri * b;
                int rowOffset = i * 8;
                for (int j = i; j < 8; j++)
                {
                    ata[rowOffset + j] += ri * r[j];
                }
            }
        }

        private static double[]? Solve8(double[] aUpper, double[] b)
        {
            var a = new double[8 * 8];
            for (int i = 0; i < 8; i++)
            {
                for (int j = i; j < 8; j++)
                {
                    double v = aUpper[i * 8 + j];
                    a[i * 8 + j] = v;
                    a[j * 8 + i] = v;
                }
            }

            var rhs = (double[])b.Clone();

            for (int k = 0; k < 8; k++)
            {
                int maxRow = k;
                double maxVal = Math.Abs(a[k * 8 + k]);
                for (int i = k + 1; i < 8; i++)
                {
                    double val = Math.Abs(a[i * 8 + k]);
                    if (val > maxVal)
                    {
                        maxVal = val;
                        maxRow = i;
                    }
                }
                if (maxVal < 1e-12) return null;

                if (maxRow != k)
                {
                    for (int j = k; j < 8; j++)
                    {
                        double tmp = a[k * 8 + j];
                        a[k * 8 + j] = a[maxRow * 8 + j];
                        a[maxRow * 8 + j] = tmp;
                    }
                    double tmpR = rhs[k];
                    rhs[k] = rhs[maxRow];
                    rhs[maxRow] = tmpR;
                }

                double pivot = a[k * 8 + k];
                for (int i = k + 1; i < 8; i++)
                {
                    double factor = a[i * 8 + k] / pivot;
                    for (int j = k; j < 8; j++)
                    {
                        a[i * 8 + j] -= factor * a[k * 8 + j];
                    }
                    rhs[i] -= factor * rhs[k];
                }
            }

            var x = new double[8];
            for (int i = 7; i >= 0; i--)
            {
                double sum = rhs[i];
                for (int j = i + 1; j < 8; j++)
                {
                    sum -= a[i * 8 + j] * x[j];
                }
                x[i] = sum / a[i * 8 + i];
            }
            return x;
        }

        private static bool Pick4(Random rng, int n, int[] pick)
        {
            for (int i = 0; i < 4; i++)
            {
                int val = rng.Next(n);
                for (int j = 0; j < i; j++)
                {
                    if (pick[j] == val) return false;
                }
                pick[i] = val;
            }
            return true;
        }

        public static double[] Mul3x3(double[] a, double[] b)
        {
            var r = new double[9];
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    r[row * 3 + col] =
                        a[row * 3 + 0] * b[0 * 3 + col] +
                        a[row * 3 + 1] * b[1 * 3 + col] +
                        a[row * 3 + 2] * b[2 * 3 + col];
                }
            }
            return r;
        }

        public static double[] Invert3x3(double[] m)
        {
            double a = m[0], b = m[1], c = m[2];
            double d = m[3], e = m[4], f = m[5];
            double g = m[6], h = m[7], i = m[8];
            double A = e * i - f * h;
            double B = -(d * i - f * g);
            double C = d * h - e * g;
            double det = a * A + b * B + c * C;
            if (Math.Abs(det) < 1e-12) return new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };
            double inv = 1.0 / det;
            return new double[]
            {
                A * inv,                 -(b * i - c * h) * inv,  (b * f - c * e) * inv,
                B * inv,                  (a * i - c * g) * inv, -(a * f - c * d) * inv,
                C * inv,                 -(a * h - b * g) * inv,  (a * e - b * d) * inv,
            };
        }
    }
}
