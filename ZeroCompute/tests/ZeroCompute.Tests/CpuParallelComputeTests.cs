using System;
using System.Threading;
using Xunit;
using ZeroCompute.Core.Cpu;
using ZeroTensor.Core;

namespace ZeroCompute.Tests
{
    public class CpuParallelComputeTests
    {
        [Fact]
        public void Compute_For_SequentialFallback_SmallN_ComputesCorrectly()
        {
            const int N = 100;
            int[] data = new int[N];

            Compute.For(N, i =>
            {
                data[i] = i * 2;
            });

            for (int i = 0; i < N; i++)
            {
                Assert.Equal(i * 2, data[i]);
            }
        }

        [Fact]
        public void Compute_For_LargeN_ParallelExecution_VisitsAllIndicesOnce()
        {
            const int N = 100_000;
            int[] flags = new int[N];

            Compute.For(N, i =>
            {
                Interlocked.Increment(ref flags[i]);
            });

            for (int i = 0; i < N; i++)
            {
                Assert.Equal(1, flags[i]);
            }
        }

        [Fact]
        public void Compute_For_RangeBased_ComputesCorrectChunks()
        {
            const int N = 50_000;
            float[] data = new float[N];

            Compute.For(N, (start, end) =>
            {
                for (int i = start; i < end; i++)
                {
                    data[i] = i + 1.0f;
                }
            }, CpuWorkloadType.ComputeBound);

            for (int i = 0; i < N; i++)
            {
                Assert.Equal(i + 1.0f, data[i]);
            }
        }

        [Fact]
        public void Compute_Tile2D_CacheTiling_CoversFullGrid()
        {
            const int W = 150;
            const int H = 230;
            int[,] grid = new int[H, W];

            Compute.Tile2D(W, H, tileW: 32, tileH: 32, (x0, y0, x1, y1) =>
            {
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                    {
                        grid[y, x]++;
                    }
                }
            });

            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    Assert.Equal(1, grid[y, x]);
                }
            }
        }

        [Fact]
        public void Compute_FusedMap_SinglePassTransformation_ProducesExactResults()
        {
            const int N = 10_000;
            float[] src = new float[N];
            float[] dst = new float[N];

            for (int i = 0; i < N; i++)
            {
                src[i] = i - 5000;
            }

            // Fused: x * 2 + 10, then ReLU
            Compute.FusedMap(src, dst, x =>
            {
                float val = x * 2.0f + 10.0f;
                return val > 0f ? val : 0f;
            });

            for (int i = 0; i < N; i++)
            {
                float expected = (src[i] * 2.0f + 10.0f);
                if (expected < 0f) expected = 0f;
                Assert.Equal(expected, dst[i]);
            }
        }

        [Fact]
        public void Compute_Reduce_SumMinMax_AccurateAcrossMultipleCores()
        {
            const int N = 65_536; // 64K
            float[] data = new float[N];
            float expectedSum = 0f;

            for (int i = 0; i < N; i++)
            {
                data[i] = (i % 100) + 1.0f;
                expectedSum += data[i];
            }

            data[1234] = 9999.0f;
            expectedSum += (9999.0f - ((1234 % 100) + 1.0f));

            data[5678] = -1234.0f;
            expectedSum += (-1234.0f - ((5678 % 100) + 1.0f));

            float actualSum = Compute.ReduceSum(data);
            float actualMax = Compute.ReduceMax(data);
            float actualMin = Compute.ReduceMin(data);

            Assert.True(Math.Abs(expectedSum - actualSum) < 1.0f, $"Sum mismatch: expected {expectedSum}, got {actualSum}");
            Assert.Equal(9999.0f, actualMax);
            Assert.Equal(-1234.0f, actualMin);
        }

        [Fact]
        public void Compute_Vector_AddMultiplyScaleFma_ProducesAccurateResults()
        {
            const int N = 1027; // Odd size testing SIMD unrolling and scalar tail
            float[] a = new float[N];
            float[] b = new float[N];
            float[] c = new float[N];
            float[] addRes = new float[N];
            float[] mulRes = new float[N];
            float[] scaleRes = new float[N];
            float[] fmaRes = new float[N];

            for (int i = 0; i < N; i++)
            {
                a[i] = i * 0.5f;
                b[i] = (i + 1) * 2.0f;
                c[i] = 7.5f;
            }

            Compute.Vector.Add(a, b, addRes);
            Compute.Vector.Multiply(a, b, mulRes);
            Compute.Vector.Scale(a, 3.0f, scaleRes);
            Compute.Vector.Fma(a, b, c, fmaRes);

            for (int i = 0; i < N; i++)
            {
                Assert.Equal(a[i] + b[i], addRes[i], 3);
                Assert.Equal(a[i] * b[i], mulRes[i], 3);
                Assert.Equal(a[i] * 3.0f, scaleRes[i], 3);
                Assert.Equal((a[i] * b[i]) + c[i], fmaRes[i], 3);
            }
        }

        [Fact]
        public void Compute_WorkerPool_RepeatedDispatches_DoNotDeadlock()
        {
            // Rapid-fire 100 parallel dispatches to stress test synchronization barrier
            for (int iter = 0; iter < 100; iter++)
            {
                int count = 10_000 + iter;
                int sum = 0;
                Compute.For(count, (start, end) =>
                {
                    int local = 0;
                    for (int i = start; i < end; i++) local++;
                    Interlocked.Add(ref sum, local);
                });

                Assert.Equal(count, sum);
            }
        }

        [Fact]
        public void Compute_Vector_Gelu_MatchesDoublePrecisionExactness()
        {
            const int N = 2048;
            float[] input = new float[N];
            float[] output = new float[N];

            const float sqrt2OverPi = 0.79788456f;
            const float coeff = 0.044715f;

            for (int i = 0; i < N; i++)
            {
                input[i] = (i - 1024) * 0.005f; // [-5.12 to +5.12]
            }

            Compute.Vector.Gelu(input, output);

            for (int i = 0; i < N; i++)
            {
                float x = input[i];
                float inner = sqrt2OverPi * (x + coeff * x * x * x);
                float expected = 0.5f * x * (1.0f + (float)Math.Tanh(inner));

                Assert.True(Math.Abs(expected - output[i]) < 2e-4f,
                    $"GELU mismatch at index {i} (x={x}): expected {expected}, got {output[i]}");
            }
        }
    }
}
