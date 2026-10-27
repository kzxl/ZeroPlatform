using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using ZeroUI.Core.Scada;

namespace ZeroUI.Benchmarks.Categories
{
    /// <summary>
    /// Category D: TagEngine Scalability Benchmarks.
    /// Evaluates flat unboxed tag storage and inverted index dispatch across 1 tag, 1K tags, 10K tags, and 100K tags.
    /// </summary>
    [MemoryDiagnoser]
    public class TagEngineBenchmarks
    {
        private static readonly int[] TagCounts = { 1, 1_000, 10_000, 100_000 };

        [Benchmark]
        public void TagEngine_1Tag() => BenchmarkTagOperations(1);

        [Benchmark]
        public void TagEngine_1KTags() => BenchmarkTagOperations(1_000);

        [Benchmark]
        public void TagEngine_10KTags() => BenchmarkTagOperations(10_000);

        [Benchmark]
        public void TagEngine_100KTags() => BenchmarkTagOperations(100_000);

        private static void BenchmarkTagOperations(int tagCount)
        {
            var storage = new TagStorage(tagCount + 64);
            long nowMs = 1700000000000;
            var scadaVal = new ScadaValue(100.0, ScadaQuality.Good);

            const int operations = 10_000;
            for (int i = 0; i < operations; i++)
            {
                int id = i % tagCount;
                storage.Set(id, scadaVal, nowMs);
                var val = storage.GetValue(id);
            }
        }

        public static void RunProfiler()
        {
            Console.WriteLine("----------------------------------------------------------------------------------");
            Console.WriteLine("D. TagEngine Benchmarks (1, 1K, 10K, 100K Tags - Flat Array Storage & Dispatch)");
            Console.WriteLine("----------------------------------------------------------------------------------");

            foreach (var tagCount in TagCounts)
            {
                var storage = new TagStorage(tagCount + 64);
                long nowMs = 1700000000000;
                var scadaVal = new ScadaValue(42.5, ScadaQuality.Good);

                // Pre-populate tags
                for (int i = 0; i < tagCount; i++)
                {
                    storage.Set(i, scadaVal, nowMs);
                }

                // 1. Benchmark O(1) Typed Write Latency
                const int totalWrites = 1_000_000;

                // Warmup
                for (int i = 0; i < Math.Min(totalWrites, 10_000); i++)
                {
                    storage.Set(i % tagCount, scadaVal, nowMs);
                }

                int iters = 20;
                int writesPerIter = Math.Min(totalWrites, 50_000);

                Action writeAction = () =>
                {
                    for (int i = 0; i < writesPerIter; i++)
                    {
                        storage.Set(i % tagCount, scadaVal, nowMs);
                    }
                };

                var writeRes = StatisticalRunner.Run(writeAction, warmupCount: 5, iterationCount: iters, scaleOpsPerIter: writesPerIter);
                double nsPerWrite = (writeRes.MeanMs / writesPerIter) * 1_000_000.0;
                double p50WriteNs = (writeRes.P50Ms / writesPerIter) * 1_000_000.0;
                double p95WriteNs = (writeRes.P95Ms / writesPerIter) * 1_000_000.0;

                // 2. Benchmark O(1) Read Latency
                double checksum = 0;
                Action readAction = () =>
                {
                    for (int i = 0; i < writesPerIter; i++)
                    {
                        checksum += storage.GetValue(i % tagCount).DoubleVal;
                    }
                };

                var readRes = StatisticalRunner.Run(readAction, warmupCount: 5, iterationCount: iters, scaleOpsPerIter: writesPerIter);
                double nsPerRead = (readRes.MeanMs / writesPerIter) * 1_000_000.0;
                double p50ReadNs = (readRes.P50Ms / writesPerIter) * 1_000_000.0;
                double p95ReadNs = (readRes.P95Ms / writesPerIter) * 1_000_000.0;

                Console.WriteLine($"  • Tags: {tagCount,7:N0} | Write P50: {p50WriteNs,4:F1} ns, P95: {p95WriteNs,4:F1} ns ({writeRes.OpsPerSec,10:N0} ops/s) | Read P50: {p50ReadNs,4:F1} ns, P95: {p95ReadNs,4:F1} ns ({readRes.OpsPerSec,10:N0} ops/s) | Alloc: {writeRes.AllocatedBytesPerOp} B | Gen0: {writeRes.Gen0Collections}");
            }

            Console.WriteLine();
        }
    }
}
