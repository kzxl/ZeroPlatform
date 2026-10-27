using System;
using System.Threading;
using Xunit;
using ZeroUI.Core.Platform;
using ZeroUI.Core.Rendering;

namespace ZeroUI.Core.Tests
{
    public class AdaptiveRefreshAndConflationTests
    {
        [Fact]
        public void DisplayRefreshDetector_ReturnsValidClampedHz()
        {
            int hz = DisplayRefreshDetector.GetPrimaryDisplayRefreshRate();
            Assert.InRange(hz, 30, 240);
        }

        [Fact]
        public void ZeroAnimationClock_TargetFps_AllowsUpTo240Hz()
        {
            int original = ZeroAnimationClock.TargetFps;
            try
            {
                ZeroAnimationClock.TargetFps = 120;
                Assert.Equal(120, ZeroAnimationClock.TargetFps);

                ZeroAnimationClock.TargetFps = 144;
                Assert.Equal(144, ZeroAnimationClock.TargetFps);

                ZeroAnimationClock.TargetFps = 240;
                Assert.Equal(240, ZeroAnimationClock.TargetFps);

                // Over 240 clamps to 240
                ZeroAnimationClock.TargetFps = 360;
                Assert.Equal(240, ZeroAnimationClock.TargetFps);

                // Below 10 clamps to 10
                ZeroAnimationClock.TargetFps = 5;
                Assert.Equal(10, ZeroAnimationClock.TargetFps);
            }
            finally
            {
                ZeroAnimationClock.TargetFps = original;
            }
        }

        [Fact]
        public void ZeroAnimationClock_AutoSynchronizeWithDisplay_MatchesDetected()
        {
            int original = ZeroAnimationClock.TargetFps;
            try
            {
                int detected = ZeroAnimationClock.AutoSynchronizeWithDisplay();
                Assert.InRange(detected, 30, 240);
                Assert.Equal(detected, ZeroAnimationClock.TargetFps);
                Assert.Equal(detected, ZeroAnimationClock.DetectedDisplayFps);
            }
            finally
            {
                ZeroAnimationClock.TargetFps = original;
            }
        }

        [Fact]
        public void ScrollConflation_AccumulatesHighFrequencyEvents_AndFlushesSinglePass()
        {
            int pendingDeltaY = 0;
            bool hasPending = false;

            // Simulate 50 high-frequency mouse wheel ticks (1,000 Hz mouse)
            const int eventCount = 50;
            const int singleDelta = 78; // 1 notch = rowHeight * 3

            for (int i = 0; i < eventCount; i++)
            {
                Interlocked.Add(ref pendingDeltaY, singleDelta);
                hasPending = true;
            }

            Assert.True(hasPending);
            Assert.Equal(eventCount * singleDelta, pendingDeltaY);

            // On frame tick (e.g. at 8.33ms / 120Hz):
            int flushedDelta = Interlocked.Exchange(ref pendingDeltaY, 0);
            hasPending = false;

            Assert.Equal(eventCount * singleDelta, flushedDelta);
            Assert.Equal(0, pendingDeltaY);
            Assert.False(hasPending);
        }
    }
}
