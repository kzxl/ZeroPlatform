using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using ZeroUI.Core.Scada;

namespace ZeroUI.Benchmarks.Categories
{
    /// <summary>
    /// Category C: Telemetry Pipeline Benchmarks.
    /// Evaluates ingestion, coalescing, and lock-free triple buffer swaps at 1K, 10K, 100K, and 1M updates/sec.
    /// </summary>
    [MemoryDiagnoser]
    public class TelemetryBenchmarks
    {
        private static readonly int[] UpdateBatches = { 1_000, 10_000, 100_000, 1_000_000 };

        [Benchmark]
        public void Telemetry_1K() => BenchmarkIngestion(1_000);

        [Benchmark]
        public void Telemetry_10K() => BenchmarkIngestion(10_000);

        [Benchmark]
        public void Telemetry_100K() => BenchmarkIngestion(100_000);

        [Benchmark]
        public void Telemetry_1M() => BenchmarkIngestion(1_000_000);

        private static void BenchmarkIngestion(int count)
        {
            var buffer = new ZeroTripleBuffer(1024);
            long ts = 1700000000000;
            var scadaVal = new ScadaValue(100.0, ScadaQuality.Good);

            for (int i = 0; i < count; i++)
            {
                var writeBuf = buffer.GetWriteBuffer();
                writeBuf.Set(i % 1000, scadaVal, ts + i);
                buffer.PublishWrite();
            }
        }

        public static void RunProfiler()
        {
            Console.WriteLine("----------------------------------------------------------------------------------");
            Console.WriteLine("C. Telemetry Benchmarks (1K, 10K, 100K, 1M updates/sec - ZeroTripleBuffer & Queue)");
            Console.WriteLine("----------------------------------------------------------------------------------");

            foreach (var count in UpdateBatches)
            {
                var tripleBuffer = new ZeroTripleBuffer(1024);
                long baseTs = 1700000000000;
                var testVal = new ScadaValue(50.0, ScadaQuality.Good);

                int iterations = count >= 1_000_000 ? 10 : count >= 100_000 ? 25 : 50;

                Action ingestAction = () =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        var writeBuf = tripleBuffer.GetWriteBuffer();
                        writeBuf.Set(i % 1000, testVal, baseTs + i);
                        tripleBuffer.PublishWrite();
                    }
                };

                var res = StatisticalRunner.Run(ingestAction, warmupCount: 5, iterationCount: iterations, scaleOpsPerIter: count);
                double nsPerUpdate = (res.MeanMs / count) * 1_000_000.0;

                Console.WriteLine($"  • Ingest {count,9:N0} updates:  P50: {res.P50Ms,6:F2} ms | P95: {res.P95Ms,6:F2} ms | P99: {res.P99Ms,6:F2} ms ({res.OpsPerSec,11:N0} up/s) | {nsPerUpdate,5:F1} ns/op | Alloc: {res.AllocatedBytesPerOp,2} B | Gen0: {res.Gen0Collections}");
            }

            Console.WriteLine();
        }
    }
}
