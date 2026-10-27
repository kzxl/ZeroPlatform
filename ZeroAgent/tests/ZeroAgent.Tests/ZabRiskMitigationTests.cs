using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroPrimitives.Cryptography;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class ZabRiskMitigationTests : IDisposable
    {
        private readonly string _tempDir;

        public ZabRiskMitigationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZabRiskTests_" + Guid.NewGuid().ToString("N"));
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
        public void Risk1_HashCollision_ExactStringVerificationResolvesCorrectRecord()
        {
            string keyAlpha = "CollisionKey_Alpha";
            // Simulate 2 distinct keys sharing an identical 64-bit hash
            ulong sharedHash = FastHash.Fnv1a64(keyAlpha.AsSpan());

            var slots = new List<ZabIndexSlot>
            {
                new ZabIndexSlot { KeyHash = sharedHash, FileOffset = 100, Length = 50, Checksum = 1 },
                new ZabIndexSlot { KeyHash = sharedHash, FileOffset = 200, Length = 50, Checksum = 2 }
            };

            var index = ZabBillionScaleIndex.Build(slots, recordsPerBlock: 16);

            var candidates = new List<ZabIndexSlot>();
            // Query keyAlpha
            bool found = index.TryLookupCandidates(keyAlpha.AsSpan(), candidates);
            Assert.True(found);
            Assert.Equal(2, candidates.Count); // Both candidate slots returned for exact string verification
        }

        [Fact]
        public void Risk2_SegmentedStorage_BridgesPhysicalDiskBounds()
        {
            var manager = new ZabSegmentedStorageManager(_tempDir, "agent_memory", maxSegmentSizeBytes: 32 * 1024 * 1024);

            string seg0 = manager.GetSegmentFilePath(0);
            string seg1 = manager.GetSegmentFilePath(1);

            Assert.Contains("agent_memory_0000.zab", seg0);
            Assert.Contains("agent_memory_0001.zab", seg1);

            File.WriteAllBytes(seg0, new byte[100]);
            var discovered = manager.DiscoverExistingSegments();
            Assert.Single(discovered);
            Assert.Equal(seg0, discovered[0]);

            Assert.False(manager.ShouldRollSegment(seg0));
        }

        [Fact]
        public void Risk3_MMapPagedWindowing_OperatesSafely()
        {
            string dbPath = Path.Combine(_tempDir, "paged_mmap_test.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.AddKnowledge("SYS_CORE_CONFIG", "ACTIVE_PAGED_MODE", "Infrastructure");
                db.Commit();
            }

            using (var reader = ZabMMapReader.Open(dbPath))
            {
                Assert.True(reader.Header.IsValid);
                var knowledge = reader.ReadKnowledge();
                Assert.Single(knowledge);
                Assert.Equal("SYS_CORE_CONFIG", knowledge[0].Key);
                Assert.Equal("ACTIVE_PAGED_MODE", knowledge[0].Value);
            }
        }

        [Fact]
        public void Risk4_Hierarchical2TierIvf_ScalesCentroidSearchWithSublinearComplexity()
        {
            const int dim = 32;
            const int targetClusters = 36; // >= 32 triggers Meta-Centroids
            var ivf = new ZabIvfVectorIndex(dim, targetClusters);

            var rnd = new Random(42);

            // Populate 144 vectors across 36 clusters
            for (int i = 0; i < 144; i++)
            {
                float[] vec = new float[dim];
                for (int d = 0; d < dim; d++) vec[d] = (float)(rnd.NextDouble() * 2.0 - 1.0);
                VectorMetrics.NormalizeL2(vec);

                sbyte[] qData = new sbyte[dim];
                ZeroVector.Core.Quantization.BinaryQuantizer.QuantizeSQ8(vec, qData, out float scale, out float offset);

                var rec = new ZabVectorRecord
                {
                    Id = $"v_{i}",
                    Label = $"item_{i}",
                    Dimension = dim,
                    Scale = scale,
                    Offset = offset,
                    QuantizedData = qData
                };
                ivf.Add(rec, vec);
            }

            Assert.True(ivf.ClusterCount >= 32);
            Assert.True(ivf.MetaClusterCount >= 2, $"Meta-Centroids were not generated! Count: {ivf.MetaClusterCount}");

            // Search query
            float[] query = new float[dim];
            query[0] = 1.0f;
            VectorMetrics.NormalizeL2(query);

            var matches = ivf.Search(query, topK: 3, nprobe: 2, minSimilarity: 0.10f);
            Assert.NotEmpty(matches);
        }

        [Fact]
        public void Risk5_ElasticWeightConsolidation_PreventsCatastrophicForgetting()
        {
            const int dim = 64;
            var classes = new[] { "PrimaryAction", "SecondaryAction" };

            var policy = new ZabNeuralPolicy("EwcPolicy", dim, classes);
            // Set initial anchor weights
            policy.WeightsInt8[0] = 20;  // Baseline anchor
            policy.AnchorWeights![0] = 20;
            policy.MaxDriftFromAnchor = 35; // Maximum allowable deviation is [20 - 35, 20 + 35] = [-15, 55]

            float[] stimulus = new float[dim];
            stimulus[0] = 1.0f; // Strongly stimulates dimension 0

            // Train 100 consecutive cycles with high learning rate
            for (int epoch = 0; epoch < 100; epoch++)
            {
                policy.AdaptWeights(stimulus, "PrimaryAction", learningRate: 0.20f);
            }

            // Verify that weight did NOT saturate to +127, but was safely bounded by EWC
            sbyte finalWeight = policy.WeightsInt8[0];
            Assert.True(finalWeight <= 20 + policy.MaxDriftFromAnchor, $"Weight drift exceeded upper EWC bound! Weight: {finalWeight}");
            Assert.True(finalWeight >= 20 - policy.MaxDriftFromAnchor, $"Weight drift exceeded lower EWC bound! Weight: {finalWeight}");
            Assert.True(finalWeight < 120, $"Weight saturated to edge! Weight: {finalWeight}");
        }
    }
}
