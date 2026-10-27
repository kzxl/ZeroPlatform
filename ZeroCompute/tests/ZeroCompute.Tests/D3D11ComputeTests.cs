using System;
using Xunit;
using ZeroCompute.Core;
using ZeroCompute.Core.Context;
using ZeroCompute.Core.DirectX;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    [Collection("D3D11Hardware")]
    public class D3D11ComputeTests
    {
        [Fact]
        public void TestD3D11ComputeContext_LifecycleAndFallback()
        {
            using (var ctx = new D3D11ComputeContext())
            {
                Assert.Equal(ComputeBackend.Direct3D11, ctx.Backend);

                // Gemm check
                var A = Tensor.FromArray(new float[] { 1, 2, 3, 4 }, 2, 2);
                var B = Tensor.FromArray(new float[] { 5, 6, 7, 8 }, 2, 2);
                var C = Tensor.FromArray(new float[4], 2, 2);

                ctx.Gemm(A, B, C);
                Assert.Equal(19.0f, C[0, 0]);
                Assert.Equal(22.0f, C[0, 1]);
                Assert.Equal(43.0f, C[1, 0]);
                Assert.Equal(50.0f, C[1, 1]);

                // Activation check
                var input = Tensor.FromArray(new float[] { -2.0f, 0.0f, 3.5f }, 3);
                var output = Tensor.FromArray(new float[3], 3);
                ctx.Activation(input, output, ComputeActivationType.ReLU);
                Assert.Equal(0.0f, output[0]);
                Assert.Equal(0.0f, output[1]);
                Assert.Equal(3.5f, output[2]);
            }
        }

        [Fact]
        public void TestComputeDevice_GpuFactory()
        {
            using (var device = ComputeDevice.Create(ComputeBackend.Direct3D11))
            {
                Assert.Equal(ComputeBackend.Direct3D11, device.Backend);

                var A = Tensor.FromArray(new float[] { 2, 3 }, 2);
                var B = Tensor.FromArray(new float[] { 4, 5 }, 2);
                var C = Tensor.FromArray(new float[2], 2);

                device.Add(A, B, C);
                Assert.Equal(6.0f, C[0]);
                Assert.Equal(8.0f, C[1]);

                device.Multiply(A, B, C);
                Assert.Equal(8.0f, C[0]);
                Assert.Equal(15.0f, C[1]);
            }
        }

        [Fact]
        public void TestStructuredBuffer_UploadDownload()
        {
            using (var ctx = new D3D11ComputeContext())
            {
                if (ctx.IsHardwareAccelerated)
                {
                    float[] srcData = new float[] { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f };
                    using (var buffer = ctx.CreateStructuredBuffer<float>(srcData.Length, allowUav: true, cpuRead: true))
                    {
                        buffer.Upload(srcData);

                        float[] readback = new float[srcData.Length];
                        buffer.Download(readback);

                        for (int i = 0; i < srcData.Length; i++)
                        {
                            Assert.Equal(srcData[i], readback[i]);
                        }
                    }
                }
            }
        }
    }
}
