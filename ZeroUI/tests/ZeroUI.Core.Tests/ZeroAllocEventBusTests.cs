using System;
using Xunit;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Tests
{
    public class ZeroAllocEventBusTests
    {
        public readonly struct TestTelemetryEvent
        {
            public readonly int TagId;
            public readonly double Value;

            public TestTelemetryEvent(int tagId, double value)
            {
                TagId = tagId;
                Value = value;
            }
        }

        [Fact]
        public void EventBus_Publish_DeliversToSubscribers_Correctly()
        {
            var bus = new EventBus();
            int receivedCount = 0;
            double lastValue = 0.0;

            using (bus.Subscribe<TestTelemetryEvent>(e =>
            {
                receivedCount++;
                lastValue = e.Value;
            }))
            {
                bus.Publish(new TestTelemetryEvent(1, 42.5));
                bus.Publish(new TestTelemetryEvent(2, 99.1));
            }

            // After dispose, no more events should be delivered
            bus.Publish(new TestTelemetryEvent(3, 100.0));

            Assert.Equal(2, receivedCount);
            Assert.Equal(99.1, lastValue);
        }

        [Fact]
        public void EventBus_Publish_ZeroAllocationInHotLoop()
        {
            var bus = new EventBus();
            int counter = 0;

            using (bus.Subscribe<TestTelemetryEvent>(e =>
            {
                counter += e.TagId;
            }))
            {
                // Warmup JIT
                for (int i = 0; i < 100; i++)
                {
                    bus.Publish(new TestTelemetryEvent(i, i * 1.5));
                }

                // Measure thread allocations in hot loop
                long beforeBytes = GC.GetAllocatedBytesForCurrentThread();

                for (int i = 0; i < 1000; i++)
                {
                    bus.Publish(new TestTelemetryEvent(1, 2.0));
                }

                long afterBytes = GC.GetAllocatedBytesForCurrentThread();
                long allocated = afterBytes - beforeBytes;

                Assert.Equal(0, allocated);
                Assert.True(counter > 0);
            }
        }
    }
}
