using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using ZeroUI.Core.Historian;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Benchmarks
{
    /// <summary>
    /// Multi-dimensional benchmark for SQLite Historian WAL architecture.
    /// Evaluates ingestion throughput across tag cardinality (1 to 10,000 tags)
    /// and commit batch sizes (1 to 1,000), measuring records/sec, MB/sec, WAL sizes,
    /// checkpoint duration, and LTTB query latency.
    /// </summary>
    public static class HistorianMultiDimensionalBenchmark
    {
        public static async Task RunAsync()
        {
            Console.WriteLine("----------------------------------------------------------------------------------");
            Console.WriteLine("3. SQLite Historian: Multi-Dimensional Architecture Benchmark");
            Console.WriteLine("   Evaluating Cardinality (1..10k tags) x Batch (1..1000) with WAL & Checkpointing");
            Console.WriteLine("----------------------------------------------------------------------------------");
            Console.WriteLine();
            Console.WriteLine("| Tags   | Batch | Records | Elapsed (ms) | Records/sec | Disk MB/s | WAL (KB) | Checkpoint | Query Latency |");
            Console.WriteLine("|:-------|:------|:--------|:-------------|:------------|:----------|:---------|:-----------|:--------------|");

            int[] tagCounts = { 1, 10, 100, 1_000, 10_000 };
            int[] batchSizes = { 1, 10, 100, 1_000 };

            foreach (int tags in tagCounts)
            {
                foreach (int batch in batchSizes)
                {
                    // Scale test records reasonably: Batch 1 does fewer iterations (disk sync limit), batch 1000 does more
                    int recordsToTest = batch == 1 ? Math.Min(500, Math.Max(tags, 100))
                                     : batch == 10 ? Math.Min(2_000, Math.Max(tags, 500))
                                     : Math.Max(tags * 2, 10_000);

                    await BenchmarkConfigurationAsync(tags, batch, recordsToTest).ConfigureAwait(false);
                }
            }

            Console.WriteLine();
        }

        private static async Task BenchmarkConfigurationAsync(int tagCount, int batchSize, int totalRecords)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZeroUI_Bench_Historian_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                using var engine = new SqliteHistorianEngine(
                    storageDirectory: tempDir,
                    batchSize: batchSize,
                    flushIntervalMs: 5000); // Prevent background timer interference; flush manually

                // Pre-generate tag paths
                var tagPaths = new string[tagCount];
                for (int i = 0; i < tagCount; i++)
                {
                    tagPaths[i] = $"Plant.Area{i / 100}.Line{i / 10}.Sensor{i:D4}.Value";
                }

                var baseTime = DateTime.UtcNow.Date.AddHours(8);

                // 1. Ingestion Benchmark
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < totalRecords; i++)
                {
                    string tag = tagPaths[i % tagCount];
                    engine.LogSample(tag, 100.0 + (i % 25), ScadaQuality.Good, baseTime.AddMilliseconds(i * 10));
                }

                await engine.FlushAsync().ConfigureAwait(false);
                sw.Stop();

                double elapsedMs = sw.Elapsed.TotalMilliseconds;
                double recordsPerSec = totalRecords / sw.Elapsed.TotalSeconds;

                // 2. File & WAL Metrics
                var metricsBefore = engine.GetStorageMetrics(baseTime.Date);
                double totalMb = metricsBefore.TotalSizeMb;
                double diskMbPerSec = totalMb / sw.Elapsed.TotalSeconds;

                // 3. Checkpoint Latency
                var checkpointDuration = await engine.CheckpointAsync(baseTime.Date, SqliteCheckpointMode.Truncate).ConfigureAwait(false);

                // 4. Query Latency (Query first tag with 1,000-point LTTB decimation)
                var querySw = Stopwatch.StartNew();
                var queryResult = await engine.QueryDecimatedAsync(
                    tagPaths[0],
                    startTime: baseTime,
                    endTime: baseTime.AddHours(24),
                    targetPoints: 1000).ConfigureAwait(false);
                querySw.Stop();

                Console.WriteLine(
                    $"| {tagCount,6:N0} | {batchSize,5} | {totalRecords,7:N0} | {elapsedMs,12:F1} | {recordsPerSec,11:N0} | {diskMbPerSec,9:F2} | {metricsBefore.WalSizeBytes / 1024.0,8:F1} | {checkpointDuration.TotalMilliseconds,8:F2}ms | {querySw.Elapsed.TotalMilliseconds,11:F2}ms |");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, recursive: true);
                    }
                }
                catch
                {
                    // Ignore transient lock during cleanup
                }
            }
        }
    }
}
