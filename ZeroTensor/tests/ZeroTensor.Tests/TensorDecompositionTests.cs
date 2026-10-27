using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorDecompositionTests
    {
        [Fact]
        public void LU_Decomposition_ReconstructsMatrix()
        {
            var A = Tensor.FromArray(new float[]
            {
                2, 1, 1,
                4, -6, 0,
                -2, 7, 2
            }, 3, 3);

            TensorDecompositions.LU(A, out var L, out var U, out var P);

            // Reconstruct PA = LU
            var LU = TensorBlas.MatMul(L, U);

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float expected = A[P[i], j];
                    float actual = LU[i, j];
                    Assert.True(Math.Abs(expected - actual) < 1e-4f, $"Mismatch at ({i}, {j}): {expected} vs {actual}");
                }
            }
        }

        [Fact]
        public void Solve_LinearSystem_FindsExactSolution()
        {
            // 2x + y = 5
            // x + 3y = 10
            // Solution: x = 1, y = 3
            var A = Tensor.FromArray(new float[]
            {
                2, 1,
                1, 3
            }, 2, 2);

            var B = Tensor.FromArray(new float[] { 5, 10 });

            var x = TensorDecompositions.Solve(A, B);

            Assert.Equal(2, x.Length);
            Assert.True(Math.Abs(x[0] - 1.0f) < 1e-4f, $"x[0] = {x[0]}, expected 1.0");
            Assert.True(Math.Abs(x[1] - 3.0f) < 1e-4f, $"x[1] = {x[1]}, expected 3.0");
        }

        [Fact]
        public void Invert_MultipliedBySelf_YieldsIdentity()
        {
            var A = Tensor.FromArray(new float[]
            {
                4, 7,
                2, 6
            }, 2, 2);

            var invA = TensorDecompositions.Invert(A);
            var prod = TensorBlas.MatMul(A, invA);

            Assert.True(Math.Abs(prod[0, 0] - 1.0f) < 1e-4f);
            Assert.True(Math.Abs(prod[0, 1] - 0.0f) < 1e-4f);
            Assert.True(Math.Abs(prod[1, 0] - 0.0f) < 1e-4f);
            Assert.True(Math.Abs(prod[1, 1] - 1.0f) < 1e-4f);
        }

        [Fact]
        public void Determinant_MatchesAnalyticValue()
        {
            // A = [[4, 7], [2, 6]] -> det = 4*6 - 7*2 = 24 - 14 = 10
            var A = Tensor.FromArray(new float[]
            {
                4, 7,
                2, 6
            }, 2, 2);

            float det = TensorDecompositions.Determinant(A);
            Assert.True(Math.Abs(det - 10.0f) < 1e-4f, $"det = {det}, expected 10.0");
        }

        [Fact]
        public void Cholesky_Decomposition_RecoversSymmetricPositiveDefinite()
        {
            // A = [[4, 12, -16], [12, 37, -43], [-16, -43, 98]]
            var A = Tensor.FromArray(new float[]
            {
                4, 12, -16,
                12, 37, -43,
                -16, -43, 98
            }, 3, 3);

            var L = TensorDecompositions.Cholesky(A);
            var Lt = L.Transpose(0, 1);
            var reconstructed = TensorBlas.MatMul(L, Lt);

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Assert.True(Math.Abs(A[r, c] - reconstructed[r, c]) < 1e-4f);
                }
            }
        }

        [Fact]
        public void QR_Decomposition_ProducesOrthogonalQAndUpperTriangularR()
        {
            var A = Tensor.FromArray(new float[]
            {
                12, -51, 4,
                6, 167, -68,
                -4, 24, -41
            }, 3, 3);

            TensorDecompositions.QR(A, out var Q, out var R);

            // Verify Q * R == A
            var reconstructed = TensorBlas.MatMul(Q, R);
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Assert.True(Math.Abs(A[r, c] - reconstructed[r, c]) < 1e-3f, $"Mismatch at ({r}, {c})");
                }
            }

            // Verify Q^T * Q == I
            var QtQ = TensorBlas.MatMul(Q.Transpose(0, 1), Q);
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    float expected = r == c ? 1.0f : 0.0f;
                    Assert.True(Math.Abs(QtQ[r, c] - expected) < 1e-3f);
                }
            }
        }

        [Fact]
        public void SVD_Decomposition_ReconstructsMatrixAccurately()
        {
            var A = Tensor.FromArray(new float[]
            {
                3, 2, 2,
                2, 3, -2
            }, 2, 3);

            TensorDecompositions.SVD(A, out var U, out var S, out var Vt);

            // Form Sigma (2x2) for reduced SVD: U (2x2) @ Sigma (2x2) @ Vt (2x3) = A (2x3)
            var Sigma = new Tensor<float>(2, 2);
            Sigma[0, 0] = S[0];
            Sigma[1, 1] = S[1];

            // Reconstruct: U @ Sigma @ Vt
            var USigma = TensorBlas.MatMul(U, Sigma);
            var reconstructed = TensorBlas.MatMul(USigma, Vt);

            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    Assert.True(Math.Abs(A[r, c] - reconstructed[r, c]) < 1e-3f, $"SVD mismatch at ({r}, {c}): {A[r, c]} vs {reconstructed[r, c]}");
                }
            }
        }
    }
}
