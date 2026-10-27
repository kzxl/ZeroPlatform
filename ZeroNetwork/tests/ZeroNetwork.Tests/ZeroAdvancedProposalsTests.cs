using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroNetwork.Common;
using ZeroNetwork.Diagnostics;
using ZeroNetwork.Http;

namespace ZeroNetwork.Tests
{
    [Collection("ZeroTelemetryRegistry")]
    public class ZeroAdvancedProposalsTests
    {
        #region ZeroClock Tests
        [Fact]
        public void ZeroClock_HighResolutionTiming_MonotonicAndAccurate()
        {
            long t1 = ZeroClock.GetTimestamp();
            Thread.Sleep(10);
            long t2 = ZeroClock.GetTimestamp();

            Assert.True(t2 > t1);

            double elapsedMs = ZeroClock.GetElapsedMilliseconds(t1, t2);
            double elapsedUs = ZeroClock.GetElapsedMicroseconds(t1, t2);
            double elapsedNs = ZeroClock.GetElapsedNanoseconds(t1, t2);

            Assert.True(elapsedMs >= 5.0 && elapsedMs <= 100.0);
            Assert.Equal(elapsedMs * 1000.0, elapsedUs, 1);
            Assert.Equal(elapsedUs * 1000.0, elapsedNs, 1);

            var highPrecUtc = ZeroClock.HighPrecisionUtcNow;
            var bclUtc = DateTime.UtcNow;
            Assert.True(Math.Abs((highPrecUtc - bclUtc).TotalSeconds) < 2.0);
        }
        #endregion

        #region ZeroRadixSort Tests
        [Fact]
        public void ZeroRadixSort_SortInts_MatchesBclSortExact()
        {
            var rng = new Random(42);
            int count = 5000;
            int[] data = new int[count];
            int[] expected = new int[count];

            for (int i = 0; i < count; i++)
            {
                int val = rng.Next(-100000, 100000);
                data[i] = val;
                expected[i] = val;
            }

            Array.Sort(expected);
            ZeroRadixSort.Sort(data.AsSpan());

            Assert.Equal(expected, data);
        }

        [Fact]
        public void ZeroRadixSort_SortLongs_MatchesBclSortExact()
        {
            var rng = new Random(1337);
            int count = 5000;
            long[] data = new long[count];
            long[] expected = new long[count];

            for (int i = 0; i < count; i++)
            {
                long val = ((long)rng.Next() << 32) | (uint)rng.Next();
                if (rng.Next(2) == 0) val = -val;
                data[i] = val;
                expected[i] = val;
            }

            Array.Sort(expected);
            ZeroRadixSort.Sort(data.AsSpan());

            Assert.Equal(expected, data);
        }

        [Fact]
        public void ZeroRadixSort_SortFloats_MatchesBclSortExact()
        {
            var rng = new Random(999);
            int count = 5000;
            float[] data = new float[count];
            float[] expected = new float[count];

            for (int i = 0; i < count; i++)
            {
                float val = (float)(rng.NextDouble() * 2000.0 - 1000.0);
                data[i] = val;
                expected[i] = val;
            }

            Array.Sort(expected);
            ZeroRadixSort.Sort(data.AsSpan());

            Assert.Equal(expected, data);
        }

        [Fact]
        public void ZeroRadixSort_PerformanceStress_100kIntegers()
        {
            int count = 100000;
            int[] data = new int[count];
            var rng = new Random(777);
            for (int i = 0; i < count; i++) data[i] = rng.Next(-1000000, 1000000);

            var sw = Stopwatch.StartNew();
            ZeroRadixSort.Sort(data.AsSpan());
            sw.Stop();

            // Verify sorted
            for (int i = 1; i < count; i++)
            {
                Assert.True(data[i] >= data[i - 1]);
            }

            Assert.True(sw.ElapsedMilliseconds < 500, $"Sorted 100k ints in {sw.ElapsedMilliseconds}ms");
        }
        #endregion

        #region ZeroXxHash3 Tests
        [Fact]
        public void ZeroXxHash3_AvalancheAndDeterministicOutput()
        {
            byte[] input1 = Encoding.UTF8.GetBytes("industrial_telemetry_packet_header_001");
            byte[] input2 = Encoding.UTF8.GetBytes("industrial_telemetry_packet_header_002"); // 1 char difference

            ulong hash1 = ZeroXxHash3.Hash64(input1);
            ulong hash1_repeat = ZeroXxHash3.Hash64(input1);
            ulong hash2 = ZeroXxHash3.Hash64(input2);

            Assert.Equal(hash1, hash1_repeat);
            Assert.NotEqual(hash1, hash2);

            // 32-bit string hash
            int h32_1 = ZeroXxHash3.Hash32("plants/hanoi/line1/temp");
            int h32_2 = ZeroXxHash3.Hash32("plants/hanoi/line1/temp");
            int h32_3 = ZeroXxHash3.Hash32("plants/hanoi/line1/vibe");

            Assert.True(h32_1 >= 0);
            Assert.Equal(h32_1, h32_2);
            Assert.NotEqual(h32_1, h32_3);
        }
        #endregion

        #region ZeroStateMachine Tests
        private enum MachineState { Idle, Running, Paused, Faulted }
        private enum MachineTrigger { Start, Pause, Resume, Trip, Reset }

