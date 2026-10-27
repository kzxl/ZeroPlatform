using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ZeroPlatform.Concurrency.Tests
{
    public class ZeroChannelTests
    {
        [Fact]
        public async Task Channel_WriteAndReadAsync_TransfersDataCleanly()
        {
            var channel = new ZeroChannel<string>(64);

            var producer = Task.Run(async () =>
            {
                await channel.WriteAsync("alpha");
                await channel.WriteAsync("beta");
                await channel.WriteAsync("gamma");
                channel.Complete();
            });

            var received = new List<string>();
            await foreach (var item in channel.ReadAllAsync())
            {
                received.Add(item);
            }

            await producer;

            Assert.Equal(new[] { "alpha", "beta", "gamma" }, received);
            Assert.True(channel.IsCompleted);
        }

        [Fact]
        public void Channel_TryWriteAndTryRead_SynchronousFastPath()
        {
            var channel = new ZeroChannel<int>(16);

            Assert.True(channel.TryWrite(1));
            Assert.True(channel.TryWrite(2));
            Assert.Equal(2, channel.Count);

            Assert.True(channel.TryRead(out int val1));
            Assert.Equal(1, val1);
            Assert.True(channel.TryRead(out int val2));
            Assert.Equal(2, val2);
            Assert.Equal(0, channel.Count);
        }

        [Fact]
        public async Task Channel_WriteToClosed_ThrowsClosedException()
        {
            var channel = new ZeroChannel<int>(16);
            channel.Complete();

            Assert.Throws<ZeroChannelClosedException>(() =>
            {
                channel.TryWrite(99);
            });

            await Assert.ThrowsAsync<ZeroChannelClosedException>(async () =>
            {
                await channel.WriteAsync(99);
            });
        }

        [Fact]
        public async Task Channel_DirectHandoff_ReaderWaitsFirst()
        {
            var channel = new ZeroChannel<int>(16);

            // Start reader before any data is written
            var readerTask = Task.Run(async () =>
            {
                return await channel.ReadAsync();
            });

            // Allow reader to register its promise
            await Task.Delay(20);

            // Writer performs direct handoff to waiting reader
            await channel.WriteAsync(777);

            int result = await readerTask;
            Assert.Equal(777, result);
        }

        [Fact]
        public async Task Channel_Backpressure_WriterWaitsForReader()
        {
            // Tiny buffer of 2 slots
            var channel = new ZeroChannel<int>(2);

            // Fill buffer completely
            Assert.True(channel.TryWrite(1));
            Assert.True(channel.TryWrite(2));

            // Third write will suspend until consumer reads
            var writerTask = Task.Run(async () =>
            {
                await channel.WriteAsync(3);
                await channel.WriteAsync(4);
            });

            await Task.Delay(20);

            // Consume first item to unblock writer
            Assert.True(channel.TryRead(out int first));
            Assert.Equal(1, first);

            Assert.True(channel.TryRead(out int second));
            Assert.Equal(2, second);

            await writerTask;

            Assert.True(channel.TryRead(out int third));
            Assert.Equal(3, third);
            Assert.True(channel.TryRead(out int fourth));
            Assert.Equal(4, fourth);
        }

        [Fact]
        public async Task Channel_HighConcurrency_TinyBuffer_MassiveHandoff()
        {
            const int Producers = 4;
            const int Consumers = 4;
            const int ItemsPerProducer = 10_000;
            const int TotalItems = Producers * ItemsPerProducer;

            // Tiny buffer capacity: forces thousands of direct handoffs and waiter queues
            var channel = new ZeroChannel<int>(4);

            long totalSumConsumed = 0;
            int totalCountConsumed = 0;

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
                consumerTasks[c] = Task.Run(async () =>
                {
                    while (Volatile.Read(ref totalCountConsumed) < TotalItems)
                    {
                        try
                        {
                            int val = await channel.ReadAsync();
                            Interlocked.Add(ref totalSumConsumed, val);
                            Interlocked.Increment(ref totalCountConsumed);
                        }
                        catch (ZeroChannelClosedException)
                        {
                            break;
                        }
                    }
                });
            }

            var producerTasks = new Task[Producers];
            for (int p = 0; p < Producers; p++)
            {
                producerTasks[p] = Task.Run(async () =>
                {
                    for (int i = 1; i <= ItemsPerProducer; i++)
                    {
                        await channel.WriteAsync(i);
                    }
                });
            }

            await Task.WhenAll(producerTasks);
            channel.Complete();
            await Task.WhenAll(consumerTasks);

            Assert.Equal(TotalItems, totalCountConsumed);
            Assert.Equal(expectedSum, totalSumConsumed);
        }

        [Fact]
        public async Task Channel_CancellationToken_CancelsWaitingRead()
        {
            var channel = new ZeroChannel<int>(16);
            using var cts = new CancellationTokenSource(50);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await channel.ReadAsync(cts.Token);
            });
        }

        [Fact]
        public async Task Channel_Complete_DrainsRemainingBufferedItems()
        {
            var channel = new ZeroChannel<string>(16);
            channel.TryWrite("one");
            channel.TryWrite("two");
            channel.Complete();

            // Should still be able to read remaining buffered items
            string first = await channel.ReadAsync();
            Assert.Equal("one", first);

            string second = await channel.ReadAsync();
            Assert.Equal("two", second);

            // Subsequent read throws closed exception
            await Assert.ThrowsAsync<ZeroChannelClosedException>(async () =>
            {
                await channel.ReadAsync();
            });
        }

        [Fact]
        public async Task Channel_Complete_WakesUpWaitingWritersWithException()
        {
            var channel = new ZeroChannel<int>(2);
            channel.TryWrite(1);
            channel.TryWrite(2);

            var writerTask = Task.Run(async () =>
            {
                await channel.WriteAsync(3);
            });

            await Task.Delay(20);
            channel.Complete();

            await Assert.ThrowsAsync<ZeroChannelClosedException>(async () =>
            {
                await writerTask;
            });
        }

        [Fact]
        public void Channel_BatchWriteAndRead_TransfersCorrectly()
        {
            var channel = new ZeroChannel<int>(64);
            int[] data = new int[] { 10, 20, 30, 40, 50, 60, 70, 80 };

            int written = channel.TryWriteBatch(data);
            Assert.Equal(8, written);
            Assert.Equal(8, channel.Count);

            Span<int> dest = stackalloc int[8];
            int read = channel.TryReadBatch(dest);
            Assert.Equal(8, read);
            Assert.Equal(0, channel.Count);

            for (int i = 0; i < 8; i++)
            {
                Assert.Equal(data[i], dest[i]);
            }
        }
    }
}
