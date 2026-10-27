using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Tests
{
    public class StandardizedAgentArchitectureTests
    {
        // =========================================================================
        // TEST 1: ToolCallParser Support for All 4 Invocation Syntaxes
        // =========================================================================
        [Fact]
        public void ToolCallParser_ParsesAllFormats_Correctly()
        {
            // 1. Classic ReAct Syntax
            string t1 = "Thought: I need to check the table.\nAction: db_query_table(factory_machines)\nObservation:";
            Assert.True(ToolCallParser.TryParseToolCall(t1, out var c1));
            Assert.Equal("db_query_table", c1.ToolName);
            Assert.Equal("factory_machines", c1.ArgumentsJson);

            // 2. Action with inline JSON object
            string t2 = "Thought: Setting PLC register.\nAction: {\"tool\": \"write_plc_holding_register\", \"arguments\": {\"unitId\": 1, \"address\": 40001, \"value\": 90}}";
            Assert.True(ToolCallParser.TryParseToolCall(t2, out var c2));
            Assert.Equal("write_plc_holding_register", c2.ToolName);
            Assert.Contains("\"unitId\": 1", c2.ArgumentsJson);

            // 3. Markdown codeblock JSON
            string t3 = "Thought: Reading registers.\n```json\n{\n  \"name\": \"read_plc_holding_registers\",\n  \"parameters\": \"{\\\"address\\\": 40001}\"\n}\n```";
            Assert.True(ToolCallParser.TryParseToolCall(t3, out var c3));
            Assert.Equal("read_plc_holding_registers", c3.ToolName);

            // 4. Direct JSON text
            string t4 = "{\"tool\": \"db_list_tables\", \"arguments\": \"{}\"}";
            Assert.True(ToolCallParser.TryParseToolCall(t4, out var c4));
            Assert.Equal("db_list_tables", c4.ToolName);

            // 5. Final Answer parsing
            string t5 = "Thought: I have finished inspecting.\nFinal Answer: Nhiệt độ máy CNC-01 hoàn toàn bình thường ở mức 72 độ C.";
            Assert.True(ToolCallParser.TryParseFinalAnswer(t5, out string finalAnswer));
            Assert.Contains("72 độ C", finalAnswer);
        }

        // =========================================================================
        // TEST 2: Two-Tier Hybrid Cognitive Bridge (Reflex NLU -> ReAct Escalation)
        // =========================================================================
        [Fact]
        public async Task TwoTierCognitiveBridge_ReflexNlu_And_ReActEscalation_OperatesSeamlessly()
        {
            // Mock LLM client simulating ReAct reasoning
            var mockLlm = new MockReActLlmClient();

            // Construct Agent using ZeroAgentBuilder
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = ZeroAgentBuilder.Create()
                .WithHitlSafetyGate(safetyGate)
                .WithIndustrialTools(true)
                .WithCognitiveEscalation(mockLlm)
                .Build();

            string sessionId = "two_tier_bridge_session_01";
            var profile = new UserProfile(sessionId, "LeadEngineer", UserRole.Supervisor);

            // 1. Reflex Tier 1 test (Standard deterministic intent -> handled in < 1ms)
            var r1 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy CNC-01", profile);
            Assert.Equal(SessionState.Completed, r1.State);
            Assert.Equal("CHECK_TEMPERATURE", r1.IntentName);
            Assert.Contains("CNC-01", r1.Text);

            // 2. Deliberation Tier 2 test: Open-ended complex request with no NLU intent
            // Should escalate automatically via CognitiveEscalationBridge to ReActAgent
            var r2 = await bot.ChatAsync(sessionId, "Phân tích và cho tôi biết tình trạng bạc đạn cùng áp suất buồng máy", profile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Equal("COGNITIVE_DELIBERATION_REACT", r2.IntentName);
            Assert.Contains("Tình trạng buồng máy ổn định", r2.Text);

            // Working memory should retain both turns
            var wm = bot.Memory.GetWorkingMemory(sessionId);
            Assert.Equal(2, wm.Turns.Count);
        }

        // =========================================================================
        // TEST 3: WorkingMemory Knapsack Token Budget Truncation
        // =========================================================================
        [Fact]
        public void WorkingMemory_KnapsackTokenBudget_PrunesHistoryCorrectly()
        {
            var wm = new WorkingMemory("test_knapsack_session");
            wm.SetSlot("machine_id", "PRESS-05");
            wm.SetSlot("area", "xưởng ép");

            // Seed 20 verbose turns (~30 tokens each)
            for (int i = 1; i <= 20; i++)
            {
                wm.AddTurn($"Tin nhắn người dùng số {i}: kiểm tra toàn diện quy trình vận hành và thông số telemetry",
                           $"Phản hồi hệ thống số {i}: Dữ liệu ghi nhận thông số bình thường tại khu vực xưởng ép",
                           "CHECK_STATUS");
            }
            Assert.Equal(20, wm.Turns.Count);

            // Prune to budget of ~150 tokens (~4-5 turns)
            int pruned = wm.PruneToTokenBudget(maxTokens: 150);
            Assert.True(pruned > 10, $"Expected > 10 turns pruned, actual: {pruned}");
            Assert.True(wm.Turns.Count <= 6, $"Expected <= 6 turns remaining, actual: {wm.Turns.Count}");

            // Crucially, slots and subject must remain intact
            Assert.Equal("PRESS-05", wm.CurrentSubject);
            Assert.Equal("xưởng ép", wm.CurrentArea);
            Assert.Contains("Tin nhắn người dùng số 20", wm.Turns[wm.Turns.Count - 1].UserMessage);
        }

        // =========================================================================
        // TEST 4: ZeroAgentBuilder Fluent Creation & Readiness
        // =========================================================================
        [Fact]
        public async Task ZeroAgentBuilder_FluentCreation_InitializesFullEngine()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = ZeroAgentBuilder.Create()
                .WithVectorDimension(128)
                .WithHitlSafetyGate(safetyGate)
                .WithIndustrialTools(true)
                .WithNeuralClassifier(true)
                .WithCustomTools(registry =>
                {
                    registry.Register("custom_echo_tool", "Echoes input text back", (string arg) => $"ECHO: {arg}");
                })
                .Build();

            Assert.NotNull(bot);
            Assert.True(bot.Tools.Count >= 7);
            Assert.True(bot.Tools.TryGetTool("custom_echo_tool", out var echoTool));
            Assert.Equal("ECHO: Hello ZeroAgent", await echoTool.ExecuteAsync("Hello ZeroAgent"));
        }

        private sealed class MockReActLlmClient : ILlmClient
        {
            private int _callCount = 0;

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                _callCount++;
                if (_callCount == 1)
                {
                    // Emit tool action
                    return Task.FromResult("Thought: I need to query the holding register.\nAction: read_plc_holding_registers({\"unitId\": 1, \"address\": 40003, \"count\": 1})");
                }
                else
                {
                    // Emit final answer
                    return Task.FromResult("Thought: I have the pressure reading.\nFinal Answer: Tình trạng buồng máy ổn định, áp suất đạt 42 PSI.");
                }
            }
        }
    }
}
