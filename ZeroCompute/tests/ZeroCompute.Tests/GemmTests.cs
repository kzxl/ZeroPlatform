using System;
using Xunit;
using ZeroCompute.Core;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    public class GemmTests
    {
        [Fact]
        public void Gemm_KnownMatrixProduct_MatchesExpected()
        {
            // A: 2x3
            // [1, 2, 3]
            // [4, 5, 6]
            var A = Tensor.FromArray(new float[]
            {
                1, 2, 3,
                4, 5, 6
            }, 2, 3);

            // B: 3x2
            // [7,  8]
            // [9,  1]
            // [2,  3]
            var B = Tensor.FromArray(new float[]
            {
                7, 8,
                9, 1,
                2, 3
            }, 3, 2);

            // C = A @ B: 2x2
            // Row 0: 1*7 + 2*9 + 3*2 = 7 + 18 + 6 = 31
            //        1*8 + 2*1 + 3*3 = 8 + 2 + 9  = 19
            // Row 1: 4*7 + 5*9 + 6*2 = 28 + 45 + 12 = 85
            //        4*8 + 5*1 + 6*3 = 32 + 5 + 18 = 55
            var C = Tensor.Zeros<float>(2, 2);

            ComputeDevice.Cpu.Gemm(A, B, C);

            Assert.Equal(31f, C[0, 0]);
            Assert.Equal(19f, C[0, 1]);
            Assert.Equal(85f, C[1, 0]);
            Assert.Equal(55f, C[1, 1]);
        }

        [Fact]
        public void Gemm_WithAlphaAndBetaScaling_AppliesCorrectFormula()
        {
            var A = Tensor.FromArray(new float[] { 1, 2, 3, 4 }, 2, 2);
            var B = Tensor.FromArray(new float[] { 2, 0, 1, 2 }, 2, 2);
            // A @ B =
            // [1*2+2*1, 1*0+2*2] = [4, 4]
            // [3*2+4*1, 3*0+4*2] = [10, 8]

            // C initial: [1, 1, 1, 1]
            var C = Tensor.Ones(2, 2);

            // C = 2.0 * (A @ B) + 3.0 * C
            // [2*4 + 3*1, 2*4 + 3*1] = [11, 11]
            // [2*10 + 3*1, 2*8 + 3*1] = [23, 19]
            ComputeDevice.Cpu.Gemm(A, B, C, alpha: 2.0f, beta: 3.0f);

            Assert.Equal(11f, C[0, 0]);
            Assert.Equal(11f, C[0, 1]);
            Assert.Equal(23f, C[1, 0]);
            Assert.Equal(19f, C[1, 1]);
        }

        [Fact]
        public void Gemm_LargeMatrixMultiplication_CompletesAccurately()
        {
            int M = 128, K = 96, N = 64;
            var A = Tensor.Ones(M, K);
            var B = Tensor.Ones(K, N);
            var C = Tensor.Zeros<float>(M, N);

            // Each element should be K = 96
            ComputeDevice.Cpu.Gemm(A, B, C);

            for (int r = 0; r < M; r += 16)
            {
                for (int c = 0; c < N; c += 16)
                {
                    Assert.Equal((float)K, C[r, c]);
                }
            }
        }
    }
}
