using System;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Matrix factorizations and linear system solvers: LU, Cholesky, QR, SVD, Matrix Inversion, and Determinants.
    /// </summary>
    public static partial class TensorDecompositions
    {
        private const float Epsilon = 1e-12f;

        #region LU Decomposition & Solver

        /// <summary>
        /// Computes the LU decomposition with partial row pivoting: P * A = L * U.
        /// </summary>
        /// <param name="A">Square input matrix (N x N).</param>
        /// <param name="L">Output unit lower triangular matrix (N x N).</param>
        /// <param name="U">Output upper triangular matrix (N x N).</param>
        /// <param name="P">Output permutation vector recording row exchanges.</param>
        /// <returns>Number of row swaps performed (used for determinant sign).</returns>
        public static int LU(Tensor<float> A, out Tensor<float> L, out Tensor<float> U, out int[] P)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("LU decomposition requires a square 2D matrix.", nameof(A));
            }

            int n = A.Shape[0];
            L = Tensor.Eye(n);
            U = A.Clone();
            P = new int[n];
            for (int i = 0; i < n; i++) P[i] = i;

            int swaps = 0;

            for (int k = 0; k < n; k++)
            {
                // Find pivot row
                float maxVal = Math.Abs(U[k, k]);
                int pivotRow = k;

                for (int i = k + 1; i < n; i++)
                {
                    float val = Math.Abs(U[i, k]);
                    if (val > maxVal)
                    {
                        maxVal = val;
                        pivotRow = i;
                    }
                }

                if (pivotRow != k)
                {
                    // Swap rows in U
                    for (int j = 0; j < n; j++)
                    {
                        float tmp = U[k, j];
                        U[k, j] = U[pivotRow, j];
                        U[pivotRow, j] = tmp;
                    }

                    // Swap rows in L (columns 0 to k-1)
                    for (int j = 0; j < k; j++)
                    {
                        float tmp = L[k, j];
                        L[k, j] = L[pivotRow, j];
                        L[pivotRow, j] = tmp;
                    }

                    // Swap permutation indices
                    int pTmp = P[k];
                    P[k] = P[pivotRow];
                    P[pivotRow] = pTmp;

                    swaps++;
                }

                float pivotVal = U[k, k];
                if (Math.Abs(pivotVal) < Epsilon)
                {
                    continue; // Singular or nearly singular matrix
                }

                for (int i = k + 1; i < n; i++)
                {
                    float factor = U[i, k] / pivotVal;
                    L[i, k] = factor;
                    U[i, k] = 0f;

                    for (int j = k + 1; j < n; j++)
                    {
                        U[i, j] -= factor * U[k, j];
                    }
                }
            }

            return swaps;
        }

        /// <summary>
        /// Solves the linear system A * X = B for square matrix A using LU decomposition.
        /// B can be a 1D vector or a 2D matrix of column vectors.
        /// </summary>
        public static Tensor<float> Solve(Tensor<float> A, Tensor<float> B)
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
            var X = new Tensor<float>(n, numRhs);

            // Permute B rows according to P
            var Pb = new Tensor<float>(n, numRhs);
            for (int i = 0; i < n; i++)
            {
                for (int c = 0; c < numRhs; c++)
                {
                    Pb[i, c] = B2D[P[i], c];
                }
            }

            // Forward substitution: L * Y = P * B
            var Y = new Tensor<float>(n, numRhs);
            for (int c = 0; c < numRhs; c++)
            {
                for (int i = 0; i < n; i++)
                {
                    float sum = Pb[i, c];
                    for (int j = 0; j < i; j++)
                    {
                        sum -= L[i, j] * Y[j, c];
                    }
                    Y[i, c] = sum; // L[i, i] is 1
                }
            }

            // Backward substitution: U * X = Y
            for (int c = 0; c < numRhs; c++)
            {
                for (int i = n - 1; i >= 0; i--)
                {
                    float sum = Y[i, c];
                    for (int j = i + 1; j < n; j++)
                    {
                        sum -= U[i, j] * X[j, c];
                    }

                    if (Math.Abs(U[i, i]) < Epsilon)
                    {
                        throw new InvalidOperationException($"Matrix is singular at diagonal entry {i}. Cannot solve.");
                    }

                    X[i, c] = sum / U[i, i];
                }
            }

            return is1D ? X.Squeeze(1) : X;
        }

        /// <summary>
        /// Computes the inverse of a square matrix A: A^(-1).
        /// </summary>
        public static Tensor<float> Invert(Tensor<float> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Cannot invert non-square matrix.", nameof(A));
            }

            int n = A.Shape[0];
            var identity = Tensor.Eye(n);
            return Solve(A, identity);
        }

        /// <summary>
        /// Computes the determinant of a square matrix A via LU decomposition: det(A) = (-1)^swaps * prod(U[i, i]).
        /// </summary>
        public static float Determinant(Tensor<float> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Determinant requires a square matrix.", nameof(A));
            }

            int n = A.Shape[0];
            int swaps = LU(A, out _, out var U, out _);

            float det = (swaps % 2 == 1) ? -1.0f : 1.0f;
            for (int i = 0; i < n; i++)
            {
                det *= U[i, i];
            }

            return det;
        }

        #endregion

        #region Cholesky Decomposition

        /// <summary>
        /// Computes the Cholesky decomposition of a symmetric positive-definite matrix A: A = L * L^T.
        /// </summary>
        /// <param name="A">Symmetric positive-definite matrix (N x N).</param>
        /// <returns>Lower triangular matrix L.</returns>
        public static Tensor<float> Cholesky(Tensor<float> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Cholesky decomposition requires a square matrix.", nameof(A));
            }

            int n = A.Shape[0];
            var L = new Tensor<float>(n, n);

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j <= i; j++)
                {
                    float sum = 0f;

                    if (j == i)
                    {
                        for (int k = 0; k < j; k++)
                        {
                            sum += L[j, k] * L[j, k];
                        }

                        float val = A[j, j] - sum;
                        if (val <= 0f)
                        {
                            throw new InvalidOperationException($"Matrix is not positive-definite at index ({j}, {j}) with value {val}.");
                        }

                        L[j, j] = (float)Math.Sqrt(val);
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

        #endregion

        #region QR Decomposition

        /// <summary>
        /// Computes the QR decomposition of matrix A (M x N, M >= N) using Householder reflections: A = Q * R.
        /// </summary>
        public static void QR(Tensor<float> A, out Tensor<float> Q, out Tensor<float> R)
        {
            if (A.Rank != 2) throw new ArgumentException("QR decomposition requires a 2D matrix.", nameof(A));
            int m = A.Shape[0];
            int n = A.Shape[1];

            if (m < n) throw new ArgumentException($"Matrix A must have rows >= cols: ({m}x{n}).", nameof(A));

            R = A.Clone();
            Q = Tensor.Eye(m);

            for (int k = 0; k < n; k++)
            {
                // Form Householder vector for column k from row k to m-1
                float normSq = 0f;
                for (int i = k; i < m; i++)
                {
                    normSq += R[i, k] * R[i, k];
                }

                float norm = (float)Math.Sqrt(normSq);
                if (norm < Epsilon) continue;

                float alpha = R[k, k] < 0f ? norm : -norm;
                float u0 = R[k, k] - alpha;

                var v = new float[m - k];
                v[0] = 1.0f;
                float vNormSq = 1.0f;

                for (int i = 1; i < m - k; i++)
                {
                    v[i] = R[k + i, k] / u0;
                    vNormSq += v[i] * v[i];
                }

                float tau = 2.0f / vNormSq;

                // Apply Householder reflection to R: R = (I - tau * v * v^T) * R
                for (int j = k; j < n; j++)
                {
                    float dot = 0f;
                    for (int i = 0; i < m - k; i++)
                    {
                        dot += v[i] * R[k + i, j];
                    }

                    float scale = tau * dot;
                    for (int i = 0; i < m - k; i++)
                    {
                        R[k + i, j] -= scale * v[i];
                    }
                }

                // Accumulate into Q: Q = Q * (I - tau * v * v^T)
                for (int i = 0; i < m; i++)
                {
                    float dot = 0f;
                    for (int j = 0; j < m - k; j++)
                    {
                        dot += Q[i, k + j] * v[j];
                    }

                    float scale = tau * dot;
                    for (int j = 0; j < m - k; j++)
                    {
                        Q[i, k + j] -= scale * v[j];
                    }
                }
            }

            // Zero out lower triangle of R explicitly
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < Math.Min(i, n); j++)
                {
                    R[i, j] = 0f;
                }
            }
        }

        #endregion

        #region Singular Value Decomposition (SVD)

        /// <summary>
        /// Computes the Singular Value Decomposition of matrix A (M x N, M >= N): A = U * S * V^T.
        /// Implemented via the numerically stable One-Sided Jacobi orthogonalization.
        /// </summary>
        /// <param name="A">Input matrix (M x N).</param>
        /// <param name="U">Left singular vectors (M x N, orthogonal columns).</param>
        /// <param name="S">Singular values vector (N elements sorted descending).</param>
        /// <param name="Vt">Right singular vectors transposed (N x N, orthogonal rows).</param>
        /// <param name="maxSweeps">Maximum number of Jacobi sweeps.</param>
        /// <param name="tolerance">Convergence threshold.</param>
        public static void SVD(
            Tensor<float> A,
            out Tensor<float> U,
            out Tensor<float> S,
            out Tensor<float> Vt,
            int maxSweeps = 30,
            float tolerance = 1e-6f)
        {
            if (A.Rank != 2) throw new ArgumentException("SVD requires a 2D matrix.", nameof(A));
            int m = A.Shape[0];
            int n = A.Shape[1];

            bool transposed = false;
            Tensor<float> workA;

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

            var V = Tensor.Eye(n);

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                float maxCorrelation = 0f;

                for (int j = 0; j < n - 1; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        // Compute dot products: a_j . a_j, a_k . a_k, a_j . a_k
                        float alpha = 0f, beta = 0f, gamma = 0f;

                        for (int i = 0; i < m; i++)
                        {
                            float aj = workA[i, j];
                            float ak = workA[i, k];
                            alpha += aj * aj;
                            beta += ak * ak;
                            gamma += aj * ak;
                        }

                        float corr = Math.Abs(gamma) / (float)Math.Sqrt(Math.Max(Epsilon, alpha * beta));
                        if (corr > maxCorrelation) maxCorrelation = corr;

                        if (Math.Abs(gamma) < tolerance) continue;

                        // Compute Jacobi rotation angle
                        float zeta = (beta - alpha) / (2.0f * gamma);
                        float t = (float)(Math.Sign(zeta) / (Math.Abs(zeta) + Math.Sqrt(1.0 + zeta * zeta)));
                        float c = 1.0f / (float)Math.Sqrt(1.0 + t * t);
                        float s = t * c;

                        // Rotate columns j and k of workA
                        for (int i = 0; i < m; i++)
                        {
                            float aj = workA[i, j];
                            float ak = workA[i, k];
                            workA[i, j] = c * aj - s * ak;
                            workA[i, k] = s * aj + c * ak;
                        }

                        // Rotate columns j and k of V
                        for (int i = 0; i < n; i++)
                        {
                            float vj = V[i, j];
                            float vk = V[i, k];
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

            // Compute singular values as norms of columns of workA
            var singularVals = new float[n];
            var colIndices = new int[n];
            for (int j = 0; j < n; j++)
            {
                float colNormSq = 0f;
                for (int i = 0; i < m; i++)
                {
                    colNormSq += workA[i, j] * workA[i, j];
                }
                singularVals[j] = (float)Math.Sqrt(colNormSq);
                colIndices[j] = j;
            }

            // Sort singular values descending
            Array.Sort(colIndices, (idx1, idx2) => singularVals[idx2].CompareTo(singularVals[idx1]));

            var sortedS = new float[n];
            U = new Tensor<float>(m, n);
            var sortedV = new Tensor<float>(n, n);

            for (int j = 0; j < n; j++)
            {
                int origCol = colIndices[j];
                float sVal = singularVals[origCol];
                sortedS[j] = sVal;

                float invS = sVal > Epsilon ? 1.0f / sVal : 0f;
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
                // If original A was transposed (M < N): A = (V) * S * (U^T)
                var tmpU = U;
                U = sortedV;
                Vt = tmpU.Transpose(0, 1).Clone();
            }
        }

        /// <summary>
        /// SVD alias matching standard naming.
        /// </summary>
        public static void Svd(
            Tensor<float> A,
            out Tensor<float> U,
            out Tensor<float> S,
            out Tensor<float> Vt,
            int maxSweeps = 30,
            float tolerance = 1e-6f) => SVD(A, out U, out S, out Vt, maxSweeps, tolerance);

        #endregion

        #region Symmetric Eigendecomposition (Eigh)

        /// <summary>
        /// Computes eigenvalues and eigenvectors of a real symmetric matrix A using the Cyclic Jacobi method: A = V * diag(w) * V^T.
        /// </summary>
        public static void Eigh(
            Tensor<float> A,
            out Tensor<float> eigenvalues,
            out Tensor<float> eigenvectors,
            int maxSweeps = 50,
            float tolerance = 1e-7f)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
            {
                throw new ArgumentException("Eigh requires a square 2D matrix.", nameof(A));
            }

            int n = A.Shape[0];
            var a = A.Clone();
            var v = Tensor.Eye(n);

            for (int sweep = 0; sweep < maxSweeps; sweep++)
            {
                float sumOffDiag = 0f;
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
                        float apq = a[p, q];
                        if (Math.Abs(apq) < 1e-12f) continue;

                        float app = a[p, p];
                        float aqq = a[q, q];
                        float theta = (aqq - app) / (2.0f * apq);
                        float t;
                        if (Math.Abs(theta) < 1e-12f)
                        {
                            t = 1.0f;
                        }
                        else
                        {
                            t = (float)(Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(1.0 + theta * theta)));
                        }
                        if (float.IsNaN(t)) t = 0f;

                        float c = 1.0f / (float)Math.Sqrt(1.0 + t * t);
                        float s = t * c;
                        float tau = s / (1.0f + c);

                        a[p, p] -= t * apq;
                        a[q, q] += t * apq;
                        a[p, q] = 0f;
                        a[q, p] = 0f;

                        for (int i = 0; i < n; i++)
                        {
                            if (i != p && i != q)
                            {
                                float aip = a[i, p];
                                float aiq = a[i, q];
                                a[i, p] = aip - s * (aiq + aip * tau);
                                a[p, i] = a[i, p];
                                a[i, q] = aiq + s * (aip - aiq * tau);
                                a[q, i] = a[i, q];
                            }
                        }

                        for (int i = 0; i < n; i++)
                        {
                            float vip = v[i, p];
                            float viq = v[i, q];
                            v[i, p] = vip - s * (viq + vip * tau);
                            v[i, q] = viq + s * (vip - viq * tau);
                        }
                    }
                }
            }

            var vals = new float[n];
            var indices = new int[n];
            for (int i = 0; i < n; i++)
            {
                vals[i] = a[i, i];
                indices[i] = i;
            }

            Array.Sort(indices, (i1, i2) => vals[i1].CompareTo(vals[i2]));

            var sortedVals = new float[n];
            var sortedV = new Tensor<float>(n, n);

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

        public static void Eigen(Tensor<float> A, out Tensor<float> eigenvalues, out Tensor<float> eigenvectors) =>
            Eigh(A, out eigenvalues, out eigenvectors);

        #endregion

        #region Moore-Penrose Pseudoinverse & Matrix Analytics

        /// <summary>
        /// Computes the Moore-Penrose pseudoinverse of matrix A using SVD: A^+ = V * S^+ * U^T.
        /// </summary>
        public static Tensor<float> Pinverse(Tensor<float> A, float rcond = 1e-5f)
        {
            if (A.Rank != 2) throw new ArgumentException("Pseudoinverse requires a 2D matrix.", nameof(A));
            int m = A.Shape[0];
            int n = A.Shape[1];

            SVD(A, out var U, out var S, out var Vt);

            float maxS = S.Length > 0 ? S[0] : 0f;
            float cutoff = rcond * maxS;

            int k = S.Length;
            var Sp = new Tensor<float>(k, k);
            for (int i = 0; i < k; i++)
            {
                float sVal = S[i];
                if (sVal > cutoff)
                {
                    Sp[i, i] = 1.0f / sVal;
                }
            }

            var V = Vt.Transpose(0, 1);
            var Ut = U.Transpose(0, 1);

            var V_Sp = TensorBlas.MatMul(V, Sp);
            return TensorBlas.MatMul(V_Sp, Ut);
        }

        public static Tensor<float> Pinv(Tensor<float> A, float rcond = 1e-5f) => Pinverse(A, rcond);

        /// <summary>
        /// Computes the sum of elements along the main diagonal of a square matrix.
        /// </summary>
        public static float Trace(Tensor<float> A)
        {
            if (A.Rank != 2 || A.Shape[0] != A.Shape[1])
                throw new ArgumentException("Trace requires a square matrix.", nameof(A));

            float sum = 0f;
            int n = A.Shape[0];
            for (int i = 0; i < n; i++) sum += A[i, i];
            return sum;
        }

        /// <summary>
        /// Extracts the diagonal of a 2D matrix.
        /// </summary>
        public static Tensor<float> Diagonal(Tensor<float> A, int offset = 0)
        {
            if (A.Rank != 2) throw new ArgumentException("Diagonal requires a 2D matrix.", nameof(A));
            int rows = A.Shape[0];
            int cols = A.Shape[1];

            int count = offset >= 0 ? Math.Min(rows, cols - offset) : Math.Min(rows + offset, cols);
            if (count <= 0) return new Tensor<float>(0);

            var diag = new Tensor<float>(count);
            int rStart = offset < 0 ? -offset : 0;
            int cStart = offset > 0 ? offset : 0;

            for (int i = 0; i < count; i++)
            {
                diag[i] = A[rStart + i, cStart + i];
            }
            return diag;
        }

        /// <summary>
        /// Computes the numerical matrix rank of A via SVD.
        /// </summary>
        public static int MatrixRank(Tensor<float> A, float tol = 1e-5f)
        {
            SVD(A, out _, out var S, out _);
            float cutoff = tol * (S.Length > 0 ? S[0] : 0f);
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
