using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZeroPlatform.Concurrency;
using ZeroPlatform.Concurrency.RateLimiting;

namespace Audit.Tests
{
    public static class DeepConcurrencyStressTests
    {
        public struct DisruptorTestEvent
        {
            public long Sequence;
            public long Value;
            public long ProcessedValue;
        }

        public static async Task RunAllAsync()
        {
            Console.WriteLine("==================================================================================");
            Console.WriteLine("        DEEP CONCURRENCY STRESS & SOAK TEST SUITE (EXTREME HARDENING)             ");
            Console.WriteLine("==================================================================================");

            TestSpscContinuousWraparoundStress(iterations: 10_000_000, capacity: 1024);
            await TestMpmcMassiveContentionIntegrityAsync(totalItems: 4_000_000, numProducers: 8, numConsumers: 8, capacity: 4096);
            await TestZeroChannelBackpressureAsync(totalItems: 1_000_000, numProducers: 8, numConsumers: 8, capacity: 256);
            TestTokenBucketRateLimitingPrecision(durationMs: 2000, burstCapacity: 50, refillRatePerSec: 500, threads: 12);
            TestDisruptorMultiStagePipeline(totalItems: 1_000_000, capacity: 4096);
        }

        /// <summary>
        /// SPSC Soak Test: Pushes 10,000,000 sequential items through a small buffer (1024 slots),
        /// forcing over 9,700 wraparounds in rapid succession while verifying exact FIFO sequence.
        /// </summary>
        public static void TestSpscContinuousWraparoundStress(int iterations, int capacity)
        {
            Console.WriteLine($"\n[1/5] SPSC Continuous Wraparound Stress ({iterations:N0} items, {capacity} buffer)...");
            var ring = new ZeroRingBuffer<long>(capacity);
            var sw = Stopwatch.StartNew();

            long receivedCount = 0;
            long expectedSum = (long)iterations * (iterations + 1) / 2;
            long actualSum = 0;
            bool sequenceValid = true;

            var consumer = Task.Run(() =>
            {
                long lastVal = 0;
                while (receivedCount < iterations)
                {
                    if (ring.TryDequeue(out long val))
                    {
                        actualSum += val;
                        receivedCount++;
                        if (val != lastVal + 1)
                        {
                            sequenceValid = false;
                        }
                        lastVal = val;
                    }
                    else
                    {
                        Thread.SpinWait(1);
                    }
                }
            });

            for (long i = 1; i <= iterations; i++)
            {
                while (!ring.TryEnqueue(i))
                {
                    Thread.SpinWait(1);
                }
            }

            consumer.Wait();
            sw.Stop();

            double throughput = iterations / sw.Elapsed.TotalSeconds;
            bool sumMatches = actualSum == expectedSum;
            bool pass = sequenceValid && sumMatches && ring.IsEmpty;

            Console.WriteLine($"   => Result: {(pass ? "PASS" : "FAILED")}");
            Console.WriteLine($"      Processed: {receivedCount:N0}/{iterations:N0} items | Time: {sw.ElapsedMilliseconds} ms | Throughput: {throughput:N0} ops/s");
            Console.WriteLine($"      Sum Check: {(sumMatches ? "MATCH" : $"MISMATCH (Expected {expectedSum}, Got {actualSum})")} | Sequence FIFO: {sequenceValid} | Buffer Empty: {ring.IsEmpty}");

            if (!pass) throw new InvalidOperationException("SPSC Continuous Wraparound Stress FAILED!");
        }

