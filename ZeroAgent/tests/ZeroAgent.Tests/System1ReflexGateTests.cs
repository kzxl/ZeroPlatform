using System;
using System.IO;
using Xunit;
using ZeroAgent.Core.Database;
using ZeroAgent.Core.Reasoning.System1;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class System1ReflexGateTests : IDisposable
    {
        private readonly string _tempDir;

        public System1ReflexGateTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "System1Tests_" + Guid.NewGuid().ToString("N"));
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

        private ZabNeuralPolicy CreateTestPolicy()
        {
            int inputDim = 4;
            int numClasses = 4;
            string[] classNames = new[] { "query_database", "call_modbus", "Risk", "deep_think" };

            float[,] fp32Weights = new float[numClasses, inputDim];
            // Class 0: query_database -> responds to feature 0
            fp32Weights[0, 0] = 5.0f;
            // Class 1: call_modbus -> responds to feature 1
            fp32Weights[1, 1] = 5.0f;
            // Class 2: Risk -> responds strongly to feature 2
            fp32Weights[2, 2] = 8.0f;
            // Class 3: deep_think -> responds to feature 3
            fp32Weights[3, 3] = 6.0f;

            float[] biases = new float[] { 0.0f, 0.0f, -1.0f, 0.0f };

            var policy = new ZabNeuralPolicy("System1Policy", inputDim, classNames);
            policy.SetWeightsFp32(fp32Weights, biases);
            return policy;
        }

        [Fact]
        public void System1_TriPrimitives_ChoiceScoreBinary_EvaluatesCorrectly()
        {
            string dbPath = Path.Combine(_tempDir, "primitives.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.SetNeuralPolicy(CreateTestPolicy());

                // Primitive 1: Choice
                float[] inputQuery = new float[] { 1.0f, 0.0f, 0.0f, 0.0f };
                string? choice = db.PredictChoice(inputQuery, out float confidence);
                Assert.Equal("query_database", choice);
                Assert.True(confidence > 0.8f);

                // Primitive 2: Score (Continuous [0.0, 1.0])
                float[] inputRisk = new float[] { 0.0f, 0.0f, 1.0f, 0.0f };
                float riskScore = db.PredictScore(inputRisk, "Risk");
                Assert.True(riskScore > 0.90f, $"Expected high risk score but got {riskScore}");

                float[] inputSafe = new float[] { 1.0f, 0.0f, 0.0f, 0.0f };
                float safeRisk = db.PredictScore(inputSafe, "Risk");
                Assert.True(safeRisk < 0.35f, $"Expected low risk score but got {safeRisk}");

                // Primitive 3: Binary Gatekeeper
                bool isUnsafe = db.PredictBinary(inputRisk, "Risk", threshold: 0.80f);
                Assert.True(isUnsafe);

                bool isSafeOp = db.PredictBinary(inputSafe, "Risk", threshold: 0.80f);
                Assert.False(isSafeOp);
            }
        }

        [Fact]
        public void System1_OnlineWeightAdaptation_LearnsFromFeedback()
        {
            string dbPath = Path.Combine(_tempDir, "online_adapt.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.SetNeuralPolicy(CreateTestPolicy());

                // Input ambiguous between class 0 (query_database) and class 1 (call_modbus)
                float[] ambigInput = new float[] { 1.0f, 1.2f, 0.0f, 0.0f };
                string? initialChoice = db.PredictChoice(ambigInput, out _);
                Assert.Equal("call_modbus", initialChoice);

                // System 2 verified that this specific query should be query_database
                // Perform online adaptation multiple steps
                for (int i = 0; i < 5; i++)
                {
                    db.AdaptNeuralWeights(ambigInput, "query_database", learningRate: 0.1f);
                }

                // Check that policy adapted to classify as query_database
                string? adaptedChoice = db.PredictChoice(ambigInput, out _);
                Assert.Equal("query_database", adaptedChoice);
            }
        }

        [Fact]
        public void System1ReflexGate_BlocksHighRiskQueries()
        {
            string dbPath = Path.Combine(_tempDir, "gate_risk.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.SetNeuralPolicy(CreateTestPolicy());

                var gate = new System1ReflexGate(db, riskBlockThreshold: 0.80f);

                // Embedding with strong feature 2 (Risk)
                float[] riskEmbedding = new float[] { 0.0f, 0.0f, 1.0f, 0.0f };
                var result = gate.EvaluateReflex("DROP TABLE erpmds_users;", riskEmbedding);

                Assert.Equal(System1DecisionType.BlockedByGuardrail, result.Decision);
                Assert.True(result.RiskScore >= 0.80f);
                Assert.Contains("high-risk", result.Reason);
            }
        }

        [Fact]
        public void System1ReflexGate_ReplaysCachedPlan_BypassesLLM()
        {
            string dbPath = Path.Combine(_tempDir, "gate_replay.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.SetNeuralPolicy(CreateTestPolicy());

                float[] planVec = new float[] { 0.8f, 0.2f, 0.0f, 0.0f };
                VectorMetrics.NormalizeL2(planVec);

                db.CachePlan(
                    goal: "Check spindle oil level",
                    solution: "Step 1: Read sensor REG4001. Step 2: Value is 85%. All OK.",
                    stepsCount: 2,
                    confidence: 0.98f,
                    goalVector: planVec);

                var gate = new System1ReflexGate(db, planCacheThreshold: 0.85f);

                // Query with matching embedding
                var result = gate.EvaluateReflex("How to check spindle oil?", planVec);

                Assert.Equal(System1DecisionType.DirectPlanReplay, result.Decision);
                Assert.NotNull(result.CachedPlan);
                Assert.Contains("Read sensor REG4001", result.CachedPlan.Solution);
                Assert.Equal(1, result.CachedPlan.HitCount);
            }
        }

        [Fact]
        public void System1ReflexGate_DispatchesConfidentTools_And_DefersUncertain()
        {
            string dbPath = Path.Combine(_tempDir, "gate_dispatch.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                db.SetNeuralPolicy(CreateTestPolicy());

                var gate = new System1ReflexGate(db, actionConfidenceThreshold: 0.80f);

                // Strong tool feature (feature 1 -> call_modbus)
                float[] modbusVec = new float[] { 0.0f, 1.0f, 0.0f, 0.0f };
                var resDispatch = gate.EvaluateReflex("Read temperature coil", modbusVec);

                Assert.Equal(System1DecisionType.DirectToolDispatch, resDispatch.Decision);
                Assert.Equal("call_modbus", resDispatch.Action);

                // Explicit deep reasoning feature (feature 3 -> deep_think)
                float[] thinkVec = new float[] { 0.0f, 0.0f, 0.0f, 1.0f };
                var resDefer = gate.EvaluateReflex("Analyze complex root cause", thinkVec);

                Assert.Equal(System1DecisionType.DeferToSystem2, resDefer.Decision);
                Assert.Contains("deep_think", resDefer.Reason);
            }
        }

        [Fact]
        public void System1ReflexGate_DistillSuccess_EnablesFutureDirectReplay()
        {
            string dbPath = Path.Combine(_tempDir, "gate_distill.zab");

            using (var db = ZabDatabase.CreateNew(dbPath))
            {
                var gate = new System1ReflexGate(db, planCacheThreshold: 0.85f);

                float[] queryVec = new float[] { 0.5f, 0.5f, 0.0f, 0.0f };
                VectorMetrics.NormalizeL2(queryVec);

                // First time: Empty database -> must defer to System 2
                var firstAttempt = gate.EvaluateReflex("Solve anomaly on line 2", queryVec);
                Assert.Equal(System1DecisionType.DeferToSystem2, firstAttempt.Decision);

                // System 2 resolves the problem successfully -> distill into System 1
                gate.DistillSuccess(
                    goal: "Solve anomaly on line 2",
                    solution: "Restart hydraulic pump and calibrate pressure valve.",
                    stepsCount: 3,
                    goalEmbedding: queryVec,
                    verifiedAction: "query_database",
                    confidence: 0.96f);

                // Second time: Same query -> System 1 immediately intercepts and replays cached plan with 0 tokens!
                var secondAttempt = gate.EvaluateReflex("Solve anomaly on line 2", queryVec);
                Assert.Equal(System1DecisionType.DirectPlanReplay, secondAttempt.Decision);
                Assert.NotNull(secondAttempt.CachedPlan);
                Assert.Contains("Restart hydraulic pump", secondAttempt.CachedPlan.Solution);
            }
        }
    }
}
