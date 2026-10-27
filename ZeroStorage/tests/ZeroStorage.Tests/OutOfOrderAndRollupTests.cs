using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroStorage.Core.Analytics;
using ZeroStorage.Core.Engine;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Tests
{
    public class OutOfOrderAndRollupTests
    {
        [Fact]
        public void TimeSeriesRollup_ComputesAccurateMinMaxAvgFirstLastCount()
        {
            // 1-minute interval = 60,000ms
            long intervalMs = 60_000L;
            var points = new List<TimeSeriesPoint>
            {
                // Bucket 0: [0, 60000)
                new TimeSeriesPoint(10_000L, 25.0),
                new TimeSeriesPoint(20_000L, 30.0),
                new TimeSeriesPoint(30_000L, 15.0),
                new TimeSeriesPoint(50_000L, 20.0),

                // Bucket 1: [60000, 120000)
                new TimeSeriesPoint(70_000L, 50.0),
                new TimeSeriesPoint(90_000L, 100.0)
            };

            var buckets = TimeSeriesRollup.ComputeRollup(points, intervalMs);

            Assert.Equal(2, buckets.Count);

            // Bucket 0
            Assert.Equal(0L, buckets[0].TimestampMs);
            Assert.Equal(15.0, buckets[0].Min);
            Assert.Equal(30.0, buckets[0].Max);
            Assert.Equal(22.5, buckets[0].Avg); // (25 + 30 + 15 + 20) / 4 = 22.5
            Assert.Equal(25.0, buckets[0].First);
            Assert.Equal(20.0, buckets[0].Last);
            Assert.Equal(4, buckets[0].Count);

            // Bucket 1
            Assert.Equal(60_000L, buckets[1].TimestampMs);
            Assert.Equal(50.0, buckets[1].Min);
            Assert.Equal(100.0, buckets[1].Max);
            Assert.Equal(75.0, buckets[1].Avg);
            Assert.Equal(50.0, buckets[1].First);
            Assert.Equal(100.0, buckets[1].Last);
            Assert.Equal(2, buckets[1].Count);
        }

        [Fact]
        public void StorageEngine_OutOfOrderArrival_DecompressesAndQueriesCorrectly()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zero_storage_ooo_{Guid.NewGuid():N}");
            try
            {
                var options = new ZeroStorageEngineOptions(tempDir)
                {
                    MemTableThresholdPoints = 10,
                    AllowOutOfOrder = true,
                    DeduplicateTimestamps = true
                };

                using (var engine = new ZeroStorageEngine(options))
                {
                    int metricId = 101;

                    // Write points in completely scrambled order:
                    // Correct chronological order: 100, 200, 300, 400, 500
                    engine.WritePoint(metricId, 300L, 30.0);
                    engine.WritePoint(metricId, 100L, 10.0);
                    engine.WritePoint(metricId, 500L, 50.0);
                    engine.WritePoint(metricId, 200L, 20.0);
                    engine.WritePoint(metricId, 400L, 40.0);

                    // Flush to on-disk segment
                    engine.Flush();

                    // Query all points
                    var queried = engine.Query(metricId, 0L, 1000L);

                    Assert.Equal(5, queried.Count);
                    Assert.Equal(100L, queried[0].TimestampMs);
                    Assert.Equal(10.0, queried[0].Value);

                    Assert.Equal(200L, queried[1].TimestampMs);
                    Assert.Equal(20.0, queried[1].Value);

                    Assert.Equal(300L, queried[2].TimestampMs);
                    Assert.Equal(30.0, queried[2].Value);

                    Assert.Equal(400L, queried[3].TimestampMs);
                    Assert.Equal(40.0, queried[3].Value);

                    Assert.Equal(500L, queried[4].TimestampMs);
                    Assert.Equal(50.0, queried[4].Value);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void StorageEngine_DeduplicatesTimestamps_KeepsLatestValue()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zero_storage_dedup_{Guid.NewGuid():N}");
            try
            {
                var options = new ZeroStorageEngineOptions(tempDir)
                {
                    DeduplicateTimestamps = true
                };

                using (var engine = new ZeroStorageEngine(options))
                {
                    int metricId = 202;

                    // Sensor sends timestamp 1000 with value 10, then resends with updated value 15
                    engine.WritePoint(metricId, 1000L, 10.0);
                    engine.WritePoint(metricId, 2000L, 20.0);
                    engine.WritePoint(metricId, 1000L, 15.0); // Duplicate timestamp

                    engine.Flush();

                    var queried = engine.Query(metricId, 0L, 5000L);

                    Assert.Equal(2, queried.Count);
                    Assert.Equal(1000L, queried[0].TimestampMs);
                    Assert.Equal(15.0, queried[0].Value); // Latest value retained
                    Assert.Equal(2000L, queried[1].TimestampMs);
                    Assert.Equal(20.0, queried[1].Value);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void StorageEngine_QueryRollup_ReturnsAggregatedBuckets()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zero_storage_rollup_{Guid.NewGuid():N}");
            try
            {
                var options = new ZeroStorageEngineOptions(tempDir)
                {
                    MemTableThresholdPoints = 5 // Trigger segment flush every 5 points
                };

                using (var engine = new ZeroStorageEngine(options))
                {
                    int metricId = 303;

                    // Ingest 10 points spanning 2 minutes (Minute1 = 60,000ms)
                    // Minute 0: [0..50,000]
                    for (int i = 0; i < 5; i++)
                    {
                        engine.WritePoint(metricId, i * 10_000L, 10.0 + i * 2.0); // 10, 12, 14, 16, 18
                    }

                    // Minute 1: [60,000..100,000]
                    for (int i = 0; i < 5; i++)
                    {
                        engine.WritePoint(metricId, 60_000L + i * 10_000L, 100.0 + i * 5.0); // 100, 105, 110, 115, 120
                    }

                    // Query Rollup per minute across on-disk segment + in-memory MemTable
                    var rollups = engine.QueryRollup(metricId, 0L, 120_000L, RollupInterval.Minute1);

                    Assert.Equal(2, rollups.Count);

                    // First minute bucket
                    Assert.Equal(0L, rollups[0].TimestampMs);
                    Assert.Equal(10.0, rollups[0].Min);
                    Assert.Equal(18.0, rollups[0].Max);
                    Assert.Equal(14.0, rollups[0].Avg);
                    Assert.Equal(5, rollups[0].Count);

                    // Second minute bucket
                    Assert.Equal(60_000L, rollups[1].TimestampMs);
                    Assert.Equal(100.0, rollups[1].Min);
                    Assert.Equal(120.0, rollups[1].Max);
                    Assert.Equal(110.0, rollups[1].Avg);
                    Assert.Equal(5, rollups[1].Count);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, recursive: true); } catch { }
                }
            }
        }
    }
}
