using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Concurrency;

namespace ZeroPrimitives.Tests
{
    public class ConcurrencyTests
    {
        [Fact]
        public void FastSpinLock_SynchronizesConcurrentIncrements()
        {
            var spinLock = new FastSpinLock();
            int counter = 0;
            const int threadsCount = 4;
            const int iterationsPerThread = 25_000;

            Parallel.For(0, threadsCount, _ =>
            {
                for (int i = 0; i < iterationsPerThread; i++)
                {
                    spinLock.Enter();
                    try
                    {
                        counter++;
                    }
                    finally
                    {
                        spinLock.Exit();
                    }
                }
            });

            Assert.Equal(threadsCount * iterationsPerThread, counter);
        }

        [Fact]
        public async Task SpscQueue_SingleProducerSingleConsumer_TransfersItemsAccurately()
        {
            var queue = new SpscQueue<int>(1024);
            const int totalItems = 100_000;
            long consumedSum = 0;

            var consumerTask = Task.Run(() =>
            {
                int received = 0;
                while (received < totalItems)
                {
                    if (queue.TryDequeue(out int item))
                    {
                        consumedSum += item;
                        received++;
                    }
                    else
                    {
                        Thread.SpinWait(10);
                    }
                }
            });

            var producerTask = Task.Run(() =>
            {
                for (int i = 1; i <= totalItems; i++)
                {
                    while (!queue.TryEnqueue(i))
                    {
                        Thread.SpinWait(10);
                    }
                }
            });

            await Task.WhenAll(producerTask, consumerTask);

            long expectedSum = (long)totalItems * (totalItems + 1) / 2;
            Assert.Equal(expectedSum, consumedSum);
            Assert.True(queue.IsEmpty);
        }
    }
}
