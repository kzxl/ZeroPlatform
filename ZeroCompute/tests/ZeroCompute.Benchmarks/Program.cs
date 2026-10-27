using System;
using System.Diagnostics;
using System.Threading.Tasks;
using ZeroCompute.Core.Cpu;

namespace ZeroCompute.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("          ZEROCOMPUTE - CPU PARALLEL COMPUTE RUNTIME BENCHMARK SUITE             ");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"Logical Processors : {Environment.ProcessorCount}");
            Console.WriteLine($"64-bit Process     : {Environment.Is64BitProcess}");
            Console.WriteLine($"OS Version         : {Environment.OSVersion}");
            Console.WriteLine($"SIMD Accelerated   : {ComputeVectorOps.IsHardwareAccelerated}");
            Console.WriteLine("================================================================================\n");

            // Warm up runtime and thread pool
            Warmup();

            RunBenchmark1_MemoryBoundArrayAdd();
            RunBenchmark2_ComputeBoundTranscendental();
            RunBenchmark3_CacheTiling2D();
            RunBenchmark4_KernelFusion();
            RunBenchmark5_ReductionSum();
            RunBenchmark6_DispatchLatencyMicro();
            RunBenchmark7_GemmMatrixMultiply();
            RunBenchmark8_GeluActivation();
            RunBenchmark9_RmsNorm();
            RunBenchmark10_GpuResidencyChained();
            RunBenchmark11_FusedGemm();
            RunBenchmark12_FlashAttention();
            RunBenchmark13_BufferPoolRecycling();
            RunBenchmark14_QuantizedGemmInt4();

            Console.WriteLine("\n[DONE] Benchmark suite finished successfully.");
        }

        private static void Warmup()
        {
            float[] a = new float[1000];
            float[] b = new float[1000];
            float[] c = new float[1000];
            Compute.Vector.Add(a, b, c);
            Compute.For(1000, i => c[i] = a[i] + b[i]);
        }

        #region Benchmark 1: Memory-Bound Array Add

        private static void RunBenchmark1_MemoryBoundArrayAdd()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 1: Array Add (Memory-Bound Workload: Low Arithmetic Intensity)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            int[] sizes = { 1_000, 100_000, 10_000_000 };
            string[] labels = { "Small (1K)", "Medium (100K)", "Huge (10M)" };

            for (int s = 0; s < sizes.Length; s++)
            {
                int n = sizes[s];
                float[] a = new float[n];
                float[] b = new float[n];
                float[] resSeq = new float[n];
                float[] resPar = new float[n];
                float[] resVec = new float[n];

                for (int i = 0; i < n; i++)
                {
                    a[i] = i * 0.1f;
                    b[i] = (i + 1) * 0.2f;
                }

                int iters = (n <= 100_000) ? 100 : 10;

                // 1. Sequential
                var sw = Stopwatch.StartNew();
                for (int iter = 0; iter < iters; iter++)
                {
                    for (int i = 0; i < n; i++) resSeq[i] = a[i] + b[i];
                }
                sw.Stop();
                double tSeq = sw.Elapsed.TotalMilliseconds / iters;

                // 2. Parallel.For
                sw.Restart();
                for (int iter = 0; iter < iters; iter++)
                {
                    Parallel.For(0, n, i => resPar[i] = a[i] + b[i]);
                }
                sw.Stop();
                double tPar = sw.Elapsed.TotalMilliseconds / iters;

                // 3. Compute.Vector.Add (Thread + SIMD unrolling + memory-aware worker cap)
                sw.Restart();
                for (int iter = 0; iter < iters; iter++)
                {
                    Compute.Vector.Add(a, b, resVec);
                }
                sw.Stop();
                double tVec = sw.Elapsed.TotalMilliseconds / iters;

                Console.WriteLine($"[{labels[s]} - N = {n:N0}]");
                Console.WriteLine($"  * Sequential         : {tSeq,8:F3} ms (1.00x)");
                Console.WriteLine($"  * Parallel.For       : {tPar,8:F3} ms ({(tSeq / tPar),5:F2}x)");
                Console.WriteLine($"  * Compute.Vector.Add : {tVec,8:F3} ms ({(tSeq / tVec),5:F2}x speedup vs Seq, {(tPar / tVec),5:F2}x vs Par.For)");
            }
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 2: Compute-Bound Transcendental Math (GELU)

        private static void RunBenchmark2_ComputeBoundTranscendental()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 2: GELU Activation (Compute-Bound: High FLOP/Byte Intensity)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int N = 2_000_000;
            float[] input = new float[N];
            float[] outputSeq = new float[N];
            float[] outputPar = new float[N];
            float[] outputCompute = new float[N];

            for (int i = 0; i < N; i++) input[i] = (i % 100) * 0.05f - 2.5f;

            const int iters = 5;
            const float sqrt2OverPi = 0.79788456f;

            // 1. Sequential
            var sw = Stopwatch.StartNew();
            for (int it = 0; it < iters; it++)
            {
                for (int i = 0; i < N; i++)
                {
                    float x = input[i];
                    float inner = sqrt2OverPi * (x + 0.044715f * x * x * x);
                    outputSeq[i] = 0.5f * x * (1.0f + (float)Math.Tanh(inner));
                }
            }
            sw.Stop();
            double tSeq = sw.Elapsed.TotalMilliseconds / iters;

            // 2. Parallel.For
            sw.Restart();
            for (int it = 0; it < iters; it++)
            {
                Parallel.For(0, N, i =>
                {
                    float x = input[i];
                    float inner = sqrt2OverPi * (x + 0.044715f * x * x * x);
                    outputPar[i] = 0.5f * x * (1.0f + (float)Math.Tanh(inner));
                });
            }
            sw.Stop();
            double tPar = sw.Elapsed.TotalMilliseconds / iters;

            // 3. Compute.For (ComputeBound worker pool with SMT-mitigated physical cores)
            sw.Restart();
            for (int it = 0; it < iters; it++)
            {
                Compute.For(N, (start, end) =>
                {
                    for (int i = start; i < end; i++)
                    {
                        float x = input[i];
                        float inner = sqrt2OverPi * (x + 0.044715f * x * x * x);
                        outputCompute[i] = 0.5f * x * (1.0f + (float)Math.Tanh(inner));
                    }
                }, CpuWorkloadType.ComputeBound);
            }
            sw.Stop();
            double tCompute = sw.Elapsed.TotalMilliseconds / iters;

            // 4. Compute.Vector.Gelu (AVX2 SIMD Vectorized Rational Approximation)
            float[] outputGelu = new float[N];
            sw.Restart();
            for (int it = 0; it < iters; it++)
            {
                Compute.Vector.Gelu(input, outputGelu);
            }
            sw.Stop();
            double tGelu = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[N = {N:N0} elements - 5 runs averaged]");
            Console.WriteLine($"  * Sequential (Scalar) : {tSeq,8:F2} ms (1.00x)");
            Console.WriteLine($"  * Parallel.For (.NET) : {tPar,8:F2} ms ({(tSeq / tPar),5:F2}x)");
            Console.WriteLine($"  * Compute.For (Scalar): {tCompute,8:F2} ms ({(tSeq / tCompute),5:F2}x)");
            Console.WriteLine($"  * Compute.Vector.Gelu : {tGelu,8:F2} ms ({(tSeq / tGelu),5:F2}x speedup vs Seq, {(tPar / tGelu),5:F2}x vs Par.For!)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 3: Matrix Multiplication (Cache Tiling vs Naive)

        private static void RunBenchmark3_CacheTiling2D()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 3: Matrix Multiplication (Cache Tiling vs Naive Stride-N Scanning)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int M = 512;
            const int K = 512;
            const int N = 512;

            var tA = ZeroTensor.Core.Tensor.Zeros<float>(M, K);
            var tB = ZeroTensor.Core.Tensor.Zeros<float>(K, N);
            var tC_Naive = ZeroTensor.Core.Tensor.Zeros<float>(M, N);
            var tC_Tiled = ZeroTensor.Core.Tensor.Zeros<float>(M, N);

            for (int i = 0; i < M; i++)
                for (int k = 0; k < K; k++)
                    tA[i, k] = (i + k) * 0.01f;

            for (int k = 0; k < K; k++)
                for (int j = 0; j < N; j++)
                    tB[k, j] = (k + j) * 0.01f;

            // 1. Naive row-by-row Parallel.For without cache tiling
            var sw = Stopwatch.StartNew();
            Parallel.For(0, M, i =>
            {
                for (int k = 0; k < K; k++)
                {
                    float aVal = tA[i, k];
                    for (int j = 0; j < N; j++)
                    {
                        tC_Naive[i, j] += aVal * tB[k, j];
                    }
                }
            });
            sw.Stop();
            double tNaive = sw.Elapsed.TotalMilliseconds;

            // 2. Cache-Tiled GEMM using ZeroCompute BlasEngine (64x64 tiles + Compute.For + unrolling)
            sw.Restart();
            ZeroCompute.Core.Blas.BlasEngine.Gemm(tA, tB, tC_Tiled);
            sw.Stop();
            double tTiled = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"[Matrix Size: {M} x {K} x {N} ({M * N * K:N0} operations)]");
            Console.WriteLine($"  * Naive Parallel.For : {tNaive,8:F2} ms (1.00x)");
            Console.WriteLine($"  * Cache-Tiled GEMM   : {tTiled,8:F2} ms ({(tNaive / tTiled),5:F2}x speedup via L1/L2 Cache Tiling)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 4: Kernel Fusion

        private static void RunBenchmark4_KernelFusion()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 4: Kernel Fusion (Single-pass L1 Cache vs 3-pass Memory Access)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int N = 2_000_000;
            float[] src = new float[N];
            float[] temp1 = new float[N];
            float[] temp2 = new float[N];
            float[] dstMulti = new float[N];
            float[] dstFused = new float[N];

            for (int i = 0; i < N; i++) src[i] = i * 0.05f - 50_000f;

            const int iters = 10;

            // 1. Multi-pass (3 round-trips to DRAM)
            var sw = Stopwatch.StartNew();
            for (int it = 0; it < iters; it++)
            {
                // Pass 1: Multiply 2.0
                Parallel.For(0, N, i => temp1[i] = src[i] * 2.0f);
                // Pass 2: Add 10.0
                Parallel.For(0, N, i => temp2[i] = temp1[i] + 10.0f);
                // Pass 3: ReLU
                Parallel.For(0, N, i => dstMulti[i] = temp2[i] > 0f ? temp2[i] : 0f);
            }
            sw.Stop();
            double tMulti = sw.Elapsed.TotalMilliseconds / iters;

            // 2. Fused (1 round-trip to DRAM, intermediate in L1/Registers)
            sw.Restart();
            for (int it = 0; it < iters; it++)
            {
                Compute.FusedMap(src, dstFused, x =>
                {
                    float v = x * 2.0f + 10.0f;
                    return v > 0f ? v : 0f;
                });
            }
            sw.Stop();
            double tFused = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[N = {N:N0} elements - 10 runs averaged]");
            Console.WriteLine($"  * Multi-Pass (3x DRAM) : {tMulti,8:F2} ms (1.00x)");
            Console.WriteLine($"  * Compute.FusedMap     : {tFused,8:F2} ms ({(tMulti / tFused),5:F2}x speedup - 66% DRAM bandwidth saved)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 5: Parallel Reduction

        private static void RunBenchmark5_ReductionSum()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 5: Parallel Reduction (Sum across 10 Million elements)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int N = 10_000_000;
            float[] data = new float[N];
            for (int i = 0; i < N; i++) data[i] = (i % 100) * 0.1f;

            const int iters = 10;

            // 1. Scalar loop
            var sw = Stopwatch.StartNew();
            float sumSeq = 0f;
            for (int it = 0; it < iters; it++)
            {
                sumSeq = 0f;
                for (int i = 0; i < N; i++) sumSeq += data[i];
            }
            sw.Stop();
            double tSeq = sw.Elapsed.TotalMilliseconds / iters;

            // 2. Compute.ReduceSum (Multi-core + 4-way SIMD accumulator unrolling)
            sw.Restart();
            float sumCompute = 0f;
            for (int it = 0; it < iters; it++)
            {
                sumCompute = Compute.ReduceSum(data);
            }
            sw.Stop();
            double tCompute = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[N = {N:N0} elements - 10 runs averaged]");
            Console.WriteLine($"  * Scalar Loop      : {tSeq,8:F2} ms (1.00x)");
            Console.WriteLine($"  * Compute.ReduceSum: {tCompute,8:F2} ms ({(tSeq / tCompute),5:F2}x speedup)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 6: Dispatch Latency Micro-benchmark

        private static void RunBenchmark6_DispatchLatencyMicro()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 6: Dispatch Overhead (50,000 rapid small-N loops, N = 100)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int N = 100;
            const int iters = 50_000;
            int[] data = new int[N];

            // 1. Parallel.For
            var sw = Stopwatch.StartNew();
            for (int it = 0; it < iters; it++)
            {
                Parallel.For(0, N, i => { data[i] = i; });
            }
            sw.Stop();
            double tPar = sw.Elapsed.TotalMilliseconds;

            // 2. Compute.For (Deterministic sequential cutoff avoids thread dispatch)
            sw.Restart();
            for (int it = 0; it < iters; it++)
            {
                Compute.For(N, i => { data[i] = i; });
            }
            sw.Stop();
            double tCompute = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"[50,000 iterations of N=100 loop]");
            Console.WriteLine($"  * Parallel.For : {tPar,8:F1} ms total ({tPar / iters * 1000,6:F2} µs/loop)");
            Console.WriteLine($"  * Compute.For  : {tCompute,8:F1} ms total ({tCompute / iters * 1000,6:F2} µs/loop, {(tPar / tCompute),5:F1}x faster via Planner cutoff)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 7: GEMM Matrix Multiplication

        private static void RunBenchmark7_GemmMatrixMultiply()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 7: GEMM Matrix Multiplication (512 x 512 Matrices, Float32)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int M = 512, K = 512, N = 512;
            float[] a = new float[M * K];
            float[] b = new float[K * N];
            float[] cScalar = new float[M * N];
            float[] cBlas = new float[M * N];

            var rnd = new Random(42);
            for (int i = 0; i < a.Length; i++) a[i] = (float)rnd.NextDouble();
            for (int i = 0; i < b.Length; i++) b[i] = (float)rnd.NextDouble();

            // 1. Naive 3-loop scalar GEMM
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < M; i++)
            {
                int iK = i * K;
                int iN = i * N;
                for (int k = 0; k < K; k++)
                {
                    float aVal = a[iK + k];
                    int kN = k * N;
                    for (int j = 0; j < N; j++)
                    {
                        cScalar[iN + j] += aVal * b[kN + j];
                    }
                }
            }
            sw.Stop();
            double tScalar = sw.Elapsed.TotalMilliseconds;

            // 2. BlasEngine Cache-Blocked + Vector<T> SIMD GEMM
            sw.Restart();
            ZeroCompute.Core.Blas.BlasEngine.Gemm(a.AsSpan(), b.AsSpan(), cBlas.AsSpan(), M, K, N);
            sw.Stop();
            double tBlas = sw.Elapsed.TotalMilliseconds;

            double gflops = (2.0 * M * K * N) / (tBlas * 1e6);

            Console.WriteLine($"[Matrix 512x512 - Single Run]");
            Console.WriteLine($"  * Naive 3-Loop Scalar : {tScalar,8:F2} ms (1.00x)");
            Console.WriteLine($"  * BlasEngine SIMD GEMM: {tBlas,8:F2} ms ({(tScalar / tBlas),5:F2}x speedup | {gflops:F2} GFLOPs)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 8: GELU Activation

        private static void RunBenchmark8_GeluActivation()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 8: GELU Activation Function (1,000,000 Elements)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int N = 1_000_000;
            float[] input = new float[N];
            float[] outTextbook = new float[N];
            float[] outSimd = new float[N];

            var rnd = new Random(1337);
            for (int i = 0; i < N; i++) input[i] = (float)(rnd.NextDouble() * 8.0 - 4.0);

            const int iters = 20;

            // 1. Scalar Math.Tanh textbook
            float sqrt2OverPi = (float)Math.Sqrt(2.0 / Math.PI);
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < iters; iter++)
            {
                for (int i = 0; i < N; i++)
                {
                    float x = input[i];
                    float inner = sqrt2OverPi * (x + 0.044715f * x * x * x);
                    outTextbook[i] = 0.5f * x * (1.0f + (float)Math.Tanh(inner));
                }
            }
            sw.Stop();
            double tScalar = sw.Elapsed.TotalMilliseconds / iters;

            // 2. BlasEngine SIMD Padé rational GELU
            sw.Restart();
            for (int iter = 0; iter < iters; iter++)
            {
                ZeroCompute.Core.Blas.BlasEngine.Activation(input.AsSpan(), outSimd.AsSpan(), ZeroCompute.Core.Context.ComputeActivationType.GELU);
            }
            sw.Stop();
            double tSimd = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[1,000,000 elements - 20 runs averaged]");
            Console.WriteLine($"  * Textbook Math.Tanh : {tScalar,8:F2} ms (1.00x)");
            Console.WriteLine($"  * SIMD Padé Rational : {tSimd,8:F2} ms ({(tScalar / tSimd),5:F2}x speedup)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 9: RMSNorm (LLM Normalization)

        private static void RunBenchmark9_RmsNorm()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 9: RMSNorm Normalization (512 Tokens x 4096 Hidden Dim, Float32)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int tokens = 512, hiddenDim = 4096;
            var input = ZeroTensor.Core.Tensor.Zeros<float>(tokens, hiddenDim);
            var weight = ZeroTensor.Core.Tensor.Ones(hiddenDim);
            var outScalar = ZeroTensor.Core.Tensor.Zeros<float>(tokens, hiddenDim);
            var outSimd = ZeroTensor.Core.Tensor.Zeros<float>(tokens, hiddenDim);

            var rnd = new Random(42);
            for (int r = 0; r < tokens; r++)
                for (int c = 0; c < hiddenDim; c++)
                    input[r, c] = (float)(rnd.NextDouble() * 2.0 - 1.0);

            const int iters = 10;

            // 1. Scalar textbook RMSNorm
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < iters; iter++)
            {
                for (int r = 0; r < tokens; r++)
                {
                    float sumSq = 0f;
                    for (int c = 0; c < hiddenDim; c++)
                    {
                        float val = input[r, c];
                        sumSq += val * val;
                    }
                    float invRms = 1.0f / (float)Math.Sqrt((sumSq / hiddenDim) + 1e-5f);
                    for (int c = 0; c < hiddenDim; c++)
                    {
                        outScalar[r, c] = input[r, c] * invRms * weight[c];
                    }
                }
            }
            sw.Stop();
            double tScalar = sw.Elapsed.TotalMilliseconds / iters;

            // 2. BlasEngine SIMD + ThreadPool RMSNorm
            sw.Restart();
            for (int iter = 0; iter < iters; iter++)
            {
                ZeroCompute.Core.Blas.BlasEngine.RmsNorm(input, outSimd, weight, 1e-5f);
            }
            sw.Stop();
            double tSimd = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[512 x 4096 tokens - 10 runs averaged]");
            Console.WriteLine($"  * Textbook Scalar RMSNorm: {tScalar,8:F2} ms (1.00x)");
            Console.WriteLine($"  * BlasEngine SIMD RMSNorm: {tSimd,8:F2} ms ({(tScalar / tSimd),5:F2}x speedup)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 10: GPU VRAM Residency vs Host Roundtrips

        private static void RunBenchmark10_GpuResidencyChained()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 10: GPU VRAM Persistent Residency vs Host Roundtrips (GEMM + GELU + RMSNorm)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            if (!ZeroCompute.Core.ComputeDevice.IsGpuAvailable)
            {
                Console.WriteLine("  [SKIP] Direct3D 11 GPU not available in this environment.");
                Console.WriteLine();
                return;
            }

            var gpu = ZeroCompute.Core.ComputeDevice.Gpu;
            const int M = 256, K = 512, N = 256;
            var hostA = ZeroTensor.Core.Tensor.Ones(M, K);
            var hostB = ZeroTensor.Core.Tensor.Ones(K, N);

            const int iters = 20;

            // 1. Host Roundtrips (CPU host memory each step)
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < iters; iter++)
            {
                var hC = ZeroTensor.Core.Tensor.Zeros<float>(M, N);
                gpu.Gemm(hostA, hostB, hC);
                gpu.Activation(hC, hC, ZeroCompute.Core.Context.ComputeActivationType.GELU);
                gpu.RmsNorm(hC, hC, weight: null);
            }
            sw.Stop();
            double tRoundtrip = sw.Elapsed.TotalMilliseconds / iters;

            // 2. Persistent GPU VRAM Residency (zero intermediate PCI-e transfers)
            var devA = gpu.ToDevice(hostA);
            var devB = gpu.ToDevice(hostB);
            var devC = gpu.AllocateDeviceTensor<float>(new ZeroTensor.Core.TensorShape(M, N));

            sw.Restart();
            for (int iter = 0; iter < iters; iter++)
            {
                gpu.Gemm(devA, devB, devC);
                gpu.Activation(devC, devC, ZeroCompute.Core.Context.ComputeActivationType.GELU);
                gpu.RmsNorm(devC, devC, weight: null);
            }
            var finalHost = devC.ToCpu();
            sw.Stop();
            double tPersistent = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[Chained: Gemm(256x512x256) -> GELU -> RMSNorm - 20 runs averaged]");
            Console.WriteLine($"  * Host Roundtrips (PCI-e Copy x3) : {tRoundtrip,8:F2} ms (1.00x)");
            Console.WriteLine($"  * Persistent GPU VRAM Residency  : {tPersistent,8:F2} ms ({(tRoundtrip / tPersistent),5:F2}x speedup | PCI-e roundtrips eliminated)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 11: Kernel Fusion (Fused Linear Layer: GEMM + Bias + GELU)

        private static void RunBenchmark11_FusedGemm()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 11: Kernel Fusion (Linear Layer: 512x512 GEMM + BiasAdd + GELU)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int M = 512, K = 512, N = 512;
            var A = ZeroTensor.Core.Tensor.Ones(M, K);
            var B = ZeroTensor.Core.Tensor.Ones(K, N);
            var bias = ZeroTensor.Core.Tensor.Ones(N);

            const int iters = 10;

            // 1. Unfused 3-Pass Execution (Gemm -> Write C -> Read C + Bias -> Write C -> Read C + GELU -> Write C)
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < iters; iter++)
            {
                var C = ZeroTensor.Core.Tensor.Zeros<float>(M, N);
                ZeroCompute.Core.ComputeDevice.Cpu.Gemm(A, B, C);
                for (int i = 0; i < M; i++)
                    for (int j = 0; j < N; j++)
                        C[i, j] += bias[j];
                ZeroCompute.Core.ComputeDevice.Cpu.Activation(C, C, ZeroCompute.Core.Context.ComputeActivationType.GELU);
            }
            sw.Stop();
            double tUnfused = sw.Elapsed.TotalMilliseconds / iters;

            // 2. Fused Execution: FusedGemm with in-register Bias and GELU activation
            sw.Restart();
            for (int iter = 0; iter < iters; iter++)
            {
                var C = ZeroTensor.Core.Tensor.Zeros<float>(M, N);
                ZeroCompute.Core.ComputeDevice.Cpu.FusedGemm(A, B, bias, C, ZeroCompute.Core.Context.ComputeActivationType.GELU);
            }
            sw.Stop();
            double tFused = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[512x512 Matrices - 10 runs averaged]");
            Console.WriteLine($"  * Unfused 3-Pass Pipeline : {tUnfused,8:F2} ms (1.00x)");
            Console.WriteLine($"  * Fused Linear Layer      : {tFused,8:F2} ms ({(tUnfused / tFused),5:F2}x speedup | DRAM roundtrips saved)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 12: FlashAttention-2 (Scaled Dot-Product Attention)

        private static void RunBenchmark12_FlashAttention()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 12: FlashAttention-2 Online Softmax vs Materialized Attention (SeqLen=512, D=64)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int seqLen = 512, headDim = 64;
            var Q = ZeroTensor.Core.Tensor.Ones(seqLen, headDim);
            var K = ZeroTensor.Core.Tensor.Ones(seqLen, headDim);
            var V = ZeroTensor.Core.Tensor.Ones(seqLen, headDim);

            const int iters = 10;
            float scale = 1.0f / (float)Math.Sqrt(headDim);

            // 1. Materialized Attention: Allocates quadratic SeqLen x SeqLen (512 x 512) attention matrix
            var sw = Stopwatch.StartNew();
            for (int iter = 0; iter < iters; iter++)
            {
                var S = ZeroTensor.Core.Tensor.Zeros<float>(seqLen, seqLen);
                for (int i = 0; i < seqLen; i++)
                {
                    for (int j = 0; j < seqLen; j++)
                    {
                        float dot = 0f;
                        for (int d = 0; d < headDim; d++) dot += Q[i, d] * K[j, d];
                        S[i, j] = dot * scale;
                    }
                }
                var P = ZeroTensor.Core.Tensor.Zeros<float>(seqLen, seqLen);
                ZeroCompute.Core.ComputeDevice.Cpu.Softmax(S, P, axis: -1);
                var O = ZeroTensor.Core.Tensor.Zeros<float>(seqLen, headDim);
                ZeroCompute.Core.ComputeDevice.Cpu.Gemm(P, V, O);
            }
            sw.Stop();
            double tMaterialized = sw.Elapsed.TotalMilliseconds / iters;

            // 2. FlashAttention-2: O(1) Memory, Online Softmax, Cache-Tiled Multi-threaded
            sw.Restart();
            for (int iter = 0; iter < iters; iter++)
            {
                var O = ZeroTensor.Core.Tensor.Zeros<float>(seqLen, headDim);
                ZeroCompute.Core.ComputeDevice.Cpu.ScaledDotProductAttention(Q, K, V, O, scale, isCausal: false);
            }
            sw.Stop();
            double tFlash = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"[Sequence Length 512, Head Dim 64 - 10 runs averaged]");
            Console.WriteLine($"  * Materialized Attention (Allocates 512x512 matrix): {tMaterialized,8:F2} ms (1.00x)");
            Console.WriteLine($"  * FlashAttention-2 Online Softmax (O(1) Memory)     : {tFlash,8:F2} ms ({(tMaterialized / tFlash),5:F2}x speedup | 0 intermediate matrices)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 13: VRAM Slab Buffer Pool Recycling vs Native Driver Allocation

        private static void RunBenchmark13_BufferPoolRecycling()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 13: VRAM Slab Buffer Pool vs OS Driver Buffer Allocation (LLM Steps)");
            Console.WriteLine("--------------------------------------------------------------------------------");

            if (!ZeroCompute.Core.ComputeDevice.IsGpuAvailable)
            {
                Console.WriteLine("  [SKIPPED] GPU acceleration not available.");
                Console.WriteLine();
                return;
            }

            var ctx = ZeroCompute.Core.ComputeDevice.Gpu.D3D11Context!;
            var shape = new ZeroTensor.Core.TensorShape(128, 4096); // 512K floats = 2MB buffer
            const int iters = 100;

            // 1. Unpooled: calling OS Display Driver CreateBuffer + Release on every forward-pass iteration
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                using var t = ctx.AllocateDeviceTensor<float>(shape, pooled: false);
            }
            sw.Stop();
            double tUnpooled = sw.Elapsed.TotalMilliseconds;

            // 2. Pooled: recycling pre-allocated VRAM buffer from D3D11BufferPool
            var pool = ctx.BufferPool!;
            long hitsBefore = pool.CacheHitCount;
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                using var t = ctx.AllocateDeviceTensor<float>(shape, pooled: true);
            }
            sw.Stop();
            double tPooled = sw.Elapsed.TotalMilliseconds;
            long hitsAfter = pool.CacheHitCount;

            Console.WriteLine($"[Buffer: 128x4096 (2.0 MB VRAM) - {iters} Alloc/Dealloc Iterations]");
            Console.WriteLine($"  * Unpooled (Direct3D 11 OS Driver Allocation): {tUnpooled,8:F2} ms ({(tUnpooled / iters),6:F3} ms/iter)");
            Console.WriteLine($"  * Pooled   (ZeroCompute D3D11BufferPool)     : {tPooled,8:F2} ms ({(tPooled / iters),6:F3} ms/iter | {(tUnpooled / tPooled),5:F2}x speedup | {hitsAfter - hitsBefore}/{iters} hits)");
            Console.WriteLine();
        }

        #endregion

        #region Benchmark 14: Quantized INT4 GEMM (AWQ/GPTQ) vs FP32 Dense GEMM

        private static void RunBenchmark14_QuantizedGemmInt4()
        {
            Console.WriteLine("--------------------------------------------------------------------------------");
            Console.WriteLine("BENCHMARK 14: Quantized INT4 GEMM (AWQ/GPTQ) vs Dense FP32 GEMM");
            Console.WriteLine("--------------------------------------------------------------------------------");

            const int M = 32;
            const int K = 1024;
            const int N = 1024;
            const int packedK = K / 2; // 512

            var A = ZeroTensor.Core.Tensor.Ones<float>(M, K);

            // FP32 weight matrix: 1024 x 1024 floats = 4 MB
            var W_fp32 = ZeroTensor.Core.Tensor.Ones<float>(K, N);
            var C_fp32 = ZeroTensor.Core.Tensor.Zeros<float>(M, N);

            // INT4 packed weights: 512 x 1024 bytes = 0.5 MB (8x reduction vs FP32!)
            var packedW = new ZeroTensor.Core.Tensor<byte>(packedK, N);
            packedW.Fill(0x33); // 2 nibbles of 3
            var scales = ZeroTensor.Core.Tensor.Ones<float>(N);
            var zp = ZeroTensor.Core.Tensor.Zeros<float>(N);
            var C_int4 = ZeroTensor.Core.Tensor.Zeros<float>(M, N);

            const int iters = 5;

            // 1. FP32 GEMM Baseline
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                ZeroCompute.Core.Blas.BlasEngine.Gemm(A, W_fp32, C_fp32);
            }
            sw.Stop();
            double tFp32 = sw.Elapsed.TotalMilliseconds / iters;

            // 2. CPU INT4 GEMM
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                ZeroCompute.Core.ComputeDevice.Cpu.GemmInt4(A, packedW, scales, C_int4, zp);
            }
            sw.Stop();
            double tCpuInt4 = sw.Elapsed.TotalMilliseconds / iters;

            // 3. GPU INT4 GEMM (if available)
            double tGpuInt4 = 0;
            if (ZeroCompute.Core.ComputeDevice.IsGpuAvailable)
            {
                var gpuC = ZeroTensor.Core.Tensor.Zeros<float>(M, N);
                // Warm up pipeline
                ZeroCompute.Core.ComputeDevice.Gpu.GemmInt4(A, packedW, scales, gpuC, zp);

                sw.Restart();
                for (int i = 0; i < iters; i++)
                {
                    ZeroCompute.Core.ComputeDevice.Gpu.GemmInt4(A, packedW, scales, gpuC, zp);
                }
                sw.Stop();
                tGpuInt4 = sw.Elapsed.TotalMilliseconds / iters;
            }

            double fp32Mb = (K * N * 4.0) / (1024 * 1024);
            double int4Mb = (packedK * N * 1.0) / (1024 * 1024);

            Console.WriteLine($"[Matrix Size: {M} x {K} x {N} | Weight Memory: FP32={fp32Mb:F2} MB, INT4={int4Mb:F2} MB ({(fp32Mb / int4Mb),4:F1}x Memory Compression)]");
            Console.WriteLine($"  * CPU FP32 Dense GEMM (AVX2+FMA)  : {tFp32,8:F2} ms (1.00x)");
            Console.WriteLine($"  * CPU INT4 Quantized GEMM         : {tCpuInt4,8:F2} ms ({(tFp32 / tCpuInt4),5:F2}x vs FP32)");
            if (ZeroCompute.Core.ComputeDevice.IsGpuAvailable)
            {
                Console.WriteLine($"  * GPU INT4 Quantized CS 5.0 GEMM  : {tGpuInt4,8:F2} ms ({(tFp32 / tGpuInt4),5:F2}x vs CPU FP32)");
            }
            Console.WriteLine();
        }

        #endregion
    }
}
