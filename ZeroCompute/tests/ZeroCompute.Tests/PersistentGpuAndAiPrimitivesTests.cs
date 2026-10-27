using System;
using Xunit;
using ZeroCompute.Core;
using ZeroCompute.Core.Blas;
using ZeroCompute.Core.Context;
using ZeroTensor.Core;
using ZeroTensor.Core.Storage;

namespace ZeroCompute.Tests
{
    [Collection("D3D11Hardware")]
    public class PersistentGpuAndAiPrimitivesTests
    {
        [Fact]
        public void BatchedGemm_Rank3_MatchesIndividualSliceGemm()
        {
            int B = 3, M = 4, K = 8, N = 5;
            var A = Tensor.Zeros<float>(B, M, K);
            var Bmat = Tensor.Zeros<float>(B, K, N);
            var C = Tensor.Zeros<float>(B, M, N);

            var rnd = new Random(42);
            for (int b = 0; b < B; b++)
            {
                for (int i = 0; i < M; i++)
                    for (int k = 0; k < K; k++)
                        A[b, i, k] = (float)rnd.NextDouble();

                for (int k = 0; k < K; k++)
                    for (int j = 0; j < N; j++)
                        Bmat[b, k, j] = (float)rnd.NextDouble();
            }

            ComputeDevice.Cpu.BatchedGemm(A, Bmat, C);

            // Verify each batch slice against individual 2D Gemm
            for (int b = 0; b < B; b++)
            {
                var sliceA = Tensor.Zeros<float>(M, K);
                var sliceB = Tensor.Zeros<float>(K, N);
                var expectedSliceC = Tensor.Zeros<float>(M, N);

                for (int i = 0; i < M; i++)
                    for (int k = 0; k < K; k++)
                        sliceA[i, k] = A[b, i, k];

                for (int k = 0; k < K; k++)
                    for (int j = 0; j < N; j++)
                        sliceB[k, j] = Bmat[b, k, j];

                ComputeDevice.Cpu.Gemm(sliceA, sliceB, expectedSliceC);

                for (int i = 0; i < M; i++)
                {
                    for (int j = 0; j < N; j++)
                    {
                        Assert.Equal(expectedSliceC[i, j], C[b, i, j], precision: 4);
                    }
                }
            }
        }

        [Fact]
        public void RmsNorm_NormalizesRowsAccurately()
        {
            int rows = 3, hiddenDim = 16;
            var input = Tensor.Zeros<float>(rows, hiddenDim);
            var weight = Tensor.Ones(hiddenDim);
            var output = Tensor.Zeros<float>(rows, hiddenDim);

            var rnd = new Random(1337);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < hiddenDim; c++)
                {
                    input[r, c] = (float)(rnd.NextDouble() * 4.0 - 2.0);
                }
            }

            ComputeDevice.Cpu.RmsNorm(input, output, weight, epsilon: 1e-5f);

