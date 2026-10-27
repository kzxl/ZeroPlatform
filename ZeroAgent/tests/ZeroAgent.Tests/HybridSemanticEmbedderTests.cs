using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Embedding;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    /// <summary>
    /// Test suite for HybridSemanticEmbedder.
    /// Verifies dual-channel lexical-neural fusion, synonym cluster anchoring,
    /// online continual contrastive learning, binary serialization, and memory integration.
    /// </summary>
    public class HybridSemanticEmbedderTests
    {
        // =========================================================================
        // TEST 1: Unit Norm & Vector Dimensions
        // =========================================================================
        [Fact]
        public void HybridEmbedder_ProducesNormalizedVectorsOfSpecifiedDimension()
        {
            var embedder = new HybridSemanticEmbedder(dimension: 128);
            var vec = embedder.Embed("Kiểm tra áp suất máy nén khí C-102");

            Assert.Equal(128, vec.Length);

            // Compute L2 norm: sqrt(sum(v_i^2))
            double normSq = 0;
            for (int i = 0; i < vec.Length; i++) normSq += vec[i] * vec[i];
            double norm = Math.Sqrt(normSq);

            Assert.InRange(norm, 0.999, 1.001);
        }

        // =========================================================================
        // TEST 2: Synonym Cluster Anchoring Bridges Lexical Mismatch
        // =========================================================================
        [Fact]
        public void HybridEmbedder_SynonymClustering_BridgesLexicalMismatch()
        {
            var embedder = new HybridSemanticEmbedder(dimension: 128);

            // "động cơ" and "motor" are synonyms with completely different spelling
            var vecDongCo = embedder.Embed("động cơ");
            var vecMotor = embedder.Embed("motor");

            float sim = VectorMetrics.CosineSimilarity(vecDongCo, vecMotor);

            // Assert: Because they are anchored via AddSynonymGroup, cosine similarity is high
            Assert.True(sim >= 0.70f, $"Expected similarity >= 0.70 between synonyms, got {sim}");
        }

        // =========================================================================
        // TEST 3: Online Continual Learning with Contrastive Updates
        // =========================================================================
        [Fact]
        public void HybridEmbedder_OnlineLearnPair_IncreasesSimilarityForPositivePairs()
        {
            var embedder = new HybridSemanticEmbedder(dimension: 128);

            string phraseA = "van giảm áp";
            string phraseB = "bộ điều áp khí nén";

            float initialSim = VectorMetrics.CosineSimilarity(embedder.Embed(phraseA), embedder.Embed(phraseB));

            // Perform 3 online learning steps
            for (int i = 0; i < 3; i++)
            {
                embedder.OnlineLearnPair(phraseA, phraseB, isSimilar: true, learningRate: 0.15f);
            }

            float updatedSim = VectorMetrics.CosineSimilarity(embedder.Embed(phraseA), embedder.Embed(phraseB));

            // Assert: Similarity must increase after positive contrastive feedback
            Assert.True(updatedSim > initialSim, $"Updated similarity ({updatedSim}) should be higher than initial ({initialSim})");
        }

        // =========================================================================
        // TEST 4: Binary Model Serialization & Deserialization
        // =========================================================================
        [Fact]
        public void HybridEmbedder_Serialization_PreservesCodebookAndWeights()
        {
            var embedderOriginal = new HybridSemanticEmbedder(dimension: 128);
            embedderOriginal.AddSynonymGroup("băng tải", "conveyor", "dây chuyền chuyển hàng");
            embedderOriginal.OnlineLearnPair("robot hàn", "máy hàn tự động", isSimilar: true);

            var originalVec = embedderOriginal.Embed("robot hàn");

            // Serialize to stream
            using var ms = new MemoryStream();
            embedderOriginal.Save(ms);

            // Deserialize into new instance
            ms.Position = 0;
            var embedderRestored = new HybridSemanticEmbedder(dimension: 128);
            embedderRestored.Load(ms);

            var restoredVec = embedderRestored.Embed("robot hàn");

            // Assert: Restored embedding must match exactly
            float sim = VectorMetrics.CosineSimilarity(originalVec, restoredVec);
            Assert.InRange(sim, 0.999f, 1.001f);
        }

        // =========================================================================
        // TEST 5: Integration with AgenticMemoryEngine & ZeroDialogEngine
        // =========================================================================
        [Fact]
        public async Task HybridEmbedder_IntegratesSeamlesslyWithDialogEngine()
        {
            var memory = new AgenticMemoryEngine(dimension: 128);
            Assert.IsType<HybridSemanticEmbedder>(memory.Embedder);

            var engine = IndustrialDialogFactory.CreateIndustrialBot();

            // Query using synonyms that rely on the hybrid embedder
            var res = await engine.ChatAsync("test_session_embedder", "Kiểm tra nhiệt máy CNC-01");

            Assert.Equal(SessionState.Completed, res.State);
            Assert.Contains("CNC-01", res.Text);
        }
    }
}
