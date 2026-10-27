using System;
using Xunit;
using ZeroCompute.Core;
using ZeroCompute.Core.Context;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    [Collection("D3D11Hardware")]
    public class KernelFusionAndSdpaTests
    {
        [Fact]
        public void FusedGemm_WithBiasAndRelu_MatchesSequentialCalculation()
        {
            int M = 4, K = 8, N = 6;
            var A = Tensor.Zeros<float>(M, K);
            var B = Tensor.Zeros<float>(K, N);
            var bias = Tensor.Zeros<float>(N);
            var actualC = Tensor.Zeros<float>(M, N);
            var expectedC = Tensor.Zeros<float>(M, N);

            var rnd = new Random(42);
            for (int i = 0; i < M; i++)
                for (int k = 0; k < K; k++)
                    A[i, k] = (float)(rnd.NextDouble() * 4.0 - 2.0);

            for (int k = 0; k < K; k++)
                for (int j = 0; j < N; j++)
                    B[k, j] = (float)(rnd.NextDouble() * 4.0 - 2.0);

            for (int j = 0; j < N; j++)
                bias[j] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            // Sequential reference: C = ReLU(A @ B + bias)
            ComputeDevice.Cpu.Gemm(A, B, expectedC);
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    float val = expectedC[i, j] + bias[j];
                    expectedC[i, j] = Math.Max(0.0f, val);
                }
            }

            // Fused execution
            ComputeDevice.Cpu.FusedGemm(A, B, bias, actualC, ComputeActivationType.ReLU);

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Assert.Equal(expectedC[i, j], actualC[i, j], precision: 4);
                }
            }
        }

        [Fact]
        public void FusedGemm_WithBiasAndGelu_MatchesSequentialCalculation()
        {
            int M = 3, K = 6, N = 4;
            var A = Tensor.Zeros<float>(M, K);
            var B = Tensor.Zeros<float>(K, N);
            var bias = Tensor.Zeros<float>(N);
            var actualC = Tensor.Zeros<float>(M, N);
            var expectedC = Tensor.Zeros<float>(M, N);

            var rnd = new Random(101);
            for (int i = 0; i < M; i++)
                for (int k = 0; k < K; k++)
                    A[i, k] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            for (int k = 0; k < K; k++)
                for (int j = 0; j < N; j++)
                    B[k, j] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            for (int j = 0; j < N; j++)
                bias[j] = (float)(rnd.NextDouble() - 0.5f);

            // Sequential reference: C = GELU(A @ B + bias)
            ComputeDevice.Cpu.Gemm(A, B, expectedC);
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    float val = expectedC[i, j] + bias[j];
                    float inner = 0.79788456f * (val + 0.044715f * val * val * val);
                    expectedC[i, j] = 0.5f * val * (1.0f + (float)Math.Tanh(inner));
                }
            }

            ComputeDevice.Cpu.FusedGemm(A, B, bias, actualC, ComputeActivationType.GELU);

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Assert.Equal(expectedC[i, j], actualC[i, j], precision: 4);
                }
            }
        }

        [Fact]
        public void FusedResidualRmsNorm_MatchesSequentialAddAndRmsNorm()
        {
            int rows = 4, hiddenDim = 16;
            var input = Tensor.Zeros<float>(rows, hiddenDim);
            var residual = Tensor.Zeros<float>(rows, hiddenDim);
            var weight = Tensor.Ones(hiddenDim);
            var expectedOutput = Tensor.Zeros<float>(rows, hiddenDim);
            var actualOutput = Tensor.Zeros<float>(rows, hiddenDim);

            var rnd = new Random(777);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < hiddenDim; c++)
                {
                    input[r, c] = (float)(rnd.NextDouble() * 4.0 - 2.0);
                    residual[r, c] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }
            }

            // Sequential reference: sum = input + residual, then RmsNorm(sum)
            var sumTensor = Tensor.Zeros<float>(rows, hiddenDim);
            ComputeDevice.Cpu.Add(input, residual, sumTensor);
            ComputeDevice.Cpu.RmsNorm(sumTensor, expectedOutput, weight, epsilon: 1e-5f);

            // Fused execution
            ComputeDevice.Cpu.FusedResidualRmsNorm(input, residual, actualOutput, weight, epsilon: 1e-5f);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < hiddenDim; c++)
                {
                    Assert.Equal(expectedOutput[r, c], actualOutput[r, c], precision: 4);
                }
            }
        }

        [Fact]
        public void ScaledDotProductAttention_NonCausal_MatchesReference()
        {
            int seqLenQ = 4, seqLenK = 4, headDim = 8;
            var Q = Tensor.Zeros<float>(seqLenQ, headDim);
            var K = Tensor.Zeros<float>(seqLenK, headDim);
            var V = Tensor.Zeros<float>(seqLenK, headDim);
            var actualOut = Tensor.Zeros<float>(seqLenQ, headDim);

            var rnd = new Random(1337);
            for (int i = 0; i < seqLenQ; i++)
                for (int d = 0; d < headDim; d++)
                    Q[i, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            for (int j = 0; j < seqLenK; j++)
                for (int d = 0; d < headDim; d++)
                {
                    K[j, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    V[j, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }

            float scale = 1.0f / (float)Math.Sqrt(headDim);

            // Compute reference attention: Softmax(Q @ K^T * scale) @ V
            var refOut = Tensor.Zeros<float>(seqLenQ, headDim);
            for (int i = 0; i < seqLenQ; i++)
            {
                // 1. dot product scores
                float[] scores = new float[seqLenK];
                float maxScore = float.MinValue;
                for (int j = 0; j < seqLenK; j++)
                {
                    float dot = 0f;
                    for (int d = 0; d < headDim; d++) dot += Q[i, d] * K[j, d];
                    scores[j] = dot * scale;
                    if (scores[j] > maxScore) maxScore = scores[j];
                }

                // 2. Softmax probabilities
                float expSum = 0f;
                float[] probs = new float[seqLenK];
                for (int j = 0; j < seqLenK; j++)
                {
                    probs[j] = (float)Math.Exp(scores[j] - maxScore);
                    expSum += probs[j];
                }
                for (int j = 0; j < seqLenK; j++) probs[j] /= expSum;

                // 3. Output = probs @ V
                for (int d = 0; d < headDim; d++)
                {
                    float sum = 0f;
                    for (int j = 0; j < seqLenK; j++) sum += probs[j] * V[j, d];
                    refOut[i, d] = sum;
                }
            }

            ComputeDevice.Cpu.ScaledDotProductAttention(Q, K, V, actualOut, scale, isCausal: false);

            for (int i = 0; i < seqLenQ; i++)
            {
                for (int d = 0; d < headDim; d++)
                {
                    Assert.Equal(refOut[i, d], actualOut[i, d], precision: 4);
                }
            }
        }

        [Fact]
        public void ScaledDotProductAttention_Causal_MatchesReference()
        {
            int seqLenQ = 4, seqLenK = 4, headDim = 8;
            var Q = Tensor.Zeros<float>(seqLenQ, headDim);
            var K = Tensor.Zeros<float>(seqLenK, headDim);
            var V = Tensor.Zeros<float>(seqLenK, headDim);
            var actualOut = Tensor.Zeros<float>(seqLenQ, headDim);

            var rnd = new Random(2026);
            for (int i = 0; i < seqLenQ; i++)
                for (int d = 0; d < headDim; d++)
                    Q[i, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            for (int j = 0; j < seqLenK; j++)
                for (int d = 0; d < headDim; d++)
                {
                    K[j, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    V[j, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }

            float scale = 1.0f / (float)Math.Sqrt(headDim);

            // Compute causal reference: keys j > i are masked
            var refOut = Tensor.Zeros<float>(seqLenQ, headDim);
            for (int i = 0; i < seqLenQ; i++)
            {
                float[] scores = new float[i + 1];
                float maxScore = float.MinValue;
                for (int j = 0; j <= i; j++)
                {
                    float dot = 0f;
                    for (int d = 0; d < headDim; d++) dot += Q[i, d] * K[j, d];
                    scores[j] = dot * scale;
                    if (scores[j] > maxScore) maxScore = scores[j];
                }

                float expSum = 0f;
                float[] probs = new float[i + 1];
                for (int j = 0; j <= i; j++)
                {
                    probs[j] = (float)Math.Exp(scores[j] - maxScore);
                    expSum += probs[j];
                }
                for (int j = 0; j <= i; j++) probs[j] /= expSum;

                for (int d = 0; d < headDim; d++)
                {
                    float sum = 0f;
                    for (int j = 0; j <= i; j++) sum += probs[j] * V[j, d];
                    refOut[i, d] = sum;
                }
            }

            ComputeDevice.Cpu.ScaledDotProductAttention(Q, K, V, actualOut, scale, isCausal: true);

            for (int i = 0; i < seqLenQ; i++)
            {
                for (int d = 0; d < headDim; d++)
                {
                    Assert.Equal(refOut[i, d], actualOut[i, d], precision: 4);
                }
            }
        }

        [Fact]
        public void Gemm_Half_MatchesFloat32Calculation()
        {
            int M = 4, K = 8, N = 6;
            var aHalf = new Tensor<Half>(M, K);
            var bHalf = new Tensor<Half>(K, N);
            var cHalf = new Tensor<Half>(M, N);

            var aFloat = Tensor.Zeros<float>(M, K);
            var bFloat = Tensor.Zeros<float>(K, N);
            var cFloat = Tensor.Zeros<float>(M, N);

            var rnd = new Random(888);
            for (int i = 0; i < M; i++)
            {
                for (int k = 0; k < K; k++)
                {
                    float val = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    aFloat[i, k] = val;
                    aHalf[i, k] = (Half)val;
                }
            }

            for (int k = 0; k < K; k++)
            {
                for (int j = 0; j < N; j++)
                {
                    float val = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    bFloat[k, j] = val;
                    bHalf[k, j] = (Half)val;
                }
            }

            ComputeDevice.Cpu.Gemm(aFloat, bFloat, cFloat);
            ComputeDevice.Cpu.Gemm(aHalf, bHalf, cHalf);

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    float halfAsFloat = (float)cHalf[i, j];
                    float expected = cFloat[i, j];
                    Assert.Equal(expected, halfAsFloat, precision: 2);
                }
            }
        }

        [Fact]
        public void GPU_FusedGemm_And_FusedResidualRmsNorm_ChainedVram()
        {
            if (!ComputeDevice.IsGpuAvailable)
            {
                return;
            }

            var gpu = ComputeDevice.Gpu;
            int M = 8, K = 16, N = 8;

            var hostA = Tensor.Zeros<float>(M, K);
            var hostB = Tensor.Zeros<float>(K, N);
            var hostBias = Tensor.Zeros<float>(N);
            var hostRes = Tensor.Zeros<float>(M, N);

            var rnd = new Random(9999);
            for (int i = 0; i < M; i++)
                for (int k = 0; k < K; k++)
                    hostA[i, k] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            for (int k = 0; k < K; k++)
                for (int j = 0; j < N; j++)
                    hostB[k, j] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            for (int j = 0; j < N; j++)
                hostBias[j] = 0.5f;

            for (int i = 0; i < M; i++)
                for (int j = 0; j < N; j++)
                    hostRes[i, j] = 1.0f;

            // 1. Move to GPU VRAM
            var devA = gpu.ToDevice(hostA);
            var devB = gpu.ToDevice(hostB);
            var devBias = gpu.ToDevice(hostBias);
            var devRes = gpu.ToDevice(hostRes);
            var devC = gpu.AllocateDeviceTensor<float>(new TensorShape(M, N));
            var devOut = gpu.AllocateDeviceTensor<float>(new TensorShape(M, N));

            // 2. Chained Fused Execution: FusedGemm(GELU) -> FusedResidualRmsNorm
            gpu.FusedGemm(devA, devB, devBias, devC, ComputeActivationType.GELU);
            gpu.FusedResidualRmsNorm(devC, devRes, devOut, weight: null);

            // 3. Download and verify
            var actual = devOut.ToCpu();

            // Reference on CPU
            var cpuC = Tensor.Zeros<float>(M, N);
            var cpuOut = Tensor.Zeros<float>(M, N);
            ComputeDevice.Cpu.FusedGemm(hostA, hostB, hostBias, cpuC, ComputeActivationType.GELU);
            ComputeDevice.Cpu.FusedResidualRmsNorm(cpuC, hostRes, cpuOut, weight: null);

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Assert.Equal(cpuOut[i, j], actual[i, j], precision: 3);
                }
            }
        }

        [Fact]
        public void GPU_ScaledDotProductAttention_MatchesCpuReference()
        {
            if (!ComputeDevice.IsGpuAvailable)
            {
                return;
            }

            var gpu = ComputeDevice.Gpu;
            int seqLenQ = 8, seqLenK = 8, headDim = 16;
            var Q = Tensor.Zeros<float>(seqLenQ, headDim);
            var K = Tensor.Zeros<float>(seqLenK, headDim);
            var V = Tensor.Zeros<float>(seqLenK, headDim);
            var expectedOut = Tensor.Zeros<float>(seqLenQ, headDim);

            var rnd = new Random(4321);
            for (int i = 0; i < seqLenQ; i++)
                for (int d = 0; d < headDim; d++)
                    Q[i, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            for (int j = 0; j < seqLenK; j++)
                for (int d = 0; d < headDim; d++)
                {
                    K[j, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                    V[j, d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }

            // CPU reference
            ComputeDevice.Cpu.ScaledDotProductAttention(Q, K, V, expectedOut, scale: null, isCausal: false);

            // GPU execution
            var devQ = gpu.ToDevice(Q);
            var devK = gpu.ToDevice(K);
            var devV = gpu.ToDevice(V);
            var devOut = gpu.AllocateDeviceTensor<float>(expectedOut.Shape);

            gpu.ScaledDotProductAttention(devQ, devK, devV, devOut, scale: null, isCausal: false);

            var actualOut = devOut.ToCpu();
            for (int i = 0; i < seqLenQ; i++)
            {
                for (int d = 0; d < headDim; d++)
                {
                    Assert.Equal(expectedOut[i, d], actualOut[i, d], precision: 3);
                }
            }
        }
    }
}
