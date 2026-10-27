using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Core.Identifiers;
using ZeroStorage.Core.Persistence;

namespace ZeroStorage.Tests
{
    public class DurablePersistentQueueTests
    {
        [Fact]
        public void DurablePersistentQueue_BasicEnqueueDequeue_ExtractsUuid7AndPayload()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "zerodurqueue_" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                using var queue = new DurablePersistentQueue(tempFile, capacityPowerOfTwo: 16, slotSize: 128);

                Assert.Equal(16, queue.Capacity);
                Assert.Equal(0, queue.Count);
                Assert.True(queue.IsEmpty);

                var id1 = Uuid7.NewUuid();
                byte[] payload1 = Encoding.UTF8.GetBytes("Telemetry Frame 1");
                Assert.True(queue.TryEnqueue(payload1, typeId: 10, recordId: id1));

                byte[] payload2 = Encoding.UTF8.GetBytes("Hardware Alarm 2");
                Assert.True(queue.TryEnqueue(payload2, typeId: 20)); // auto-generated Uuid7

                Assert.Equal(2, queue.Count);
                Assert.False(queue.IsEmpty);

                Span<byte> dest = stackalloc byte[128];

                // Dequeue 1
                Assert.True(queue.TryDequeue(dest, out int readLen1, out Uuid7 recId1, out ushort typeId1));
                Assert.Equal(payload1.Length, readLen1);
                Assert.Equal(id1, recId1);
                Assert.Equal(10, typeId1);
                Assert.Equal("Telemetry Frame 1", Encoding.UTF8.GetString(dest.Slice(0, readLen1)));

                // Dequeue 2
                Assert.True(queue.TryDequeue(dest, out int readLen2, out Uuid7 recId2, out ushort typeId2));
                Assert.Equal(payload2.Length, readLen2);
                Assert.NotEqual(default(Uuid7), recId2);
                Assert.Equal(20, typeId2);
                Assert.Equal("Hardware Alarm 2", Encoding.UTF8.GetString(dest.Slice(0, readLen2)));

                Assert.Equal(0, queue.Count);
                Assert.True(queue.IsEmpty);
                Assert.False(queue.TryDequeue(dest, out _, out _, out _));
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
        public void DurablePersistentQueue_PersistenceAcrossInstances()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "zerodurqueue_reopen_" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                var originalIds = new Uuid7[5];
                for (int i = 0; i < 5; i++) originalIds[i] = Uuid7.NewUuid();

                // Step 1: Write 5 items and close
                using (var queue1 = new DurablePersistentQueue(tempFile, capacityPowerOfTwo: 16, slotSize: 128))
                {
                    for (int i = 0; i < 5; i++)
                    {
                        byte[] msg = Encoding.UTF8.GetBytes($"Durable Audit Event #{i}");
                        Assert.True(queue1.TryEnqueue(msg, typeId: (ushort)i, recordId: originalIds[i]));
                    }
                    queue1.Flush();
                    Assert.Equal(5, queue1.Count);
                }

                // Step 2: Reopen from disk file, read items
                using (var queue2 = new DurablePersistentQueue(tempFile, capacityPowerOfTwo: 16, slotSize: 128))
                {
                    Assert.Equal(5, queue2.Count);
                    Span<byte> dest = stackalloc byte[128];

                    for (int i = 0; i < 5; i++)
                    {
                        Assert.True(queue2.TryDequeue(dest, out int len, out Uuid7 recId, out ushort typeId));
                        Assert.Equal(originalIds[i], recId);
                        Assert.Equal(i, typeId);
                        Assert.Equal($"Durable Audit Event #{i}", Encoding.UTF8.GetString(dest.Slice(0, len)));
                    }

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
        public async Task DurablePersistentQueue_ConcurrentStressTest()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "zerodurqueue_stress_" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                const int capacity = 1024;
                const int numProducers = 4;
                const int itemsPerProducer = 2500;
                const int totalItems = numProducers * itemsPerProducer;
                const int numConsumers = 4;

                using var queue = new DurablePersistentQueue(tempFile, capacityPowerOfTwo: capacity, slotSize: 64);

                long receivedCount = 0;
                long checksumSent = 0;
                long checksumReceived = 0;

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                // Consumers
                var consumerTasks = new Task[numConsumers];
                for (int c = 0; c < numConsumers; c++)
                {
                    consumerTasks[c] = Task.Run(() =>
                    {
                        byte[] buf = new byte[64];
                        while (Interlocked.Read(ref receivedCount) < totalItems && !cts.Token.IsCancellationRequested)
                        {
                            if (queue.TryDequeue(buf, out int len, out _, out _))
                            {
                                Assert.Equal(sizeof(long), len);
                                long val = BitConverter.ToInt64(buf, 0);
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

                // Producers
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

                            while (!queue.TryEnqueue(payload, (ushort)producerId))
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
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); } catch { }
                }
            }
        }
    }
}
