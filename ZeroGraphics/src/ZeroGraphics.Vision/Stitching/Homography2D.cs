using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZeroGraphics.Vision.Stitching
{
    /// <summary>
    /// Planar Projective Transformation (Homography) represented by a 3x3 matrix.
    /// Maps 2D coordinates between multi-camera views and perspective planes with zero external dependencies.
    /// </summary>
    public sealed class Homography2D
    {
        // 3x3 row-major elements:
        // [ h00  h01  h02 ]
        // [ h10  h11  h12 ]
        // [ h20  h21  h22 ]
        public double H00 { get; }
        public double H01 { get; }
        public double H02 { get; }
        public double H10 { get; }
        public double H11 { get; }
        public double H12 { get; }
        public double H20 { get; }
        public double H21 { get; }
        public double H22 { get; }

        public static readonly Homography2D Identity = new Homography2D(1, 0, 0, 0, 1, 0, 0, 0, 1);

        public Homography2D(
            double h00, double h01, double h02,
            double h10, double h11, double h12,
            double h20, double h21, double h22)
        {
            H00 = h00; H01 = h01; H02 = h02;
            H10 = h10; H11 = h11; H12 = h12;
            H20 = h20; H21 = h21; H22 = h22;
        }

        /// <summary>
        /// Maps a 2D coordinate (x, y) through the projective transform with perspective division.
        /// </summary>
        public PointF TransformPoint(double x, double y)
        {
            double w = H20 * x + H21 * y + H22;
            if (Math.Abs(w) < 1e-12) w = 1e-12;

            double invW = 1.0 / w;
            double px = (H00 * x + H01 * y + H02) * invW;
            double py = (H10 * x + H11 * y + H12) * invW;

            return new PointF((float)px, (float)py);
        }

        public PointF TransformPoint(PointF pt) => TransformPoint(pt.X, pt.Y);

        /// <summary>
        /// Computes the inverse homography transformation matrix H^-1.
        /// </summary>
        public Homography2D Invert()
        {
            double c00 = H11 * H22 - H12 * H21;
            double c01 = H12 * H20 - H10 * H22;
            double c02 = H10 * H21 - H11 * H20;

            double det = H00 * c00 + H01 * c01 + H02 * c02;
            if (Math.Abs(det) < 1e-12)
                throw new InvalidOperationException("Homography matrix is singular and cannot be inverted.");

            double invDet = 1.0 / det;

            double inv00 = c00 * invDet;
            double inv01 = (H02 * H21 - H01 * H22) * invDet;
            double inv02 = (H01 * H12 - H02 * H11) * invDet;

            double inv10 = c01 * invDet;
            double inv11 = (H00 * H22 - H02 * H20) * invDet;
            double inv12 = (H02 * H10 - H00 * H12) * invDet;

            double inv20 = c02 * invDet;
            double inv21 = (H01 * H20 - H00 * H21) * invDet;
            double inv22 = (H00 * H11 - H01 * H10) * invDet;

            return new Homography2D(
                inv00, inv01, inv02,
                inv10, inv11, inv12,
                inv20, inv21, inv22);
        }

        /// <summary>
        /// Multiplies this homography by another: Result = This * Other.
        /// </summary>
        public Homography2D Multiply(Homography2D other)
        {
            if (other == null) throw new ArgumentNullException(nameof(other));

            double r00 = H00 * other.H00 + H01 * other.H10 + H02 * other.H20;
            double r01 = H00 * other.H01 + H01 * other.H11 + H02 * other.H21;
            double r02 = H00 * other.H02 + H01 * other.H12 + H02 * other.H22;

            double r10 = H10 * other.H00 + H11 * other.H10 + H12 * other.H20;
            double r11 = H10 * other.H01 + H11 * other.H11 + H12 * other.H21;
            double r12 = H10 * other.H02 + H11 * other.H12 + H12 * other.H22;

            double r20 = H20 * other.H00 + H21 * other.H10 + H22 * other.H20;
            double r21 = H20 * other.H01 + H21 * other.H11 + H22 * other.H21;
            double r22 = H20 * other.H02 + H21 * other.H12 + H22 * other.H22;

            return new Homography2D(r00, r01, r02, r10, r11, r12, r20, r21, r22);
        }

        /// <summary>
        /// Estimates the 3x3 homography matrix mapping source points to destination points
        /// using Direct Linear Transformation (DLT) with Gauss-Jordan elimination.
        /// Requires at least 4 corresponding point pairs (no 3 collinear).
        /// </summary>
        public static Homography2D Estimate(IReadOnlyList<PointF> srcPoints, IReadOnlyList<PointF> dstPoints)
        {
            if (srcPoints == null) throw new ArgumentNullException(nameof(srcPoints));
            if (dstPoints == null) throw new ArgumentNullException(nameof(dstPoints));
            if (srcPoints.Count < 4 || dstPoints.Count < 4)
                throw new ArgumentException("Homography estimation requires at least 4 corresponding point pairs.");
            if (srcPoints.Count != dstPoints.Count)
                throw new ArgumentException("Source and destination point counts must match.");

            int n = srcPoints.Count;
            int numEq = 2 * n;

            // System A * h = b (with h22 = 1, solving for 8 unknowns h00..h21)
            // If n == 4: 8x8 system
            // If n > 4: solve normal equations (A^T A) h = A^T b
            double[,] aMat = new double[numEq, 8];
            double[] bVec = new double[numEq];

            for (int i = 0; i < n; i++)
            {
                double x = srcPoints[i].X;
                double y = srcPoints[i].Y;
                double u = dstPoints[i].X;
                double v = dstPoints[i].Y;

                int row1 = 2 * i;
                int row2 = 2 * i + 1;

                // u = (h00*x + h01*y + h02) / (h20*x + h21*y + 1)
                // -> h00*x + h01*y + h02 - u*x*h20 - u*y*h21 = u
                aMat[row1, 0] = x;
                aMat[row1, 1] = y;
                aMat[row1, 2] = 1.0;
                aMat[row1, 3] = 0.0;
                aMat[row1, 4] = 0.0;
                aMat[row1, 5] = 0.0;
                aMat[row1, 6] = -u * x;
                aMat[row1, 7] = -u * y;
                bVec[row1] = u;

                // v = (h10*x + h11*y + h12) / (h20*x + h21*y + 1)
                // -> h10*x + h11*y + h12 - v*x*h20 - v*y*h21 = v
                aMat[row2, 0] = 0.0;
                aMat[row2, 1] = 0.0;
                aMat[row2, 2] = 0.0;
                aMat[row2, 3] = x;
                aMat[row2, 4] = y;
                aMat[row2, 5] = 1.0;
                aMat[row2, 6] = -v * x;
                aMat[row2, 7] = -v * y;
                bVec[row2] = v;
            }

            double[] h = new double[8];

            if (n == 4)
            {
                // Solve 8x8 exactly via Gauss-Jordan elimination
                SolveLinearSystem(aMat, bVec, h, 8);
            }
            else
            {
                // Normal equations: (A^T A) * h = A^T b
                double[,] ata = new double[8, 8];
                double[] atb = new double[8];

                for (int r = 0; r < 8; r++)
                {
                    for (int c = 0; c < 8; c++)
                    {
                        double sum = 0.0;
                        for (int k = 0; k < numEq; k++)
                            sum += aMat[k, r] * aMat[k, c];
                        ata[r, c] = sum;
                    }

                    double bSum = 0.0;
                    for (int k = 0; k < numEq; k++)
                        bSum += aMat[k, r] * bVec[k];
                    atb[r] = bSum;
                }

                SolveLinearSystem(ata, atb, h, 8);
            }

            return new Homography2D(
                h[0], h[1], h[2],
                h[3], h[4], h[5],
                h[6], h[7], 1.0);
        }

        private static void SolveLinearSystem(double[,] a, double[] b, double[] x, int size)
        {
            // Augmented matrix [A | b]
            double[,] aug = new double[size, size + 1];
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                    aug[r, c] = a[r, c];
                aug[r, size] = b[r];
            }

            // Gauss-Jordan elimination with partial pivoting
            for (int col = 0; col < size; col++)
            {
                int maxRow = col;
                double maxVal = Math.Abs(aug[col, col]);

                for (int r = col + 1; r < size; r++)
                {
                    double val = Math.Abs(aug[r, col]);
                    if (val > maxVal)
                    {
                        maxVal = val;
                        maxRow = r;
                    }
                }

                if (maxVal < 1e-12)
                    throw new InvalidOperationException("Degenerate point configuration: matrix is rank deficient.");

                // Swap rows
                if (maxRow != col)
                {
                    for (int c = col; c <= size; c++)
                    {
                        double temp = aug[col, c];
                        aug[col, c] = aug[maxRow, c];
                        aug[maxRow, c] = temp;
                    }
                }

                // Normalize pivot row
                double pivot = aug[col, col];
                double invPivot = 1.0 / pivot;
                for (int c = col; c <= size; c++)
                {
                    aug[col, c] *= invPivot;
                }

                // Eliminate other rows
                for (int r = 0; r < size; r++)
                {
                    if (r != col)
                    {
                        double factor = aug[r, col];
                        if (Math.Abs(factor) > 1e-15)
                        {
                            for (int c = col; c <= size; c++)
                            {
                                aug[r, c] -= factor * aug[col, c];
                            }
                        }
                    }
                }
            }

            for (int r = 0; r < size; r++)
            {
                x[r] = aug[r, size];
            }
        }
    }
}
