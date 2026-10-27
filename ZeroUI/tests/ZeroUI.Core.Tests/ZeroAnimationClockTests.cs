using System;
using System.Threading;
using Xunit;
using ZeroUI.Core.Rendering;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Tests
{
    public class ZeroAnimationClockTests
    {
        [Fact]
        public void BlinkPhases_ToggleDeterministically_BasedOnElapsedTime()
        {
            // Reset and trigger initial tick
            ZeroAnimationClock.ManualTick(0.016);

            // BlinkFast toggles every 250ms (2 Hz)
            // BlinkSlow toggles every 500ms (1 Hz)
            bool initialFast = ZeroAnimationClock.BlinkFast;
            bool initialSlow = ZeroAnimationClock.BlinkSlow;

            // Advance by 300 ms -> BlinkFast must toggle, BlinkSlow should remain unchanged
            ZeroAnimationClock.ManualTick(0.300);
            Assert.NotEqual(initialFast, ZeroAnimationClock.BlinkFast);
            Assert.Equal(initialSlow, ZeroAnimationClock.BlinkSlow);

            // Advance by another 300 ms (total 600ms) -> BlinkSlow must now toggle
            ZeroAnimationClock.ManualTick(0.300);
            Assert.NotEqual(initialSlow, ZeroAnimationClock.BlinkSlow);
        }

        [Fact]
        public void ContinuousPhases_StayWithinValidRange()
        {
            for (double t = 0.0; t < 2.0; t += 0.05)
            {
                ZeroAnimationClock.ManualTick(0.05);

                float pulse = ZeroAnimationClock.PulsePhase;
                float fluid = ZeroAnimationClock.FluidPhase;

                Assert.InRange(pulse, 0.0f, 1.0f);
                Assert.InRange(fluid, 0.0f, 1.0f);
            }
        }

        [Fact]
        public void ManualTick_AutomaticallyFlushes_UiDispatcherPendingActions()
        {
            UiDispatcher.Reset();

            bool dirtyExecuted = false;
            int finalValue = 0;

            // Enqueue dirty UI action via UiDispatcher
            UiDispatcher.EnqueueDirty("Tank1.Pressure", () =>
            {
                dirtyExecuted = true;
                finalValue = 42;
            });

            Assert.False(dirtyExecuted);

            // Frame tick on ZeroAnimationClock must auto-flush UiDispatcher
            ZeroAnimationClock.ManualTick(0.016);

            Assert.True(dirtyExecuted, "UiDispatcher.FlushPending() should be automatically called on animation frame tick");
            Assert.Equal(42, finalValue);

            UiDispatcher.Reset();
        }

        [Fact]
        public void SubscribeAndUnsubscribe_Lifecycle_ManagesSubscriberCountAndRunningState()
        {
            ZeroAnimationClock.Stop();
            Assert.False(ZeroAnimationClock.IsRunning);

            int callCount = 0;
            var sub = ZeroAnimationClock.Subscribe((delta, frame) => callCount++);

            Assert.True(ZeroAnimationClock.IsRunning, "Clock should start upon first subscription");
            Assert.True(ZeroAnimationClock.SubscriberCount > 0);

            // Dispose token to unsubscribe
            sub.Dispose();

            Assert.Equal(0, ZeroAnimationClock.SubscriberCount);
            Assert.False(ZeroAnimationClock.IsRunning, "Clock should automatically stop when subscribers drop to 0");
        }

        [Fact]
        public void ContractListener_ReceivesFrameUpdates()
        {
            var listener = new TestFrameListener();
            ZeroAnimationClock.Subscribe(listener);

            ZeroAnimationClock.ManualTick(0.033);

            Assert.True(listener.FramesReceived >= 1);
            Assert.True(listener.LastDelta > 0);

            ZeroAnimationClock.Unsubscribe(listener);
        }

        [Fact]
        public void SetSynchronizationContext_ConfiguresContextCorrectly()
        {
            var customContext = new SynchronizationContext();
            ZeroAnimationClock.SetSynchronizationContext(customContext);
            ZeroAnimationClock.ManualTick(0.016);
            Assert.True(ZeroAnimationClock.FrameCount > 0);
            ZeroAnimationClock.SetSynchronizationContext(null);
        }

        [Fact]
        public void DisplayRefreshDetector_ReturnsValidClampedRate()
        {
            int rate = ZeroUI.Core.Platform.DisplayRefreshDetector.GetPrimaryDisplayRefreshRate();
            Assert.InRange(rate, 30, 240);
        }

        [Fact]
        public void AutoSynchronizeWithDisplay_ConfiguresTargetFpsCorrectly()
        {
            int initialFps = ZeroAnimationClock.TargetFps;
            int syncFps = ZeroAnimationClock.AutoSynchronizeWithDisplay();

            Assert.InRange(syncFps, 30, 240);
            Assert.Equal(syncFps, ZeroAnimationClock.TargetFps);
            Assert.Equal(syncFps, ZeroAnimationClock.DetectedDisplayFps);

            // Restore initial Fps
            ZeroAnimationClock.TargetFps = initialFps;
        }

        private sealed class TestFrameListener : IAnimationFrameListener
        {
            public int FramesReceived { get; private set; }
            public double LastDelta { get; private set; }
            public long LastFrame { get; private set; }

            public void OnAnimationFrame(double deltaSeconds, long frameCount)
            {
                FramesReceived++;
                LastDelta = deltaSeconds;
                LastFrame = frameCount;
            }
        }
    }
}
