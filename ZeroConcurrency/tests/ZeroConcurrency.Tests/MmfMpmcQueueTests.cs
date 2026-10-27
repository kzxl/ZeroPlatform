using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroConcurrency.Ipc;

namespace ZeroConcurrency.Tests
{
    public class MmfMpmcQueueTests
    {
        [Fact]
        public void MpmcQueue_BasicEnqueueDequeue_RoundtripsSuccessfully()
        {
            string mapName = "ZeroTest_MPMC_" + Guid.NewGuid().ToString("N");
            using var queue = ZeroMmfMpmcQueue.CreateOrOpen(mapName, capacity: 8, slotSize: 128);

            Assert.Equal(8, queue.Capacity);
            Assert.Equal(128 - 16, queue.MaxPayloadSize);
            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);

            byte[] msg1 = Encoding.UTF8.GetBytes("Frame #1 - Ultra HD");
            byte[] msg2 = Encoding.UTF8.GetBytes("Telemetry Event #2");

            Assert.True(queue.TryEnqueue(msg1, typeId: 101, flags: 1));
            Assert.True(queue.TryEnqueue(msg2, typeId: 102, flags: 2));
            Assert.Equal(2, queue.Count);
            Assert.False(queue.IsEmpty);

            Span<byte> buffer = stackalloc byte[128];

            Assert.True(queue.TryDequeue(buffer, out int readLen1, out ushort typeId1, out ushort flags1));
            Assert.Equal(msg1.Length, readLen1);
            Assert.Equal(101, typeId1);
            Assert.Equal(1, flags1);
            Assert.Equal("Frame #1 - Ultra HD", Encoding.UTF8.GetString(buffer.Slice(0, readLen1)));

            Assert.True(queue.TryDequeue(buffer, out int readLen2, out ushort typeId2, out ushort flags2));
            Assert.Equal(msg2.Length, readLen2);
            Assert.Equal(102, typeId2);
            Assert.Equal(2, flags2);
            Assert.Equal("Telemetry Event #2", Encoding.UTF8.GetString(buffer.Slice(0, readLen2)));

            Assert.Equal(0, queue.Count);
            Assert.True(queue.IsEmpty);
            Assert.False(queue.TryDequeue(buffer, out _));
        }

        [Fact]
        public void MpmcQueue_Backpressure_RejectsWhenFull()
        {
            string mapName = "ZeroTest_Full_" + Guid.NewGuid().ToString("N");
            using var queue = ZeroMmfMpmcQueue.CreateOrOpen(mapName, capacity: 4, slotSize: 64);

            byte[] data = new byte[] { 10, 20, 30 };

            for (int i = 0; i < 4; i++)
            {
                Assert.True(queue.TryEnqueue(data));
            }

            Assert.Equal(4, queue.Count);
            // 5th write must be rejected due to backpressure
            Assert.False(queue.TryEnqueue(data));

            // Dequeue one slot
            Span<byte> buf = stackalloc byte[64];
            Assert.True(queue.TryDequeue(buf, out int bytesRead));
            Assert.Equal(3, bytesRead);
            Assert.Equal(3, queue.Count);

            // Now enqueue should succeed
            Assert.True(queue.TryEnqueue(data));
            Assert.Equal(4, queue.Count);
        }

