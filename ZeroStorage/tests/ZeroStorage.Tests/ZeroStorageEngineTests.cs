using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroStorage.Core.Engine;

namespace ZeroStorage.Tests
{
    public class ZeroStorageEngineTests
    {
        [Fact]
        public void ZeroStorageEngine_IngestFlushQuery_Seamless()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zs_engine_test_{Guid.NewGuid():N}");
            try
            {
                var options = new ZeroStorageEngineOptions(tempDir)
                {
                    MemTableThresholdPoints = 100,
                    EnableSparseIndex = true
                };

                using (var engine = new ZeroStorageEngine(options))
                {
                    long baseTime = 1700000000000L;

                    // Ingest 50 points (less than threshold 100, remains in MemTable)
                    for (int i = 0; i < 50; i++)
                    {
                        engine.WritePoint(1, baseTime + i * 1000, 25.0 + i * 0.1);
                    }

                    Assert.Equal(50, engine.TotalMemTablePoints);
                    Assert.Equal(0, engine.ActiveSegmentCount);

                    // Query from MemTable
                    var q1 = engine.Query(1, baseTime, baseTime + 49000);
                    Assert.Equal(50, q1.Count);
                    Assert.Equal(25.0, q1[0].Value, 4);

                    // Force flush to immutable segment
                    engine.Flush();
                    Assert.Equal(0, engine.TotalMemTablePoints);
                    Assert.Equal(1, engine.ActiveSegmentCount);

                    // Ingest 50 more points into MemTable
                    for (int i = 50; i < 100; i++)
                    {
                        engine.WritePoint(1, baseTime + i * 1000, 25.0 + i * 0.1);
                    }

                    // Query across BOTH segment and MemTable
                    var q2 = engine.Query(1, baseTime, baseTime + 99000);
                    Assert.Equal(100, q2.Count);
                    Assert.Equal(25.0, q2[0].Value, 4);
                    Assert.Equal(25.0 + 99 * 0.1, q2[99].Value, 4);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void ZeroStorageEngine_TagBasedQueries_ResolvesMultipleSeries()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zs_tag_test_{Guid.NewGuid():N}");
            try
            {
                var options = new ZeroStorageEngineOptions(tempDir);
                using (var engine = new ZeroStorageEngine(options))
                {
                    long baseTime = 1700000000000L;

                    var tags1 = new Dictionary<string, string> { ["line"] = "L1", ["cell"] = "C1" };
                    var tags2 = new Dictionary<string, string> { ["line"] = "L1", ["cell"] = "C2" };

                    for (int i = 0; i < 20; i++)
                    {
                        engine.WritePoint("vibration", tags1, baseTime + i * 100, 1.2 + i * 0.05);
                        engine.WritePoint("vibration", tags2, baseTime + i * 100, 2.4 + i * 0.05);
                    }

                    // Query by line="L1": should return both series
                    var results = engine.QuerySeries("vibration", baseTime, baseTime + 1900,
                        new KeyValuePair<string, string>("line", "L1")
                    );

                    Assert.Equal(2, results.Count);
                    foreach (var kvp in results)
                    {
                        Assert.Equal(20, kvp.Value.Count);
                    }

                    // Query by line="L1" AND cell="C1": should return only 1 series
                    var c1Results = engine.QuerySeries("vibration", baseTime, baseTime + 1900,
                        new KeyValuePair<string, string>("line", "L1"),
                        new KeyValuePair<string, string>("cell", "C1")
                    );

                    Assert.Single(c1Results);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }

        [Fact]
        public void ZeroStorageEngine_Compaction_MergesSegments()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zs_comp_test_{Guid.NewGuid():N}");
            try
            {
                var options = new ZeroStorageEngineOptions(tempDir)
                {
                    MemTableThresholdPoints = 20
                };

                using (var engine = new ZeroStorageEngine(options))
                {
                    long baseTime = 1700000000000L;

                    // Write 60 points -> triggers 3 segment flushes (threshold = 20)
                    for (int i = 0; i < 60; i++)
                    {
                        engine.WritePoint(100, baseTime + i * 100, i * 2.0);
                    }

                    Assert.True(engine.ActiveSegmentCount >= 3);

                    // Compact
                    int compactedMetrics = engine.Compact();
                    Assert.Equal(1, compactedMetrics);
                    Assert.Equal(1, engine.ActiveSegmentCount);

                    // Verify data integrity after compaction
                    var points = engine.Query(100, baseTime, baseTime + 5900);
                    Assert.Equal(60, points.Count);
                    Assert.Equal(0.0, points[0].Value, 4);
                    Assert.Equal(118.0, points[59].Value, 4);
                }
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            }
        }
    }
}
