using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ZeroPlatform.Concurrency.Tests
{
    public class ZeroMpmcRingBufferTests
    {
        [Fact]
        public void Basic_EnqueueDequeue_WorksAsExpected()
        {
            var ring = new ZeroMpmcRingBuffer<int>(8);
            Assert.True(ring.IsEmpty);
            Assert.Equal(0, ring.Count);
            Assert.Equal(8, ring.Capacity);

            Assert.True(ring.TryEnqueue(100));
            Assert.True(ring.TryEnqueue(200));
            Assert.False(ring.IsEmpty);

            Assert.True(ring.TryDequeue(out int val1));
            Assert.Equal(100, val1);
            Assert.True(ring.TryDequeue(out int val2));
            Assert.Equal(200, val2);
            Assert.True(ring.IsEmpty);
        }

        [Fact]
        public async Task MPMC_MultiThreadedProducersAndConsumers_IntegrityPreserved()
        {
            const int Producers = 4;
            const int Consumers = 4;
            const int ItemsPerProducer = 100_000;
            const int TotalExpectedItems = Producers * ItemsPerProducer;

            var ring = new ZeroMpmcRingBuffer<int>(2048);
            long totalConsumedSum = 0;
            int totalConsumedCount = 0;

            long expectedSum = 0;
            for (int p = 0; p < Producers; p++)
            {
                for (int i = 1; i <= ItemsPerProducer; i++)
                {
                    expectedSum += i;
                }
            }

            var consumerTasks = new Task[Consumers];
            for (int c = 0; c < Consumers; c++)
            {
                consumerTasks[c] = Task.Run(() =>
                {
                    SpinWait spinner = default;
                    while (Volatile.Read(ref totalConsumedCount) < TotalExpectedItems)
                    {
                        if (ring.TryDequeue(out int item))
                        {
                            Interlocked.Add(ref totalConsumedSum, item);
                            Interlocked.Increment(ref totalConsumedCount);
                            spinner.Reset();
                        }
                        else
                        {
                            spinner.SpinOnce();
                        }
                    }
                });
            }

            var producerTasks = new Task[Producers];
            for (int p = 0; p < Producers; p++)
            {
                producerTasks[p] = Task.Run(() =>
                {
                    SpinWait spinner = default;
                    for (int i = 1; i <= ItemsPerProducer; i++)
                    {
                        while (!ring.TryEnqueue(i))
                        {
                            spinner.SpinOnce();
                        }
                        spinner.Reset();
                    }
                });
            }

            await Task.WhenAll(producerTasks);
            await Task.WhenAll(consumerTasks);

            Assert.Equal(TotalExpectedItems, totalConsumedCount);
            Assert.Equal(expectedSum, totalConsumedSum);
            Assert.True(ring.IsEmpty);
        }

        [Fact]
        public async Task MPMC64_MultiThreadedProducersAndConsumers_IntegrityPreserved()
        {
            const int Producers = 4;
            const int Consumers = 4;
            const int ItemsPerProducer = 50_000;
            const int TotalExpectedItems = Producers * ItemsPerProducer;

            var ring = new ZeroMpmcRingBuffer64<int>(2048);
            long totalConsumedSum = 0;
            int totalConsumedCount = 0;

            long expectedSum = 0;
            for (int p = 0; p < Producers; p++)
            {
                for (int i = 1; i <= ItemsPerProducer; i++)
                {
                    expectedSum += i;
                }
            }

            var consumerTasks = new Task[Consumers];
            for (int c = 0; c < Consumers; c++)
            {
                consumerTasks[c] = Task.Run(() =>
                {
                    SpinWait spinner = default;
                    while (Volatile.Read(ref totalConsumedCount) < TotalExpectedItems)
                    {
                        if (ring.TryDequeue(out int val))
                        {
                            Interlocked.Add(ref totalConsumedSum, val);
                            Interlocked.Increment(ref totalConsumedCount);
                            spinner.Reset();
                        }
                        else
                        {
                            spinner.SpinOnce();
                        }
                    }
                });
            }

            var producerTasks = new Task[Producers];
            for (int p = 0; p < Producers; p++)
            {
                producerTasks[p] = Task.Run(() =>
                {
                    SpinWait spinner = default;
                    for (int i = 1; i <= ItemsPerProducer; i++)
                    {
                        while (!ring.TryEnqueue(i))
                        {
                            spinner.SpinOnce();
                        }
                        spinner.Reset();
                    }
                });
            }

            await Task.WhenAll(producerTasks);
            await Task.WhenAll(consumerTasks);

            Assert.Equal(TotalExpectedItems, totalConsumedCount);
            Assert.Equal(expectedSum, totalConsumedSum);
            Assert.True(ring.IsEmpty);
        }
    }
}
