using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.Persistence;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Tests
{
    public class RetentionPolicyEngineTests
    {
        [Fact]
        public void TestDownsample_Aggregations()
        {
            long baseTime = 10000;
            var points = new List<TimeSeriesPoint>
            {
                new TimeSeriesPoint(baseTime + 0, 10.0),
                new TimeSeriesPoint(baseTime + 200, 20.0),
                new TimeSeriesPoint(baseTime + 500, 30.0),
                new TimeSeriesPoint(baseTime + 900, 40.0),
                // Next bucket: baseTime + 1000
                new TimeSeriesPoint(baseTime + 1100, 100.0),
                new TimeSeriesPoint(baseTime + 1500, 200.0),
            };

            long bucketSizeMs = 1000;

            // Mean
            var meanPts = RetentionPolicyEngine.Downsample(points, bucketSizeMs, AggregationType.Mean);
            Assert.Equal(2, meanPts.Count);
            Assert.Equal(baseTime, meanPts[0].TimestampMs);
            Assert.Equal((10.0 + 20.0 + 30.0 + 40.0) / 4.0, meanPts[0].Value, precision: 4);
            Assert.Equal(baseTime + 1000, meanPts[1].TimestampMs);
            Assert.Equal((100.0 + 200.0) / 2.0, meanPts[1].Value, precision: 4);

            // Min & Max
            var minPts = RetentionPolicyEngine.Downsample(points, bucketSizeMs, AggregationType.Min);
            Assert.Equal(10.0, minPts[0].Value);
            Assert.Equal(100.0, minPts[1].Value);

            var maxPts = RetentionPolicyEngine.Downsample(points, bucketSizeMs, AggregationType.Max);
            Assert.Equal(40.0, maxPts[0].Value);
            Assert.Equal(200.0, maxPts[1].Value);

            // First & Last
            var firstPts = RetentionPolicyEngine.Downsample(points, bucketSizeMs, AggregationType.First);
            Assert.Equal(10.0, firstPts[0].Value);
            Assert.Equal(100.0, firstPts[1].Value);

            var lastPts = RetentionPolicyEngine.Downsample(points, bucketSizeMs, AggregationType.Last);
            Assert.Equal(40.0, lastPts[0].Value);
            Assert.Equal(200.0, lastPts[1].Value);

            // Sum & Count
            var sumPts = RetentionPolicyEngine.Downsample(points, bucketSizeMs, AggregationType.Sum);
            Assert.Equal(100.0, sumPts[0].Value);
            Assert.Equal(300.0, sumPts[1].Value);

            var countPts = RetentionPolicyEngine.Downsample(points, bucketSizeMs, AggregationType.Count);
            Assert.Equal(4.0, countPts[0].Value);
            Assert.Equal(2.0, countPts[1].Value);
        }

        [Fact]
        public void TestDownsample_100HzTelemetryCompression()
        {
            // Simulate 1,000 points of 100Hz telemetry (10s at 10ms intervals)
            long startTime = 1700000000000L;
            var points = new List<TimeSeriesPoint>(1000);
            for (int i = 0; i < 1000; i++)
            {
                long t = startTime + i * 10;
                double v = Math.Sin(i * 0.05);
                points.Add(new TimeSeriesPoint(t, v));
            }

            // Downsample to 1-second buckets (bucketSizeMs = 1000)
            var downsampled = RetentionPolicyEngine.Downsample(points, 1000, AggregationType.Mean);

            // 10 seconds of 100Hz telemetry -> exactly 10 downsampled points (99% reduction)
            Assert.Equal(10, downsampled.Count);

            var rollups = RetentionPolicyEngine.GenerateRollups(points, 1000);
            Assert.Equal(10, rollups.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(100, rollups[i].Count);
                Assert.True(rollups[i].Min <= rollups[i].Mean);
                Assert.True(rollups[i].Mean <= rollups[i].Max);
            }
        }

        [Fact]
        public void TestDownsampleBlock()
        {
            var points = new List<TimeSeriesPoint>();
            long start = 50000;
            for (int i = 0; i < 200; i++)
            {
                points.Add(new TimeSeriesPoint(start + i * 50, i * 2.0));
            }

            var rawBlock = TimeSeriesBlock.FromPoints(metricId: 7, points);
            Assert.Equal(200, rawBlock.Count);

            // Downsample block to 1-second buckets (1000ms)
            var dsBlock = RetentionPolicyEngine.DownsampleBlock(rawBlock, 1000, AggregationType.Mean);
            Assert.Equal(7, dsBlock.MetricId);

            var dsPoints = dsBlock.Decompress();
            Assert.Equal(10, dsPoints.Count); // 200 pts * 50ms = 10,000ms = 10 buckets
        }

        [Fact]
        public void TestPurgeExpired_PrunesOldBlocksAndSplitsBoundary()
        {
            string srcLog = Path.Combine(Path.GetTempPath(), $"purge_src_{Guid.NewGuid():N}.zts");
            string dstLog = Path.Combine(Path.GetTempPath(), $"purge_dst_{Guid.NewGuid():N}.zts");

            try
            {
                long baseTime = 100000;
                using (var log = new MemoryMappedTimeSeriesLog(srcLog, 2 * 1024 * 1024))
                {
                    // Block 1: 100000 to 109000 (10 points, 1000ms apart)
                    var b1Pts = new List<TimeSeriesPoint>();
                    for (int i = 0; i < 10; i++) b1Pts.Add(new TimeSeriesPoint(baseTime + i * 1000, 1.0));
                    log.AppendBlock(TimeSeriesBlock.FromPoints(1, b1Pts));

                    // Block 2 (Boundary): 110000 to 119000 (10 points, 1000ms apart)
                    var b2Pts = new List<TimeSeriesPoint>();
                    for (int i = 0; i < 10; i++) b2Pts.Add(new TimeSeriesPoint(baseTime + 10000 + i * 1000, 2.0));
                    log.AppendBlock(TimeSeriesBlock.FromPoints(1, b2Pts));

                    // Block 3: 120000 to 129000 (10 points, 1000ms apart)
                    var b3Pts = new List<TimeSeriesPoint>();
                    for (int i = 0; i < 10; i++) b3Pts.Add(new TimeSeriesPoint(baseTime + 20000 + i * 1000, 3.0));
                    log.AppendBlock(TimeSeriesBlock.FromPoints(1, b3Pts));
                }

                // Cutoff at 115000:
                // Block 1 (ends at 109000) is completely expired.
                // Block 2 (110000-119000) has points from 115000 onwards kept (5 points: 115000, 116000, 117000, 118000, 119000).
                // Block 3 (120000-129000) is completely valid.
                using (var src = new MemoryMappedTimeSeriesLog(srcLog, 2 * 1024 * 1024))
                {
                    int retained = RetentionPolicyEngine.PurgeExpired(src, dstLog, cutoffTimestampMs: 115000);
                    Assert.Equal(2, retained);
                }

                using (var dst = new MemoryMappedTimeSeriesLog(dstLog, 2 * 1024 * 1024))
                {
                    var blocks = dst.ReadAllBlocks();
                    Assert.Equal(2, blocks.Count);

                    var b0 = blocks[0].Decompress();
                    Assert.Equal(5, b0.Count);
                    Assert.Equal(115000, b0[0].TimestampMs);
                    Assert.Equal(119000, b0[4].TimestampMs);

                    var b1 = blocks[1].Decompress();
                    Assert.Equal(10, b1.Count);
                    Assert.Equal(120000, b1[0].TimestampMs);
                }
            }
            finally
            {
                if (File.Exists(srcLog)) File.Delete(srcLog);
                if (File.Exists(dstLog)) File.Delete(dstLog);
            }
        }

        [Fact]
        public void TestExecuteLifecycle_FullCycleRollupAndPurge()
        {
            string rawLogPath = Path.Combine(Path.GetTempPath(), $"life_raw_{Guid.NewGuid():N}.zts");
            string rollupLogPath = Path.Combine(Path.GetTempPath(), $"life_rollup_{Guid.NewGuid():N}.zts");

            try
            {
                long currentTime = 200000;
                long rawRetentionDuration = 50000; // raw cutoff is 150000

                // Create raw log with 2 blocks:
                // Block 1: 100000 to 140000 (old, < 150000) -> 5 points
                // Block 2: 160000 to 190000 (fresh, >= 150000) -> 4 points
                using (var raw = new MemoryMappedTimeSeriesLog(rawLogPath, 2 * 1024 * 1024))
                {
                    var b1 = new List<TimeSeriesPoint>();
                    for (int i = 0; i < 5; i++) b1.Add(new TimeSeriesPoint(100000 + i * 10000, 10.0 + i));
                    raw.AppendBlock(TimeSeriesBlock.FromPoints(101, b1));

                    var b2 = new List<TimeSeriesPoint>();
                    for (int i = 0; i < 4; i++) b2.Add(new TimeSeriesPoint(160000 + i * 10000, 50.0 + i));
                    raw.AppendBlock(TimeSeriesBlock.FromPoints(101, b2));
                }

                var rule = new RetentionRule(
                    rawRetentionDurationMs: rawRetentionDuration,
                    rollupBucketSizeMs: 20000, // 20s rollup buckets
                    rollupAggregation: AggregationType.Mean,
                    archiveRetentionDurationMs: 500000 // keep archive
                );

                var result = RetentionPolicyEngine.ExecuteLifecycle(rawLogPath, rollupLogPath, currentTime, rule);

                Assert.Equal(2, result.RawBlocksExamined);
                Assert.Equal(1, result.RawBlocksRetained);
                Assert.Equal(1, result.RawBlocksPurged);
                Assert.Equal(5, result.RawPointsAggregated);
                Assert.True(result.RollupBlocksCreated >= 1);

                // Verify Raw Log now has only the fresh block
                using (var raw = new MemoryMappedTimeSeriesLog(rawLogPath, 2 * 1024 * 1024))
                {
                    var remainingBlocks = raw.ReadAllBlocks();
                    Assert.Single(remainingBlocks);
                    Assert.Equal(101, remainingBlocks[0].MetricId);
                    Assert.Equal(4, remainingBlocks[0].Count);
                    Assert.Equal(160000, remainingBlocks[0].StartTimeMs);
                }

                // Verify Rollup Log has the downsampled summary
                using (var rollup = new MemoryMappedTimeSeriesLog(rollupLogPath, 2 * 1024 * 1024))
                {
                    var rollupBlocks = rollup.ReadAllBlocks();
                    Assert.NotEmpty(rollupBlocks);
                    Assert.Equal(101, rollupBlocks[0].MetricId);
                    var pts = rollupBlocks[0].Decompress();
                    // 5 points at 100000, 110000, 120000, 130000, 140000
                    // In 20,000 buckets: [100000, 120000), [120000, 140000), [140000, 160000)
                    Assert.Equal(3, pts.Count);
                }
            }
            finally
            {
                if (File.Exists(rawLogPath)) File.Delete(rawLogPath);
                if (File.Exists(rollupLogPath)) File.Delete(rollupLogPath);
            }
        }
    }
}
