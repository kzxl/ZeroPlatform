using System;

namespace ZeroTensor.Core
{
    public static partial class TensorDecompositions
    {
        private const double EpsilonDouble = 1e-15;

        #region Double LU Decomposition & Solver

        /// <summary>
        /// Computes the LU decomposition with partial row pivoting for double precision: P * A = L * U.
        /// </summary>
        public static int LU(Tensor<double> A, out Tensor<double> L, out Tensor<double> U, out int[] P)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("LU decomposition requires a square 2D matrix.", nameof(A));
            }

            int n = A.Shape[0];
            L = Tensor.Zeros<double>(n, n);
            for (int i = 0; i < n; i++) L[i, i] = 1.0;
            U = A.Clone();
            P = new int[n];
            for (int i = 0; i < n; i++) P[i] = i;

            int swaps = 0;

            for (int k = 0; k < n; k++)
            {
                double maxVal = Math.Abs(U[k, k]);
                int pivotRow = k;

                for (int i = k + 1; i < n; i++)
                {
                    double val = Math.Abs(U[i, k]);
                    if (val > maxVal)
                    {
                        maxVal = val;
                        pivotRow = i;
                    }
                }

                if (pivotRow != k)
                {
                    for (int j = 0; j < n; j++)
                    {
                        double tmp = U[k, j];
                        U[k, j] = U[pivotRow, j];
                        U[pivotRow, j] = tmp;
                    }

                    for (int j = 0; j < k; j++)
                    {
                        double tmp = L[k, j];
                        L[k, j] = L[pivotRow, j];
                        L[pivotRow, j] = tmp;
                    }

                    int pTmp = P[k];
                    P[k] = P[pivotRow];
                    P[pivotRow] = pTmp;

                    swaps++;
                }

                double pivotVal = U[k, k];
                if (Math.Abs(pivotVal) < EpsilonDouble)
                {
                    continue;
                }

                for (int i = k + 1; i < n; i++)
                {
                    double factor = U[i, k] / pivotVal;
                    L[i, k] = factor;
                    U[i, k] = 0.0;

                    for (int j = k + 1; j < n; j++)
                    {
                        U[i, j] -= factor * U[k, j];
                    }
                }
            }

