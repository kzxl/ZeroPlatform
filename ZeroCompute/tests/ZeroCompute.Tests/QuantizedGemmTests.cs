using System;
using Xunit;
using ZeroCompute.Core;
using ZeroCompute.Core.DirectX;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    [Collection("D3D11Hardware")]
    public class QuantizedGemmTests
    {
        [Fact]
        public void Cpu_GemmInt8_ComputesCorrectly()
        {
            // A: 2x3, B: 3x2 (INT8)
            var A = new Tensor<float>(2, 3);
            new float[] { 1f, 2f, 3f, 4f, 5f, 6f }.CopyTo(A.AsSpan());

            var B = new Tensor<sbyte>(3, 2);
            new sbyte[] { 10, -5, 2, 4, -1, 3 }.CopyTo(B.AsSpan());

            var scales = new Tensor<float>(2);
            new float[] { 0.5f, 2.0f }.CopyTo(scales.AsSpan());

            var zp = new Tensor<float>(2);
            new float[] { 0f, 0f }.CopyTo(zp.AsSpan());

            var C = Tensor.Zeros<float>(2, 2);

            ComputeDevice.Cpu.GemmInt8(A, B, scales, C, zp);

            // Row 0:
            // col 0: (1*10 + 2*2 + 3*(-1)) * 0.5 = (10 + 4 - 3) * 0.5 = 11 * 0.5 = 5.5
            // col 1: (1*(-5) + 2*4 + 3*3) * 2.0 = (-5 + 8 + 9) * 2.0 = 12 * 2.0 = 24.0
            // Row 1:
            // col 0: (4*10 + 5*2 + 6*(-1)) * 0.5 = (40 + 10 - 6) * 0.5 = 44 * 0.5 = 22.0
            // col 1: (4*(-5) + 5*4 + 6*3) * 2.0 = (-20 + 20 + 18) * 2.0 = 18 * 2.0 = 36.0
            Assert.Equal(5.5f, C[0, 0], precision: 3);
            Assert.Equal(24.0f, C[0, 1], precision: 3);
            Assert.Equal(22.0f, C[1, 0], precision: 3);
            Assert.Equal(36.0f, C[1, 1], precision: 3);
        }

        [Fact]
        public void Gpu_GemmInt8_MatchesCpuResults()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            const int M = 32;
            const int K = 64;
            const int N = 48;

            var rnd = new Random(42);
            var A = new Tensor<float>(M, K);
            for (int i = 0; i < A.Length; i++) A.AsSpan()[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            var B = new Tensor<sbyte>(K, N);
            for (int i = 0; i < B.Length; i++) B.AsSpan()[i] = (sbyte)rnd.Next(-64, 64);

            var scales = new Tensor<float>(N);
            for (int j = 0; j < N; j++) scales.AsSpan()[j] = (float)(rnd.NextDouble() * 0.1 + 0.01);

            var zp = new Tensor<float>(N);
            for (int j = 0; j < N; j++) zp.AsSpan()[j] = (float)rnd.Next(-5, 5);

            var cpuC = Tensor.Zeros<float>(M, N);
            var gpuC = Tensor.Zeros<float>(M, N);

            ComputeDevice.Cpu.GemmInt8(A, B, scales, cpuC, zp);
            ComputeDevice.Gpu.GemmInt8(A, B, scales, gpuC, zp);

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Assert.Equal(cpuC[i, j], gpuC[i, j], precision: 2);
                }
            }
        }

        [Fact]
        public void Cpu_GemmInt4_ComputesCorrectly()
        {
            // A: 1x4, packedWeights: 2x2 (representing 4x2 INT4 weights)
            // packedK = 2, K = 4, N = 2
            var A = new Tensor<float>(1, 4);
            new float[] { 1f, 2f, 3f, 4f }.CopyTo(A.AsSpan());

            var packedW = new Tensor<byte>(2, 2);
            new byte[] { 0x53, 0x04, 0x12, 0x21 }.CopyTo(packedW.AsSpan());

            var scales = new Tensor<float>(2);
            new float[] { 1.0f, 0.5f }.CopyTo(scales.AsSpan());

            var zp = new Tensor<float>(2);
            new float[] { 0.0f, 0.0f }.CopyTo(zp.AsSpan());

            var C = Tensor.Zeros<float>(1, 2);

            ComputeDevice.Cpu.GemmInt4(A, packedW, scales, C, zp);

            // Col 0: (1*3 + 2*5 + 3*2 + 4*1) * 1.0 = (3 + 10 + 6 + 4) = 23.0
            // Col 1: (1*4 + 2*0 + 3*1 + 4*2) * 0.5 = (4 + 0 + 3 + 8) * 0.5 = 15.0 * 0.5 = 7.5
            Assert.Equal(23.0f, C[0, 0], precision: 3);
            Assert.Equal(7.5f, C[0, 1], precision: 3);
        }

        [Fact]
        public void Gpu_GemmInt4_MatchesCpuResults()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            const int M = 32;
            const int K = 64;
            const int packedK = K / 2; // 32
            const int N = 48;

            var rnd = new Random(123);
            var A = new Tensor<float>(M, K);
            for (int i = 0; i < A.Length; i++) A.AsSpan()[i] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            var packedW = new Tensor<byte>(packedK, N);
            for (int i = 0; i < packedW.Length; i++) packedW.AsSpan()[i] = (byte)rnd.Next(0, 256);

            var scales = new Tensor<float>(N);
            for (int j = 0; j < N; j++) scales.AsSpan()[j] = (float)(rnd.NextDouble() * 0.05 + 0.01);

            var zp = new Tensor<float>(N);
            for (int j = 0; j < N; j++) zp.AsSpan()[j] = 8.0f; // typical INT4 symmetric offset

            var cpuC = Tensor.Zeros<float>(M, N);
            var gpuC = Tensor.Zeros<float>(M, N);

            ComputeDevice.Cpu.GemmInt4(A, packedW, scales, cpuC, zp);
            ComputeDevice.Gpu.GemmInt4(A, packedW, scales, gpuC, zp);

            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Assert.Equal(cpuC[i, j], gpuC[i, j], precision: 2);
                }
            }
        }

        [Fact]
        public void Gpu_GemmInt4_WithDeviceOutputTensor_KeepsDataInVram()
        {
            if (!ComputeDevice.IsGpuAvailable) return;

            const int M = 16;
            const int K = 32;
            const int packedK = K / 2;
            const int N = 16;

            var A = Tensor.Ones<float>(M, K);
            var packedW = new Tensor<byte>(packedK, N);
            packedW.Fill(0x22); // nibble0 = 2, nibble1 = 2
            var scales = Tensor.Ones<float>(N);

            var ctx = ComputeDevice.Gpu.D3D11Context!;
            using var devC = ctx.AllocateDeviceTensor<float>(new TensorShape(M, N), pooled: true);

            ctx.GemmInt4(A, packedW, scales, devC);

            // Verify result by downloading
            var hostC = Tensor.Zeros<float>(M, N);
            ((D3D11TensorStorage<float>)devC.Storage).Buffer.Download(hostC.AsSpan());

            // Each row of A has 32 ones. Each weight is 2. Acc = 32 * 2 = 64
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    Assert.Equal(64.0f, hostC[i, j], precision: 3);
                }
            }
        }
    }
}
