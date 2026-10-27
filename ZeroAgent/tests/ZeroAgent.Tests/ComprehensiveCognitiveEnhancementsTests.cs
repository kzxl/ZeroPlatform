using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Reasoning.Cache;
using ZeroAgent.Core.Reasoning.GraphOfThought;
using ZeroAgent.Core.Reasoning.Reflexion;
using ZeroAgent.Core.Swarm.Debate;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.Embedding;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Tests
{
    public class ComprehensiveCognitiveEnhancementsTests
    {
        // Simple in-memory mock for ILlmClient
        private sealed class MockLlmClient : ILlmClient
        {
            private readonly Func<string, string> _handler;

            public MockLlmClient(Func<string, string>? handler = null)
            {
                _handler = handler ?? (prompt => "Mock LLM output response.");
            }

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_handler(prompt));
            }
        }

        // =========================================================================
        // 1. REFLEXION ENGINE & EPISODIC REPLAY TESTS
        // =========================================================================

        [Fact]
        public async Task ReflexionEngine_DistillsFailureAndStoresEpisode()
        {
            var memory = new InMemoryReflexionMemory();
            var engine = new ReflexionEngine(memory);

            var context = new AgentContext("Truy van nhiet do truc chinh spindle CNC");
            var failedTrace = new List<AgentMessage>
            {
                new AgentMessage(AgentRole.User, "Truy van nhiet do truc chinh spindle CNC"),
                new AgentMessage(AgentRole.Assistant, "Thought: Query table sensor_temperature\nAction: query_db({\"table\":\"sensor_temperature\"})"),
                new AgentMessage(AgentRole.System, "Error: Table 'sensor_temperature' not found in schema. Available: 'spindle_telemetry'.")
            };

            var failedResponse = new AgentResponse(
                success: false,
                output: "Execution failed due to missing table",
                totalSteps: 1,
                elapsed: TimeSpan.FromMilliseconds(200),
                trace: failedTrace);

            var mockLlm = new MockLlmClient(prompt =>
                "Root Cause: Queried non-existent table 'sensor_temperature'\nLesson: Check schema first, use 'spindle_telemetry' table instead.");

            var episode = await engine.ReflectAndRememberAsync(context, failedResponse, mockLlm);

            Assert.NotNull(episode);
            Assert.Contains("sensor_temperature", episode.FailureReason);
            Assert.Contains("spindle_telemetry", episode.SelfReflection);

            // Verify episode recalled by goal
            var recalled = memory.Recall("spindle CNC nhiet do", topK: 1);
            Assert.Single(recalled);
            Assert.Equal(context.Goal, recalled[0].Goal);
        }

        [Fact]
        public void ReflexionEngine_InjectsRelevantLessonsIntoContext()
        {
            var memory = new InMemoryReflexionMemory();
            var engine = new ReflexionEngine(memory);

            memory.Remember(new ReflexionEpisode(
                goal: "Ket noi toi cong Modbus TCP PLC",
                failureReason: "Attempted port 80 HTTP instead of 502",
                selfReflection: "Modbus TCP server requires port 502 with UnitId 1."));

            var context = new AgentContext("Ket noi toi cong Modbus TCP PLC");

            int injected = engine.InjectReflectionsIntoContext(context, topK: 1);

            Assert.Equal(1, injected);
            Assert.True(context.History.Count >= 2);
            var reflexionMsg = context.History.Find(m => m.Content.Contains("[REFLEXION EXPERIENCE REPLAY"));
            Assert.NotNull(reflexionMsg);
            Assert.Contains("502", reflexionMsg.Content);
        }

        // =========================================================================
        // 2. GRAPH-OF-THOUGHTS (GoT) TESTS
        // =========================================================================

        [Fact]
        public void GraphOfThought_SupportsMultiParentBranchingAndAggregation()
        {
            var got = new GraphOfThought();

            // Root problem statement
            var root = got.AddThought("Tối ưu hóa nhiệt độ buồng sấy công nghiệp", 0.50f);
            Assert.NotNull(root);
            Assert.False(root.IsAggregated);

            // Branch 1: Giảm công suất điện trở
            var nodeBranch1 = got.AddThought(
                "Giảm công suất điện trở gia nhiệt xuống 15%",
                0.75f,
                root.Id);

            // Branch 2: Tăng tốc độ quạt đối lưu
            var nodeBranch2 = got.AddThought(
                "Tăng lưu lượng quạt tuần hoàn đối lưu thêm 25%",
                0.82f,
                root.Id);

            Assert.Contains(nodeBranch1.Id, root.ChildIds);
            Assert.Contains(nodeBranch2.Id, root.ChildIds);

            // Aggregate: Hợp nhất cả 2 nhánh (Multi-parent)
            var aggregateNode = got.Aggregate(
                "Đồng thời giảm điện trở 10% và tăng quạt đối lưu 20% để vừa tiết kiệm điện vừa giảm nhiệt đều",
                0.94f,
                new[] { nodeBranch1.Id, nodeBranch2.Id });

            Assert.True(aggregateNode.IsAggregated);
            Assert.Equal(2, aggregateNode.ParentIds.Count);
            Assert.Contains(nodeBranch1.Id, aggregateNode.ParentIds);
            Assert.Contains(nodeBranch2.Id, aggregateNode.ParentIds);
            Assert.Contains(aggregateNode.Id, nodeBranch1.ChildIds);
            Assert.Contains(aggregateNode.Id, nodeBranch2.ChildIds);

            // Trace lineage back to root
            var lineage = got.TraceLineage(aggregateNode.Id);
            Assert.Contains(lineage, n => n.Id == root.Id);
            Assert.Contains(lineage, n => n.Id == nodeBranch1.Id);
            Assert.Contains(lineage, n => n.Id == nodeBranch2.Id);
            Assert.Contains(lineage, n => n.Id == aggregateNode.Id);

            // Best node selection
            var best = got.FindBestNode();
            Assert.NotNull(best);
            Assert.Equal(aggregateNode.Id, best.Id);
            Assert.Equal(0.94f, best.Score);
        }

        [Fact]
        public async Task GraphOfThoughtEngine_DeliberatesAndSynthesizesMultiPerspectives()
        {
            var mockLlm = new MockLlmClient(prompt =>
            {
                if (prompt.Contains("Master Cognitive Synthesizer"))
                    return "Consolidated Decision: Calibrate relief valves and verify 300L/min pneumatic flow with E-Stop standby.";
                if (prompt.Contains("Operational Safety"))
                    return "Safety analysis: Ensure E-Stop is active and pressure relief valves are calibrated.";
                if (prompt.Contains("Technical Feasibility"))
                    return "Feasibility analysis: Pneumatic flow rate of 300L/min is compatible with existing pipe.";
                return "General analysis recommendation.";
            });

            var engine = new GraphOfThoughtEngine(mockLlm);
            var perspectives = new[] { "Technical Feasibility", "Operational Safety" };

            var synthesizedNode = await engine.DeliberateGraphAsync(
                goal: "Khac phuc su co ha ap tren duong ong khi nen",
                perspectives: perspectives);

            Assert.NotNull(synthesizedNode);
            Assert.True(synthesizedNode.IsAggregated);
            Assert.Equal(2, synthesizedNode.ParentIds.Count);
            Assert.Contains("Consolidated Decision", synthesizedNode.Content);
        }

        // =========================================================================
        // 3. ADVERSARIAL MULTI-AGENT DEBATE TESTS
        // =========================================================================

        [Fact]
        public async Task AdversarialDebateEngine_ExecutesDebateAndProducesVerdict()
        {
            var mockLlm = new MockLlmClient(prompt =>
            {
                if (prompt.Contains("Red Team / Challenger"))
                    return "Critique: High conveyor speed of 2.5m/s may cause packaging slippage and pose worker fatigue risks.";
                if (prompt.Contains("Proposer Agent"))
                    return "Defense: Adding anti-slip guide rails and automatic sensors mitigates worker fatigue.";
                if (prompt.Contains("Master Arbiter"))
                    return "Consensus: 0.88\nApproved: true\nDecision: Upgrade conveyor to 2.2m/s with anti-slip rails and optical curtain safety interlocks.";
                return "Default response";
            });

            var debateEngine = new AdversarialDebateEngine(mockLlm);

            string goal = "Nang cao nang suat dong goi bang tai nha xuong";
            string proposedPlan = "Kien nghi tang toc do truot bang tai tu 1.2m/s len 2.5m/s.";

            var verdict = await debateEngine.DebateAsync(goal, proposedPlan, minApprovalConsensus: 0.70f);

            Assert.NotNull(verdict);
            Assert.True(verdict.IsApproved);
            Assert.InRange(verdict.ConsensusScore, 0.70f, 1.0f);
            Assert.False(string.IsNullOrWhiteSpace(verdict.ChallengerCritique));
            Assert.False(string.IsNullOrWhiteSpace(verdict.ArbiterRationale));
            Assert.False(string.IsNullOrWhiteSpace(verdict.FinalActionableDecision));
            Assert.Contains("anti-slip rails", verdict.FinalActionableDecision);
        }

        // =========================================================================
        // 4. AGENT PLAN SEMANTIC CACHE TESTS
        // =========================================================================

        [Fact]
        public void AgentPlanSemanticCache_CachesAndRetrievesWithSubMillisecondSpeed()
        {
            var cache = new AgentPlanSemanticCache();
            var embedder = new LexicalSemanticEmbedder(dimension: 64);

            string originalGoal = "Huong dan kiem tra rung dong va qua nhiet truc chinh spindle";
            var goalEmbedding = embedder.Embed(originalGoal);

            var executionTrace = new List<AgentMessage>
            {
                new AgentMessage(AgentRole.User, originalGoal),
                new AgentMessage(AgentRole.Assistant, "Result: Bearing vibration is 1.2mm/s, normal range.")
            };

            var response = new AgentResponse(
                success: true,
                output: "Vibration inspection completed successfully.",
                totalSteps: 2,
                elapsed: TimeSpan.FromMilliseconds(450),
                trace: executionTrace);

            cache.CachePlan(originalGoal, goalEmbedding, response, confidence: 0.98f);
            Assert.Equal(1, cache.Count);

            // Lookup with a semantically close phrase
            string similarGoal = "Huong dan kiem tra rung dong truc chinh spindle";
            var queryEmbedding = embedder.Embed(similarGoal);

            var sw = Stopwatch.StartNew();
            var retrievedPlan = cache.TryGetPlan(queryEmbedding, minSimilarity: 0.80f);
            sw.Stop();

            Assert.NotNull(retrievedPlan);
            Assert.Equal(originalGoal, retrievedPlan.Goal);
            Assert.Equal("Vibration inspection completed successfully.", retrievedPlan.Solution);
            Assert.Equal(1, retrievedPlan.HitCount);

            // Sub-millisecond lookup latency verification
            Assert.True(sw.ElapsedMilliseconds < 5, $"Lookup took {sw.ElapsedMilliseconds}ms, expected sub-5ms");

            // Lookup with unrelated query
            string unrelatedGoal = "Bao cao doanh thu tai chinh va luong thang 13";
            var unrelatedEmbedding = embedder.Embed(unrelatedGoal);
            var missPlan = cache.TryGetPlan(unrelatedEmbedding, minSimilarity: 0.80f);
            Assert.Null(missPlan);
        }

        // =========================================================================
        // 5. DETERMINISTIC JSON GRAMMAR AUTO-HEALER TESTS
        // =========================================================================

        public class ToolCallPayloadDto
        {
            public string? Action { get; set; }
            public string? Target { get; set; }
            public int TimeoutSeconds { get; set; }
            public List<string>? Items { get; set; }
        }

        [Fact]
        public void JsonGrammarAutoHealer_RepairsSeverelyCorruptedJson()
        {
            // Corrupted with:
            // 1. Markdown code fence
            // 2. Unquoted keys (action, target, timeoutSeconds, items)
            // 3. Single quotes for strings
            // 4. Trailing commas
            // 5. Unclosed braces/brackets at the end
            string brokenJson = @"```json
            {
                action: 'calibrate_sensor',
                target: 'spindle_encoder_01',
                timeoutSeconds: 30,
                items: ['temp_probe', 'rpm_counter',],
            ";

            string healedJson = JsonGrammarAutoHealer.Heal(brokenJson);

            Assert.NotNull(healedJson);
            Assert.StartsWith("{", healedJson.Trim());
            Assert.EndsWith("}", healedJson.Trim());
            Assert.DoesNotContain("```", healedJson);
            Assert.DoesNotContain(",}", healedJson);
            Assert.DoesNotContain(",]", healedJson);

            // Validate that healed JSON can be safely deserialized to DTO
            var dto = JsonGrammarAutoHealer.HealAndDeserialize<ToolCallPayloadDto>(brokenJson);
            Assert.NotNull(dto);
            Assert.Equal("calibrate_sensor", dto.Action);
            Assert.Equal("spindle_encoder_01", dto.Target);
            Assert.Equal(30, dto.TimeoutSeconds);
            Assert.NotNull(dto.Items);
            Assert.Equal(2, dto.Items.Count);
            Assert.Equal("temp_probe", dto.Items[0]);
            Assert.Equal("rpm_counter", dto.Items[1]);
        }

        [Fact]
        public void JsonGrammarAutoHealer_HealsUndefinedAndNaN()
        {
            string jsonWithSpecialLiterals = "{ \"metric\": NaN, \"notes\": undefined, \"valid\": true }";
            string healed = JsonGrammarAutoHealer.Heal(jsonWithSpecialLiterals);

            Assert.Contains("\"metric\": null", healed);
            Assert.Contains("\"notes\": null", healed);
            Assert.Contains("\"valid\": true", healed);
        }
    }
}