            return swaps;
        }

        /// <summary>
        /// Solves the linear system A * X = B for square matrix A using LU decomposition (double precision).
        /// </summary>
        public static Tensor<double> Solve(Tensor<double> A, Tensor<double> B)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Matrix A must be square.", nameof(A));
            }

            int n = A.Shape[0];
            LU(A, out var L, out var U, out var P);

            bool is1D = B.Rank == 1;
            var B2D = is1D ? B.Unsqueeze(1) : B;

            if (B2D.Shape[0] != n)
            {
                throw new ArgumentException($"Row count of B ({B2D.Shape[0]}) does not match A ({n}).", nameof(B));
            }

            int numRhs = B2D.Shape[1];
            var X = new Tensor<double>(n, numRhs);

            var Pb = new Tensor<double>(n, numRhs);
            for (int i = 0; i < n; i++)
            {
                for (int c = 0; c < numRhs; c++)
                {
                    Pb[i, c] = B2D[P[i], c];
                }
            }

            var Y = new Tensor<double>(n, numRhs);
            for (int c = 0; c < numRhs; c++)
            {
                for (int i = 0; i < n; i++)
                {
                    double sum = Pb[i, c];
                    for (int j = 0; j < i; j++)
                    {
                        sum -= L[i, j] * Y[j, c];
                    }
                    Y[i, c] = sum;
                }
            }

            for (int c = 0; c < numRhs; c++)
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    double sum = Y[i, c];
                    for (int j = i + 1; j < n; j++)
                    {
                        sum -= U[i, j] * X[j, c];
                    }

                    if (Math.Abs(U[i, i]) < EpsilonDouble)
                    {
                        throw new InvalidOperationException($"Matrix is singular at diagonal entry {i}. Cannot solve.");
                    }

                    X[i, c] = sum / U[i, i];
                }
            }

            return is1D ? X.Squeeze(1) : X;
        }

        /// <summary>
        /// Computes the inverse of a square matrix A (double precision).
        /// </summary>
        public static Tensor<double> Invert(Tensor<double> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Cannot invert non-square matrix.", nameof(A));
            }

            int n = A.Shape[0];
            var identity = Tensor.Zeros<double>(n, n);
            for (int i = 0; i < n; i++) identity[i, i] = 1.0;
            return Solve(A, identity);
        }

        /// <summary>
        /// Computes the determinant of a square matrix A (double precision).
        /// </summary>
        public static double Determinant(Tensor<double> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Determinant requires a square matrix.", nameof(A));
            }

            int n = A.Shape[0];
            int swaps = LU(A, out _, out var U, out _);

            double det = (swaps % 2 == 1) ? -1.0 : 1.0;
            for (int i = 0; i < n; i++)
            {
                det *= U[i, i];
            }

            return det;
        }

        #endregion

        #region Double Cholesky & QR

        /// <summary>
        /// Computes the Cholesky decomposition of a symmetric positive-definite matrix A (double precision): A = L * L^T.
        /// </summary>
        public static Tensor<double> Cholesky(Tensor<double> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Cholesky decomposition requires a square matrix.", nameof(A));
            }

            int n = A.Shape[0];
            var L = new Tensor<double>(n, n);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    double sum = 0.0;

                    if (j == i)
                    {
                        for (int k = 0; k < j; k++)
                        {
                            sum += L[j, k] * L[j, k];
                        }

                        double val = A[j, j] - sum;
                        if (val <= 0.0)
                        {
                            throw new InvalidOperationException($"Matrix is not positive-definite at index ({j}, {j}) with value {val}.");
                        }

                        L[j, j] = Math.Sqrt(val);
                    }
                    else
                    {
                        for (int k = 0; k < j; k++)
                        {
                            sum += L[i, k] * L[j, k];
                        }

                        L[i, j] = (A[i, j] - sum) / L[j, j];
                    }
                }
            }

            return L;
        }

        /// <summary>
        /// Computes the QR decomposition of matrix A using Householder reflections (double precision): A = Q * R.
        /// </summary>
        public static void QR(Tensor<double> A, out Tensor<double> Q, out Tensor<double> R)
        {
            if (A.Rank != 2) throw new ArgumentException("QR decomposition requires a 2D matrix.", nameof(A));
            int m = A.Shape[0];
            int n = A.Shape[1];

            if (m < n) throw new ArgumentException($"Matrix A must have rows >= cols: ({m}x{n}).", nameof(A));

            R = A.Clone();
            Q = Tensor.Zeros<double>(m, m);
            for (int i = 0; i < m; i++) Q[i, i] = 1.0;

            for (int k = 0; k < n; k++)
            {
                double normSq = 0.0;
                for (int i = k; i < m; i++)
                {
                    normSq += R[i, k] * R[i, k];
                }

                double norm = Math.Sqrt(normSq);
                if (norm < EpsilonDouble) continue;

                double alpha = R[k, k] < 0.0 ? norm : -norm;
                double u0 = R[k, k] - alpha;

                var v = new double[m - k];
                v[0] = 1.0;
                double vNormSq = 1.0;

                for (int i = 1; i < m - k; i++)
                {
                    v[i] = R[k + i, k] / u0;
                    vNormSq += v[i] * v[i];
                }

                double tau = 2.0 / vNormSq;

                for (int j = k; j < n; j++)
                {
                    double dot = 0.0;
                    for (int i = 0; i < m - k; i++)
                    {
                        dot += v[i] * R[k + i, j];
                    }

                    double scale = tau * dot;
                    for (int i = 0; i < m - k; i++)
                    {
                        R[k + i, j] -= scale * v[i];
                    }
                }

                for (int i = 0; i < m; i++)
                {
                    double dot = 0.0;
                    for (int j = 0; j < m - k; j++)
                    {
                        dot += Q[i, k + j] * v[j];
                    }

                    double scale = tau * dot;
                    for (int j = 0; j < m - k; j++)
                    {
                        Q[i, k + j] -= scale * v[j];
                    }
                }
            }

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < Math.Min(i, n); j++)
                {
                    R[i, j] = 0.0;
                }
            }
        }

        #endregion

        #region Double SVD, Eigh & Pseudoinverse

        /// <summary>
        /// Computes SVD of matrix A (double precision): A = U * S * V^T.
        /// </summary>
        public static void SVD(
            Tensor<double> A,
            out Tensor<double> U,
            out Tensor<double> S,
            out Tensor<double> Vt,
            int maxSweeps = 30,
            double tolerance = 1e-12)
        {
            if (A.Rank != 2) throw new ArgumentException("SVD requires a 2D matrix.", nameof(A));
            int m = A.Shape[0];
            int n = A.Shape[1];

            bool transposed = false;
            Tensor<double> workA;

            if (m < n)
            {
                workA = A.Transpose(0, 1).Clone();
                int tmp = m;
                m = n;
                n = tmp;
                transposed = true;
            }
            else
            {
                workA = A.Clone();
            }

            var V = Tensor.Zeros<double>(n, n);
            for (int i = 0; i < n; i++) V[i, i] = 1.0;

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                double maxCorrelation = 0.0;

                for (int j = 0; j < n - 1; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        double alpha = 0.0, beta = 0.0, gamma = 0.0;

                        for (int i = 0; i < m; i++)
                        {
                            double aj = workA[i, j];
                            double ak = workA[i, k];
                            alpha += aj * aj;
                            beta += ak * ak;
                            gamma += aj * ak;
                        }

                        double corr = Math.Abs(gamma) / Math.Sqrt(Math.Max(EpsilonDouble, alpha * beta));
                        if (corr > maxCorrelation) maxCorrelation = corr;

                        if (Math.Abs(gamma) < tolerance) continue;

                        double zeta = (beta - alpha) / (2.0 * gamma);
                        double t = Math.Sign(zeta) / (Math.Abs(zeta) + Math.Sqrt(1.0 + zeta * zeta));
                        double c = 1.0 / Math.Sqrt(1.0 + t * t);
                        double s = t * c;

                        for (int i = 0; i < m; i++)
                        {
                            double aj = workA[i, j];
                            double ak = workA[i, k];
                            workA[i, j] = c * aj - s * ak;
                            workA[i, k] = s * aj + c * ak;
                        }

                        for (int i = 0; i < n; i++)
                        {
                            double vj = V[i, j];
                            double vk = V[i, k];
                            V[i, j] = c * vj - s * vk;
                            V[i, k] = s * vj + c * vk;
                        }
                    }
                }

                if (maxCorrelation < tolerance)
                {
                    break;
                }
            }

            var singularVals = new double[n];
            var colIndices = new int[n];
            for (int j = 0; j < n; j++)
            {
                double colNormSq = 0.0;
                for (int i = 0; i < m; i++)
                {
                    colNormSq += workA[i, j] * workA[i, j];
                }
                singularVals[j] = Math.Sqrt(colNormSq);
                colIndices[j] = j;
            }

            Array.Sort(colIndices, (idx1, idx2) => singularVals[idx2].CompareTo(singularVals[idx1]));

            var sortedS = new double[n];
            U = new Tensor<double>(m, n);
            var sortedV = new Tensor<double>(n, n);

            for (int j = 0; j < n; j++)
            {
                int origCol = colIndices[j];
                double sVal = singularVals[origCol];
                sortedS[j] = sVal;

                double invS = sVal > EpsilonDouble ? 1.0 / sVal : 0.0;
                for (int i = 0; i < m; i++)
                {
                    U[i, j] = workA[i, origCol] * invS;
                }

                for (int i = 0; i < n; i++)
                {
                    sortedV[i, j] = V[i, origCol];
                }
            }

            S = Tensor.FromArray(sortedS, n);
            Vt = sortedV.Transpose(0, 1).Clone();

            if (transposed)
            {
                var tmpU = U;
                U = sortedV;
                Vt = tmpU.Transpose(0, 1).Clone();
            }
        }

        public static void Svd(
            Tensor<double> A,
            out Tensor<double> U,
            out Tensor<double> S,
            out Tensor<double> Vt,
            int maxSweeps = 30,
            double tolerance = 1e-12) => SVD(A, out U, out S, out Vt, maxSweeps, tolerance);

        /// <summary>
        /// Computes eigenvalues and eigenvectors of a real symmetric matrix A using the Cyclic Jacobi method (double precision).
        /// </summary>
        public static void Eigh(
            Tensor<double> A,
            out Tensor<double> eigenvalues,
            out Tensor<double> eigenvectors,
            int maxSweeps = 50,
            double tolerance = 1e-12)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Eigh requires a square 2D matrix.", nameof(A));
            }

            int n = A.Shape[0];
            var a = A.Clone();
            var v = Tensor.Zeros<double>(n, n);
            for (int i = 0; i < n; i++) v[i, i] = 1.0;

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                double sumOffDiag = 0.0;
                for (int i = 0; i < n; i++)
                {
                    for (int j = i + 1; j < n; j++)
                    {
                        sumOffDiag += Math.Abs(a[i, j]);
                    }
                }

                if (sumOffDiag < tolerance)
                {
                    break;
                }

                for (int p = 0; p < n - 1; p++)
                {
                    for (int q = p + 1; q < n; q++)
                    {
                        double apq = a[p, q];
                        if (Math.Abs(apq) < 1e-15) continue;

                        double app = a[p, p];
                        double aqq = a[q, q];
                        double theta = (aqq - app) / (2.0 * apq);
                        double t;
                        if (Math.Abs(theta) < 1e-15)
                        {
                            t = 1.0;
                        }
                        else
                        {
                            t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(1.0 + theta * theta));
                        }
                        if (double.IsNaN(t)) t = 0.0;

                        double c = 1.0 / Math.Sqrt(1.0 + t * t);
                        double s = t * c;
                        double tau = s / (1.0 + c);

                        a[p, p] -= t * apq;
                        a[q, q] += t * apq;
                        a[p, q] = 0.0;
                        a[q, p] = 0.0;

                        for (int i = 0; i < n; i++)
                        {
                            if (i != p && i != q)
                            {
                                double aip = a[i, p];
                                double aiq = a[i, q];
                                a[i, p] = aip - s * (aiq + aip * tau);
                                a[p, i] = a[i, p];
                                a[i, q] = aiq + s * (aip - aiq * tau);
                                a[q, i] = a[i, q];
                            }
                        }

                        for (int i = 0; i < n; i++)
                        {
                            double vip = v[i, p];
                            double viq = v[i, q];
                            v[i, p] = vip - s * (viq + vip * tau);
                            v[i, q] = viq + s * (vip - viq * tau);
                        }
                    }
                }
            }

            var vals = new double[n];
            var indices = new int[n];
            for (int i = 0; i < n; i++)
            {
                vals[i] = a[i, i];
                indices[i] = i;
            }

            Array.Sort(indices, (i1, i2) => vals[i1].CompareTo(vals[i2]));

            var sortedVals = new double[n];
            var sortedV = new Tensor<double>(n, n);

            for (int j = 0; j < n; j++)
            {
                int origCol = indices[j];
                sortedVals[j] = vals[origCol];
                for (int i = 0; i < n; i++)
                {
                    sortedV[i, j] = v[i, origCol];
                }
            }

            eigenvalues = Tensor.FromArray(sortedVals, n);
            eigenvectors = sortedV;
        }

        public static void Eigen(Tensor<double> A, out Tensor<double> eigenvalues, out Tensor<double> eigenvectors) =>
            Eigh(A, out eigenvalues, out eigenvectors);

        /// <summary>
        /// Computes the Moore-Penrose pseudoinverse of matrix A (double precision): A^+ = V * S^+ * U^T.
        /// </summary>
        public static Tensor<double> Pinverse(Tensor<double> A, double rcond = 1e-12)
        {
            if (A.Rank != 2) throw new ArgumentException("Pseudoinverse requires a 2D matrix.", nameof(A));
            int m = A.Shape[0];
            int n = A.Shape[1];

            SVD(A, out var U, out var S, out var Vt);

            double maxS = S.Length > 0 ? S[0] : 0.0;
            double cutoff = rcond * maxS;

            int k = S.Length;
            var Sp = new Tensor<double>(k, k);
            for (int i = 0; i < k; i++)
            {
                double sVal = S[i];
                if (sVal > cutoff)
                {
                    Sp[i, i] = 1.0 / sVal;
                }
            }

            var V = Vt.Transpose(0, 1);
            var Ut = U.Transpose(0, 1);

            var V_Sp = TensorBlas.MatMul(V, Sp);
            return TensorBlas.MatMul(V_Sp, Ut);
        }

        public static Tensor<double> Pinv(Tensor<double> A, double rcond = 1e-12) => Pinverse(A, rcond);

        /// <summary>
        /// Computes the sum of elements along the main diagonal of a square double matrix.
        /// </summary>
        public static double Trace(Tensor<double> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
                throw new ArgumentException("Trace requires a square matrix.", nameof(A));

            double sum = 0.0;
            int n = A.Shape[0];
            for (int i = 0; i < n; i++) sum += A[i, i];
            return sum;
        }

        /// <summary>
        /// Extracts the diagonal of a 2D double matrix.
        /// </summary>
        public static Tensor<double> Diagonal(Tensor<double> A, int offset = 0)
        {
            if (A.Rank != 2) throw new ArgumentException("Diagonal requires a 2D matrix.", nameof(A));
            int rows = A.Shape[0];
            int cols = A.Shape[1];

            int count = offset >= 0 ? Math.Min(rows, cols - offset) : Math.Min(rows + offset, cols);
            if (count <= 0) return new Tensor<double>(0);

            var diag = new Tensor<double>(count);
            int rStart = offset < 0 ? -offset : 0;
            int cStart = offset > 0 ? offset : 0;

            for (int i = 0; i < count; i++)
            {
                diag[i] = A[rStart + i, cStart + i];
            }
            return diag;
        }

        /// <summary>
        /// Computes the numerical matrix rank of A via SVD (double precision).
        /// </summary>
        public static int MatrixRank(Tensor<double> A, double tol = 1e-12)
        {
            SVD(A, out _, out var S, out _);
            double cutoff = tol * (S.Length > 0 ? S[0] : 0.0);
            int rank = 0;
            for (int i = 0; i < S.Length; i++)
            {
                if (S[i] > cutoff) rank++;
            }
            return rank;
        }

        #endregion
    }
}
