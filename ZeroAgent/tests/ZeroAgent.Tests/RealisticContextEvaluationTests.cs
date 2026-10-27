using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;
using ZeroAgent.Core.Tools;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Engine;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Tests
{
    /// <summary>
    /// Comprehensive multi-aspect evaluation test suite simulating realistic industrial factory floor contexts:
    /// 1. Deep Multi-turn Context Retention & Subject Switching
    /// 2. Conversational Interruption & Pending Slot Resumption
    /// 3. Two-Tier Analytical Deliberation (ReAct Multi-step Escalation)
    /// 4. RBAC & Human-In-The-Loop (HITL) Safety Gate Interception (Approve/Reject)
    /// 5. Noisy, Unaccented & Mixed Dialect Robustness
    /// 6. Long-Shift Knapsack Budget Pressure & Entity Invariance
    /// 7. Concurrent Multi-Operator Session Isolation & Latency Benchmark
    /// </summary>
    public class RealisticContextEvaluationTests
    {
        private readonly ITestOutputHelper _output;

        public RealisticContextEvaluationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // =========================================================================
        // SCENARIO 1: Deep Multi-turn Context Retention & Subject Switching
        // =========================================================================
        [Fact]
        public async Task RealisticContext_MultiTurn_SubjectSwitching_PreservesMemoryAndResolvesAnaphora()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            string sessionId = "shift_session_multiturn_01";
            var profile = new UserProfile(sessionId, "TranVanBao", UserRole.Supervisor);

            // Turn 1: Establish first subject (CNC-05)
            var r1 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy CNC-05", profile);
            Assert.Equal(SessionState.Completed, r1.State);
            Assert.Equal("CHECK_TEMPERATURE", r1.IntentName);
            Assert.Contains("CNC-05", r1.Text);

            var wm = bot.Memory.GetWorkingMemory(sessionId);
            Assert.Equal("CNC-05", wm.CurrentSubject);

            // Turn 2: Follow-up question using pronoun 'nó' and changing metric to pressure
            var r2 = await bot.ChatAsync(sessionId, "Còn áp suất của nó thì sao?", profile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Contains("CNC-05", r2.Text);
            Assert.Equal("CNC-05", wm.CurrentSubject);
            Assert.Equal("pressure", wm.CurrentMetric);

            // Turn 3: Interleaved knowledge query (SOP check)
            var r3 = await bot.ChatAsync(sessionId, "Cho tôi xem quy trình xử lý quá nhiệt Lò nung F-01", profile);
            Assert.Equal(SessionState.Idle, r3.State);
            Assert.Equal("KNOWLEDGE_RETRIEVAL", r3.IntentName);
            Assert.Contains("Lò nung F-01", r3.Text);

            // Turn 4: Switch subject explicitly to PRESS-02
            var r4 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy PRESS-02", profile);
            Assert.Equal(SessionState.Completed, r4.State);
            Assert.Contains("PRESS-02", r4.Text);
            Assert.Equal("PRESS-02", wm.CurrentSubject);

            // Turn 5: Command with pronoun referring to the new subject ('dừng con đó lại')
            var r5 = await bot.ChatAsync(sessionId, "Dừng con đó lại", profile);
            Assert.Equal(SessionState.Completed, r5.State);
            Assert.True(r5.IsActionExecuted);
            Assert.Contains("PRESS-02", r5.Text);

            // Turn 6: Switch back to the previous machine CNC-05
            var r6 = await bot.ChatAsync(sessionId, "Quay lại máy CNC-05, kiểm tra nhiệt độ", profile);
            Assert.Equal(SessionState.Completed, r6.State);
            Assert.Contains("CNC-05", r6.Text);
            Assert.Equal("CNC-05", wm.CurrentSubject);

            _output.WriteLine($"[SCENARIO 1 PASSED] 6 turns executed. Working memory turns count: {wm.Turns.Count}, Active subject: {wm.CurrentSubject}");
        }

        // =========================================================================
        // SCENARIO 2: Conversational Interruption & Pending Slot Resumption
        // =========================================================================
        [Fact]
        public async Task RealisticContext_Interruption_And_PendingSlotResumption_Succeeds()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            string sessionId = "shift_session_interruption_02";
            var profile = new UserProfile(sessionId, "NguyenThiHoa", UserRole.Supervisor);

            // Turn 1: User triggers action without providing required slot
            var r1 = await bot.ChatAsync(sessionId, "Tôi muốn yêu cầu dừng thiết bị", profile);
            Assert.Equal(SessionState.CollectingSlots, r1.State);
            Assert.Equal("STOP_MACHINE", r1.IntentName);
            Assert.False(r1.IsActionExecuted);
            Assert.Contains("thiết bị nào", r1.Text);

            var session = bot.GetOrCreateSession(sessionId);
            Assert.Equal("machine_id", session.PendingRequiredSlot);

            // Turn 2: User interrupts with an unrelated SOP documentation inquiry
            var r2 = await bot.ChatAsync(sessionId, "Khoan đã, tài liệu hướng dẫn bôi trơn bạc đạn máy CNC ở đâu?", profile);
            Assert.Equal(SessionState.Idle, r2.State);
            Assert.Equal("KNOWLEDGE_RETRIEVAL", r2.IntentName);
            Assert.Contains("ISO VG 68", r2.Text);

            // Turn 3: User returns to execute the stop command by providing the machine ID
            var r3 = await bot.ChatAsync(sessionId, "Dừng máy CNC-04 đi", profile);
            Assert.Equal(SessionState.Completed, r3.State);
            Assert.Equal("STOP_MACHINE", r3.IntentName);
            Assert.True(r3.IsActionExecuted);
            Assert.Contains("CNC-04", r3.Text);

            _output.WriteLine($"[SCENARIO 2 PASSED] Interrupted slot successfully resolved and executed on CNC-04");
        }

        // =========================================================================
        // SCENARIO 3: Two-Tier Analytical Deliberation (ReAct Multi-step Escalation)
        // =========================================================================
        [Fact]
        public async Task RealisticContext_AnalyticalDeliberation_EscalatesToMultiStepReAct()
        {
            var mockLlm = new DiagnosticDiagnosticReActLlmClient();
            var safetyGate = new HitlSafetyGate { AutoApprove = true };

            var bot = ZeroAgentBuilder.Create()
                .WithHitlSafetyGate(safetyGate)
                .WithIndustrialTools(true)
                .WithCognitiveEscalation(mockLlm)
                .Build();

            string sessionId = "shift_session_react_03";
            var profile = new UserProfile(sessionId, "ChuyenGiaKyThuat", UserRole.Supervisor);

            // Turn 1: Normal reflex inquiry
            var r1 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy CNC-01", profile);
            Assert.Equal(SessionState.Completed, r1.State);
            Assert.Equal("CHECK_TEMPERATURE", r1.IntentName);
            Assert.Contains("CNC-01", r1.Text);

            // Turn 2: Complex analytical inquiry requiring deep deliberation
            var r2 = await bot.ChatAsync(sessionId, "Phân tích nguyên nhân nhiệt độ cao và đối chiếu sự cố trước đây của máy CNC-01", profile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Equal("COGNITIVE_DELIBERATION_REACT", r2.IntentName);
            Assert.Contains("Kẹt cánh quạt làm mát", r2.Text);
            Assert.Contains("khuyến nghị bổ sung mỡ bôi trơn ISO VG 68", r2.Text, StringComparison.OrdinalIgnoreCase);

            Assert.True(mockLlm.ExecutionStepsCount >= 2, $"Expected at least 2 ReAct reasoning steps, got {mockLlm.ExecutionStepsCount}");
            _output.WriteLine($"[SCENARIO 3 PASSED] ReAct Agent executed {mockLlm.ExecutionStepsCount} steps to answer analytical request.");
        }

        // =========================================================================
        // SCENARIO 4: RBAC & Human-In-The-Loop Safety Gate Interception (Approve/Reject)
        // =========================================================================
        [Fact]
        public async Task RealisticContext_RbacAndHitlGate_EnforcesStrictSecurityBoundaries()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = false };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);

            // Case A: Operator (lacks STOP_MACHINE permission)
            string opSession = "session_operator_01";
            var opProfile = new UserProfile(opSession, "CongNhanA", UserRole.Operator); // Operator has ReadOnly access

            var rOp = await bot.ChatAsync(opSession, "Dừng máy CNC-01 ngay lập tức", opProfile);
            Assert.Equal(SessionState.ActionBlockedByPermission, rOp.State);
            Assert.False(rOp.IsActionExecuted);
            Assert.Contains("không có quyền", rOp.Text, StringComparison.OrdinalIgnoreCase);

            // Case B: Supervisor with approval granted by Safety Gate
            string supSession = "session_supervisor_01";
            var supProfile = new UserProfile(supSession, "QuanDocB", UserRole.Supervisor);

            safetyGate.AutoApprove = true; // Simulating operator confirmation via popup / push notification
            var rSup = await bot.ChatAsync(supSession, "Dừng máy CNC-01", supProfile);
            Assert.Equal(SessionState.Completed, rSup.State);
            Assert.True(rSup.IsActionExecuted);
            Assert.Contains("CNC-01", rSup.Text);

            // Case C: Supervisor with HITL rejection
            string rejectSession = "session_supervisor_reject_01";
            var rejectGate = new HitlSafetyGate { AutoApprove = false };
            var botReject = IndustrialDialogFactory.CreateIndustrialBot(rejectGate, enableNeuralClassifier: true);

            var rejectTask = botReject.ChatAsync(rejectSession, "Dừng máy CNC-02", supProfile);

            // Wait for safety gate to intercept
            for (int w = 0; w < 30 && !rejectGate.PendingRequests.Any(); w++)
            {
                await Task.Delay(20);
            }
            var pendingReq = rejectGate.PendingRequests.FirstOrDefault();
            Assert.NotNull(pendingReq);
            rejectGate.Reject(pendingReq.RequestId, "SeniorEngineer");

            var rReject = await rejectTask;
            Assert.Contains("Action rejected", rReject.Text, StringComparison.OrdinalIgnoreCase);

            _output.WriteLine("[SCENARIO 4 PASSED] RBAC and HITL Safety Gate verified for denial, approval, and explicit interception rejection.");
        }

        // =========================================================================
        // SCENARIO 5: Noisy, Unaccented & Mixed Vietnamese-English Industrial Dialect
        // =========================================================================
        [Theory]
        [InlineData("kiem tra nhiet do may CNC-07 giup voi", "CHECK_TEMPERATURE", "CNC-07")]
        [InlineData("xem nhiet do cua con PRESS-03", "CHECK_TEMPERATURE", "PRESS-03")]
        [InlineData("check temp cho cnc-09 xem nao", "CHECK_TEMPERATURE", "CNC-09")]
        [InlineData("ngat dien may CNC-02 gap", "STOP_MACHINE", "CNC-02")]
        [InlineData("dung thiet bi PRESS-01 ngay", "STOP_MACHINE", "PRESS-01")]
        public async Task RealisticContext_NoisyAndMixedDialect_RecognizesIntentsAndSlotsAccurately(
            string input, string expectedIntent, string expectedSlot)
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            string sessionId = $"test_noisy_{Guid.NewGuid():N}";
            var profile = new UserProfile(sessionId, "FieldTech", UserRole.Supervisor);

            var response = await bot.ChatAsync(sessionId, input, profile);
            Assert.Equal(expectedIntent, response.IntentName);
            Assert.Contains(expectedSlot, response.Text);
        }

        // =========================================================================
        // SCENARIO 6: Long-Shift Knapsack Budget Pressure & Entity Invariance (25 Turns)
        // =========================================================================
        [Fact]
        public async Task RealisticContext_LongShiftKnapsackPruning_MaintainsEntityIntegrity()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            string sessionId = "shift_session_25_turns";
            var profile = new UserProfile(sessionId, "ShiftSupervisor", UserRole.Supervisor);

            // Establish primary subject in Turn 1
            await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy CNC-99", profile);

            // Execute 24 subsequent turns simulating shift status checks
            for (int turn = 2; turn <= 25; turn++)
            {
                if (turn % 2 == 0)
                {
                    await bot.ChatAsync(sessionId, "Nhiệt độ nó giờ sao rồi?", profile);
                }
                else
                {
                    await bot.ChatAsync(sessionId, "Còn áp suất của nó thì sao?", profile);
                }
            }

            var wm = bot.Memory.GetWorkingMemory(sessionId);
            Assert.Equal("CNC-99", wm.CurrentSubject);

            // Prune working memory to strict knapsack budget (e.g. 300 tokens)
            int prunedTurns = wm.PruneToTokenBudget(maxTokens: 300);
            Assert.True(prunedTurns >= 15, $"Expected >= 15 turns pruned under 300 token limit, got {prunedTurns}");

            // Crucial evaluation assertion: Entity memory remains intact
            Assert.Equal("CNC-99", wm.CurrentSubject);
            Assert.True(wm.TryGetSlot("machine_id", out var machineId));
            Assert.Equal("CNC-99", machineId);

            // Immediate turn 26 after pruning: pronoun dereference must still work seamlessly
            var r26 = await bot.ChatAsync(sessionId, "Dừng nó lại", profile);
            Assert.Equal(SessionState.Completed, r26.State);
            Assert.True(r26.IsActionExecuted);
            Assert.Contains("CNC-99", r26.Text);

            _output.WriteLine($"[SCENARIO 6 PASSED] Successfully handled 26 turns with Knapsack token pruning; entity CNC-99 preserved.");
        }

        // =========================================================================
        // SCENARIO 7: Concurrent Multi-Operator Session Isolation & Latency Benchmark
        // =========================================================================
        [Fact]
        public async Task RealisticContext_50ConcurrentOperators_IsolationAndLatencyBenchmark()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            int operatorCount = 50;
            var latencies = new List<double>();
            var lockObj = new object();

            var tasks = new Task[operatorCount];
            var swTotal = Stopwatch.StartNew();

            for (int i = 0; i < operatorCount; i++)
            {
                int opIdx = i;
                tasks[i] = Task.Run(async () =>
                {
                    string sessId = $"op_session_{opIdx:D3}";
                    string target = (opIdx % 2 == 0) ? $"CNC-{opIdx:D2}" : $"PRESS-{opIdx:D2}";
                    var userProf = new UserProfile(sessId, $"Operator_{opIdx}", UserRole.Supervisor);

                    var swTurn = Stopwatch.StartNew();

                    // Turn 1: Inquire machine
                    var r1 = await bot.ChatAsync(sessId, $"Kiểm tra nhiệt độ máy {target}", userProf);
                    Assert.Contains(target, r1.Text);

                    // Turn 2: Pronoun dereference
                    var r2 = await bot.ChatAsync(sessId, "Dừng con đó lại", userProf);
                    swTurn.Stop();

                    Assert.True(r2.IsActionExecuted);
                    Assert.Contains(target, r2.Text);

                    lock (lockObj)
                    {
                        latencies.Add(swTurn.Elapsed.TotalMilliseconds);
                    }
                });
            }

            await Task.WhenAll(tasks);
            swTotal.Stop();

            latencies.Sort();
            double p50 = latencies[(int)(operatorCount * 0.50)];
            double p95 = latencies[(int)(operatorCount * 0.95)];
            double avg = latencies.Average();

            _output.WriteLine($"============================================================");
            _output.WriteLine($"[LATENCY & THROUGHPUT BENCHMARK - 50 CONCURRENT OPERATORS]");
            _output.WriteLine($"Total Elapsed Time: {swTotal.ElapsedMilliseconds} ms for 100 turns ({1000.0 * 100 / swTotal.ElapsedMilliseconds:F1} turns/sec)");
            _output.WriteLine($"Average Turn-Pair Latency: {avg:F2} ms");
            _output.WriteLine($"P50 Latency: {p50:F2} ms");
            _output.WriteLine($"P95 Latency: {p95:F2} ms");
            _output.WriteLine($"Memory Isolation: 100% (No cross-talk detected)");
            _output.WriteLine($"============================================================");

            Assert.True(avg < 100, $"Average latency should be < 100ms on CPU, got {avg:F2}ms");
        }

        // =========================================================================
        // HELPER CLASSES
        // =========================================================================
        private sealed class DiagnosticDiagnosticReActLlmClient : ILlmClient
        {
            public int ExecutionStepsCount { get; private set; }

            public Task<string> CompleteAsync(string prompt, CancellationToken cancellationToken = default)
            {
                ExecutionStepsCount++;
                if (ExecutionStepsCount == 1)
                {
                    // Step 1: Query telemetry metric
                    return Task.FromResult("Thought: Tôi cần kiểm tra nhiệt độ cảm biến và nhật ký vận hành gần nhất của máy CNC-01.\n" +
                                           "Action: query_tsdb_metric({\"metricName\": \"motor_temperature\"})");
                }
                else if (ExecutionStepsCount == 2)
                {
                    // Step 2: Query historical database
                    return Task.FromResult("Thought: Nhiệt độ đo được là 73.5 độ C, cao hơn mức chuẩn. Tôi cần kiểm tra bảng dữ liệu thiết bị.\n" +
                                           "Action: db_query_table({\"tableName\": \"factory_machines\", \"limit\": 3})");
                }
                else
                {
                    // Final Answer
                    return Task.FromResult("Thought: Đã có đủ dữ liệu từ cảm biến và lịch sử thiết bị.\n" +
                                           "Final Answer: Phân tích máy CNC-01: Nhiệt độ động cơ ghi nhận 73.5°C (vượt ngưỡng tối ưu 70°C). Đối chiếu lịch sử sự cố trước đây, nguyên nhân tương đồng là do 'Kẹt cánh quạt làm mát'. Khuyến nghị bổ sung mỡ bôi trơn ISO VG 68 và kiểm tra cụm cánh tản nhiệt.");
                }
            }
        }
    }
}
