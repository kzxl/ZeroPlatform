using System;
using System.Diagnostics;

namespace ZeroUI.Benchmarks
{
    /// <summary>
    /// Rigorous statistical benchmark runner implementing Directives 28 & 29:
    /// - Warmup cycles: 10
    /// - Measured iterations: 100
    /// - Percentiles: Min, P50 (Median), P95, P99, Max, Mean
    /// - Memory: GC.GetAllocatedBytesForCurrentThread(), Gen0, Gen1, Gen2
    /// </summary>
    public static class StatisticalRunner
    {
        public readonly struct BenchmarkResult
        {
            public readonly double MinMs;
            public readonly double P50Ms;
            public readonly double P95Ms;
            public readonly double P99Ms;
            public readonly double MaxMs;
            public readonly double MeanMs;
            public readonly double OpsPerSec;
            public readonly long AllocatedBytesPerOp;
            public readonly int Gen0Collections;
            public readonly int Gen1Collections;
            public readonly int Gen2Collections;
            public readonly int Iterations;

            public BenchmarkResult(
                double minMs, double p50Ms, double p95Ms, double p99Ms, double maxMs, double meanMs,
                double opsPerSec, long allocatedBytesPerOp, int gen0, int gen1, int gen2, int iterations)
            {
                MinMs = minMs;
                P50Ms = p50Ms;
                P95Ms = p95Ms;
                P99Ms = p99Ms;
                MaxMs = maxMs;
                MeanMs = meanMs;
                OpsPerSec = opsPerSec;
                AllocatedBytesPerOp = allocatedBytesPerOp;
                Gen0Collections = gen0;
                Gen1Collections = gen1;
                Gen2Collections = gen2;
                Iterations = iterations;
            }
        }

        public static BenchmarkResult Run(Action action, int warmupCount = 10, int iterationCount = 100, double scaleOpsPerIter = 1.0)
        {
            // 1. Tiered JIT & Cache Warmup (Directive 29: Warmup 10)
            for (int w = 0; w < warmupCount; w++)
            {
                action();
            }

            // 2. Force full clean garbage collection prior to measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            double[] samplesMs = new double[iterationCount];
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            int g0Before = GC.CollectionCount(0);
            int g1Before = GC.CollectionCount(1);
            int g2Before = GC.CollectionCount(2);

            var swIter = new Stopwatch();

            // 3. Measure iterations (Directive 29: Iterations 100)
            for (int i = 0; i < iterationCount; i++)
            {
                swIter.Restart();
                action();
                swIter.Stop();
                samplesMs[i] = swIter.Elapsed.TotalMilliseconds;
            }

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            int g0After = GC.CollectionCount(0);
            int g1After = GC.CollectionCount(1);
            int g2After = GC.CollectionCount(2);

            // 4. Calculate Percentiles (P50, P95, P99)
            Array.Sort(samplesMs);

            double min = samplesMs[0];
            double max = samplesMs[iterationCount - 1];
            double p50 = GetPercentile(samplesMs, 0.50);
            double p95 = GetPercentile(samplesMs, 0.95);
            double p99 = GetPercentile(samplesMs, 0.99);

            double sum = 0;
            for (int i = 0; i < iterationCount; i++)
            {
                sum += samplesMs[i];
            }
            double mean = sum / iterationCount;

            double opsPerSec = (scaleOpsPerIter / (mean / 1000.0));
            long totalAlloc = allocAfter - allocBefore;
            long allocPerOp = (long)(totalAlloc / (iterationCount * scaleOpsPerIter));
            if (allocPerOp < 0) allocPerOp = 0;

            return new BenchmarkResult(
                min, p50, p95, p99, max, mean,
                opsPerSec, allocPerOp,
                g0After - g0Before, g1After - g1Before, g2After - g2Before, iterationCount);
        }

        private static double GetPercentile(double[] sortedSamples, double percentile)
        {
            if (sortedSamples.Length == 0) return 0;
            if (sortedSamples.Length == 1) return sortedSamples[0];

            double position = (sortedSamples.Length - 1) * percentile;
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex)
            {
                return sortedSamples[lowerIndex];
            }

            double fraction = position - lowerIndex;
            return sortedSamples[lowerIndex] + (fraction * (sortedSamples[upperIndex] - sortedSamples[lowerIndex]));
        }
    }
}
