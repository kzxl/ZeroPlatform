using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Tests
{
    public class ZeroRuntimeTests
    {
        [Fact]
        public void VirtualTime_StepAdvancesCycles_InStrictDeterministicOrder()
        {
            using var runtime = new ZeroRuntime("DeterministicOrderTest", RuntimeMode.VirtualTime);
            var orderLog = new List<RuntimeCycle>();

            runtime.Register(RuntimeCycle.Plc, () => orderLog.Add(RuntimeCycle.Plc));
            runtime.Register(RuntimeCycle.Logic, () => orderLog.Add(RuntimeCycle.Logic));
            runtime.Register(RuntimeCycle.Telemetry, () => orderLog.Add(RuntimeCycle.Telemetry));
            runtime.Register(RuntimeCycle.Historian, () => orderLog.Add(RuntimeCycle.Historian));
            runtime.Register(RuntimeCycle.Ui, () => orderLog.Add(RuntimeCycle.Ui));
            runtime.Register(RuntimeCycle.Animation, () => orderLog.Add(RuntimeCycle.Animation));
            runtime.Register(RuntimeCycle.Cleanup, () => orderLog.Add(RuntimeCycle.Cleanup));

            runtime.Start();

            // Step by 1000 ms (1 second), so all 7 cycles trigger on this boundary
            runtime.Step(TimeSpan.FromMilliseconds(1000));

            // Verify strict deterministic order:
            // Plc -> Logic -> Telemetry -> Historian -> Ui -> Animation -> Cleanup
            Assert.True(orderLog.Count >= 7);

            // Take the first occurrence of each cycle and verify sequential ordering
            int idxPlc = orderLog.IndexOf(RuntimeCycle.Plc);
            int idxLogic = orderLog.IndexOf(RuntimeCycle.Logic);
            int idxTelemetry = orderLog.IndexOf(RuntimeCycle.Telemetry);
            int idxHistorian = orderLog.IndexOf(RuntimeCycle.Historian);
            int idxUi = orderLog.IndexOf(RuntimeCycle.Ui);
            int idxAnimation = orderLog.IndexOf(RuntimeCycle.Animation);
            int idxCleanup = orderLog.IndexOf(RuntimeCycle.Cleanup);

            Assert.True(idxPlc < idxLogic, "PLC must execute before Logic");
            Assert.True(idxLogic < idxTelemetry, "Logic must execute before Telemetry");
            Assert.True(idxTelemetry < idxHistorian, "Telemetry must execute before Historian");
            Assert.True(idxHistorian < idxUi, "Historian must execute before UI");
            Assert.True(idxUi < idxAnimation, "UI must execute before Animation");
            Assert.True(idxAnimation < idxCleanup, "Animation must execute before Cleanup");
        }

        [Fact]
        public void VirtualTime_HarmonicFrequencies_ExecutesAccurateIterationCounts()
        {
            using var runtime = new ZeroRuntime("HarmonicCadenceTest", RuntimeMode.VirtualTime);

            int plcCount = 0;
            int logicCount = 0;
            int historianCount = 0;
            int cleanupCount = 0;

            runtime.SetCycleInterval(RuntimeCycle.Plc, TimeSpan.FromMilliseconds(10));
            runtime.SetCycleInterval(RuntimeCycle.Logic, TimeSpan.FromMilliseconds(10));
            runtime.SetCycleInterval(RuntimeCycle.Historian, TimeSpan.FromMilliseconds(100));
            runtime.SetCycleInterval(RuntimeCycle.Cleanup, TimeSpan.FromMilliseconds(1000));

            runtime.Register(RuntimeCycle.Plc, () => plcCount++);
            runtime.Register(RuntimeCycle.Logic, () => logicCount++);
            runtime.Register(RuntimeCycle.Historian, () => historianCount++);
            runtime.Register(RuntimeCycle.Cleanup, () => cleanupCount++);

            runtime.Start();

            // Advance time by 10 ms steps, up to 1000 ms total
            for (int i = 0; i < 100; i++)
            {
                runtime.Step(TimeSpan.FromMilliseconds(10));
            }

            Assert.Equal(100, plcCount);
            Assert.Equal(100, logicCount);
            Assert.Equal(10, historianCount);
            Assert.Equal(1, cleanupCount);
        }

        [Fact]
        public void RealTime_RunsBackgroundMasterLoop_TracksMetrics()
        {
            using var runtime = new ZeroRuntime("RealTimeMetricsTest", RuntimeMode.RealTime);
            int triggerCount = 0;

            runtime.SetCycleInterval(RuntimeCycle.Logic, TimeSpan.FromMilliseconds(5));
            runtime.Register(RuntimeCycle.Logic, () => Interlocked.Increment(ref triggerCount));

            runtime.Start();
            Assert.True(runtime.IsRunning);

            // Wait ~60 ms in real time for ~8-12 cycles to tick
            Thread.Sleep(60);
            runtime.Stop();

            Assert.False(runtime.IsRunning);
            Assert.True(triggerCount >= 2, $"Expected at least 2 triggers, got {triggerCount}");

            var stats = runtime.GetCycleStats(RuntimeCycle.Logic);
            Assert.True(stats.CycleCount >= 2);
            Assert.True(stats.AvgDurationMicros >= 0);
        }

        [Fact]
        public void CycleOverrun_DetectionAndDiagnostics_AccuratelyTracked()
        {
            using var runtime = new ZeroRuntime("OverrunTest", RuntimeMode.VirtualTime);

            runtime.SetCycleInterval(RuntimeCycle.Logic, TimeSpan.FromMilliseconds(1)); // 1 ms threshold
            bool overrunFired = false;
            runtime.CycleOverrun += (cycle, durMs, intMs) =>
            {
                if (cycle == RuntimeCycle.Logic) overrunFired = true;
            };

            // Register a task that artificially spins to exceed 1 ms
            runtime.Register(RuntimeCycle.Logic, () =>
            {
                Thread.Sleep(3); // 3 ms > 1 ms interval
            });

            runtime.Start();
            runtime.Step(TimeSpan.FromMilliseconds(1));

            var stats = runtime.GetCycleStats(RuntimeCycle.Logic);
            Assert.True(stats.OverrunCount > 0, "Overrun count should be incremented");
            Assert.True(overrunFired, "CycleOverrun event should have fired");
        }

        [Fact]
        public void Unregister_CleanlyDetachesListeners()
        {
            using var runtime = new ZeroRuntime("UnregisterTest", RuntimeMode.VirtualTime);
            int counter = 0;

            var sub = runtime.Register(RuntimeCycle.Plc, () => counter++);

            runtime.Start();
            runtime.Step(TimeSpan.FromMilliseconds(10));
            Assert.Equal(1, counter);

            // Dispose subscription token
            sub.Dispose();

            // Next step should not invoke counter
            runtime.Step(TimeSpan.FromMilliseconds(10));
            Assert.Equal(1, counter);
        }

        [Fact]
        public void PauseAndResume_FreezesAndResumesSchedule()
        {
            using var runtime = new ZeroRuntime("PauseResumeTest", RuntimeMode.VirtualTime);
            int counter = 0;

            runtime.Register(RuntimeCycle.Plc, () => counter++);
            runtime.Start();

            runtime.Step(TimeSpan.FromMilliseconds(10));
            Assert.Equal(1, counter);

            // Pause
            runtime.Pause();
            Assert.True(runtime.IsPaused);

            runtime.Step(TimeSpan.FromMilliseconds(10));
            Assert.Equal(1, counter); // No increment while paused

            // Resume
            runtime.Resume();
            Assert.False(runtime.IsPaused);

            runtime.Step(TimeSpan.FromMilliseconds(10));
            Assert.Equal(2, counter);
        }

        [Fact]
        public void IRuntimeTask_ZeroAllocationContract_ExecutesSuccessfully()
        {
            using var runtime = new ZeroRuntime("ClassTaskTest", RuntimeMode.VirtualTime);
            var mockTask = new MockPlcTask();

            using var sub = runtime.Register(RuntimeCycle.Plc, mockTask);
            runtime.Start();

            runtime.Step(TimeSpan.FromMilliseconds(10));

            Assert.Equal(1, mockTask.ExecutionCount);
            Assert.True(mockTask.LastDelta.TotalMilliseconds > 0);
            Assert.Equal(1, mockTask.LastIndex);
        }

        [Fact]
        public void Diagnostics_AggregatesAllSevenCycles()
        {
            using var runtime = new ZeroRuntime("DiagnosticsTest", RuntimeMode.VirtualTime);
            runtime.Start();
            runtime.Step(TimeSpan.FromMilliseconds(1000));

            var diag = runtime.GetDiagnostics();
            Assert.NotNull(diag);
            Assert.Equal(7, diag.Cycles.Length);

            var plcStats = diag.GetStats(RuntimeCycle.Plc);
            Assert.Equal(RuntimeCycle.Plc, plcStats.Cycle);
            Assert.Equal(10.0, plcStats.IntervalMs);
            Assert.True(plcStats.IsEnabled);
            Assert.False(plcStats.IsUiMarshaled);

            var uiStats = diag.GetStats(RuntimeCycle.Ui);
            Assert.Equal(RuntimeCycle.Ui, uiStats.Cycle);
            Assert.True(uiStats.IsUiMarshaled);
        }

        private sealed class MockPlcTask : IRuntimeTask
        {
            public int ExecutionCount { get; private set; }
            public TimeSpan LastDelta { get; private set; }
            public long LastIndex { get; private set; }

            public void Execute(TimeSpan delta, long cycleIndex)
            {
                ExecutionCount++;
                LastDelta = delta;
                LastIndex = cycleIndex;
            }
        }
    }
}