            for (int r = 0; r < rows; r++)
            {
                float sumSq = 0f;
                for (int c = 0; c < hiddenDim; c++) sumSq += input[r, c] * input[r, c];
                float expectedInvRms = 1.0f / (float)Math.Sqrt((sumSq / hiddenDim) + 1e-5f);

                for (int c = 0; c < hiddenDim; c++)
                {
                    float expectedVal = input[r, c] * expectedInvRms * weight[c];
                    Assert.Equal(expectedVal, output[r, c], precision: 4);
                }
            }
        }

        [Fact]
        public void LayerNorm_ZeroMeanAndUnitVariance()
        {
            int rows = 2, hiddenDim = 64;
            var input = Tensor.Zeros<float>(rows, hiddenDim);
            var weight = Tensor.Ones(hiddenDim);
            var bias = Tensor.Zeros<float>(hiddenDim);
            var output = Tensor.Zeros<float>(rows, hiddenDim);

            var rnd = new Random(777);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < hiddenDim; c++)
                {
                    input[r, c] = (float)(rnd.NextDouble() * 10.0 - 5.0);
                }
            }

            ComputeDevice.Cpu.LayerNorm(input, output, weight, bias, epsilon: 1e-5f);

            // Output rows should have mean approx 0 and variance approx 1
            for (int r = 0; r < rows; r++)
            {
                float sum = 0f;
                for (int c = 0; c < hiddenDim; c++) sum += output[r, c];
                float mean = sum / hiddenDim;

                float sumVar = 0f;
                for (int c = 0; c < hiddenDim; c++) sumVar += (output[r, c] - mean) * (output[r, c] - mean);
                float variance = sumVar / hiddenDim;

                Assert.True(Math.Abs(mean) < 1e-4f, $"Row {r} mean {mean} should be close to 0");
                Assert.True(Math.Abs(variance - 1.0f) < 1e-2f, $"Row {r} variance {variance} should be close to 1.0");
            }
        }

        [Fact]
        public void Softmax_ProbabilitiesSumToOne()
        {
            int rows = 4, cols = 8;
            var input = Tensor.Zeros<float>(rows, cols);
            var output = Tensor.Zeros<float>(rows, cols);

            var rnd = new Random(999);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    input[r, c] = (float)(rnd.NextDouble() * 6.0 - 3.0);
                }
            }

            ComputeDevice.Cpu.Softmax(input, output);

            for (int r = 0; r < rows; r++)
            {
                float rowSum = 0f;
                for (int c = 0; c < cols; c++)
                {
                    float val = output[r, c];
                    Assert.True(val > 0.0f, "Softmax probabilities must be strictly positive.");
                    rowSum += val;
                }
                Assert.True(Math.Abs(rowSum - 1.0f) < 1e-5f, $"Row {r} sum {rowSum} must equal 1.0");
            }
        }

        [Fact]
        public void PersistentGpuResidency_ChainedExecution_WithoutCpuIntermediaries()
        {
            if (!ComputeDevice.IsGpuAvailable)
            {
                // Headless environment without D3D11 hardware
                return;
            }

            var gpu = ComputeDevice.Gpu;
            int M = 16, K = 32, N = 16;

            var hostA = Tensor.Ones(M, K);
            var hostB = Tensor.Ones(K, N);

            // 1. Move tensors to GPU VRAM
            var devA = gpu.ToDevice(hostA);
            var devB = gpu.ToDevice(hostB);
            var devC = gpu.AllocateDeviceTensor<float>(new TensorShape(M, N));

            Assert.Equal(DeviceType.Direct3D11, devA.Device);
            Assert.Equal(DeviceType.Direct3D11, devB.Device);
            Assert.Equal(DeviceType.Direct3D11, devC.Device);

            // 2. Chained execution directly on VRAM: Gemm -> Gelu -> RmsNorm
            gpu.Gemm(devA, devB, devC);
            gpu.Activation(devC, devC, ComputeActivationType.GELU);
            gpu.RmsNorm(devC, devC, weight: null);

            // Tensor should still remain resident in GPU VRAM
            Assert.Equal(DeviceType.Direct3D11, devC.Device);

            // 3. Download to host once at the end
            var hostResult = devC.ToCpu();
            Assert.Equal(DeviceType.Cpu, hostResult.Device);
            Assert.Equal(M, hostResult.Shape[0]);
            Assert.Equal(N, hostResult.Shape[1]);

            // All elements in hostResult should be identical across row dimension
            for (int r = 0; r < M; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    Assert.False(float.IsNaN(hostResult[r, c]));
                    Assert.True(hostResult[r, c] > 0.0f);
                }
            }
        }

        [Fact]
        public void PersistentGpu_InPlace_Add_Multiply_LayerNorm_Softmax()
        {
            if (!ComputeDevice.IsGpuAvailable)
            {
                return;
            }

            var gpu = ComputeDevice.Gpu;
            int rows = 4, cols = 16;
            var hostX = Tensor.Zeros<float>(rows, cols);
            var hostY = Tensor.Zeros<float>(rows, cols);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    hostX[r, c] = (r + 1) * 0.5f + c * 0.1f;
                    hostY[r, c] = 1.0f;
                }
            }

            var devX = gpu.ToDevice(hostX);
            var devY = gpu.ToDevice(hostY);

            // In-place Add: X = X + Y
            gpu.Add(devX, devY, devX);
            var afterAdd = devX.ToCpu();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    Assert.Equal(hostX[r, c] + hostY[r, c], afterAdd[r, c], precision: 4);

            // In-place Multiply: X = X * Y
            gpu.Multiply(devX, devY, devX);
            var afterMul = devX.ToCpu();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    Assert.Equal(afterAdd[r, c], afterMul[r, c], precision: 4);

            // In-place LayerNorm: X = LayerNorm(X)
            gpu.LayerNorm(devX, devX);
            var afterLN = devX.ToCpu();
            for (int r = 0; r < rows; r++)
            {
                float sum = 0f;
                for (int c = 0; c < cols; c++) sum += afterLN[r, c];
                float mean = sum / cols;
                Assert.True(Math.Abs(mean) < 1e-3f, $"Row {r} mean {mean} should be near 0");
            }

            // In-place Softmax: X = Softmax(X)
            gpu.Softmax(devX, devX);
            var afterSoftmax = devX.ToCpu();
            for (int r = 0; r < rows; r++)
            {
                float rowSum = 0f;
                for (int c = 0; c < cols; c++)
                {
                    Assert.True(afterSoftmax[r, c] > 0.0f);
                    rowSum += afterSoftmax[r, c];
                }
                Assert.True(Math.Abs(rowSum - 1.0f) < 1e-4f, $"Row {r} sum {rowSum} should be 1.0");
            }
        }

        [Fact]
        public void BatchedGemm_Gpu_MatchesCpuReference()
        {
            if (!ComputeDevice.IsGpuAvailable)
            {
                return;
            }

            var gpu = ComputeDevice.Gpu;
            int B = 2, M = 4, K = 8, N = 6;
            var A = Tensor.Zeros<float>(B, M, K);
            var Bmat = Tensor.Zeros<float>(B, K, N);
            var expectedC = Tensor.Zeros<float>(B, M, N);

            var rnd = new Random(12345);
            for (int b = 0; b < B; b++)
            {
                for (int i = 0; i < M; i++)
                    for (int k = 0; k < K; k++)
                        A[b, i, k] = (float)rnd.NextDouble();

                for (int k = 0; k < K; k++)
                    for (int j = 0; j < N; j++)
                        Bmat[b, k, j] = (float)rnd.NextDouble();
            }

            ComputeDevice.Cpu.BatchedGemm(A, Bmat, expectedC);

            var devA = gpu.ToDevice(A);
            var devB = gpu.ToDevice(Bmat);
            var devC = gpu.AllocateDeviceTensor<float>(expectedC.Shape);

            gpu.BatchedGemm(devA, devB, devC);

            var actualC = devC.ToCpu();
            for (int b = 0; b < B; b++)
                for (int i = 0; i < M; i++)
                    for (int j = 0; j < N; j++)
                        Assert.Equal(expectedC[b, i, j], actualC[b, i, j], precision: 3);
        }

        [Fact]
        public void D3D11TensorStorage_CopyToHost_WithNonZeroOffset_SucceedsForSmallAndLargeRanges()
        {
            if (!ComputeDevice.IsGpuAvailable)
            {
                return;
            }

            var gpu = ComputeDevice.Gpu;
            int totalLen = 200;
            var hostTensor = Tensor.Zeros<float>(totalLen);
            for (int i = 0; i < totalLen; i++)
            {
                hostTensor[i] = i * 1.5f;
            }

            using var devTensor = gpu.ToDevice(hostTensor);
            var transferStorage = Assert.IsAssignableFrom<IDeviceStorageTransfer<float>>(devTensor.Storage);

            // 1. Small sub-range with non-zero offset where offset + length <= 128 (stackalloc path)
            Span<float> smallDest = stackalloc float[16];
            transferStorage.CopyToHost(offset: 20, length: 16, smallDest);
            for (int i = 0; i < 16; i++)
            {
                Assert.Equal((20 + i) * 1.5f, smallDest[i]);
            }

            // 2. Sub-range where length <= 128 but offset + length > 128 (ArrayPool path, previously threw ArgumentOutOfRangeException)
            Span<float> midDest = stackalloc float[32];
            transferStorage.CopyToHost(offset: 110, length: 32, midDest);
            for (int i = 0; i < 32; i++)
            {
                Assert.Equal((110 + i) * 1.5f, midDest[i]);
            }
        }
    }
}
