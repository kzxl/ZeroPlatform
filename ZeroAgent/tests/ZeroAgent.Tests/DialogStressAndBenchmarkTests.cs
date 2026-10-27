using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ZeroAgent.Dialog;
using ZeroAgent.Dialog.DST;
using ZeroAgent.Dialog.Memory;
using ZeroAgent.Tools.Safety;

namespace ZeroAgent.Tests
{
    public class DialogStressAndBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public DialogStressAndBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task DeepContext_MultiTurn_8Turns_WithCrossMemoryInterleaving()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            string sessionId = "stress_deep_session_01";
            var operatorProfile = new UserProfile(sessionId, "NguyenVanA", UserRole.Operator);
            var supervisorProfile = new UserProfile(sessionId, "TranVanB", UserRole.Supervisor);

            // Turn 1: Incomplete request (missing machine_id slot)
            var r1 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ giúp tôi", operatorProfile);
            Assert.Equal(SessionState.CollectingSlots, r1.State);
            Assert.Contains("Bạn muốn kiểm tra nhiệt độ của thiết bị nào", r1.Text);

            // Turn 2: User supplies missing machine_id slot
            var r2 = await bot.ChatAsync(sessionId, "CNC-01", operatorProfile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Contains("CNC-01", r2.Text);
            Assert.Contains("73.5°C", r2.Text);
            Assert.True(r2.IsActionExecuted);

            // Turn 3: Follow-up using pronoun "nó"
            var r3 = await bot.ChatAsync(sessionId, "Nhiệt độ nó giờ sao?", operatorProfile);
            Assert.Equal(SessionState.Completed, r3.State);
            Assert.Contains("CNC-01", r3.Text);
            Assert.True(r3.IsActionExecuted);

            // Turn 4: Interleaved topic: Query Semantic Memory (SOP document)
            var r4 = await bot.ChatAsync(sessionId, "Cho tôi xem quy trình xử lý quá nhiệt Lò nung F-01", operatorProfile);
            Assert.Contains("Quy trình xử lý quá nhiệt Lò nung F-01", r4.Text);
            Assert.Contains("van tuần hoàn làm mát C-2", r4.Text);

            // Turn 5: Interleaved topic: Query Episodic Memory (Historical incident recall)
            var r5 = await bot.ChatAsync(sessionId, "Trước đây con CNC-01 từng bị sự cố gì không?", operatorProfile);
            Assert.Contains("Ghi nhận sự cố trước đây", r5.Text);
            Assert.Contains("quá nhiệt bạc đạn máy CNC-01", r5.Text);
            Assert.Contains("cánh quạt làm mát số 3", r5.Text);

            // Turn 6: Action with pronoun "nó" as Operator -> Should resolve to CNC-01, but RBAC blocks!
            var r6 = await bot.ChatAsync(sessionId, "Dừng nó lại ngay", operatorProfile);
            Assert.Equal(SessionState.ActionBlockedByPermission, r6.State);
            Assert.Contains("Truy cập bị từ chối", r6.Text);
            Assert.Contains("STOP_MACHINE", r6.Text);
            Assert.False(r6.IsActionExecuted);

            // Turn 7: Retry action as Supervisor -> Approved and executed
            var r7 = await bot.ChatAsync(sessionId, "Dừng nó lại ngay", supervisorProfile);
            Assert.Equal(SessionState.Completed, r7.State);
            Assert.Contains("CNC-01", r7.Text);
            Assert.True(r7.IsActionExecuted);

            // Turn 8: Switch context to new subject PRESS-03
            var r8 = await bot.ChatAsync(sessionId, "Kiểm tra nhiệt độ máy PRESS-03", operatorProfile);
            Assert.Equal(SessionState.Completed, r8.State);
            Assert.Contains("PRESS-03", r8.Text);

            // Turn 9: Pronoun "nó" should now refer to PRESS-03, NOT CNC-01!
            var r9 = await bot.ChatAsync(sessionId, "Dừng nó lại ngay", supervisorProfile);
            Assert.Equal(SessionState.Completed, r9.State);
            Assert.Contains("PRESS-03", r9.Text);
            Assert.True(r9.IsActionExecuted);
        }

        [Fact]
        public async Task ConcurrentSessions_Isolation_100ParallelUsers()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            int concurrentCount = 100;

            var tasks = new Task[concurrentCount];
            for (int i = 0; i < concurrentCount; i++)
            {
                int idx = i;
                tasks[i] = Task.Run(async () =>
                {
                    string sessionId = $"session_concurrent_{idx:D3}";
                    string targetMachine = $"CNC-{idx:D2}";
                    var profile = new UserProfile(sessionId, $"User_{idx}", UserRole.Supervisor);

                    // Turn 1: Establish subject
                    var r1 = await bot.ChatAsync(sessionId, $"Kiểm tra nhiệt độ máy {targetMachine}", profile);
                    Assert.Contains(targetMachine, r1.Text);

                    // Turn 2: Pronoun dereference
                    var r2 = await bot.ChatAsync(sessionId, "Dừng nó lại", profile);
                    Assert.True(r2.IsActionExecuted);
                    Assert.Contains(targetMachine, r2.Text);
                });
            }

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task Noisy_And_TypoTolerance_Testing()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            string sessionId = "session_noisy_01";
            var profile = new UserProfile(sessionId, "Tech1", UserRole.Supervisor);

            // 1. Unaccented Vietnamese
            var r1 = await bot.ChatAsync(sessionId, "kiem tra nhiet do may CNC-01", profile);
            Assert.Equal(SessionState.Completed, r1.State);
            Assert.Contains("CNC-01", r1.Text);

