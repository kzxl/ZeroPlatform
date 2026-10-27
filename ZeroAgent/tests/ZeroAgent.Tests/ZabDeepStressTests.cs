using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroAgent.Core.Reasoning.System1;
using ZeroAgent.Dialog.Memory;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class ZabDeepStressTests : IDisposable
    {
        private readonly string _tempDir;

        public ZabDeepStressTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZabDeepStress_" + Guid.NewGuid().ToString("N"));
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
        public async Task ZabDatabase_Stress_ConcurrentMultiReaderMultiWriter_NoDeadlockOrCorruption()
        {
            string dbPath = Path.Combine(_tempDir, "concurrent_stress.zab");
            int dim = 16;

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                // Seed initial data
                for (int i = 0; i < 20; i++)
                {
                    float[] v = new float[dim];
                    v[i % dim] = 1.0f;
                    VectorMetrics.NormalizeL2(v);
                    db.StoreVector($"seed_vec_{i}", v);
                    db.AddKnowledge($"seed_key_{i}", $"val_{i}");
                }
                db.Commit();
            }

            // Launch concurrent stress test: 12 Reader tasks + 4 Writer tasks
            int totalOpsPerWorker = 50;
            var exceptions = new List<Exception>();
            var lockObj = new object();

            using (var db = ZabDatabase.Open(dbPath))
            {
                // Configure WAL auto checkpointing
                db.Wal.AutoCheckpointThreshold = 20;

                var tasks = new List<Task>();

                // 4 Writers
                for (int w = 0; w < 4; w++)
                {
                    int writerId = w;
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            for (int i = 0; i < totalOpsPerWorker; i++)
                            {
                                int keyId = writerId * 1000 + i;
                                db.AddKnowledge($"dyn_key_{keyId}", $"dyn_val_{keyId}");

                                float[] vec = new float[dim];
                                vec[i % dim] = 1.0f;
                                VectorMetrics.NormalizeL2(vec);
                                db.StoreVector($"dyn_vec_{keyId}", vec);

                                if (i % 15 == 0)
                                {
                                    db.CachePlan($"Plan {keyId}", $"Solution {keyId}", 2, 0.95f, vec);
                                }

                                if (i % 25 == 0)
                                {
                                    db.Checkpoint();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj) exceptions.Add(ex);
                        }
                    }));
                }

                // 12 Readers
                for (int r = 0; r < 12; r++)
                {
                    int readerId = r;
                    tasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            float[] q = new float[dim];
                            q[readerId % dim] = 1.0f;
                            VectorMetrics.NormalizeL2(q);

                            for (int i = 0; i < totalOpsPerWorker; i++)
                            {
                                // Concurrent vector search
                                var vMatches = db.SearchVectors(q, topK: 3, minSimilarity: 0.1f);

                                // Concurrent O(1) key lookup
                                var kMatch = db.FindKnowledge($"seed_key_{i % 20}");

                                // Concurrent plan lookup
                                var planMatch = db.LookupPlan(q, minSimilarity: 0.1f);

                                Thread.Sleep(1);
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (lockObj) exceptions.Add(ex);
                        }
                    }));
                }

                await Task.WhenAll(tasks);
                Assert.Empty(exceptions);

                // Commit and verify integrity
                db.Commit();
                Assert.True(db.Knowledge.Count >= 20 + 4 * totalOpsPerWorker);
                Assert.True(db.Vectors.Count >= 20 + 4 * totalOpsPerWorker);
            }

            // Reopen and ensure file integrity passes CRC32C verification
            using (var dbReopened = ZabDatabase.Open(dbPath))
            {
                Assert.True(dbReopened.Knowledge.Count >= 20 + 4 * totalOpsPerWorker);
            }
        }

        [Fact]
        public void ZabDatabase_Stress_MultiTornWriteResilience_TruncatesSafely()
        {
            string dbPath = Path.Combine(_tempDir, "multi_torn_write.zab");
            string walPath = dbPath + "-wal";

            // Stage 1: Write baseline data with AutoCheckpointOnDispose = false to preserve WAL frames
            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.AutoCheckpointOnDispose = false;
                db.Wal.AutoCheckpointThreshold = 100000;

                for (int i = 0; i < 50; i++)
                {
                    db.AddKnowledge($"key_{i}", $"val_{i}");
                }
            }

            Assert.True(File.Exists(walPath));
            long cleanWalLength = new FileInfo(walPath).Length;
            Assert.True(cleanWalLength > 0);

            // Simulate 3 successive brutal power outages corrupting the WAL tail
            // Outage 1: Appending a torn header (only 5 bytes of the 16-byte frame header)
            byte[] torn1 = new byte[] { 0x5A, 0x57, 0x41, 0x4C, 0x01 };
            using (var fs = new FileStream(walPath, FileMode.Append, FileAccess.Write))
            {
                fs.Write(torn1, 0, torn1.Length);
            }

            // Reopen: Database must cleanly truncate back to cleanWalLength and restore all 50 items
            using (var db1 = ZabDatabase.Open(dbPath))
            {
                db1.AutoCheckpointOnDispose = false;
                Assert.Equal(50, db1.Knowledge.Count);
            }
            Assert.Equal(cleanWalLength, new FileInfo(walPath).Length);

            // Outage 2: Appending a valid frame header but truncated payload (declared 64 bytes, only 10 written)
            byte[] torn2 = new byte[]
            {
                0x5A, 0x57, 0x41, 0x4C, // Magic "ZWAL"
                0x01,                   // OpCode AddKnowledge
                0x00, 0x00, 0x00, 0x40, // Length = 64 bytes
                0x12, 0x34, 0x56, 0x78, // Checksum
                0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x01, 0x02, 0x03, 0x04 // Only 10 bytes written before crash!
            };
            using (var fs = new FileStream(walPath, FileMode.Append, FileAccess.Write))
            {
                fs.Write(torn2, 0, torn2.Length);
            }

            using (var db2 = ZabDatabase.Open(dbPath))
            {
                db2.AutoCheckpointOnDispose = false;
                Assert.Equal(50, db2.Knowledge.Count);
            }
            Assert.Equal(cleanWalLength, new FileInfo(walPath).Length);

            // Outage 3: Full frame written but corrupted payload bit (CRC32C mismatch)
            byte[] torn3 = new byte[]
            {
                0x5A, 0x57, 0x41, 0x4C,
                0x01,
                0x00, 0x00, 0x00, 0x04, // Length = 4 bytes
                0xDE, 0xAD, 0xBE, 0xEF, // Incorrect CRC32C
                0x01, 0x02, 0x03, 0x04
            };
            using (var fs = new FileStream(walPath, FileMode.Append, FileAccess.Write))
            {
                fs.Write(torn3, 0, torn3.Length);
            }

            using (var db3 = ZabDatabase.Open(dbPath))
            {
                db3.AutoCheckpointOnDispose = false;
                Assert.Equal(50, db3.Knowledge.Count);
            }
            Assert.Equal(cleanWalLength, new FileInfo(walPath).Length);
        }

        [Fact]
        public void ZabDatabase_Stress_LargeScaleVectorSearchFidelity()
        {
            string dbPath = Path.Combine(_tempDir, "large_scale_vectors.zab");
            int dim = 64;
            int numVectors = 128;

            // Generate synthetic dataset
            var rnd = new Random(42);
            float[][] dataset = new float[numVectors][];
            for (int i = 0; i < numVectors; i++)
            {
                dataset[i] = new float[dim];
                for (int d = 0; d < dim; d++)
                {
                    dataset[i][d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                }
                VectorMetrics.NormalizeL2(dataset[i]);
            }

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                for (int i = 0; i < numVectors; i++)
                {
                    db.StoreVector($"Item_{i:D4}", dataset[i]);
                }
                db.Commit();

                // Test fidelity for 20 query probes
                for (int q = 0; q < 20; q++)
                {
                    int targetIdx = q * 6;
                    float[] query = (float[])dataset[targetIdx].Clone();

                    // Search using TwoStageVectorIndex accelerator inside ZabDatabase
                    var results = db.SearchVectors(query, topK: 1, minSimilarity: 0.80f);
                    Assert.Single(results);
                    Assert.Equal($"Item_{targetIdx:D4}", results[0].Record.Label);
                    Assert.True(results[0].Similarity > 0.95f, $"Similarity was {results[0].Similarity}");
                }
            }
        }

        [Fact]
        public void ZabMemoryStorageBridge_Stress_EbbinghausDecayLifecycle()
        {
            string dbPath = Path.Combine(_tempDir, "ebbinghaus_lifecycle.zab");

            var engine = new AgenticMemoryEngine(dimension: 128);

            // Record 3 incidents with different initial half lives
            float[] vec1 = engine.Embedder.Embed("Coolant pump failure code P101");
            float[] vec2 = engine.Embedder.Embed("Vibration sensor anomaly axis X");
            float[] vec3 = engine.Embedder.Embed("Emergency stop triggered line 4");

            var ep1 = engine.Episodic.Record("Coolant pump failure P101", "Replaced impeller", vec1, halfLifeHours: 24.0);
            var ep2 = engine.Episodic.Record("Vibration anomaly X", "Tightened bearing bolts", vec2, halfLifeHours: 48.0);
            var ep3 = engine.Episodic.Record("Emergency stop line 4", "Reset safety relay", vec3, halfLifeHours: 12.0);

            // Access ep1 multiple times to trigger frequency reinforcement
            ep1.AccessCount = 10;

            // Persist to ZabDatabase
            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                ZabMemoryStorageBridge.PersistToZab(engine, db);
            }

            // Hydrate into new engine
            var hydratedEngine = new AgenticMemoryEngine(dimension: 128);
            using (var dbReopened = ZabDatabase.Open(dbPath))
            {
                ZabMemoryStorageBridge.HydrateFromZab(hydratedEngine, dbReopened);
                Assert.Equal(3, hydratedEngine.Episodic.Count);

                var epList = hydratedEngine.Episodic.GetAllEpisodes();
                var loadedEp1 = epList.First(e => e.Issue.Contains("Coolant pump"));
                var loadedEp2 = epList.First(e => e.Issue.Contains("Vibration"));
                var loadedEp3 = epList.First(e => e.Issue.Contains("Emergency stop"));

                // Verify frequency reinforcement persisted
                Assert.Equal(10, loadedEp1.AccessCount);
                Assert.Equal(24.0, loadedEp1.HalfLifeHours);

                // Compute retention at T + 36 hours
                DateTime futureTime = loadedEp1.TimestampUtc.AddHours(36);
                float ret1 = loadedEp1.ComputeRetention(futureTime);
                float ret2 = loadedEp2.ComputeRetention(futureTime);
                float ret3 = loadedEp3.ComputeRetention(futureTime);

                // ep1 has frequency boost (accessCount=10) so its retention remains high despite 36h
                Assert.True(ret1 > ret3, $"Expected reinforced episode retention ({ret1}) > short half-life episode ({ret3})");
            }
        }

        [Fact]
        public void System1_Stress_WeightAdaptationBoundaryAndConvergence()
        {
            int inputDim = 8;
            int numClasses = 3;
            var classNames = new[] { "ActionA", "ActionB", "ActionC" };

            float[,] initialWeights = new float[numClasses, inputDim];
            // Initially biased towards ActionA
            for (int d = 0; d < inputDim; d++) initialWeights[0, d] = 2.0f;

            var policy = new ZabNeuralPolicy("StressPolicy", inputDim, classNames);
            policy.SetWeightsFp32(initialWeights);

            float[] inputSample = new float[inputDim];
            for (int d = 0; d < inputDim; d++) inputSample[d] = 1.0f;
            VectorMetrics.NormalizeL2(inputSample);

            // Initial prediction must be ActionA
            string initial = policy.PredictChoice(inputSample, out _);
            Assert.Equal("ActionA", initial);

            // Execute 60 continuous online adaptation steps towards ActionC
            for (int step = 0; step < 60; step++)
            {
                policy.AdaptWeights(inputSample, "ActionC", learningRate: 0.08f);

                // S8 boundary check: weights must strictly stay within [-127, 127]
                for (int i = 0; i < policy.WeightsInt8.Length; i++)
                {
                    sbyte w = policy.WeightsInt8[i];
                    Assert.True(w >= -127 && w <= 127, $"Weight at {i} violated INT8 bounds: {w}");
                }
            }

            // Check that policy converged to ActionC with high confidence
            string adapted = policy.PredictChoice(inputSample, out float conf);
            Assert.Equal("ActionC", adapted);
            Assert.True(conf > 0.85f, $"Expected high confidence for ActionC but got {conf}");
        }
    }
}