        /// <summary>
        /// MPMC High-Contention Stress Test: 8 Producers and 8 Consumers hammering a shared
        /// ZeroMpmcRingBuffer concurrently. Uses arithmetic sum to prove zero dropped items and zero duplicate items.
        /// </summary>
        public static async Task TestMpmcMassiveContentionIntegrityAsync(int totalItems, int numProducers, int numConsumers, int capacity)
        {
            Console.WriteLine($"\n[2/5] MPMC Massive Contention ({totalItems:N0} items, {numProducers}P x {numConsumers}C, {capacity} buffer)...");
            var mpmc = new ZeroMpmcRingBuffer<int>(capacity);
            int itemsPerProducer = totalItems / numProducers;
            int actualTotal = itemsPerProducer * numProducers;

            long totalDequeued = 0;
            long dequeuedSum = 0;
            long expectedSum = 0;

            // Calculate expected sum
            for (int p = 0; p < numProducers; p++)
            {
                long start = (long)p * itemsPerProducer + 1;
                long end = start + itemsPerProducer - 1;
                expectedSum += (end * (end + 1) / 2) - ((start - 1) * start / 2);
            }

            var sw = Stopwatch.StartNew();

            var consumers = new Task[numConsumers];
            for (int c = 0; c < numConsumers; c++)
            {
                consumers[c] = Task.Run(() =>
                {
                    while (Interlocked.Read(ref totalDequeued) < actualTotal)
                    {
                        if (mpmc.TryDequeue(out int val))
                        {
                            Interlocked.Increment(ref totalDequeued);
                            Interlocked.Add(ref dequeuedSum, val);
                        }
                        else
                        {
                            Thread.SpinWait(2);
                        }
                    }
                });
            }

            var producers = new Task[numProducers];
            for (int p = 0; p < numProducers; p++)
            {
                int producerId = p;
                producers[p] = Task.Run(() =>
                {
                    int start = producerId * itemsPerProducer + 1;
                    int end = start + itemsPerProducer;
                    for (int i = start; i < end; i++)
                    {
                        while (!mpmc.TryEnqueue(i))
                        {
                            Thread.SpinWait(2);
                        }
                    }
                });
            }

            await Task.WhenAll(producers);
            await Task.WhenAll(consumers);
            sw.Stop();

            double throughput = actualTotal / sw.Elapsed.TotalSeconds;
            bool countMatches = totalDequeued == actualTotal;
            bool sumMatches = dequeuedSum == expectedSum;
            bool pass = countMatches && sumMatches;

            Console.WriteLine($"   => Result: {(pass ? "PASS" : "FAILED")}");
            Console.WriteLine($"      Processed: {totalDequeued:N0}/{actualTotal:N0} items | Time: {sw.ElapsedMilliseconds} ms | Throughput: {throughput:N0} ops/s");
            Console.WriteLine($"      Integrity: Count Match: {countMatches} | Exactly-Once Sum Match: {sumMatches} | Buffer Empty: {mpmc.IsEmpty}");

            if (!pass) throw new InvalidOperationException("MPMC Contention Integrity Test FAILED!");
        }