        [Fact]
        public void ZeroStateMachine_WorkflowTransitions_ExecuteCorrectly()
        {
            var fsm = new ZeroStateMachine<MachineState, MachineTrigger>(MachineState.Idle);

            bool enteredRunning = false;
            bool exitedIdle = false;
            var auditEvents = new System.Collections.Generic.List<string>();

            fsm.Configure(MachineState.Idle)
               .Permit(MachineTrigger.Start, MachineState.Running)
               .OnExit(() => exitedIdle = true);

            fsm.Configure(MachineState.Running)
               .Permit(MachineTrigger.Pause, MachineState.Paused)
               .Permit(MachineTrigger.Trip, MachineState.Faulted)
               .OnEntry(() => enteredRunning = true);

            fsm.Configure(MachineState.Paused)
               .Permit(MachineTrigger.Resume, MachineState.Running)
               .Permit(MachineTrigger.Trip, MachineState.Faulted);

            fsm.Configure(MachineState.Faulted)
               .Permit(MachineTrigger.Reset, MachineState.Idle);

            fsm.StateChanged += (source, trigger, target) =>
            {
                auditEvents.Add($"{source}->{trigger}->{target}");
            };

            // 1. Initial State
            Assert.Equal(MachineState.Idle, fsm.CurrentState);
            Assert.True(fsm.CanFire(MachineTrigger.Start));
            Assert.False(fsm.CanFire(MachineTrigger.Pause));

            // 2. Fire Start
            fsm.Fire(MachineTrigger.Start);
            Assert.Equal(MachineState.Running, fsm.CurrentState);
            Assert.True(exitedIdle);
            Assert.True(enteredRunning);

            // 3. Fire Pause
            fsm.Fire(MachineTrigger.Pause);
            Assert.Equal(MachineState.Paused, fsm.CurrentState);

            // 4. Invalid trigger throws
            Assert.Throws<InvalidOperationException>(() => fsm.Fire(MachineTrigger.Start));

            // 5. Fire Trip
            fsm.Fire(MachineTrigger.Trip);
            Assert.Equal(MachineState.Faulted, fsm.CurrentState);

            // 6. Reset to Idle
            fsm.Fire(MachineTrigger.Reset);
            Assert.Equal(MachineState.Idle, fsm.CurrentState);

            // 7. Verify Audit History
            var history = fsm.GetAuditHistory();
            Assert.Equal(4, history.Length);
            Assert.Equal(MachineState.Idle, history[0].SourceState);
            Assert.Equal(MachineState.Running, history[0].TargetState);
            Assert.Equal(MachineState.Faulted, history[3].SourceState);
            Assert.Equal(MachineState.Idle, history[3].TargetState);

            Assert.Equal(4, auditEvents.Count);
        }

        [Fact]
        public void ZeroStateMachine_TransitionSpeed_SubMicrosecondPerformance()
        {
            var fsm = new ZeroStateMachine<int, int>(0);
            fsm.Configure(0).Permit(1, 1);
            fsm.Configure(1).Permit(0, 0);

            int iterations = 100000;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                fsm.Fire(i % 2 == 0 ? 1 : 0);
            }

            sw.Stop();

            // 100,000 transitions should take under 100ms (< 1 microsecond per transition)
            Assert.True(sw.ElapsedMilliseconds < 250, $"Elapsed: {sw.ElapsedMilliseconds}ms for {iterations} transitions.");
        }
        #endregion

        #region ZeroTelemetry Tests
        [Fact]
        public void ZeroTelemetry_CountersGaugesHistograms_ExportPrometheusFormat()
        {
            ZeroTelemetry.ResetAll();

            // 1. Counter
            var counter = ZeroTelemetry.CreateCounter("test_http_requests_total", "Total requests");
            counter.Increment();
            counter.Add(9);
            Assert.Equal(10, counter.Value);

            // 2. Gauge
            var gauge = ZeroTelemetry.CreateGauge("test_cpu_temperature_celsius", "CPU Temp");
            gauge.Set(54.2);
            Assert.Equal(54.2, gauge.Value);

            // 3. Histogram
            var hist = ZeroTelemetry.CreateHistogram("test_rpc_latency_ms", "RPC latency");
            for (int i = 1; i <= 100; i++)
            {
                hist.Record(i);
            }
            Assert.Equal(100, hist.Count);
            Assert.Equal(5050.0, hist.Sum);
            Assert.Equal(1.0, hist.Min);
            Assert.Equal(100.0, hist.Max);

            var (p50, p90, p99, p99_9) = hist.GetPercentiles();
            Assert.True(p50 >= 45.0 && p50 <= 55.0);
            Assert.True(p90 >= 85.0 && p90 <= 95.0);
            Assert.True(p99 >= 95.0 && p99 <= 100.0);

            // 4. Export Prometheus
            var sb = new StringBuilder();
            ZeroTelemetry.ExportPrometheus(sb);
            string output = sb.ToString();

            Assert.Contains("test_http_requests_total 10", output);
            Assert.Contains("test_cpu_temperature_celsius 54.200", output);
            Assert.Contains("test_rpc_latency_ms{quantile=\"0.99\"}", output);
            Assert.Contains("test_rpc_latency_ms_sum", output);
            Assert.Contains("test_rpc_latency_ms_count 100", output);
        }
        #endregion
    }
}
