using System;
using Xunit;
using ZeroCompute.Core;
using ZeroCompute.Core.Blas;
using ZeroCompute.Core.Context;
using ZeroCompute.Core.Cpu;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    [Collection("D3D11Hardware")]
    public class ExtendedArchitecturalTests
    {
        [Fact]
        public void ComputeDevice_GetBestDevice_ReturnsValidDevice()
        {
            var bestDevice = ComputeDevice.GetBestDevice();
            Assert.NotNull(bestDevice);

            // Best device should be either D3D11 (if hardware available) or CPU fallback
            if (ComputeDevice.IsGpuAvailable)
            {
                Assert.NotNull(ComputeDevice.Gpu);
                Assert.Equal(ComputeBackend.Direct3D11, bestDevice.Backend);
            }
            else
            {
                Assert.Equal(ComputeBackend.CpuParallel, bestDevice.Backend);
            }
        }

        [Fact]
        public void ComputeDevice_FunctionalGemm_AllocatesAndComputes()
        {
            var A = Tensor.FromArray(new float[]
            {
                1, 2,
                3, 4
            }, 2, 2);

            var B = Tensor.FromArray(new float[]
            {
                5, 6,
                7, 8
            }, 2, 2);

            var C = ComputeDevice.GetBestDevice().Gemm(A, B);

            Assert.NotNull(C);
            Assert.Equal(2, C.Shape[0]);
            Assert.Equal(2, C.Shape[1]);

            // C[0,0] = 1*5 + 2*7 = 19
            // C[0,1] = 1*6 + 2*8 = 22
            // C[1,0] = 3*5 + 4*7 = 43
            // C[1,1] = 3*6 + 4*8 = 50
            Assert.Equal(19f, C[0, 0]);
            Assert.Equal(22f, C[0, 1]);
            Assert.Equal(43f, C[1, 0]);
            Assert.Equal(50f, C[1, 1]);
        }

        [Fact]
        public void BlasEngine_SpanGemm_MatchesTensorOutput()
        {
            int M = 4, K = 8, N = 6;
            float[] aData = new float[M * K];
            float[] bData = new float[K * N];
            float[] cData = new float[M * N];

            for (int i = 0; i < aData.Length; i++) aData[i] = (i + 1) * 0.5f;
            for (int i = 0; i < bData.Length; i++) bData[i] = (i % 5) - 2.0f;

            var A = Tensor.FromArray(aData, M, K);
            var B = Tensor.FromArray(bData, K, N);
            var CExpected = Tensor.Zeros<float>(M, N);

            BlasEngine.Gemm(A, B, CExpected);
            BlasEngine.Gemm(aData.AsSpan(), bData.AsSpan(), cData.AsSpan(), M, K, N);

            for (int r = 0; r < M; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    Assert.Equal(CExpected[r, c], cData[r * N + c], precision: 5);
                }
            }
        }

        [Fact]
        public void BlasEngine_SpanAddAndMultiply_ComputeCorrectly()
        {
            int size = 128;
            float[] a = new float[size];
            float[] b = new float[size];
            float[] sum = new float[size];
            float[] prod = new float[size];

            for (int i = 0; i < size; i++)
            {
                a[i] = i * 2.0f;
                b[i] = 10.0f;
            }

            BlasEngine.Add(a.AsSpan(), b.AsSpan(), sum.AsSpan());
            BlasEngine.Multiply(a.AsSpan(), b.AsSpan(), prod.AsSpan());

            for (int i = 0; i < size; i++)
            {
                Assert.Equal(a[i] + b[i], sum[i]);
                Assert.Equal(a[i] * b[i], prod[i]);
            }
        }

        [Fact]
        public void BlasEngine_GeluActivation_HighPrecisionRationalApproximation()
        {
            // Compare SIMD GELU against textbook standard formula:
            // 0.5 * x * (1 + tanh(sqrt(2/pi) * (x + 0.044715 * x^3)))
            float[] inputs = { -3.0f, -2.0f, -1.0f, -0.5f, 0.0f, 0.5f, 1.0f, 2.0f, 3.0f };
            float[] outputs = new float[inputs.Length];

            BlasEngine.Activation(inputs.AsSpan(), outputs.AsSpan(), ComputeActivationType.GELU);

            float sqrt2OverPi = (float)Math.Sqrt(2.0 / Math.PI);

            for (int i = 0; i < inputs.Length; i++)
            {
                float x = inputs[i];
                float inner = sqrt2OverPi * (x + 0.044715f * x * x * x);
                float expectedGelu = 0.5f * x * (1.0f + (float)Math.Tanh(inner));

                // Assert approximation error is small (< 0.005)
                Assert.True(Math.Abs(expectedGelu - outputs[i]) < 0.005f,
                    $"GELU mismatch at x={x}: Expected {expectedGelu}, Actual {outputs[i]}");
            }
        }

        private readonly struct LinearOp : IElementwiseOp<float>
        {
            private readonly float _slope;
            private readonly float _intercept;

            public LinearOp(float slope, float intercept)
            {
                _slope = slope;
                _intercept = intercept;
            }

            public float Apply(float x) => x * _slope + _intercept;
        }

        [Fact]
        public void Compute_FusedMap_ExecutesCustomStructOp()
        {
            float[] src = { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
            float[] dst = new float[src.Length];

            var op = new LinearOp(3.0f, 7.0f);
            Compute.FusedMap(src.AsSpan(), dst.AsSpan(), op);

            for (int i = 0; i < src.Length; i++)
            {
                Assert.Equal(src[i] * 3.0f + 7.0f, dst[i]);
            }
        }

        [Fact]
        public void NumaTopology_AllocateBuffer_SafeSpanLifecycle()
        {
            int floatCount = 1024;
            int byteSize = floatCount * sizeof(float);

            using (var numaBuffer = NumaTopology.AllocateBuffer(byteSize))
            {
                Assert.False(numaBuffer.IsDisposed);
                Assert.Equal(byteSize, numaBuffer.ByteSize);
                Assert.NotEqual(IntPtr.Zero, numaBuffer.Pointer);

                Span<float> span = numaBuffer.AsSpan<float>();
                Assert.Equal(floatCount, span.Length);

                for (int i = 0; i < span.Length; i++)
                {
                    span[i] = i * 1.5f;
                }

                for (int i = 0; i < span.Length; i++)
                {
                    Assert.Equal(i * 1.5f, span[i]);
                }
            }
        }

        [Fact]
        public void ComputeWorkerPool_NestedReentrancy_DoesNotDeadlock()
        {
            // Verify that calling DispatchRange inside another DispatchRange does not deadlock
            // due to threadpool starvation or recursive worker reservation.
            int outerCount = 16;
            int innerCount = 32;
            int[] results = new int[outerCount * innerCount];

            ComputeWorkerPool.Shared.DispatchRange(outerCount, 4, (startOuter, countOuter) =>
            {
                for (int i = startOuter; i < startOuter + countOuter; i++)
                {
                    int capturedI = i;
                    ComputeWorkerPool.Shared.DispatchRange(innerCount, 4, (startInner, countInner) =>
                    {
                        for (int j = startInner; j < startInner + countInner; j++)
                        {
                            results[capturedI * innerCount + j] = capturedI * 1000 + j;
                        }
                    });
                }
            });

            for (int i = 0; i < outerCount; i++)
            {
                for (int j = 0; j < innerCount; j++)
                {
                    Assert.Equal(i * 1000 + j, results[i * innerCount + j]);
                }
            }
        }
    }
}