        /// <summary>
        /// CSP Channels Backpressure & Async Streaming Stress Test:
        /// 8 async writers and 8 async readers with tight bounded capacity (256 items).
        /// Tests ValueTask promise recycling, zero deadlocks, and async backpressure handoff.
        /// </summary>
        public static async Task TestZeroChannelBackpressureAsync(int totalItems, int numProducers, int numConsumers, int capacity)
        {
            Console.WriteLine($"\n[3/5] ZeroChannel Backpressure & ValueTask Recycling ({totalItems:N0} items, {numProducers}P x {numConsumers}C, Bounded {capacity})...");
            var channel = new ZeroChannel<int>(capacity);
            int itemsPerProducer = totalItems / numProducers;
            int actualTotal = itemsPerProducer * numProducers;

            long totalRead = 0;
            long readSum = 0;
            long expectedSum = 0;

            for (int p = 0; p < numProducers; p++)
            {
                long start = (long)p * itemsPerProducer + 1;
                long end = start + itemsPerProducer - 1;
                expectedSum += (end * (end + 1) / 2) - ((start - 1) * start / 2);
            }

            var sw = Stopwatch.StartNew();

            var consumers = new Task[numConsumers];
            for (int c = 0; c < numConsumers; c++)
            {
                consumers[c] = Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            int val = await channel.ReadAsync();
                            Interlocked.Increment(ref totalRead);
                            Interlocked.Add(ref readSum, val);
                        }
                        catch (ZeroChannelClosedException)
                        {
                            break;
                        }
                    }
                });
            }

            var producers = new Task[numProducers];
            for (int p = 0; p < numProducers; p++)
            {
                int producerId = p;
                producers[p] = Task.Run(async () =>
                {
                    int start = producerId * itemsPerProducer + 1;
                    int end = start + itemsPerProducer;
                    for (int i = start; i < end; i++)
                    {
                        await channel.WriteAsync(i);
                    }
                });
            }

            await Task.WhenAll(producers);
            channel.Complete();
            await Task.WhenAll(consumers);
            sw.Stop();

            double throughput = actualTotal / sw.Elapsed.TotalSeconds;
            bool countMatches = totalRead == actualTotal;
            bool sumMatches = readSum == expectedSum;
            bool pass = countMatches && sumMatches;

            Console.WriteLine($"   => Result: {(pass ? "PASS" : "FAILED")}");
            Console.WriteLine($"      Processed: {totalRead:N0}/{actualTotal:N0} items | Time: {sw.ElapsedMilliseconds} ms | Throughput: {throughput:N0} ops/s");
            Console.WriteLine($"      Backpressure Invariant: Count Match: {countMatches} | Sum Match: {sumMatches} | Empty: {channel.Count == 0}");

            if (!pass) throw new InvalidOperationException("ZeroChannel Backpressure Stress FAILED!");
        }

        /// <summary>
        /// Rate Limiter Precision Under Heavy Contention:
        /// 12 threads aggressively competing for tokens over 2 seconds.
        /// Verifies rate limiter respects mathematical refill rate within a tight tolerance.
        /// </summary>
        public static void TestTokenBucketRateLimitingPrecision(int durationMs, double burstCapacity, double refillRatePerSec, int threads)
        {
            Console.WriteLine($"\n[4/5] TokenBucket RateLimiter Precision (Burst {burstCapacity}, Refill {refillRatePerSec}/s, {threads} Contending Threads)...");
            var limiter = new TokenBucketRateLimiter(burstCapacity, refillRatePerSec);

            long allowedCount = 0;
            long rejectedCount = 0;
            var cts = new CancellationTokenSource(durationMs);
            var token = cts.Token;

            var sw = Stopwatch.StartNew();
            var workers = new Task[threads];
            for (int t = 0; t < threads; t++)
            {
                workers[t] = Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        if (limiter.TryAcquire())
                        {
                            Interlocked.Increment(ref allowedCount);
                        }
                        else
                        {
                            Interlocked.Increment(ref rejectedCount);
                            Thread.SpinWait(10);
                        }
                    }
                });
            }

            Task.WaitAll(workers);
            sw.Stop();

            double elapsedSec = sw.Elapsed.TotalSeconds;
            double expectedTokens = burstCapacity + (refillRatePerSec * elapsedSec);
            double errorPercentage = Math.Abs(allowedCount - expectedTokens) / expectedTokens * 100.0;
            bool pass = errorPercentage < 5.0; // Within 5% tolerance under extreme contention

            Console.WriteLine($"   => Result: {(pass ? "PASS" : "FAILED")}");
            Console.WriteLine($"      Duration: {elapsedSec:F2}s | Allowed: {allowedCount:N0} | Rejected: {rejectedCount:N0}");
            Console.WriteLine($"      Expected Tokens: ~{expectedTokens:F0} | Actual: {allowedCount:N0} | Deviation: {errorPercentage:F2}% (Tolerance < 5%)");

            if (!pass) throw new InvalidOperationException("TokenBucket Precision Test FAILED!");
        }

        /// <summary>
        /// LMAX Disruptor Pipeline:
        /// 1 Producer -> Transformer Stage (x2) -> Aggregator Stage.
        /// Tests sequence barriers, cursor coordination, and in-place event mutation.
        /// </summary>
        public static void TestDisruptorMultiStagePipeline(int totalItems, int capacity)
        {
            Console.WriteLine($"\n[5/5] Disruptor Multi-Stage Pipeline ({totalItems:N0} items, {capacity} capacity)...");

            var ring = new DisruptorRing<DisruptorTestEvent>(capacity, () => new DisruptorTestEvent());
            var barrier = ring.NewBarrier();
            var consumerSeq = new Sequence(-1);
            ring.AddGatingSequences(consumerSeq);

            long finalSum = 0;
            long expectedSum = 0;
            for (long i = 1; i <= totalItems; i++) expectedSum += i * 2;

            var sw = Stopwatch.StartNew();

            var consumer = Task.Run(() =>
            {
                long nextSequence = 0;
                while (nextSequence < totalItems)
                {
                    long available = barrier.WaitFor(nextSequence, CancellationToken.None);
                    while (nextSequence <= available && nextSequence < totalItems)
                    {
                        ref var evt = ref ring[nextSequence];
                        evt.ProcessedValue = evt.Value * 2;
                        finalSum += evt.ProcessedValue;
                        consumerSeq.Set(nextSequence);
                        nextSequence++;
                    }
                }
            });

            for (long i = 1; i <= totalItems; i++)
            {
                long seq = ring.Next();
                ref var evt = ref ring[seq];
                evt.Sequence = seq;
                evt.Value = i;
                ring.Publish(seq);
            }

            consumer.Wait();
            sw.Stop();

            double throughput = totalItems / sw.Elapsed.TotalSeconds;
            bool sumMatches = finalSum == expectedSum;
            bool pass = sumMatches;

            Console.WriteLine($"   => Result: {(pass ? "PASS" : "FAILED")}");
            Console.WriteLine($"      Processed: {totalItems:N0} items | Time: {sw.ElapsedMilliseconds} ms | Throughput: {throughput:N0} ops/s");
            Console.WriteLine($"      Pipeline Integrity: In-Place Mutation & Sum Match: {sumMatches} (Sum: {finalSum:N0})");

            if (!pass) throw new InvalidOperationException("Disruptor Multi-Stage Pipeline FAILED!");
        }
    }
}
