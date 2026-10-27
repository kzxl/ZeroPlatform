using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPlatform.Concurrency.RateLimiting;

namespace ZeroPlatform.Concurrency.Tests
{
    public class DisruptorAndRateLimiterTests
    {
        [Fact]
        public void TokenBucket_BurstCapacity_AllowsInstantBurst()
        {
            var limiter = new TokenBucketRateLimiter(capacity: 5, tokensPerSecond: 10);

            Assert.Equal(5, limiter.Capacity);
            Assert.Equal(10, limiter.TokensPerSecond);

            for (int i = 0; i < 5; i++)
            {
                Assert.True(limiter.TryAcquire(1.0), $"Burst acquire {i} failed");
            }

            // Exceeded capacity: should fail immediately
            Assert.False(limiter.TryAcquire(1.0));
        }

        [Fact]
        public async Task TokenBucket_ReplenishesOverTime()
        {
            var limiter = new TokenBucketRateLimiter(capacity: 10, tokensPerSecond: 100, initialTokens: 0);

            Assert.False(limiter.TryAcquire(1.0));

            // Wait ~50ms -> should replenish ~5 tokens
            await Task.Delay(50);

            Assert.True(limiter.AvailableTokens >= 1.0);
            Assert.True(limiter.TryAcquire(1.0));
        }

        [Fact]
        public async Task TokenBucket_AcquireAsync_WaitsAndSucceeds()
        {
            var limiter = new TokenBucketRateLimiter(capacity: 5, tokensPerSecond: 50, initialTokens: 0);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await limiter.AcquireAsync(2.0);
            sw.Stop();

            // 2 tokens at 50/sec should take ~40ms
            Assert.True(sw.ElapsedMilliseconds >= 25, $"AcquireAsync elapsed was too fast: {sw.ElapsedMilliseconds}ms");
        }

        [Fact]
        public async Task DisruptorRing_SingleProducerSingleConsumer_StreamsDataCorrectly()
        {
            const int count = 5000;
            var ring = new DisruptorRing<int>(1024);
            var barrier = ring.NewBarrier();
            var consumerSeq = new Sequence(-1);
            ring.AddGatingSequences(consumerSeq);

            var consumerTask = Task.Run(() =>
            {
                int received = 0;
                long expectedSeq = 0;

                while (received < count)
                {
                    long available = barrier.WaitFor(expectedSeq);
                    while (expectedSeq <= available && received < count)
                    {
                        int value = ring[expectedSeq];
                        Assert.Equal(received * 2, value);
                        received++;
                        consumerSeq.Set(expectedSeq);
                        expectedSeq++;
                    }
                }
            });

            for (int i = 0; i < count; i++)
            {
                long seq = ring.Next();
                ring[seq] = i * 2;
                ring.Publish(seq);
            }

            await consumerTask;
        }

        [Fact]
        public async Task DisruptorRing_MultiStagePipeline_ExecutesInOrder()
        {
            const int count = 2000;
            var ring = new DisruptorRing<long>(512);

            // Stage 1: doubles the value
            var stage1Barrier = ring.NewBarrier();
            var stage1Seq = new Sequence(-1);

            // Stage 2: depends on Stage 1 completing
            var stage2Barrier = ring.NewBarrier(stage1Seq);
            var stage2Seq = new Sequence(-1);

            // Producer gates on both stages
            ring.AddGatingSequences(stage1Seq, stage2Seq);

            var stage1Task = Task.Run(() =>
            {
                long current = 0;
                while (current < count)
                {
                    long available = stage1Barrier.WaitFor(current);
                    while (current <= available && current < count)
                    {
                        ring[current] *= 2;
                        stage1Seq.Set(current);
                        current++;
                    }
                }
            });

            var stage2Task = Task.Run(() =>
            {
                long current = 0;
                while (current < count)
                {
                    long available = stage2Barrier.WaitFor(current);
                    while (current <= available && current < count)
                    {
                        long value = ring[current];
                        Assert.Equal((current + 1) * 2, value);
                        stage2Seq.Set(current);
                        current++;
                    }
                }
            });

            for (int i = 0; i < count; i++)
            {
                long seq = ring.Next();
                ring[seq] = i + 1;
                ring.Publish(seq);
            }

            await Task.WhenAll(stage1Task, stage2Task);
        }

        [Fact]
        public void DisruptorRing_MultiProducerOutOfOrderPublish_NeverMovesCursorBackwardOrExposesUncommitted()
        {
            var ring = new DisruptorRing<long>(16);
            var barrier = ring.NewBarrier();

            long seq0 = ring.Next(); // 0
            long seq1 = ring.Next(); // 1
            long seq2 = ring.Next(); // 2

            ring[seq0] = 100;
            ring[seq1] = 200;
            ring[seq2] = 300;

            // Publish seq2 and seq1 out-of-order BEFORE seq0 is published!
            ring.Publish(seq2);
            Assert.Equal(-1L, ring.Cursor.Get());
            Assert.False(ring.IsAvailable(seq0));
            Assert.False(ring.IsAvailable(seq1));
            Assert.True(ring.IsAvailable(seq2));
            Assert.Equal(-1L, ring.GetHighestPublishedSequence(0, 2));

            ring.Publish(seq1);
            Assert.Equal(-1L, ring.Cursor.Get());
            Assert.True(ring.IsAvailable(seq1));
            Assert.Equal(-1L, ring.GetHighestPublishedSequence(0, 2));

            // Now publish seq0 -> cursor must atomically advance from -1 to 2!
            ring.Publish(seq0);
            Assert.Equal(2L, ring.Cursor.Get());
            Assert.Equal(2L, barrier.WaitFor(0));
            Assert.Equal(2L, ring.GetHighestPublishedSequence(0, 2));
        }

        [Fact]
        public async Task DisruptorRing_ConcurrentMultiProducers_PublishesAllWithoutLossOrUncommittedReads()
        {
            const int producers = 4;
            const int itemsPerProducer = 1000;
            const int totalItems = producers * itemsPerProducer;

            var ring = new DisruptorRing<long>(256);
            var barrier = ring.NewBarrier();
            var consumerSeq = new Sequence(-1);
            ring.AddGatingSequences(consumerSeq);

            long sumConsumed = 0;
            int countConsumed = 0;

            var consumerTask = Task.Run(() =>
            {
                long nextSeq = 0;
                while (countConsumed < totalItems)
                {
                    long available = barrier.WaitFor(nextSeq);
                    while (nextSeq <= available && countConsumed < totalItems)
                    {
                        long val = ring[nextSeq];
                        // Producer always writes a positive non-zero sentinel value before Publish
                        Assert.True(val > 0, $"Uncommitted slot read at sequence {nextSeq}: {val}");
                        ring[nextSeq] = 0; // Reset slot to catch any premature wrap-around read
                        sumConsumed += val;
                        countConsumed++;
                        consumerSeq.Set(nextSeq);
                        nextSeq++;
                    }
                }
            });

            var producerTasks = new Task[producers];
            for (int p = 0; p < producers; p++)
            {
                producerTasks[p] = Task.Run(() =>
                {
                    for (int i = 1; i <= itemsPerProducer; i++)
                    {
                        long seq = ring.Next();
                        ring[seq] = i;
                        ring.Publish(seq);
                    }
                });
            }

            await Task.WhenAll(producerTasks);
            await consumerTask;

            long expectedSumPerProducer = (long)itemsPerProducer * (itemsPerProducer + 1) / 2;
            Assert.Equal(totalItems, countConsumed);
            Assert.Equal(producers * expectedSumPerProducer, sumConsumed);
        }
    }
}