            // 2. Slang / colloquial
            var r2 = await bot.ChatAsync(sessionId, "nhiệt độ nó giờ sao ta?", profile);
            Assert.Equal(SessionState.Completed, r2.State);
            Assert.Contains("CNC-01", r2.Text);

            // 3. Compact command
            var r3 = await bot.ChatAsync(sessionId, "tat may CNC-01", profile);
            Assert.Equal(SessionState.Completed, r3.State);
            Assert.Contains("CNC-01", r3.Text);
        }

        [Fact]
        public async Task Benchmark_Profiling_Latency_Breakdown_Microseconds()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            string sessionId = "benchmark_session";
            var profile = new UserProfile(sessionId, "BenchUser", UserRole.Supervisor);
            string query = "Kiểm tra nhiệt độ máy CNC-01";

            // Warm-up
            for (int i = 0; i < 10; i++)
            {
                await bot.ChatAsync(sessionId, query, profile);
            }

            int iterations = 200;
            var latenciesMicroseconds = new List<double>(iterations);

            var sw = new Stopwatch();

            for (int i = 0; i < iterations; i++)
            {
                sw.Restart();
                var response = await bot.ChatAsync(sessionId, query, profile);
                sw.Stop();

                double us = sw.Elapsed.TotalMilliseconds * 1000.0;
                latenciesMicroseconds.Add(us);
            }

            latenciesMicroseconds.Sort();
            double minUs = latenciesMicroseconds[0];
            double maxUs = latenciesMicroseconds[latenciesMicroseconds.Count - 1];
            double medianUs = latenciesMicroseconds[latenciesMicroseconds.Count / 2];
            double p95Us = latenciesMicroseconds[(int)(latenciesMicroseconds.Count * 0.95)];
            double p99Us = latenciesMicroseconds[(int)(latenciesMicroseconds.Count * 0.99)];

            double sum = 0;
            for (int i = 0; i < latenciesMicroseconds.Count; i++) sum += latenciesMicroseconds[i];
            double avgUs = sum / latenciesMicroseconds.Count;

            _output.WriteLine("=================================================");
            _output.WriteLine("📊 ZEROAGENT.DIALOG FULL PIPELINE LATENCY PROFILE");
            _output.WriteLine("=================================================");
            _output.WriteLine($"Iterations : {iterations}");
            _output.WriteLine($"Min        : {minUs:F1} µs ({minUs / 1000.0:F4} ms)");
            _output.WriteLine($"Median     : {medianUs:F1} µs ({medianUs / 1000.0:F4} ms)");
            _output.WriteLine($"Avg        : {avgUs:F1} µs ({avgUs / 1000.0:F4} ms)");
            _output.WriteLine($"P95        : {p95Us:F1} µs ({p95Us / 1000.0:F4} ms)");
            _output.WriteLine($"P99        : {p99Us:F1} µs ({p99Us / 1000.0:F4} ms)");
            _output.WriteLine($"Max        : {maxUs:F1} µs ({maxUs / 1000.0:F4} ms)");
            _output.WriteLine("=================================================");

            // Assert sub-millisecond to near-sub-millisecond average latency (< 1.5 ms under test runner load)
            Assert.True(avgUs < 1500.0, $"Average latency {avgUs:F1} µs exceeded 1500 µs (1.5 ms)");
        }

        [Fact]
        public async Task Benchmark_HighThroughput_ConcurrencyStressTest()
        {
            var safetyGate = new HitlSafetyGate { AutoApprove = true };
            var bot = IndustrialDialogFactory.CreateIndustrialBot(safetyGate, enableNeuralClassifier: true);
            int totalRequests = 1000;
            var profile = new UserProfile("bench_user", "BenchOperator", UserRole.Supervisor);

            // Warm-up
            for (int i = 0; i < 10; i++)
            {
                await bot.ChatAsync($"warmup_{i}", "Kiểm tra nhiệt độ máy CNC-01", profile);
            }

            long memBefore = GC.GetTotalMemory(true);
            var sw = Stopwatch.StartNew();

            var tasks = new Task[totalRequests];
            for (int i = 0; i < totalRequests; i++)
            {
                string sId = $"session_{i % 50}";
                tasks[i] = bot.ChatAsync(sId, "Kiểm tra nhiệt độ máy CNC-01", profile);
            }
            await Task.WhenAll(tasks);

            sw.Stop();
            long memAfter = GC.GetTotalMemory(false);
            long allocatedBytes = Math.Max(0, memAfter - memBefore);

            double totalSeconds = sw.Elapsed.TotalSeconds;
            double qps = totalRequests / totalSeconds;

            _output.WriteLine("=================================================");
            _output.WriteLine("⚡ HIGH-THROUGHPUT CONCURRENCY STRESS BENCHMARK");
            _output.WriteLine("=================================================");
            _output.WriteLine($"Total Turns   : {totalRequests:N0}");
            _output.WriteLine($"Total Elapsed : {sw.ElapsedMilliseconds} ms ({totalSeconds:F3} s)");
            _output.WriteLine($"Throughput    : {qps:N1} Queries/Sec (QPS)");
            _output.WriteLine($"Est. Alloc    : {allocatedBytes / 1024.0:F1} KB ({allocatedBytes / (double)totalRequests:F1} B/turn)");
            _output.WriteLine("=================================================");

            // Assert throughput > 750 QPS under test suite concurrency
            Assert.True(qps > 750.0, $"Throughput {qps:F1} QPS was lower than expected 750 QPS");
        }
    }
}
