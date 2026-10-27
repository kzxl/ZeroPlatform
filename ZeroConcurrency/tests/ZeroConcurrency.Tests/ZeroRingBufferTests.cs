using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ZeroPlatform.Concurrency.Tests
{
    public class ZeroRingBufferTests
    {
        [Fact]
        public void Constructor_InvalidCapacity_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new ZeroRingBuffer<int>(0));
            Assert.Throws<ArgumentException>(() => new ZeroRingBuffer<int>(3));
            Assert.Throws<ArgumentException>(() => new ZeroRingBuffer<int>(100));
        }

        [Fact]
        public void Basic_EnqueueAndDequeue_WorksAsExpected()
        {
            var ring = new ZeroRingBuffer<int>(4);
            Assert.True(ring.IsEmpty);
            Assert.False(ring.IsFull);
            Assert.Equal(0, ring.Count);
            Assert.Equal(4, ring.Capacity);

            Assert.True(ring.TryEnqueue(10));
            Assert.True(ring.TryEnqueue(20));
            Assert.Equal(2, ring.Count);

            Assert.True(ring.TryPeek(out int peeked));
            Assert.Equal(10, peeked);

            Assert.True(ring.TryDequeue(out int val1));
            Assert.Equal(10, val1);
            Assert.Equal(1, ring.Count);

            Assert.True(ring.TryDequeue(out int val2));
            Assert.Equal(20, val2);
            Assert.True(ring.IsEmpty);
            Assert.False(ring.TryDequeue(out _));
        }

        [Fact]
        public void Buffer_FullCondition_RejectsOverflow()
        {
            var ring = new ZeroRingBuffer<string>(4);
            Assert.True(ring.TryEnqueue("A"));
            Assert.True(ring.TryEnqueue("B"));
            Assert.True(ring.TryEnqueue("C"));
            Assert.True(ring.TryEnqueue("D"));

            Assert.True(ring.IsFull);
            Assert.False(ring.TryEnqueue("E"));

            Assert.True(ring.TryDequeue(out var first));
            Assert.Equal("A", first);
            Assert.False(ring.IsFull);

            Assert.True(ring.TryEnqueue("E"));
            Assert.True(ring.IsFull);
        }

        [Fact]
        public async Task SPSC_HighConcurrencyThroughput_ZeroDataLoss()
        {
            const int TotalItems = 1_000_000;
            var ring = new ZeroRingBuffer<int>(4096);
            long expectedSum = 0;

            for (int i = 1; i <= TotalItems; i++)
            {
                expectedSum += i;
            }

            var consumerTask = Task.Run(() =>
            {
                int count = 0;
                long sum = 0;
                SpinWait spinner = default;

                while (count < TotalItems)
                {
                    if (ring.TryDequeue(out int value))
                    {
                        sum += value;
                        count++;
                        spinner.Reset();
                    }
                    else
                    {
                        spinner.SpinOnce();
                    }
                }

                return sum;
            });

            var producerTask = Task.Run(() =>
            {
                SpinWait spinner = default;
                for (int i = 1; i <= TotalItems; i++)
                {
                    while (!ring.TryEnqueue(i))
                    {
                        spinner.SpinOnce();
                    }
                    spinner.Reset();
                }
            });

            await Task.WhenAll(producerTask, consumerTask);
            long receivedSum = await consumerTask;

            Assert.Equal(expectedSum, receivedSum);
            Assert.True(ring.IsEmpty);
        }

        [Fact]
        public void Batch_EnqueueAndDequeue_TransfersCorrectly()
        {
            var ring = new ZeroRingBuffer<int>(16);
            int[] data = [1, 2, 3, 4, 5];

            int enqueued = ring.TryEnqueueBatch(data);
            Assert.Equal(5, enqueued);
            Assert.Equal(5, ring.Count);

            int[] output = new int[5];
            int dequeued = ring.TryDequeueBatch(output);
            Assert.Equal(5, dequeued);
            Assert.Equal(data, output);
            Assert.True(ring.IsEmpty);
        }

        [Fact]
        public void Wraparound_NearIntMaxValue_EnqueuesAndDequeuesAccurately()
        {
            var ring = new ZeroRingBuffer<int>(64);
            var tailField = typeof(ZeroRingBufferProducer<int>).GetField("_tail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var headField = typeof(ZeroRingBuffer<int>).GetField("_head", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cachedHeadField = typeof(ZeroRingBufferProducer<int>).GetField("_cachedHead", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cachedTailField = typeof(ZeroRingBuffer<int>).GetField("_cachedTail", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            int nearMax = int.MaxValue - 10;
            tailField!.SetValue(ring, nearMax);
            headField!.SetValue(ring, nearMax);
            cachedHeadField!.SetValue(ring, nearMax);
            cachedTailField!.SetValue(ring, nearMax);

            for (int i = 0; i < 20; i++)
            {
                Assert.True(ring.TryEnqueue(i), $"Enqueue failed at {i}");
            }

            for (int i = 0; i < 20; i++)
            {
                Assert.True(ring.TryDequeue(out int val), $"Dequeue failed at {i}");
                Assert.Equal(i, val);
            }

            Assert.True(ring.IsEmpty);
        }
    }
}
