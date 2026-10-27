using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;

namespace ZeroAgent.Tests
{
    public class ContextCompressionTests
    {
        private readonly ITestOutputHelper _output;

        public ContextCompressionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ObservationCompactor_ShortOutput_ReturnsUnchanged()
        {
            string shortOutput = "Status: OK, RPM=1500, Temp=42.5C";
            string compacted = ObservationCompactor.Compact("telemetry_tool", shortOutput, maxChars: 200);

            Assert.Equal(shortOutput, compacted);
        }

        [Fact]
        public void ObservationCompactor_LongOutput_TruncatesAndRetainsSignatures()
        {
            var lines = new List<string>();
            lines.Add("HEADER: TSDB Reading for Line 01");
            for (int i = 1; i <= 100; i++)
            {
                lines.Add($"Point {i}: timestamp=2026-10-01T00:00:{i:D2}Z, val={i * 1.5}");
            }
            lines.Add("FOOTER: Total 100 points scanned, Status=NORMAL");
            string longOutput = string.Join("\n", lines);

            string compacted = ObservationCompactor.Compact("tsdb_query", longOutput, maxChars: 300);

            Assert.True(compacted.Length < longOutput.Length);
            Assert.Contains("HEADER: TSDB", compacted);
            Assert.Contains("FOOTER: Total 100 points", compacted);
            Assert.Contains("tsdb_query output truncated", compacted);
            Assert.Contains("lines omitted", compacted);
        }

        [Fact]
        public void ContextBudgetManager_EstimatesAndPrunesContext()
        {
            var manager = new ContextBudgetManager();
            var context = new AgentContext("Analyze vibration on CNC-01");
            context.AddMessage(AgentRole.System, "System role: Industrial Diagnostic Agent.");

            for (int i = 1; i <= 20; i++)
            {
                context.AddMessage(AgentRole.User, $"Query step {i}: read telemetry and inspect anomaly metrics");
                context.AddMessage(AgentRole.Assistant, $"Observation step {i}: normal range confirmed at zone A");
            }

            int estimatedBefore = manager.EstimateContextTokens(context);
            Assert.True(estimatedBefore > 300);

            int pruned = manager.PruneContextToBudget(context, maxTokens: 150, out var evicted);

            Assert.True(pruned > 10);
            Assert.NotEmpty(evicted);
            int estimatedAfter = manager.EstimateContextTokens(context);
            Assert.True(estimatedAfter <= 150);

            // System prompt should still be retained
            Assert.Contains(context.History, m => m.Role == AgentRole.System);
        }

        [Fact]
        public void DeterministicContextCompactor_CompactsTurnsAndPreservesEntities()
        {
            var compactor = DeterministicContextCompactor.Instance;
            var slots = new Dictionary<string, string>
            {
                ["machine_id"] = "CNC-01",
                ["metric"] = "vibration",
                ["area"] = "Xưởng ép 2"
            };

            var turns = new List<DialogTurn>
            {
                new DialogTurn("Kiểm tra độ rung CNC-01", "Độ rung 8.4 mm/s - CẢNH BÁO", "DIAGNOSTIC_ANOMALY"),
                new DialogTurn("Xem lịch bảo trì của nó", "Quá hạn bảo trì 14 ngày", "QUERY_MAINTENANCE"),
                new DialogTurn("Ai là kỹ thuật viên phụ trách?", "Kỹ thuật viên: Nguyễn Văn A", "QUERY_OPERATOR")
            };

            string summary = compactor.CompactTurns(turns, slots, existingSummary: null);

            Assert.Contains("<CONTEXT_SUMMARY>", summary);
            Assert.Contains("</CONTEXT_SUMMARY>", summary);
            Assert.Contains("CNC-01", summary);
            Assert.Contains("vibration", summary);
            Assert.Contains("Xưởng ép 2", summary);
            Assert.Contains("DIAGNOSTIC_ANOMALY", summary);
            Assert.Contains("8.4 mm/s", summary);
            Assert.Contains("QUERY_MAINTENANCE", summary);

            _output.WriteLine("Generated Context Summary:\n" + summary);
        }

