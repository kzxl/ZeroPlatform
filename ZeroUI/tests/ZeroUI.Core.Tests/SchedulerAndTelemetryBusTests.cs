using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    public class SchedulerAndTelemetryBusTests
    {
        [Fact]
        public void ZeroScheduler_ExecutesIntervalJob_IncrementsCount()
        {
            var scheduler = new ZeroScheduler("TestScheduler");
            int counter = 0;

            var job = scheduler.ScheduleInterval("job_1", TimeSpan.FromMilliseconds(5), () =>
            {
                Interlocked.Increment(ref counter);
            });

            scheduler.Start();
            Thread.Sleep(50);
            scheduler.Stop();

            Assert.True(counter >= 3, $"Counter was {counter}, expected at least 3");
            Assert.True(job.Metrics.Invocations >= 3);
            Assert.True(job.Metrics.AverageDurationMs >= 0.0);
        }

        [Fact]
        public void ZeroScheduler_PauseAndRemoveJob_FunctionsCorrectly()
        {
            var scheduler = new ZeroScheduler("TestScheduler2");
            int count = 0;

            var job = scheduler.ScheduleInterval("job_pause", TimeSpan.FromMilliseconds(5), () =>
            {
                Interlocked.Increment(ref count);
            });

            scheduler.Start();
            Thread.Sleep(20);

            job.SetEnabled(false);
            int snapshot = count;
            Thread.Sleep(20);

            Assert.Equal(snapshot, count); // Paused, no further execution

            Assert.True(scheduler.RemoveJob("job_pause"));
            Assert.False(scheduler.TryGetJob("job_pause", out _));

            scheduler.Stop();
        }

        [Fact]
        public void ZeroTelemetryBus_PublishSpan_ReceivesBatch()
        {
            var bus = new ZeroTelemetryBus("TestBus");
            int receivedCount = 0;
            double lastVal = 0.0;

            using var sub = bus.SubscribeUpdates(batch =>
            {
                Interlocked.Add(ref receivedCount, batch.Length);
                if (batch.Length > 0)
                {
                    lastVal = batch[batch.Length - 1].Value.AsDouble();
                }
            });

            Span<TagUpdate> updates = stackalloc TagUpdate[3];
            updates[0] = new TagUpdate(101, new ScadaValue(12.5), 1000);
            updates[1] = new TagUpdate(102, new ScadaValue(45.0), 1000);
            updates[2] = new TagUpdate(103, new ScadaValue(99.9), 1000);

            bus.Publish(updates);

            Assert.Equal(3, receivedCount);
            Assert.Equal(99.9, lastVal);
            Assert.Equal(3, bus.TotalUpdatesPublished);
            Assert.Equal(1, bus.TotalBatchesPublished);
        }

        [Fact]
        public void ZeroTelemetryBus_TypedTopics_DispatchesCorrectly()
        {
            var bus = new ZeroTelemetryBus("TestBusTopic");
            string receivedMsg = string.Empty;

            using var sub = bus.Subscribe<string>("alarms", msg =>
            {
                receivedMsg = msg;
            });

            bus.Publish("alarms", "HIGH_TEMP_WARNING");
            Assert.Equal("HIGH_TEMP_WARNING", receivedMsg);

            // Unsubscribe test
            sub.Dispose();
            bus.Publish("alarms", "CRITICAL_PRESSURE");
            Assert.Equal("HIGH_TEMP_WARNING", receivedMsg); // Not updated because disposed
        }
    }
}
