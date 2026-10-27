using System;
using Xunit;
using ZeroTensor.Core;

namespace ZeroTensor.Tests
{
    public class TensorAdvancedLinearAlgebraTests
    {
        [Fact]
        public void ReadmeQuickstart_Examples_ExecuteSuccessfully()
        {
            // 1. Creating and Slicing Tensors
            var a = Tensor.Create<float>(new[] { 3, 3 }, new float[]
            {
                1f, 2f, 3f,
                4f, 5f, 6f,
                7f, 8f, 9f
            });
            var reshaped = a.Reshape(1, 9);
            var transposed = a.Transpose();
            Assert.Equal(a[0, 1], transposed[1, 0]);

            // 2. Cache-Blocked Matrix Multiplication (GEMM)
            var m1 = Tensor.RandomUniform(16, 16, min: -1.0f, max: 1.0f);
            var m2 = Tensor.RandomUniform(16, 16, min: -1.0f, max: 1.0f);
            var result = TensorBlas.Gemm(m1, m2);
            Assert.Equal(new TensorShape(16, 16), result.Shape);

            // 3. Singular Value Decomposition (SVD)
            var matrix = Tensor.Create<double>(new[] { 3, 3 }, new double[]
            {
                4.0, 11.0, 14.0,
                8.0,  7.0, -2.0,
                1.0,  2.0,  3.0
            });
            TensorDecompositions.Svd(matrix, out var u, out var s, out var vt);
            Assert.True(s[0] > 0);
        }

        [Fact]
        public void BatchedMatMul_4D_ExecutesCorrectly()
        {
            // Batch shape: [2, 3], Matrix: [4, 5] x [5, 2] -> [2, 3, 4, 2]
            var a = Tensor.Full(1.0f, 2, 3, 4, 5);
            var b = Tensor.Full(2.0f, 2, 3, 5, 2);

            var c = TensorBlas.MatMul(a, b);

            Assert.Equal(new TensorShape(2, 3, 4, 2), c.Shape);
            // Each element is sum(1.0 * 2.0) across K=5 elements = 10.0f
            Assert.Equal(10.0f, c[0, 0, 0, 0]);
            Assert.Equal(10.0f, c[1, 2, 3, 1]);
        }

        [Fact]
        public void Double_GEMM_And_Norm()
        {
            var a = Tensor.Create<double>(new[] { 2, 3 }, new double[]
            {
                1, 2, 3,
                4, 5, 6
            });

            var b = Tensor.Create<double>(new[] { 3, 2 }, new double[]
            {
                7, 8,
                9, 1,
                2, 3
            });

            var c = TensorBlas.MatMul(a, b);
            Assert.Equal(new TensorShape(2, 2), c.Shape);
            Assert.Equal(31.0, c[0, 0]);
            Assert.Equal(19.0, c[0, 1]);
            Assert.Equal(85.0, c[1, 0]);
            Assert.Equal(55.0, c[1, 1]);

            var v = Tensor.FromArray(new double[] { 3.0, 4.0 });
            double norm = TensorBlas.Norm(v);
            Assert.Equal(5.0, norm);
        }

        [Fact]
        public void Eigh_Float_RecoversExactEigenvalues_And_Eigenvectors()
        {
            // Symmetric matrix:
            // [2, 1]
            // [1, 2]
            // Eigenvalues: 1 and 3
            var A = Tensor.FromArray(new float[]
            {
                2f, 1f,
                1f, 2f
            }, 2, 2);

            TensorDecompositions.Eigh(A, out var w, out var V);

            Assert.Equal(2, w.Length);
            Assert.True(Math.Abs(w[0] - 1.0f) < 1e-4f, $"w[0]={w[0]}, expected 1.0");
            Assert.True(Math.Abs(w[1] - 3.0f) < 1e-4f, $"w[1]={w[1]}, expected 3.0");

            // Verify orthogonality: V * V^T = I
            var Vt = V.Transpose(0, 1);
            var V_Vt = TensorBlas.MatMul(V, Vt);
            Assert.True(Math.Abs(V_Vt[0, 0] - 1.0f) < 1e-4f);
            Assert.True(Math.Abs(V_Vt[1, 1] - 1.0f) < 1e-4f);
            Assert.True(Math.Abs(V_Vt[0, 1]) < 1e-4f);

            // Verify A * V = V * diag(w)
            var AV = TensorBlas.MatMul(A, V);
            for (int col = 0; col < 2; col++)
            {
                for (int row = 0; row < 2; row++)
                {
                    float expected = V[row, col] * w[col];
                    Assert.True(Math.Abs(AV[row, col] - expected) < 1e-4f);
                }
            }
        }

        [Fact]
        public void Eigh_Double_RecoversExactEigenvalues()
        {
            var A = Tensor.Create<double>(new[] { 3, 3 }, new double[]
            {
                4.0, 0.0, 0.0,
                0.0, 7.0, 0.0,
                0.0, 0.0, 2.0
            });

            TensorDecompositions.Eigh(A, out var w, out var V);

            // Eigenvalues sorted ascending: 2, 4, 7
            Assert.True(Math.Abs(w[0] - 2.0) < 1e-8);
            Assert.True(Math.Abs(w[1] - 4.0) < 1e-8);
            Assert.True(Math.Abs(w[2] - 7.0) < 1e-8);
        }

        [Fact]
        public void Pinverse_SatisfiesMoorePenroseCondition()
        {
            // A is 3x2
            var A = Tensor.FromArray(new float[]
            {
                1f, 2f,
                3f, 4f,
                5f, 6f
            }, 3, 2);

            var Apinv = TensorDecompositions.Pinverse(A);
            Assert.Equal(new TensorShape(2, 3), Apinv.Shape);

            // Condition 1: A * A^+ * A = A
            var A_Apinv = TensorBlas.MatMul(A, Apinv);
            var A_recon = TensorBlas.MatMul(A_Apinv, A);

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    Assert.True(Math.Abs(A[r, c] - A_recon[r, c]) < 1e-3f, $"Mismatch at ({r}, {c})");
                }
            }
        }

        [Fact]
        public void MatrixAnalytics_Trace_Diagonal_Rank()
        {
            var A = Tensor.FromArray(new float[]
            {
                5f, 2f, 1f,
                0f, 3f, 7f,
                1f, 4f, 2f
            }, 3, 3);

            // Trace: 5 + 3 + 2 = 10
            float tr = TensorDecompositions.Trace(A);
            Assert.Equal(10f, tr);

            // Diagonal
            var diag = TensorDecompositions.Diagonal(A);
            Assert.Equal(new float[] { 5f, 3f, 2f }, diag.ToArray());

            // Rank
            int rank = TensorDecompositions.MatrixRank(A);
            Assert.Equal(3, rank);
        }
    }
}