        [Fact]
        public void WorkingMemory_AutoCompactsOlderTurns_WhenExceedingMaxRetainedTurns()
        {
            var wm = new WorkingMemory("test_session_compaction")
            {
                MaxRetainedTurns = 5 // Low threshold for rapid testing
            };

            wm.SetSlot("machine_id", "PUMP-88");
            wm.SetSlot("area", "Trạm bơm số 3");

            for (int i = 1; i <= 15; i++)
            {
                wm.AddTurn($"Tin nhắn user {i}", $"Phản hồi bot {i}", $"INTENT_{i}");
            }

            // Recent turns bounded to MaxRetainedTurns
            Assert.True(wm.Turns.Count <= 5);

            // Crucially, SummaryContext must now contain the compacted history of the first 10 turns
            Assert.NotNull(wm.SummaryContext);
            Assert.Contains("<CONTEXT_SUMMARY>", wm.SummaryContext);
            Assert.Contains("PUMP-88", wm.SummaryContext);
            Assert.Contains("Trạm bơm số 3", wm.SummaryContext);

            // Effective context history combines both summary and recent turns
            string effectiveHistory = wm.GetEffectiveContextHistory(maxRecentTurns: 3);
            Assert.Contains("<CONTEXT_SUMMARY>", effectiveHistory);
            Assert.Contains("Tin nhắn user 15", effectiveHistory);
        }

        [Fact]
        public void WorkingMemory_PruneToTokenBudget_UpdatesSummaryContext()
        {
            var wm = new WorkingMemory("test_prune_budget");
            wm.SetSlot("machine_id", "ROBOT-ARM-04");

            for (int i = 1; i <= 25; i++)
            {
                wm.AddTurn($"Người dùng yêu cầu phân tích thông số kỹ thuật turn {i}",
                           $"Hệ thống xác nhận trạng thái robot an toàn turn {i}",
                           "ROBOT_INSPECT");
            }

            Assert.Equal(25, wm.Turns.Count);
            Assert.Null(wm.SummaryContext);

            // Prune to 100 tokens
            int pruned = wm.PruneToTokenBudget(maxTokens: 100);
            Assert.True(pruned >= 15);
            Assert.True(wm.Turns.Count <= 10);

            // Pruned turns must be distilled into SummaryContext
            Assert.NotNull(wm.SummaryContext);
            Assert.Contains("ROBOT-ARM-04", wm.SummaryContext);
            Assert.Contains("ROBOT_INSPECT", wm.SummaryContext);
        }

        [Fact]
        public async Task CognitiveEscalationBridge_InjectsSummaryContextIntoReAct()
        {
            var wm = new WorkingMemory("session_escalation");
            wm.SetSlot("machine_id", "FURNACE-02");
            wm.SummaryContext = "<CONTEXT_SUMMARY>\n# Active Entities & State:\n- machine_id: FURNACE-02\n# Milestones:\n- [ALERT] Temperature exceeded 950C\n</CONTEXT_SUMMARY>";

            wm.AddTurn("Nhiệt độ hiện tại thế nào?", "Đang ở mức 965 độ C", "CHECK_TEMP");

            var mockLlm = new RecordingMockLlmClient();
            var reAct = new ReActAgent("TestAgent", "Diagnostics", new AgentToolRegistry(), mockLlm);
            var bridge = new CognitiveEscalationBridge(reAct);

            var session = new DialogueSession("session_escalation");
            var profile = new UserProfile("admin", "Admin", UserRole.Supervisor);

            await bridge.EscalateAsync(session, wm, profile, "Phân tích nguyên nhân tăng nhiệt");

            // Verify that the prompt sent to LLM contains the <CONTEXT_SUMMARY>
            Assert.NotNull(mockLlm.LastPrompt);
            Assert.Contains("<CONTEXT_SUMMARY>", mockLlm.LastPrompt);
            Assert.Contains("FURNACE-02", mockLlm.LastPrompt);
            Assert.Contains("Temperature exceeded 950C", mockLlm.LastPrompt);
        }

        [Fact]
        public void PerformanceBenchmark_1000CompactionCycles_ExecutesInUnder20ms()
        {
            var compactor = DeterministicContextCompactor.Instance;
            var slots = new Dictionary<string, string> { ["machine_id"] = "CNC-01", ["status"] = "ACTIVE" };
            var batch = new List<DialogTurn>
            {
                new DialogTurn("Check status", "All clear", "CHECK"),
                new DialogTurn("Read temperature", "45.2C normal", "READ")
            };

            string? summary = null;

            // Warmup
            summary = compactor.CompactTurns(batch, slots, summary);

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                summary = compactor.CompactTurns(batch, slots, summary);
            }
            sw.Stop();

            _output.WriteLine($"[BENCHMARK] 1,000 deterministic compaction cycles completed in {sw.ElapsedMilliseconds} ms ({sw.Elapsed.TotalMicroseconds / 1000.0:F3} us/op).");
            Assert.True(sw.ElapsedMilliseconds < 100, $"Expected < 100ms, actual: {sw.ElapsedMilliseconds}ms");
        }

        private sealed class RecordingMockLlmClient : ILlmClient
        {
            public string? LastPrompt { get; private set; }

            public Task<string> CompleteAsync(string prompt, System.Threading.CancellationToken cancellationToken = default)
            {
                LastPrompt = prompt;
                return Task.FromResult("Final Answer: Đã phân tích xong dữ liệu.");
            }
        }
    }
}
