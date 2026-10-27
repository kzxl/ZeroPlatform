using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.Persistence;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Tests
{
    public class MemoryMappedLogTests
    {
        [Fact]
        public void MemoryMappedLog_AppendAndQuery_WorksReliably()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"zerostorage_test_{Guid.NewGuid():N}.zts");

            try
            {
                using (var log = new MemoryMappedTimeSeriesLog(tempFile, initialCapacity: 64 * 1024))
                {
                    // Block 1: Metric 1, Time 1000 - 2000
                    var pts1 = new List<TimeSeriesPoint>
                    {
                        new TimeSeriesPoint(1000, 10.0),
                        new TimeSeriesPoint(1500, 15.0),
                        new TimeSeriesPoint(2000, 20.0)
                    };
                    var block1 = TimeSeriesBlock.FromPoints(metricId: 1, pts1);
                    log.AppendBlock(block1);

                    // Block 2: Metric 2, Time 1000 - 2000
                    var pts2 = new List<TimeSeriesPoint>
                    {
                        new TimeSeriesPoint(1000, 100.0),
                        new TimeSeriesPoint(2000, 200.0)
                    };
                    var block2 = TimeSeriesBlock.FromPoints(metricId: 2, pts2);
                    log.AppendBlock(block2);

                    // Block 3: Metric 1, Time 2500 - 3500
                    var pts3 = new List<TimeSeriesPoint>
                    {
                        new TimeSeriesPoint(2500, 25.0),
                        new TimeSeriesPoint(3000, 30.0),
                        new TimeSeriesPoint(3500, 35.0)
                    };
                    var block3 = TimeSeriesBlock.FromPoints(metricId: 1, pts3);
                    log.AppendBlock(block3);

                    Assert.Equal(3, log.BlockCount);

                    // Query Metric 1 in range [1200, 2600]
                    // Should match block 1 (1000-2000) and block 3 (2500-3500)
                    var queryResults = log.Query(metricId: 1, fromTimeMs: 1200, toTimeMs: 2600);
                    Assert.Equal(2, queryResults.Count);

                    // Decompress and verify
                    var decompressedPts = queryResults[0].Decompress();
                    Assert.Equal(3, decompressedPts.Count);
                    Assert.Equal(10.0, decompressedPts[0].Value);
                    Assert.Equal(15.0, decompressedPts[1].Value);
                    Assert.Equal(20.0, decompressedPts[2].Value);
                }

                // Reopen file and verify persistence
                using (var reopened = new MemoryMappedTimeSeriesLog(tempFile))
                {
                    Assert.Equal(3, reopened.BlockCount);
                    var all = reopened.ReadAllBlocks();
                    Assert.Equal(3, all.Count);
                    Assert.Equal(1, all[0].MetricId);
                    Assert.Equal(2, all[1].MetricId);
                    Assert.Equal(1, all[2].MetricId);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
