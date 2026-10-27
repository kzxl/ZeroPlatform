using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroStorage.Core.Engine;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.Persistence;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Tests
{
    public class AggregatePushDownTests : IDisposable
    {
        private readonly string _testDir;

        public AggregatePushDownTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), "zerostorage_agg_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, true);
            }
            catch { }
        }

        [Fact]
        public void MemoryMappedLog_Aggregate_EnclosedBlocks_ComputesAccurateStats()
        {
            string logPath = Path.Combine(_testDir, "test_agg.zts");

            // Create 3 blocks with 100 points each
            // Block 1: [1000..1099], values: [10.0..109.0]
            // Block 2: [2000..2099], values: [200.0..299.0]
            // Block 3: [3000..3099], values: [300.0..399.0]
            using (var log = new MemoryMappedTimeSeriesLog(logPath, 1024 * 1024, enableSparseIndex: true))
            {
                var pts1 = new List<TimeSeriesPoint>();
                for (int i = 0; i < 100; i++) pts1.Add(new TimeSeriesPoint(1000 + i, 10.0 + i));
                log.AppendBlock(TimeSeriesBlock.FromPoints(1, pts1));

                var pts2 = new List<TimeSeriesPoint>();
                for (int i = 0; i < 100; i++) pts2.Add(new TimeSeriesPoint(2000 + i, 200.0 + i));
                log.AppendBlock(TimeSeriesBlock.FromPoints(1, pts2));

                var pts3 = new List<TimeSeriesPoint>();
                for (int i = 0; i < 100; i++) pts3.Add(new TimeSeriesPoint(3000 + i, 300.0 + i));
                log.AppendBlock(TimeSeriesBlock.FromPoints(1, pts3));
            }

            using (var log = new MemoryMappedTimeSeriesLog(logPath, 1024 * 1024, enableSparseIndex: true))
            {
                // Query exactly Block 2 (enclosed range [2000..2099])
                var aggBlock2 = log.Aggregate(1, 2000, 2099);
                Assert.Equal(100, aggBlock2.Count);
                Assert.Equal(200.0, aggBlock2.Min);
                Assert.Equal(299.0, aggBlock2.Max);
                Assert.Equal((200.0 + 299.0) / 2.0, aggBlock2.Mean, 3);

                // Query spanning all 3 blocks ([1000..3099])
                var aggAll = log.Aggregate(1, 1000, 3099);
                Assert.Equal(300, aggAll.Count);
                Assert.Equal(10.0, aggAll.Min);
                Assert.Equal(399.0, aggAll.Max);

                // Query out of range
                var aggEmpty = log.Aggregate(1, 5000, 6000);
                Assert.Equal(0, aggEmpty.Count);
                Assert.True(double.IsNaN(aggEmpty.Min));
                Assert.True(double.IsNaN(aggEmpty.Max));
            }
        }

        [Fact]
        public void MemoryMappedLog_Aggregate_BoundaryStraddling_DecompressesOnlyBoundaries()
        {
            string logPath = Path.Combine(_testDir, "test_agg_boundary.zts");

            using (var log = new MemoryMappedTimeSeriesLog(logPath, 1024 * 1024, enableSparseIndex: true))
            {
                // Block 1: [1000..1099], values: [100..199]
                var pts1 = new List<TimeSeriesPoint>();
                for (int i = 0; i < 100; i++) pts1.Add(new TimeSeriesPoint(1000 + i, 100 + i));
                log.AppendBlock(TimeSeriesBlock.FromPoints(1, pts1));

                // Block 2: [2000..2099], values: [200..299]
                var pts2 = new List<TimeSeriesPoint>();
                for (int i = 0; i < 100; i++) pts2.Add(new TimeSeriesPoint(2000 + i, 200 + i));
                log.AppendBlock(TimeSeriesBlock.FromPoints(1, pts2));
            }

            using (var log = new MemoryMappedTimeSeriesLog(logPath, 1024 * 1024, enableSparseIndex: true))
            {
                // Query [1050..2049]: 50 points from Block 1 ([1050..1099] values [150..199])
                // + 50 points from Block 2 ([2000..2049] values [200..249])
                // Total 100 points
                var agg = log.Aggregate(1, 1050, 2049);
                Assert.Equal(100, agg.Count);
                Assert.Equal(150.0, agg.Min);
                Assert.Equal(249.0, agg.Max);
            }
        }

        [Fact]
        public void ZeroStorageEngine_Aggregate_CombinesSegmentsAndMemTable()
        {
            var options = new ZeroStorageEngineOptions(_testDir)
            {
                MemTableThresholdPoints = 100, // Flushes every 100 points
                EnableSparseIndex = true
            };

            using (var engine = new ZeroStorageEngine(options))
            {
                var tags = new Dictionary<string, string>
                {
                    { "machine", "CNC_01" },
                    { "sensor", "spindle_vibe" }
                };

                // Write 150 points: first 100 flushed to disk segment, remaining 50 in MemTable
                for (int i = 0; i < 150; i++)
                {
                    engine.WritePoint("vibration", tags, 1000 + i, 10.0 + i);
                }

                // Query entire range [1000..1149]
                var agg = engine.QuerySeriesAggregate("vibration", 1000, 1149, new KeyValuePair<string, string>("machine", "CNC_01"));
                Assert.Single(agg);

                foreach (var kvp in agg)
                {
                    var summary = kvp.Value;
                    Assert.Equal(150, summary.Count);
                    Assert.Equal(10.0, summary.Min);
                    Assert.Equal(159.0, summary.Max);
                    Assert.Equal((10.0 + 159.0) / 2.0, summary.Mean, 2);
                }

                // Query only MemTable points [1100..1149] (50 points)
                var memAgg = engine.Aggregate(1, 1100, 1149);
                Assert.Equal(50, memAgg.Count);
                Assert.Equal(110.0, memAgg.Min);
                Assert.Equal(159.0, memAgg.Max);
            }
        }
    }
}
