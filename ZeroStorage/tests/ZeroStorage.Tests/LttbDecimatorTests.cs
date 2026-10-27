using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroStorage.Core.Analytics;
using ZeroStorage.Core.Engine;
using ZeroStorage.Core.Gorilla;

namespace ZeroStorage.Tests
{
    public class LttbDecimatorTests
    {
        [Fact]
        public void Decimate_SmallOrEqualCount_ReturnsOriginalList()
        {
            var points = new List<TimeSeriesPoint>
            {
                new TimeSeriesPoint(1000, 10.0),
                new TimeSeriesPoint(2000, 20.0),
                new TimeSeriesPoint(3000, 30.0)
            };

            var res5 = LttbDecimator.Decimate(points, 5);
            Assert.Equal(3, res5.Count);

            var res3 = LttbDecimator.Decimate(points, 3);
            Assert.Equal(3, res3.Count);

            var res2 = LttbDecimator.Decimate(points, 2);
            Assert.Equal(2, res2.Count);
            Assert.Equal(1000, res2[0].TimestampMs);
            Assert.Equal(3000, res2[1].TimestampMs);

            var res1 = LttbDecimator.Decimate(points, 1);
            Assert.Single(res1);
            Assert.Equal(1000, res1[0].TimestampMs);
        }

        [Fact]
        public void Decimate_LargeDataset_ReturnsExactTargetThreshold()
        {
            int n = 5000;
            int threshold = 250;
            var points = new List<TimeSeriesPoint>(n);

            for (int i = 0; i < n; i++)
            {
                points.Add(new TimeSeriesPoint(1000 + i * 10, Math.Sin(i * 0.05) * 50.0));
            }

            var decimated = LttbDecimator.Decimate(points, threshold);

            Assert.Equal(threshold, decimated.Count);
            // Verify chronologically sorted
            for (int i = 1; i < decimated.Count; i++)
            {
                Assert.True(decimated[i].TimestampMs > decimated[i - 1].TimestampMs);
            }

            // Verify first and last points match
            Assert.Equal(points[0].TimestampMs, decimated[0].TimestampMs);
            Assert.Equal(points[0].Value, decimated[0].Value);

            Assert.Equal(points[n - 1].TimestampMs, decimated[threshold - 1].TimestampMs);
            Assert.Equal(points[n - 1].Value, decimated[threshold - 1].Value);
        }

        [Fact]
        public void Decimate_SharpExtrema_PreservesCriticalSpikesAndValleys()
        {
            // Simulates industrial machine with steady state ~10.0, plus 1 severe vibration spike of 500.0 at i=500
            // and 1 sudden drop of -200.0 at i=750
            int n = 1000;
            var points = new List<TimeSeriesPoint>(n);

            for (int i = 0; i < n; i++)
            {
                double val = 10.0 + (i % 5) * 0.2;
                if (i == 500) val = 500.0;  // Critical transient spike
                if (i == 750) val = -200.0; // Critical pressure dip
                points.Add(new TimeSeriesPoint(1000 + i * 10, val));
            }

            // Downsample from 1,000 to 50 points (20x reduction)
            var decimated = LttbDecimator.Decimate(points, 50);

            Assert.Equal(50, decimated.Count);

            bool spikePreserved = false;
            bool dipPreserved = false;

            for (int i = 0; i < decimated.Count; i++)
            {
                if (decimated[i].Value >= 490.0) spikePreserved = true;
                if (decimated[i].Value <= -190.0) dipPreserved = true;
            }

            Assert.True(spikePreserved, "LTTB failed to preserve the critical 500.0 vibration spike!");
            Assert.True(dipPreserved, "LTTB failed to preserve the critical -200.0 pressure dip!");
        }

        [Fact]
        public void ZeroStorageEngine_QueryLttb_ReturnsDownsampledStream()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zs_lttb_test_{Guid.NewGuid():N}");
            try
            {
                var options = new ZeroStorageEngineOptions(tempDir)
                {
                    MemTableThresholdPoints = 100, // Will flush every 100 points
                    EnableSparseIndex = true
                };

                using (var engine = new ZeroStorageEngine(options))
                {
                    // Ingest 350 points (3 segments of 100 points flushed + 50 points in MemTable)
                    for (int i = 0; i < 350; i++)
                    {
                        engine.WritePoint(1, 1000 + i * 10, 20.0 + Math.Sin(i * 0.1) * 10.0);
                    }

                    // Query with LTTB downsampling to 40 points
                    var downsampled = engine.QueryLttb(1, 1000, 5000, 40);

                    Assert.Equal(40, downsampled.Count);
                    Assert.Equal(1000, downsampled[0].TimestampMs);
                    Assert.Equal(1000 + 349 * 10, downsampled[39].TimestampMs);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }
    }
}
