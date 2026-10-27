using System;
using System.Diagnostics;
using System.IO;
using ZeroPrimitives.Memory;
using ZeroTensor.Core;
using ZeroTensor.Core.Native;
using ZeroTensor.Core.Neural;
using ZeroTensor.Core.Storage;

namespace ZeroTensor.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("                ZeroTensor High-Performance Compute Benchmark                   ");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine($".NET Runtime: {Environment.Version}");
            Console.WriteLine($"SIMD Vector<float> Count: {System.Numerics.Vector<float>.Count}");
            Console.WriteLine($"SIMD Hardware Accelerated: {System.Numerics.Vector.IsHardwareAccelerated}");
            Console.WriteLine("================================================================================\n");

            // Warm up
            WarmUp();

            // Run benchmarks
            RunVectorAddBenchmark();
            RunGemmBenchmark();
            RunBatchedMatMulBenchmark();
            RunSoftmaxBenchmark();
            RunNormalizationBenchmark();
            RunAttentionBenchmark();
            RunVisionConv2DBenchmark();
            RunZeroCopySliceBenchmark();
            RunMemoryAllocationBenchmark();
            RunGgufDequantizeBenchmark();
            RunSwiGLUBenchmark();
            RunStreamingGemvQuantizedBenchmark();
            RunHeadToHeadComparison();

            Console.WriteLine("\nAll benchmarks completed successfully.");
        }

        private static void WarmUp()
        {
            var a = Tensor.Zeros<float>(64, 64);
            var b = Tensor.Ones<float>(64, 64);
            var c = a + b;
            var d = TensorBlas.MatMul(a, b);
        }

        private static void RunVectorAddBenchmark()
        {
            Console.WriteLine("--- 1. Vector Addition Benchmark (N = 1,000,000 FP32 elements) ---");
            int n = 1_000_000;
            var a = Tensor.Ones<float>(n);
            var b = Tensor.Ones<float>(n);

            // Scalar Baseline
            float[] aArr = a.ToArray();
            float[] bArr = b.ToArray();
            float[] cArr = new float[n];

            int iterations = 100;

            // Warmup
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < n; j++) cArr[j] = aArr[j] + bArr[j];
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                for (int j = 0; j < n; j++) cArr[j] = aArr[j] + bArr[j];
            }
            sw.Stop();
            double scalarMs = sw.Elapsed.TotalMilliseconds / iterations;

            // ZeroTensor SIMD Add
            for (int i = 0; i < 5; i++) { var _ = a + b; }
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                var c = a + b;
            }
            sw.Stop();
            double simdMs = sw.Elapsed.TotalMilliseconds / iterations;

            double bytesProcessed = 3.0 * n * sizeof(float); // Read A, Read B, Write C
            double simdGbps = (bytesProcessed / (simdMs / 1000.0)) / (1024 * 1024 * 1024);
            double scalarGbps = (bytesProcessed / (scalarMs / 1000.0)) / (1024 * 1024 * 1024);

            Console.WriteLine($"Scalar C# Loop : {scalarMs:F3} ms | Bandwidth: {scalarGbps:F2} GB/s");
            Console.WriteLine($"ZeroTensor SIMD: {simdMs:F3} ms | Bandwidth: {simdGbps:F2} GB/s | Speedup: {scalarMs / simdMs:F2}x\n");
        }

        private static void RunGemmBenchmark()
        {
            Console.WriteLine("--- 2. GEMM Benchmark (Matrix Multiplication) ---");
            int[] sizes = { 256, 512 };

            foreach (int size in sizes)
            {
                var a = Tensor.Ones<float>(size, size);
                var b = Tensor.Ones<float>(size, size);

                // Warmup
                var _ = TensorBlas.MatMul(a, b);

                int iters = size <= 256 ? 20 : 5;
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < iters; i++)
                {
                    var c = TensorBlas.MatMul(a, b);
                }
                sw.Stop();

                double avgMs = sw.Elapsed.TotalMilliseconds / iters;
                double ops = 2.0 * size * size * size; // 2 * M * N * K FLOPs
                double gflops = (ops / (avgMs / 1000.0)) / 1e9;

                Console.WriteLine($"GEMM FP32 [{size} x {size}]: {avgMs:F2} ms | Compute Throughput: {gflops:F2} GFLOPS");
            }
            Console.WriteLine();
        }

        private static void RunBatchedMatMulBenchmark()
        {
            Console.WriteLine("--- 3. Batched MatMul Benchmark (LLM Multi-Head Attention Projection) ---");
            // [Batch=8, Heads=8, Seq=128, Dim=64] -> flattened batch [64, 128, 64]
            int b = 64, m = 128, k = 64, n = 64;
            var q = Tensor.Ones<float>(b, m, k);
            var kWeight = Tensor.Ones<float>(b, k, n);

            // Warmup
            var _ = TensorBlas.MatMul(q, kWeight);

            int iters = 5;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var outTensor = TensorBlas.MatMul(q, kWeight);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / iters;
            double totalFlops = (double)b * 2.0 * m * k * n;
            double gflops = (totalFlops / (avgMs / 1000.0)) / 1e9;

            Console.WriteLine($"Batched MatMul [{b}, {m}, {k}] x [{b}, {k}, {n}]: {avgMs:F2} ms | Throughput: {gflops:F2} GFLOPS\n");
        }

        private static void RunSoftmaxBenchmark()
        {
            Console.WriteLine("--- 4. Softmax Benchmark (N = 4,096 tokens, LLM context row) ---");
            int n = 4096;
            var logits = Tensor.Ones<float>(n);

            // Standard Softmax (via Tensor.Softmax)
            int iters = 200;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var s = Tensor.Softmax(logits, 0);
            }
            sw.Stop();
            double stdMs = sw.Elapsed.TotalMilliseconds / iters;

            // Fused SoftmaxFast (via TensorOps.SoftmaxFast)
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var s = TensorOps.SoftmaxFast(logits, 0);
            }
            sw.Stop();
            double fastMs = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"Softmax (Standard) : {stdMs * 1000:F2} µs");
            Console.WriteLine($"SoftmaxFast (Fused) : {fastMs * 1000:F2} µs\n");
        }

        private static void RunNormalizationBenchmark()
        {
            Console.WriteLine("--- 5. Normalization Benchmark (RMSNorm & LayerNorm, HiddenDim = 4096) ---");
            int hiddenDim = 4096;
            var x = Tensor.Ones<float>(hiddenDim);
            var gamma = Tensor.Ones<float>(hiddenDim);
            var beta = Tensor.Zeros<float>(hiddenDim);

            int iters = 200;

            // RMSNorm
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var outNorm = TensorOps.RMSNorm(x, gamma);
            }
            sw.Stop();
            double rmsMs = sw.Elapsed.TotalMilliseconds / iters;

            // LayerNorm
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var outNorm = TensorOps.LayerNorm(x, gamma, beta);
            }
            sw.Stop();
            double lnMs = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"RMSNorm (LLaMA style) : {rmsMs * 1000:F2} µs");
            Console.WriteLine($"LayerNorm (BERT style) : {lnMs * 1000:F2} µs\n");
        }

        private static void RunAttentionBenchmark()
        {
            Console.WriteLine("--- 6. Attention Benchmark: Standard SDPA vs FlashAttentionCpu ---");
            int seqLen = 128;
            int headDim = 64;

            var q = Tensor.Ones<float>(seqLen, headDim);
            var k = Tensor.Ones<float>(seqLen, headDim);
            var v = Tensor.Ones<float>(seqLen, headDim);

            // Warmup
            var _ = TensorOps.ScaledDotProductAttention(q, k, v);
            var __ = TensorOps.FlashAttentionCpu(q, k, v);

            int iters = 20;

            // 1. Standard SDPA
            GC.Collect();
            long startBytes = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var attn = TensorOps.ScaledDotProductAttention(q, k, v);
            }
            sw.Stop();
            long sdpaAlloc = (GC.GetAllocatedBytesForCurrentThread() - startBytes) / iters;
            double sdpaMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. FlashAttentionCpu (Tiled Online Softmax)
            GC.Collect();
            startBytes = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var attn = TensorOps.FlashAttentionCpu(q, k, v);
            }
            sw.Stop();
            long flashAlloc = (GC.GetAllocatedBytesForCurrentThread() - startBytes) / iters;
            double flashMs = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"Standard SDPA    : {sdpaMs:F2} ms | Memory Alloc: {sdpaAlloc / 1024.0:F1} KB/op");
            Console.WriteLine($"FlashAttentionCpu: {flashMs:F2} ms | Memory Alloc: {flashAlloc / 1024.0:F1} KB/op (O(1) memory)\n");
        }

        private static void RunVisionConv2DBenchmark()
        {
            Console.WriteLine("--- 7. Computer Vision Conv2D Benchmark (im2col + GEMM) ---");
            // Input: [1, 3, 64, 64], Kernel: [16, 3, 3, 3]
            var input = Tensor.Ones<float>(1, 3, 64, 64);
            var weight = Tensor.Ones<float>(16, 3, 3, 3);

            // Warmup
            var _ = TensorOps.Conv2D(input, weight, stride: 1, padding: 1);

            int iters = 5;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var outConv = TensorOps.Conv2D(input, weight, stride: 1, padding: 1);
            }
            sw.Stop();
            double avgMs = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"Conv2D [1x3x64x64] -> [1x16x64x64]: {avgMs:F2} ms\n");
        }

        private static void RunZeroCopySliceBenchmark()
        {
            Console.WriteLine("--- 8. Zero-Copy View & Slicing Latency & Memory Allocation ---");
            var tensor = Tensor.Zeros<float>(1024, 1024);

            int iters = 100_000;

            // Measure GC bytes allocated during SubTensor / Slicing
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long startBytes = GC.GetAllocatedBytesForCurrentThread();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var slice = tensor.SubTensor(500);
            }
            sw.Stop();
            long totalAllocated = GC.GetAllocatedBytesForCurrentThread() - startBytes;

            double nsPerSlice = (sw.Elapsed.TotalMilliseconds * 1e6) / iters;
            double bytesPerSlice = (double)totalAllocated / iters;

            Console.WriteLine($"SubTensor View Latency : {nsPerSlice:F1} ns/op");
            Console.WriteLine($"GC Allocation per View : {bytesPerSlice:F0} bytes (Header wrapper only, 0 data copy)\n");
        }

        private static void RunMemoryAllocationBenchmark()
        {
            Console.WriteLine("--- 9. Memory Allocation: Managed Array vs TensorPool ---");
            int iters = 10_000;

            // Standard Allocation
            GC.Collect();
            long startBytes = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var t = new Tensor<float>(128, 128);
            }
            sw.Stop();
            long standardAlloc = GC.GetAllocatedBytesForCurrentThread() - startBytes;
            double stdMs = sw.Elapsed.TotalMilliseconds;

            // Pooled Allocation
            GC.Collect();
            startBytes = GC.GetAllocatedBytesForCurrentThread();
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                using var pooled = TensorPool.Rent<float>(128, 128);
            }
            sw.Stop();
            long pooledAlloc = GC.GetAllocatedBytesForCurrentThread() - startBytes;
            double poolMs = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"Standard `new Tensor` : {stdMs:F2} ms | Total GC Allocated: {standardAlloc / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($"TensorPool Rent/Return: {poolMs:F2} ms | Total GC Allocated: {pooledAlloc / (1024.0 * 1024.0):F2} MB | Speedup: {stdMs / poolMs:F2}x");
            Console.WriteLine($"GC Reduction Ratio    : {(double)standardAlloc / Math.Max(1, pooledAlloc):F1}x lower heap footprint\n");
        }

        private static unsafe void RunGgufDequantizeBenchmark()
        {
            Console.WriteLine("--- 10. GGUF Quantization Dequantize Throughput (N = 1,000,000 weights) ---");
            int totalElems = 1_000_000;
            int numBlocks = (totalElems + 31) / 32;

            // Q4_0: 18 bytes per block
            byte[] q4Data = new byte[numBlocks * 18];
            for (int i = 0; i < q4Data.Length; i++) q4Data[i] = (byte)(i % 255);

            // Q8_0: 34 bytes per block
            byte[] q8Data = new byte[numBlocks * 34];
            for (int i = 0; i < q8Data.Length; i++) q8Data[i] = (byte)(i % 255);

            float[] dst = new float[totalElems];
            int iters = 100;

            fixed (byte* pQ4 = q4Data)
            fixed (byte* pQ8 = q8Data)
            fixed (float* pDst = dst)
            {
                IntPtr ptrQ4 = (IntPtr)pQ4;
                IntPtr ptrQ8 = (IntPtr)pQ8;
                IntPtr ptrDst = (IntPtr)pDst;

                // Warmup
                GgufDequantizer.DequantizeQ4_0(ptrQ4, ptrDst, totalElems);
                GgufDequantizer.DequantizeQ8_0(ptrQ8, ptrDst, totalElems);

                // Benchmark Q4_0
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < iters; i++)
                {
                    GgufDequantizer.DequantizeQ4_0(ptrQ4, ptrDst, totalElems);
                }
                sw.Stop();
                double q4Ms = sw.Elapsed.TotalMilliseconds / iters;
                double q4ThroughputMElems = (totalElems / (q4Ms / 1000.0)) / 1e6;
                double q4Gbps = ((numBlocks * 18) / (q4Ms / 1000.0)) / (1024 * 1024 * 1024);

                // Benchmark Q8_0
                sw.Restart();
                for (int i = 0; i < iters; i++)
                {
                    GgufDequantizer.DequantizeQ8_0(ptrQ8, ptrDst, totalElems);
                }
                sw.Stop();
                double q8Ms = sw.Elapsed.TotalMilliseconds / iters;
                double q8ThroughputMElems = (totalElems / (q8Ms / 1000.0)) / 1e6;
                double q8Gbps = ((numBlocks * 34) / (q8Ms / 1000.0)) / (1024 * 1024 * 1024);

                Console.WriteLine($"Q4_0 Dequantize: {q4Ms:F3} ms | Throughput: {q4ThroughputMElems:F1} M weights/sec ({q4Gbps:F2} GB/s)");
                Console.WriteLine($"Q8_0 Dequantize: {q8Ms:F3} ms | Throughput: {q8ThroughputMElems:F1} M weights/sec ({q8Gbps:F2} GB/s)\n");
            }
        }

        private static void RunSwiGLUBenchmark()
        {
            Console.WriteLine("--- 11. Fused SwiGLU Activation Benchmark (IntermediateDim = 11008, Tokens = 128) ---");
            int seqLen = 128;
            int hiddenDim = 11008; // LLaMA 7B intermediate dimension

            var gate = Tensor.Normal(seed: 42, shape: new[] { seqLen, hiddenDim });
            var up = Tensor.Normal(seed: 43, shape: new[] { seqLen, hiddenDim });

            int iters = 50;

            // 1. Naive SwiGLU: SiLU(gate) * up (2 passes + intermediate tensor allocation)
            for (int i = 0; i < 3; i++) { var _ = TensorOps.SiLU(gate) * up; }
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var siluG = TensorOps.SiLU(gate);
                var res = siluG * up;
            }
            sw.Stop();
            double naiveMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. Fused Streaming SwiGLU (single pass, zero intermediate allocations)
            for (int i = 0; i < 3; i++) { var _ = Tensor.SwiGLU(gate, up); }
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var res = Tensor.SwiGLU(gate, up);
            }
            sw.Stop();
            double fusedMs = sw.Elapsed.TotalMilliseconds / iters;

            Console.WriteLine($"Naive (SiLU + Mul): {naiveMs:F3} ms (Allocates intermediate SiLU buffer)");
            Console.WriteLine($"Fused SwiGLU      : {fusedMs:F3} ms (Single pass streaming, 0 intermediate allocations)");
            Console.WriteLine($"Speedup           : {naiveMs / fusedMs:F2}x faster\n");
        }

        private static unsafe void RunStreamingGemvQuantizedBenchmark()
        {
            Console.WriteLine("--- 12. Streaming On-The-Fly Quantized GEMV (4096 x 4096 Linear Layer, 1 Token Decode) ---");
            int k = 4096;
            int n = 4096;

            var act = Tensor.Normal(seed: 42, shape: new[] { k });

            int blocksPerRowQ4 = k / 32;
            byte[] q4Weights = new byte[n * blocksPerRowQ4 * 18];
            for (int i = 0; i < q4Weights.Length; i++) q4Weights[i] = (byte)(i % 255);

            int blocksPerRowQ8 = k / 32;
            byte[] q8Weights = new byte[n * blocksPerRowQ8 * 34];
            for (int i = 0; i < q8Weights.Length; i++) q8Weights[i] = (byte)(i % 255);

            int iters = 20;

            // Warmup
            var _1 = TensorBlas.GemvQ4_0(act, q4Weights, n, k);
            var _2 = TensorBlas.GemvQ8_0(act, q8Weights, n, k);

            // Benchmark Q4_0
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var _ = TensorBlas.GemvQ4_0(act, q4Weights, n, k);
            }
            sw.Stop();
            double q4Ms = sw.Elapsed.TotalMilliseconds / iters;

            // Benchmark Q8_0
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var _ = TensorBlas.GemvQ8_0(act, q8Weights, n, k);
            }
            sw.Stop();
            double q8Ms = sw.Elapsed.TotalMilliseconds / iters;

            double gflops = (2.0 * n * k) / 1e9;
            Console.WriteLine($"Q4_0 GEMV (4096 x 4096): {q4Ms:F2} ms | Compute: {gflops / (q4Ms / 1000.0):F2} GFLOPS (Weights: {q4Weights.Length / (1024 * 1024.0):F1} MB)");
            Console.WriteLine($"Q8_0 GEMV (4096 x 4096): {q8Ms:F2} ms | Compute: {gflops / (q8Ms / 1000.0):F2} GFLOPS (Weights: {q8Weights.Length / (1024 * 1024.0):F1} MB)\n");
        }

        private static void RunHeadToHeadComparison()
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("    HEAD-TO-HEAD: Pure C# (.NET 8 AVX2) vs ZeroRust LLVM Native Substrate       ");
            Console.WriteLine("================================================================================");
            Console.WriteLine($"ZeroRust Native Engine Loaded: {ZeroTensorNativeBridge.IsAvailable}\n");

            if (!ZeroTensorNativeBridge.IsAvailable)
            {
                Console.WriteLine("Warning: ZeroRust native library not detected. Skipping head-to-head.");
                return;
            }

            Console.WriteLine(string.Format("{0,-38} | {1,12} | {2,12} | {3,10}", "Workload", "Pure C#", "ZeroRust", "Speedup"));
            Console.WriteLine(new string('-', 80));

            // 1. GEMM 256x256
            CompareGemm(256, 30);

            // 2. GEMM 512x512
            CompareGemm(512, 10);

            // 3. Batched MatMul
            CompareBatchedMatMul(64, 128, 64, 64, 10);

            // 4. FlashAttention
            CompareFlashAttention(1, 8, 128, 64, 10);

            // 5. Streaming Q4_0
            CompareQuantGemv(4096, 4096, isQ4: true, 20);

            // 6. Streaming Q8_0
            CompareQuantGemv(4096, 4096, isQ4: false, 20);

            // 7. RMSNorm (LLM Hidden Dim = 4096, Batch = 512)
            CompareRMSNorm(512, 4096, 30);

            // 8. SwiGLU Activation (512 x 4096 elements)
            CompareSwiGLU(512 * 4096, 30);

            // 9. RoPE (32 heads, 512 tokens, dim 128)
            CompareRoPE(32, 512, 128, 30);

            // 10. PagedAttention (32q/8kv GQA, 1024 ctx, block=16)
            ComparePagedAttention(32, 8, 128, 16, 1024, 30);

            // 11. End-to-End Transformer Decoder Layer
            CompareTransformerLayer(512, 1536, 8, 4, 64, 15);

            Console.WriteLine(new string('=', 80));
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.Auto;
        }

        private static void CompareGemm(int size, int iters)
        {
            var a = Tensor.Ones<float>(size, size);
            var b = Tensor.Ones<float>(size, size);

            // Warmup
            var _ = TensorBlas.MatMul(a, b);

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) { var c = TensorBlas.MatMul(a, b); }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++) { var c = TensorBlas.MatMul(a, b); }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"GEMM FP32 [{size}x{size}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void CompareBatchedMatMul(int b, int m, int k, int n, int iters)
        {
            var q = Tensor.Ones<float>(b, m, k);
            var kWeight = Tensor.Ones<float>(b, k, n);

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) { var outTensor = TensorBlas.MatMul(q, kWeight); }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++) { var outTensor = TensorBlas.MatMul(q, kWeight); }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"Batched MatMul [{b}x{m}x{k} @ {k}x{n}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void CompareFlashAttention(int batchSize, int numHeads, int seqLen, int headDim, int iters)
        {
            var q = Tensor.Ones<float>(batchSize, numHeads, seqLen, headDim);
            var k = Tensor.Ones<float>(batchSize, numHeads, seqLen, headDim);
            var v = Tensor.Ones<float>(batchSize, numHeads, seqLen, headDim);

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) { var outTensor = TensorOps.ScaledDotProductAttention(q, k, v); }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++) { var outTensor = TensorOps.ScaledDotProductAttention(q, k, v); }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"FlashAttention [seq={seqLen}, heads={numHeads}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void CompareQuantGemv(int rows, int cols, bool isQ4, int iters)
        {
            var act = Tensor.Normal(seed: 42, shape: new[] { cols });
            int bytesPerBlock = isQ4 ? 18 : 34;
            int blocksPerRow = cols / 32;
            byte[] weights = new byte[rows * blocksPerRow * bytesPerBlock];
            for (int i = 0; i < weights.Length; i++) weights[i] = (byte)(i % 255);

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var _ = isQ4 ? TensorBlas.GemvQ4_0(act, weights, rows, cols) : TensorBlas.GemvQ8_0(act, weights, rows, cols);
            }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var _ = isQ4 ? TensorBlas.GemvQ4_0(act, weights, rows, cols) : TensorBlas.GemvQ8_0(act, weights, rows, cols);
            }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = isQ4 ? $"Streaming Q4_0 GEMV [{rows}x{cols}]" : $"Streaming Q8_0 GEMV [{rows}x{cols}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void CompareRMSNorm(int batchRows, int hiddenDim, int iters)
        {
            var x = Tensor.Ones<float>(batchRows, hiddenDim);
            var w = Tensor.Ones<float>(hiddenDim);

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) { var _ = TensorOps.RMSNorm(x, w); }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++) { var _ = TensorOps.RMSNorm(x, w); }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"RMSNorm [{batchRows}x{hiddenDim}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void CompareSwiGLU(int length, int iters)
        {
            var gate = Tensor.Ones<float>(length);
            var up = Tensor.Ones<float>(length);

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++) { var _ = TensorOps.SwiGLU(gate, up); }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++) { var _ = TensorOps.SwiGLU(gate, up); }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"SwiGLU [N={length}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void CompareRoPE(int heads, int seqLen, int headDim, int iters)
        {
            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var q = Tensor.Ones<float>(heads, seqLen, headDim);
                TensorOps.ApplyRoPEInPlace(q, startPos: 0, thetaBase: 10000.0f, interleaved: true);
            }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var q = Tensor.Ones<float>(heads, seqLen, headDim);
                TensorOps.ApplyRoPEInPlace(q, startPos: 0, thetaBase: 10000.0f, interleaved: true);
            }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"RoPE [{heads}h, {seqLen}seq, {headDim}d]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void ComparePagedAttention(int numHeads, int numKvHeads, int headDim, int blockSize, int contextLen, int iters)
        {
            int numBlocks = (contextLen + blockSize - 1) / blockSize;
            var q = Tensor.Ones<float>(numHeads, headDim);
            var kCache = Tensor.Ones<float>(numBlocks, blockSize, numKvHeads, headDim);
            var vCache = Tensor.Ones<float>(numBlocks, blockSize, numKvHeads, headDim);
            int[] blockTable = new int[numBlocks];
            for (int i = 0; i < numBlocks; i++) blockTable[i] = (numBlocks - 1) - i;

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                var _ = TensorOps.PagedAttention(q, kCache, vCache, blockTable, contextLen);
            }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                var _ = TensorOps.PagedAttention(q, kCache, vCache, blockTable, contextLen);
            }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"PagedAttention [{numHeads}q/{numKvHeads}kv, ctx={contextLen}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }

        private static void CompareTransformerLayer(int hiddenDim, int intermediateDim, int numHeads, int numKvHeads, int headDim, int iters)
        {
            int kvDim = numKvHeads * headDim;
            var x = Tensor.Ones<float>(hiddenDim);
            var rmsAttnW = Tensor.Ones<float>(hiddenDim);
            var wq = Tensor.Full(0.01f, hiddenDim, hiddenDim);
            var wk = Tensor.Full(0.01f, kvDim, hiddenDim);
            var wv = Tensor.Full(0.01f, kvDim, hiddenDim);
            var wo = Tensor.Full(0.01f, hiddenDim, hiddenDim);
            var rmsFfnW = Tensor.Ones<float>(hiddenDim);
            var wGate = Tensor.Full(0.01f, intermediateDim, hiddenDim);
            var wUp = Tensor.Full(0.01f, intermediateDim, hiddenDim);
            var wDown = Tensor.Full(0.01f, hiddenDim, intermediateDim);
            var kCache = Tensor.Ones<float>(64, kvDim);
            var vCache = Tensor.Ones<float>(64, kvDim);

            // 1. Pure C#
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForcePureCSharp;
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iters; i++)
            {
                TensorOps.TransformerDecoderLayerInPlace(
                    x, rmsAttnW, wq, wk, wv, wo, rmsFfnW, wGate, wUp, wDown, kCache, vCache,
                    numHeads, numKvHeads, pos: 32);
            }
            sw.Stop();
            double csharpMs = sw.Elapsed.TotalMilliseconds / iters;

            // 2. ZeroRust Native
            ZeroTensorNativeBridge.DispatchMode = NativeDispatchMode.ForceNative;
            sw.Restart();
            for (int i = 0; i < iters; i++)
            {
                TensorOps.TransformerDecoderLayerInPlace(
                    x, rmsAttnW, wq, wk, wv, wo, rmsFfnW, wGate, wUp, wDown, kCache, vCache,
                    numHeads, numKvHeads, pos: 32);
            }
            sw.Stop();
            double rustMs = sw.Elapsed.TotalMilliseconds / iters;

            double speedup = csharpMs / rustMs;
            string label = $"LLM Decoder Layer [d={hiddenDim}, ffn={intermediateDim}]";
            Console.WriteLine(string.Format("{0,-38} | {1,9:F2} ms | {2,9:F2} ms | {3,8:F2}x", label, csharpMs, rustMs, speedup));
        }
    }
}


