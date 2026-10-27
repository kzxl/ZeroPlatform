using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Tests
{
    public class WorkerQueueConflationTests
    {
        private sealed class TelemetryUpdate
        {
            public string Key { get; }
            public double Value { get; }

            public TelemetryUpdate(string key, double value)
            {
                Key = key;
                Value = value;
            }
        }

        [Fact]
        public async Task WorkerQueue_LatestPerKey_ConflatesIntermediateStaleUpdates()
        {
            var processed = new List<TelemetryUpdate>();
            using var barrier = new SemaphoreSlim(0, 100);

            // Block handler artificially to simulate high load / backpressure
            using var queue = new WorkerQueue<TelemetryUpdate>(
                handler: async (item, ct) =>
                {
                    await Task.Delay(10, ct);
                    lock (processed)
                    {
                        processed.Add(item);
                    }
                    barrier.Release();
                },
                capacity: 50,
                backpressure: QueueBackpressureMode.LatestPerKey,
                concurrency: 1,
                keySelector: item => item.Key);

            // Enqueue rapid bursts for Motor1 and Motor2
            queue.TryEnqueue(new TelemetryUpdate("Motor1", 10.0));
            queue.TryEnqueue(new TelemetryUpdate("Motor1", 20.0));
            queue.TryEnqueue(new TelemetryUpdate("Motor1", 30.0));
            queue.TryEnqueue(new TelemetryUpdate("Motor1", 40.0));

            queue.TryEnqueue(new TelemetryUpdate("Motor2", 100.0));
            queue.TryEnqueue(new TelemetryUpdate("Motor2", 200.0));
            queue.TryEnqueue(new TelemetryUpdate("Motor2", 300.0));

            await queue.CompleteAsync();

            // Motor1 and Motor2 should be processed with latest values (40.0 and 300.0)
            lock (processed)
            {
                var motor1Items = processed.FindAll(x => x.Key == "Motor1");
                var motor2Items = processed.FindAll(x => x.Key == "Motor2");

                Assert.True(motor1Items.Count >= 1);
                Assert.True(motor2Items.Count >= 1);

                // The final processed item for each key MUST be the latest
                Assert.Equal(40.0, motor1Items[motor1Items.Count - 1].Value);
                Assert.Equal(300.0, motor2Items[motor2Items.Count - 1].Value);
            }

            // Verify dropped / conflated count tracked accurately
            Assert.True(queue.DroppedCount > 0);
        }
    }
}
