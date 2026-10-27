using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.Indexing;
using ZeroStorage.Core.Persistence;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Tests
{
    public class SparseTimeIndexTests
    {
        [Fact]
        public void SparseTimeIndex_QueryBinarySearch_FiltersAccurately()
        {
            var index = new SparseTimeIndex();
            int metricId = 42;

            // Add 100 blocks, each covering 1,000ms: [0, 999], [1000, 1999], ..., [99000, 99999]
            for (int i = 0; i < 100; i++)
            {
                long start = i * 1000L;
                long end = start + 999L;
                long offset = 32 + i * 128L;
                index.AddEntry(new SparseIndexEntry(metricId, start, end, offset, 128, 100, 0.0, 100.0, 5000.0));
            }

            Assert.Equal(100, index.TotalEntries);

            // Query window: [2500, 4200]
            // Should overlap: block 2 [2000, 2999], block 3 [3000, 3999], block 4 [4000, 4999]
            var matches = index.QueryOverlappingEntries(metricId, 2500, 4200);
            Assert.Equal(3, matches.Count);
            Assert.Equal(2000L, matches[0].StartTimeMs);
            Assert.Equal(3000L, matches[1].StartTimeMs);
            Assert.Equal(4000L, matches[2].StartTimeMs);

            // Query single exact timestamp: 50000
            var exactMatches = index.QueryOverlappingEntries(metricId, 50000, 50000);
            Assert.Single(exactMatches);
            Assert.Equal(50000L, exactMatches[0].StartTimeMs);

            // Query completely outside window
            var emptyMatches = index.QueryOverlappingEntries(metricId, 200000, 300000);
            Assert.Empty(emptyMatches);
        }

        [Fact]
        public void SparseTimeIndex_SaveAndLoad_RoundTripsWithCrcValidation()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"sparse_idx_{Guid.NewGuid():N}.tidx");
            try
            {
                var index = new SparseTimeIndex();
                index.AddEntry(new SparseIndexEntry(1, 1000, 2000, 32, 64, 10, 1.0, 5.0, 30.0));
                index.AddEntry(new SparseIndexEntry(2, 2000, 3000, 96, 64, 10, 2.0, 8.0, 50.0));
                index.Save(tempFile);

                var loaded = SparseTimeIndex.Load(tempFile);
                Assert.Equal(2, loaded.TotalEntries);

                var m1 = loaded.QueryOverlappingEntries(1, 0, 5000);
                Assert.Single(m1);
                Assert.Equal(1000, m1[0].StartTimeMs);
                Assert.Equal(2000, m1[0].EndTimeMs);
                Assert.Equal(32, m1[0].FileOffset);

                var m2 = loaded.QueryOverlappingEntries(2, 0, 5000);
                Assert.Single(m2);
                Assert.Equal(2000, m2[0].StartTimeMs);
                Assert.Equal(96, m2[0].FileOffset);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void SparseTimeIndex_LogAcceleration_MatchesSequentialQuery()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"log_sparse_acc_{Guid.NewGuid():N}.tsdb");
            try
            {
                using (var log = new MemoryMappedTimeSeriesLog(tempFile, 4 * 1024 * 1024, enableSparseIndex: true))
                {
                    long baseTime = 1600000000000L;
                    for (int b = 0; b < 20; b++)
                    {
                        var pts = new List<TimeSeriesPoint>();
                        for (int i = 0; i < 50; i++)
                        {
                            pts.Add(new TimeSeriesPoint(baseTime + (b * 50 + i) * 100, i * 1.5));
                        }
                        log.AppendBlock(TimeSeriesBlock.FromPoints(10, pts));
                    }

                    Assert.NotNull(log.SparseIndex);
                    Assert.Equal(20, log.SparseIndex.TotalEntries);

                    // Query window covering blocks 5 to 8
                    long queryFrom = baseTime + (5 * 50 * 100);
                    long queryTo = baseTime + (8 * 50 * 100);

                    var results = log.Query(10, queryFrom, queryTo);
                    Assert.True(results.Count > 0);

                    // Compare with index disabled (sequential fallback)
                    log.SparseIndex = null;
                    var sequentialResults = log.Query(10, queryFrom, queryTo);

                    Assert.Equal(sequentialResults.Count, results.Count);
                    for (int i = 0; i < results.Count; i++)
                    {
                        Assert.Equal(sequentialResults[i].Count, results[i].Count);
                        Assert.Equal(sequentialResults[i].StartTimeMs, results[i].StartTimeMs);
                        Assert.Equal(sequentialResults[i].EndTimeMs, results[i].EndTimeMs);
                    }
                }
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                if (File.Exists(tempFile + ".tidx")) File.Delete(tempFile + ".tidx");
            }
        }
    }
}
