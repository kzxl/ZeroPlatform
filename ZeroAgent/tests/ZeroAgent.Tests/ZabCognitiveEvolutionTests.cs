using System;
using System.IO;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroAgent.Core.Reasoning.Cognitive;
using ZeroAgent.Dialog.Memory;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class ZabCognitiveEvolutionTests : IDisposable
    {
        private readonly string _tempDir;

        public ZabCognitiveEvolutionTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "ZabCognitiveTests_" + Guid.NewGuid().ToString("N"));
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
        public void ZabSleepConsolidator_DistillsEpisodicToSystem1ReflexesAndReflexions()
        {
            string dbPath = Path.Combine(_tempDir, "sleep_test.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                // Initialize neural policy with action classes
                var policy = new ZabNeuralPolicy("ActionRouter", 128, new[] { "RestartConnectionPool", "Escalate" });
                db.SetNeuralPolicy(policy);

                var memoryEngine = new AgenticMemoryEngine(dimension: 128);

                // Incident 1: Repeated success (AccessCount = 3)
                float[] emb1 = new float[128];
                emb1[0] = 1.0f;
                var ep1 = memoryEngine.Episodic.Record("Database connection timeout", "RestartConnectionPool", emb1, success: true);
                ep1.AccessCount = 3;

                // Incident 2: Single success (AccessCount = 1) -> does not trigger reflex
                float[] emb2 = new float[128];
                emb2[1] = 1.0f;
                var ep2 = memoryEngine.Episodic.Record("Minor UI flicker", "RefreshWidget", emb2, success: true);
                ep2.AccessCount = 1;

                // Incident 3: Failure -> triggers reflexion
                float[] emb3 = new float[128];
                emb3[2] = 1.0f;
                var ep3 = memoryEngine.Episodic.Record("Unsafe direct schema drop", "Never bypass schema backup", emb3, success: false);

                // Run Sleep Consolidation
                var report = ZabSleepConsolidator.Consolidate(memoryEngine, db, new SleepConsolidationOptions
                {
                    MinAccessCountForReflex = 2,
                    AdaptationLearningRate = 0.1f
                });

                Assert.Equal(3, report.TotalEpisodesScanned);
                Assert.Equal(1, report.PlansCached);
                Assert.Equal(1, report.ReflexesSynthesized);
                Assert.Equal(1, report.ReflexionsGenerated);

                // Verify Plan Cache in ZabDatabase
                var cached = db.LookupPlan(emb1, minSimilarity: 0.85f);
                Assert.NotNull(cached);
                Assert.Equal("RestartConnectionPool", cached!.Solution);

                // Verify Reflexion in ZabDatabase
                var reflexions = db.RecallReflexions("schema drop", topK: 1);
                Assert.Single(reflexions);
                Assert.Contains("backup", reflexions[0].Lesson);
            }
        }

        [Fact]
        public void ZabCausalGraph_PredictsActionOutcomeAndForesightAccurately()
        {
            var causalGraph = new ZabCausalGraph();

            float[] stateEmb = new float[64];
            stateEmb[0] = 1.0f;
            stateEmb[1] = 0.8f;
            VectorMetrics.NormalizeL2(stateEmb);

            // Record past experiences
            causalGraph.RecordTransition("HighServerLoad", "KillWorker", "ServiceUnresponsive", reward: -0.90f, stateEmb);
            causalGraph.RecordTransition("HighServerLoad", "ScaleWorkerPool", "LoadNormalized", reward: +0.95f, stateEmb);

            // Query foresight for "KillWorker"
            var foresightKill = causalGraph.EvaluateAction(stateEmb, "KillWorker");
            Assert.True(foresightKill.HasPriorExperience);
            Assert.Equal("ServiceUnresponsive", foresightKill.PredictedOutcome);
            Assert.True(foresightKill.ExpectedReward < 0.0f);

            // Query foresight for "ScaleWorkerPool"
            var foresightScale = causalGraph.EvaluateAction(stateEmb, "ScaleWorkerPool");
            Assert.True(foresightScale.HasPriorExperience);
            Assert.Equal("LoadNormalized", foresightScale.PredictedOutcome);
            Assert.True(foresightScale.ExpectedReward > 0.80f);
        }

        [Fact]
        public void ZabMixtureOfReflexes_EvaluatesSpecializedMicroExpertsInMicroseconds()
        {
            const int dim = 64;

            // 1. Setup Routing Expert
            var routingPolicy = new ZabNeuralPolicy("RoutingExpert", dim, new[] { "check_inventory", "create_order" });
            // Bias towards check_inventory for dim 0
            routingPolicy.WeightsInt8[0] = 120; // class 0 ("check_inventory"), dim 0

            // 2. Setup Security Expert
            var securityPolicy = new ZabNeuralPolicy("SecurityExpert", dim, new[] { "Safe", "Risk" });
            // High risk weight for dim 5
            securityPolicy.WeightsInt8[dim + 5] = 125; // class 1 ("Risk"), dim 5

            var mor = new ZabMixtureOfReflexes(routingPolicy, securityPolicy);

            // Test Query 1: Safe request (stimulates dim 0)
            float[] safeVec = new float[dim];
            safeVec[0] = 1.0f;
            var safeResult = mor.Evaluate(safeVec);

            Assert.False(safeResult.IsBlockedBySecurity);
            Assert.Equal("check_inventory", safeResult.RoutedAction);
            Assert.True(safeResult.RoutingConfidence >= 0.50f);

            // Test Query 2: Hostile request (stimulates dim 5)
            float[] hostileVec = new float[dim];
            hostileVec[5] = 1.0f;
            var hostileResult = mor.Evaluate(hostileVec);

            Assert.True(hostileResult.IsBlockedBySecurity);
            Assert.True(hostileResult.SecurityRiskScore >= 0.80f);
        }

        [Fact]
        public void ZabMixtureOfReflexes_SupportsPlugAndPlayDomainExperts_ForModularMoe()
        {
            const int dim = 32;
            var mor = new ZabMixtureOfReflexes();

            // 1. Create 2 Domain Experts (Inventory vs Finance)
            float[] invCentroid = new float[dim];
            invCentroid[0] = 1.0f; // Dimension 0 corresponds to Inventory concepts

            float[] finCentroid = new float[dim];
            finCentroid[1] = 1.0f; // Dimension 1 corresponds to Finance concepts

            var invExpert = new DomainExpertModule(
                domainId: "inventory",
                displayName: "Kho & Vật tư",
                description: "Quản lý phiếu nhập xuất, tồn kho, vị trí kệ",
                handledIntents: new[] { "CHECK_STOCK", "CREATE_GRN" },
                centroidEmbedding: invCentroid);

            var finExpert = new DomainExpertModule(
                domainId: "finance",
                displayName: "Tài chính & Công nợ",
                description: "Quản lý hóa đơn, công nợ phải thu, sổ cái",
                handledIntents: new[] { "QUERY_DEBT", "APPROVE_PAYMENT" },
                centroidEmbedding: finCentroid);

            // 2. Register experts into MoR
            mor.RegisterDomainExpert(invExpert);
            mor.RegisterDomainExpert(finExpert);

            Assert.Equal(2, mor.DomainExperts.Count);

            // 3. Query with Inventory Vector
            float[] queryInv = new float[dim];
            queryInv[0] = 0.95f; queryInv[2] = 0.05f;

            var resultInv = mor.Evaluate(queryInv);
            Assert.Equal("inventory", resultInv.MatchedDomainId);
            Assert.True(resultInv.DomainAffinityScore > 0.90f);
            Assert.NotNull(resultInv.MatchedExpert);
            Assert.Equal("Kho & Vật tư", resultInv.MatchedExpert!.DisplayName);

            // 4. Query with Finance Vector
            float[] queryFin = new float[dim];
            queryFin[1] = 0.98f;

            var resultFin = mor.Evaluate(queryFin);
            Assert.Equal("finance", resultFin.MatchedDomainId);
            Assert.True(resultFin.DomainAffinityScore > 0.95f);

            // 5. Plug-and-Play: Dynamically add a third expert (Production) at runtime
            float[] prodCentroid = new float[dim];
            prodCentroid[2] = 1.0f;

            var prodExpert = new DomainExpertModule(
                domainId: "production",
                displayName: "Lệnh Sản xuất & BOM",
                description: "Quản lý công đoạn, định mức tiêu hao, lệnh gia công",
                handledIntents: new[] { "CHECK_BOM", "CREATE_WORK_ORDER" },
                centroidEmbedding: prodCentroid);

            mor.RegisterDomainExpert(prodExpert);
            Assert.Equal(3, mor.DomainExperts.Count);

            float[] queryProd = new float[dim];
            queryProd[2] = 1.0f;

            var resultProd = mor.Evaluate(queryProd);
            Assert.Equal("production", resultProd.MatchedDomainId);
            Assert.True(resultProd.DomainAffinityScore > 0.95f);

            // 6. Dynamically unregister an expert
            bool removed = mor.UnregisterDomainExpert("finance");
            Assert.True(removed);
            Assert.Equal(2, mor.DomainExperts.Count);
            Assert.False(mor.TryGetDomainExpert("finance", out _));
        }
    }
}