        [Fact]
        public async Task MpmcQueue_MultiProducerMultiConsumer_ConcurrentStressTest()
        {
            string mapName = "ZeroTest_Stress_" + Guid.NewGuid().ToString("N");
            const int capacity = 1024;
            const int numProducers = 4;
            const int itemsPerProducer = 5000;
            const int totalItems = numProducers * itemsPerProducer;
            const int numConsumers = 4;

            using var queue = ZeroMmfMpmcQueue.CreateOrOpen(mapName, capacity: capacity, slotSize: 64);

            long receivedCount = 0;
            long checksumSent = 0;
            long checksumReceived = 0;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            // Start Consumer Tasks
            var consumerTasks = new Task[numConsumers];
            for (int c = 0; c < numConsumers; c++)
            {
                consumerTasks[c] = Task.Run(() =>
                {
                    byte[] localBuf = new byte[64];
                    while (Interlocked.Read(ref receivedCount) < totalItems && !cts.Token.IsCancellationRequested)
                    {
                        if (queue.TryDequeue(localBuf, out int readLen, out ushort typeId, out ushort flags))
                        {
                            Assert.Equal(sizeof(long), readLen);
                            long val = BitConverter.ToInt64(localBuf, 0);
                            Interlocked.Add(ref checksumReceived, val);
                            Interlocked.Increment(ref receivedCount);
                        }
                        else
                        {
                            Thread.SpinWait(1);
                        }
                    }
                });
            }

            // Start Producer Tasks
            var producerTasks = new Task[numProducers];
            for (int p = 0; p < numProducers; p++)
            {
                int producerId = p;
                producerTasks[p] = Task.Run(() =>
                {
                    byte[] payload = new byte[sizeof(long)];
                    for (int i = 1; i <= itemsPerProducer; i++)
                    {
                        long val = ((long)producerId << 32) | (uint)i;
                        Interlocked.Add(ref checksumSent, val);
                        BitConverter.GetBytes(val).CopyTo(payload, 0);

                        while (!queue.TryEnqueue(payload, (ushort)producerId, 0))
                        {
                            Thread.SpinWait(1);
                        }
                    }
                });
            }

            await Task.WhenAll(producerTasks);
            await Task.WhenAll(consumerTasks);

            Assert.Equal(totalItems, Interlocked.Read(ref receivedCount));
            Assert.Equal(Interlocked.Read(ref checksumSent), Interlocked.Read(ref checksumReceived));
        }

        [Fact]
        public void MpmcQueue_DurableFileBacked_SurvivesAndPersists()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "zerompmc_" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                // Session 1: Create, write 3 items, flush, and close
                using (var queue1 = ZeroMmfMpmcQueue.CreateFromFile(tempFile, capacity: 16, slotSize: 64))
                {
                    Assert.True(queue1.TryEnqueue(Encoding.UTF8.GetBytes("Durable Message A"), 1, 0));
                    Assert.True(queue1.TryEnqueue(Encoding.UTF8.GetBytes("Durable Message B"), 2, 0));
                    Assert.True(queue1.TryEnqueue(Encoding.UTF8.GetBytes("Durable Message C"), 3, 0));
                    queue1.Flush();
                }

                // Session 2: Reopen from disk file, read items
                using (var queue2 = ZeroMmfMpmcQueue.CreateFromFile(tempFile, capacity: 16, slotSize: 64))
                {
                    Assert.Equal(3, queue2.Count);

                    Span<byte> buf = stackalloc byte[64];

                    Assert.True(queue2.TryDequeue(buf, out int lenA, out ushort typeA, out _));
                    Assert.Equal("Durable Message A", Encoding.UTF8.GetString(buf.Slice(0, lenA)));
                    Assert.Equal(1, typeA);

                    Assert.True(queue2.TryDequeue(buf, out int lenB, out ushort typeB, out _));
                    Assert.Equal("Durable Message B", Encoding.UTF8.GetString(buf.Slice(0, lenB)));
                    Assert.Equal(2, typeB);

                    Assert.True(queue2.TryDequeue(buf, out int lenC, out ushort typeC, out _));
                    Assert.Equal("Durable Message C", Encoding.UTF8.GetString(buf.Slice(0, lenC)));
                    Assert.Equal(3, typeC);

                    Assert.Equal(0, queue2.Count);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }

        [Fact]
        public void MpmcQueue_HybridTimeout_WakesOnSignal()
        {
            string mapName = "ZeroTest_Signal_" + Guid.NewGuid().ToString("N");
            using var queue = ZeroMmfMpmcQueue.CreateOrOpen(mapName, capacity: 8, slotSize: 64, enableSignal: true);

            // Producer enqueues on background thread after 30ms
            Task.Run(() =>
            {
                Thread.Sleep(30);
                queue.TryEnqueue(Encoding.UTF8.GetBytes("Signal Wakeup Payload"), 99, 1);
            });

            Span<byte> buf = stackalloc byte[64];
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Wait with 2000ms timeout. Must wake up near ~30-60ms, NOT wait 2000ms!
            Assert.True(queue.TryDequeue(buf, out int readLen, out ushort typeId, out _, timeoutMs: 2000));
            sw.Stop();

            Assert.Equal("Signal Wakeup Payload", Encoding.UTF8.GetString(buf.Slice(0, readLen)));
            Assert.Equal(99, typeId);
            Assert.True(sw.ElapsedMilliseconds < 1500, $"Expected quick wakeup, but took {sw.ElapsedMilliseconds}ms");
        }
    }
}
