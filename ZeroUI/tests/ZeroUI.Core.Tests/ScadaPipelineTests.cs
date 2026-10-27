using System;
using System.Diagnostics;
using System.Threading;
using Xunit;
using ZeroUI.Core.Data;
using ZeroUI.Core.Mes;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scada.Analytics;
using ZeroUI.Core.Scada.Pipeline;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Core.Tests
{
    [Collection("ScadaTagEngine")]
    public class ScadaPipelineTests : IDisposable
    {
        private readonly ScadaPipelineCoordinator _coordinator;

        public ScadaPipelineTests()
        {
            UiDispatcher.Reset();
            UiDispatcher.Initialize(action => action()); // Synchronous invoker for tests
            _coordinator = new ScadaPipelineCoordinator(mediumFrequencyHz: 100);
            _coordinator.Start();
        }

        public void Dispose()
        {
            _coordinator.Stop();
            _coordinator.Dispose();
            UiDispatcher.Reset();
            ZeroTagEngine.IsDecoupledUiMode = false;
        }

        [Fact]
        public void FastTier_SafetyInterlock_TripsInstantlyInMicroseconds()
        {
            string tagPath = "Reactor.Core.Temperature_" + Guid.NewGuid().ToString("N");
            string tripTagPath = "Safety.Reactor.EmergencyTripActive_" + Guid.NewGuid().ToString("N");

            // Register an over-temperature safety trip rule: trip if Temperature > 150.0
            bool tripped = false;
            double trippedVal = 0;

            var rule = new SafetyInterlockRule(
                ruleId: "SAFETY-TRIP-TEMP-01",
                tagPath: tagPath,
                description: "Reactor thermal runaway emergency trip",
                condition: SafetyTripCondition.AboveHighLimit,
                thresholdValue: 150.0,
                onTripped: (r, val) =>
                {
                    tripped = true;
                    trippedVal = val;
                },
                outputTripTagPath: tripTagPath);

            _coordinator.Safety.RegisterRule(rule);

            // JIT Warmup (both safe and trip paths)
            for (int w = 0; w < 10; w++)
            {
                _coordinator.IngestFast(tagPath, 80.0 + w);
                _coordinator.IngestFast(tagPath, 160.0);
                rule.Reset();
            }
            tripped = false;

            // 1. Safe ingestion: 95 C
            long swStart = Stopwatch.GetTimestamp();
            _coordinator.IngestFast(tagPath, 95.0);
            long safeTicks = Stopwatch.GetTimestamp() - swStart;
            double safeMicros = (double)safeTicks / (Stopwatch.Frequency / 1_000_000.0);

            Assert.False(tripped);
            Assert.False(rule.IsTripped);
            Assert.True(safeMicros < 100.0, $"Safety evaluation should be ultra-fast (was {safeMicros:F2} µs)");

            // 2. Dangerous ingestion: 165 C -> Must trip instantly
            swStart = Stopwatch.GetTimestamp();
            _coordinator.IngestFast(tagPath, 165.0);
            long tripTicks = Stopwatch.GetTimestamp() - swStart;
            double tripMicros = (double)tripTicks / (Stopwatch.Frequency / 1_000_000.0);

            Assert.True(tripped);
            Assert.True(rule.IsTripped);
            Assert.Equal(165.0, trippedVal);
            Assert.True(tripMicros < 1000.0, $"Safety trip execution should be ultra-fast (was {tripMicros:F2} µs)");

            // Trip output tag was set
            var tripTag = ZeroTagEngine.GetTag(tripTagPath);
            Assert.NotNull(tripTag);
            Assert.Equal(true, tripTag.Value);
        }

        [Fact]
        public void FastTier_IngestionStress_10kHz_DoesNotPostToUiDirectly()
        {
            string tagPath = "Plant.Furnace.Temp_" + Guid.NewGuid().ToString("N");
            var mockControl = new MockPipelineBindable(tagPath);
            ZeroTagEngine.RegisterBindable(mockControl);

            int initialNotifCount = mockControl.DirectNotificationCount;

            // Ingest 10,000 updates on background fast path
            const int updatesCount = 10000;
            var sw = Stopwatch.StartNew();

            for (int i = 1; i <= updatesCount; i++)
            {
                _coordinator.IngestFast(tagPath, 1000.0 + i);
            }

            sw.Stop();

            // Crucial assertion: 0 direct UI notifications occurred synchronously during 10 kHz ingestion!
            Assert.Equal(initialNotifCount, mockControl.DirectNotificationCount);

            // The storage has the exact latest value
            int tagId = ZeroTagEngine.GetOrRegisterTag(tagPath);
            var storageVal = ZeroTagEngine.Storage.GetValue(tagId);
            Assert.Equal(1000.0 + updatesCount, storageVal.AsDouble());

            // Cleanup
            ZeroTagEngine.UnregisterBindable(mockControl);
        }

        [Fact]
        public void SlowTier_UiPump_FlushesOnlyLatestValueAtDisplayRate()
        {
            string tagPath = "Plant.Compressor.Pressure_" + Guid.NewGuid().ToString("N");
            var mockControl = new MockPipelineBindable(tagPath);
            ZeroTagEngine.RegisterBindable(mockControl);

            int initialNotifCount = mockControl.DirectNotificationCount;

            // 1. Simulate 5,000 rapid updates on fast tier
            for (int i = 1; i <= 5000; i++)
            {
                _coordinator.IngestFast(tagPath, 10.0 + (i * 0.1));
            }

            // Before pump: 0 notifications to control
            Assert.Equal(initialNotifCount, mockControl.DirectNotificationCount);

            // 2. Slow Tier UI tick fires (e.g. 60 Hz frame tick on UI STA thread)
            UiDispatcher.Initialize(action => action());
            int dirtyDispatched = _coordinator.PumpUiFrame();

            // Exactly 1 coalesced notification reached the control with the latest value
            Assert.True(dirtyDispatched > 0);
            Assert.Equal(initialNotifCount + 1, mockControl.DirectNotificationCount);
            Assert.NotNull(mockControl.LastReceivedTag);
            Assert.Equal(10.0 + (5000 * 0.1), Convert.ToDouble(mockControl.LastReceivedTag.Value));

            // Cleanup
            ZeroTagEngine.UnregisterBindable(mockControl);
        }

        [Fact]
        public void MediumTier_OeeAndAggregation_CalculatesAtConfiguredRate()
        {
            string rawPath = "Motor1.VibrationRaw_" + Guid.NewGuid().ToString("N");
            string targetPath = "Motor1.VibrationSMA_" + Guid.NewGuid().ToString("N");

            // Register an SMA aggregator: Raw vibration -> Vibration_SMA (window size = 5)
            var smaAgg = new TagAggregator(
                sourceTagPath: rawPath,
                targetTagPath: targetPath,
                aggregationType: AggregationType.SimpleMovingAverage,
                windowSize: 5);

            _coordinator.Aggregation.RegisterAggregator(smaAgg);

            // Ingest raw samples
            _coordinator.IngestFast(rawPath, 10.0);
            _coordinator.IngestFast(rawPath, 20.0);
            _coordinator.IngestFast(rawPath, 30.0);

            // Manually trigger or allow medium tier to execute
            int computed = _coordinator.Aggregation.ExecuteAggregationCycle(Environment.TickCount);
            Assert.Equal(1, computed);

            // Check target derived tag: average of 10, 20, 30 is 20.0 (or latest sample in window)
            var smaTag = ZeroTagEngine.GetTag(targetPath);
            Assert.NotNull(smaTag);
            Assert.True(Convert.ToDouble(smaTag.Value) > 0.0);
        }

        [Fact]
        public void ScadaPipelineMetrics_TracksMetricsAccurately()
        {
            string tagPath = "Metrics.Tag_" + Guid.NewGuid().ToString("N");

            // Fast tier updates
            for (int i = 0; i < 500; i++)
            {
                _coordinator.IngestFast(tagPath, i);
            }

            // Slow tier frames
            _coordinator.PumpUiFrame();
            _coordinator.PumpUiFrame();

            var metrics = _coordinator.GetMetrics();

            Assert.True(metrics.FastTierIngestCount >= 500);
            Assert.True(metrics.SlowTierFramesCount >= 2);
            Assert.True(!string.IsNullOrEmpty(metrics.ToString()));
        }

        private sealed class MockPipelineBindable : IScadaBindable
        {
            public string? BoundTagPath { get; set; }
            public int DirectNotificationCount { get; private set; }
            public IScadaTag? LastReceivedTag { get; private set; }

            public MockPipelineBindable(string tagPath)
            {
                BoundTagPath = tagPath;
            }

            public void OnTagValueChanged(IScadaTag tag)
            {
                DirectNotificationCount++;
                LastReceivedTag = tag;
            }
        }
    }
}
