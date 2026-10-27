using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class ZabScaleAndMMapTests : IDisposable
    {
        private readonly string _tempDir;

        public ZabScaleAndMMapTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZabScaleTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }
            }
            catch { }
        }

        [Fact]
        public void ZabMMapReader_ZeroCopy_ReadsHeaderManifestAndVectorsAccurately()
        {
            string dbPath = Path.Combine(_tempDir, "mmap_test.zab");

            var manifest = new ZabManifest
            {
                Name = "MMapAgent",
                Role = "HighScaleArchitect",
                RegisteredTools = { "ToolA", "ToolB", "ToolC" }
            };

            using (var db = ZabDatabase.CreateNew(dbPath, manifest))
            {
                db.AddKnowledge("SYS_CACHE_POLICY", "LRU_EBBINGHAUS_HYBRID", "Settings");
                
                // Add vectors
                for (int i = 0; i < 32; i++)
                {
                    float[] vec = new float[64];
                    vec[i % 64] = 1.0f;
                    vec[(i + 1) % 64] = 0.5f;
                    db.StoreVector($"vec_{i}", vec);
                }

                db.Commit();
            }

            // Open via ZabMMapReader
            using (var mmapReader = ZabMMapReader.Open(dbPath))
            {
                Assert.True(mmapReader.Header.IsValid);
                Assert.True(mmapReader.VerifyChecksum());

                var readManifest = mmapReader.ReadManifest();
                Assert.Equal("MMapAgent", readManifest.Name);
                Assert.Equal("HighScaleArchitect", readManifest.Role);
                Assert.Equal(3, readManifest.RegisteredTools.Count);

                var knowledge = mmapReader.ReadKnowledge();
                Assert.Single(knowledge);
                Assert.Equal("SYS_CACHE_POLICY", knowledge[0].Key);
                Assert.Equal("LRU_EBBINGHAUS_HYBRID", knowledge[0].Value);

                var vectors = mmapReader.ReadVectors();
                Assert.Equal(32, vectors.Count);
                Assert.Equal("vec_0", vectors[0].Label);
            }
        }

        [Fact]
        public void ZabIvfVectorIndex_LargeScale_SublinearSearchWithHighRecall()
        {
            const int dim = 64;
            const int totalVectors = 128;
            var ivf = new ZabIvfVectorIndex(dim, targetClusters: 8);

            var random = new Random(1337);
            var groundTruthList = new (ZabVectorRecord Record, float[] RawVector)[totalVectors];

            for (int i = 0; i < totalVectors; i++)
            {
                float[] vec = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    vec[d] = (float)(random.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(vec);

                // Quantize to SQ8 for record
                sbyte[] qData = new sbyte[dim];
                ZeroVector.Core.Quantization.BinaryQuantizer.QuantizeSQ8(vec, qData, out float scale, out float offset);

                var rec = new ZabVectorRecord
                {
                    Id = $"ivf_vec_{i}",
                    Label = $"item_{i}",
                    Dimension = dim,
                    Scale = scale,
                    Offset = offset,
                    QuantizedData = qData
                };

                ivf.Add(rec, vec);
                groundTruthList[i] = (rec, vec);
            }

            Assert.Equal(totalVectors, ivf.TotalVectors);
            Assert.True(ivf.ClusterCount >= 2);

            // Execute test queries and evaluate Top-1 recall
            int hits = 0;
            const int testQueries = 20;

            for (int q = 0; q < testQueries; q++)
            {
                int targetIdx = q * 5;
                float[] queryVec = (float[])groundTruthList[targetIdx].RawVector.Clone();

                // Slight perturbation
                queryVec[0] += 0.01f;
                VectorMetrics.NormalizeL2(queryVec);

                var results = ivf.Search(queryVec, topK: 5, nprobe: 3, minSimilarity: 0.50f);
                Assert.NotEmpty(results);

                // Check if ground truth is in Top-5
                bool found = false;
                for (int r = 0; r < results.Count; r++)
                {
                    if (results[r].Record.Id == groundTruthList[targetIdx].Record.Id)
                    {
                        found = true;
                        break;
                    }
                }
                if (found) hits++;
            }

            double recall = (double)hits / testQueries;
            Assert.True(recall >= 0.90, $"IVF Recall too low: {recall:P2}");
        }

        [Fact]
        public void ZabWalJournal_MaxSizeBytes_TriggersAutoCheckpointWhenExceeded()
        {
            string baselinePath = Path.Combine(_tempDir, "wal_journal_size_test.zab");
            File.WriteAllBytes(baselinePath, new byte[128]); // Dummy baseline

            using (var journal = new ZabWalJournal(baselinePath, autoCheckpointThreshold: 10000, maxWalSizeBytes: 5 * 1024))
            {
                Assert.False(journal.ShouldCheckpoint());

                byte[] chunk = new byte[1024]; // 1 KB
                for (int i = 0; i < 4; i++)
                {
                    journal.AppendFrame(ZabWalOpCode.AddKnowledge, chunk);
                }

                // File size ~ 4 KB < 5 KB
                Assert.False(journal.ShouldCheckpoint());

                // Append 2 more frames -> size ~ 6 KB > 5 KB
                journal.AppendFrame(ZabWalOpCode.AddKnowledge, chunk);
                journal.AppendFrame(ZabWalOpCode.AddKnowledge, chunk);

                Assert.True(journal.ShouldCheckpoint(), "WAL size threshold did not trigger checkpoint request.");

                // Execute checkpoint
                journal.Checkpoint(() => { });

                Assert.Equal(0, journal.PendingFrames);
                Assert.False(journal.ShouldCheckpoint());
                var walInfo = new FileInfo(journal.WalFilePath);
                Assert.Equal(0, walInfo.Length);
            }
        }

        [Fact]
        public void ZabDatabase_Compact_PurgesWasteAndPreservesIntegrity()
        {
            string dbPath = Path.Combine(_tempDir, "compact_test.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                // Add 10 items
                for (int i = 0; i < 10; i++)
                {
                    db.AddKnowledge($"K_{i}", $"Initial_{i}", "General");
                }

                // Delete 5 items
                for (int i = 0; i < 5; i++)
                {
                    db.DeleteKnowledge($"K_{i}");
                }

                // Update remaining 5 items
                for (int i = 5; i < 10; i++)
                {
                    var rec = db.FindKnowledge($"K_{i}");
                    Assert.NotNull(rec);
                    db.UpdateKnowledge(rec!.Id, $"Updated_{i}");
                }

                // Execute Compact
                db.Compact();

                Assert.Equal(5, db.Knowledge.Count);
                Assert.Null(db.FindKnowledge("K_0"));
                Assert.NotNull(db.FindKnowledge("K_5"));
                Assert.Equal("Updated_5", db.FindKnowledge("K_5")!.Value);
            }

            // Re-open and verify persistence
            using (var reopened = ZabDatabase.Open(dbPath))
            {
                Assert.Equal(5, reopened.Knowledge.Count);
                Assert.Equal("Updated_5", reopened.FindKnowledge("K_5")!.Value);
            }
        }
    }
}
