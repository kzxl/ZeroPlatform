using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Memory;

namespace ZeroPrimitives.Tests
{
    public class EmpiricalConcurrencyStressTests
    {
        [Fact]
        public void ArrayPoolRentScope_MultiThreaded_RapidRentAndDoubleTripleDispose_Stress()
        {
            const int threadCount = 12;
            const int iterationsPerThread = 10_000;
            var exceptions = new ConcurrentBag<Exception>();
            using var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];

            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        barrier.SignalAndWait();

                        int[] sizes = { 16, 64, 256, 1024, 4096, 16384, 65536, 131072 };

                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            int minLength = sizes[(threadId + i) % sizes.Length];

                            // Rent scope
                            var scope = ArrayPoolRentScope<byte>.Rent(minLength);
                            if (scope.Length != minLength)
                            {
                                throw new InvalidOperationException($"Expected scope length {minLength}, got {scope.Length}");
                            }

                            Span<byte> span = scope.Span;
                            if (span.Length != minLength)
                            {
                                throw new InvalidOperationException($"Expected span length {minLength}, got {span.Length}");
                            }

                            // Write marker byte to ensure span memory is accessible
                            span[0] = (byte)(threadId & 0xFF);
                            span[span.Length - 1] = (byte)(i & 0xFF);

                            // First Dispose
                            scope.Dispose();

                            // Assert safe post-dispose state
                            if (!scope.Span.IsEmpty)
                            {
                                throw new InvalidOperationException("Span must be empty after Dispose()");
                            }
                            if (scope.RawArray != null)
                            {
                                throw new InvalidOperationException("RawArray must be null after Dispose()");
                            }

                            // Second Dispose (Double-dispose)
                            scope.Dispose();

                            // Third Dispose (Triple-dispose)
                            scope.Dispose();

                            // Assert still safe
                            if (!scope.Span.IsEmpty || scope.RawArray != null)
                            {
                                throw new InvalidOperationException("Span must remain empty and RawArray null after repeated Dispose()");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                })
                {
                    IsBackground = true,
                    Name = $"RentScopeStressThread-{threadId}"
                };

                threads[t].Start();
            }

            foreach (var thread in threads)
            {
                bool finished = thread.Join(TimeSpan.FromSeconds(30));
                Assert.True(finished, "A stress worker thread timed out (possible deadlock).");
            }

            Assert.Empty(exceptions);

            // Post-stress validation: verify ArrayPool<byte>.Shared is not corrupted
            // Rent 200 distinct arrays simultaneously to verify freelist integrity
            var rentedArrays = new List<byte[]>();
            try
            {
                var seenReferences = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
                for (int i = 0; i < 200; i++)
                {
                    byte[] arr = ArrayPool<byte>.Shared.Rent(4096);
                    Assert.NotNull(arr);
                    bool added = seenReferences.Add(arr);
                    Assert.True(added, "Duplicate array reference rented concurrently from ArrayPool. Freelist corrupted!");
                    rentedArrays.Add(arr);
                }
            }
            finally
            {
                foreach (var arr in rentedArrays)
                {
                    ArrayPool<byte>.Shared.Return(arr);
                }
            }
        }

        [Fact]
        public void ArrayPoolRentScope_MultiThreaded_RandomChurnWithTripleDispose_AndMemoryCheck()
        {
            const int threadCount = 16;
            const int iterationsPerThread = 5_000;
            var exceptions = new ConcurrentBag<Exception>();
            using var barrier = new Barrier(threadCount);
            var threads = new Thread[threadCount];

            long initialMemory = GC.GetTotalMemory(forceFullCollection: true);

            for (int t = 0; t < threadCount; t++)
            {
                int threadId = t;
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        var rand = new Random(threadId * 1000 + 42);
                        barrier.SignalAndWait();

                        for (int i = 0; i < iterationsPerThread; i++)
                        {
                            int mode = i % 5;
                            int size = rand.Next(0, 100_000);

                            if (mode == 0)
                            {
                                // Zero or negative length
                                var s0 = ArrayPoolRentScope<int>.Rent(size % 2 == 0 ? 0 : -size);
                                Assert.True(s0.Span.IsEmpty);
                                s0.Dispose();
                                s0.Dispose();
                                s0.Dispose();
                            }
                            else if (mode == 1)
                            {
                                // Single Dispose
                                var s1 = ArrayPoolRentScope<int>.Rent(size);
                                if (size > 0) s1.Span[0] = i;
                                s1.Dispose();
                            }
                            else if (mode == 2)
                            {
                                // Double Dispose
                                var s2 = ArrayPoolRentScope<int>.Rent(size);
                                if (size > 0) s2.Span[0] = i;
                                s2.Dispose();
                                s2.Dispose();
                            }
                            else
                            {
                                // Triple Dispose with nested scope simulation
                                var s3 = ArrayPoolRentScope<int>.Rent(size);
                                if (size > 0) s3.Span[size - 1] = i;
                                s3.Dispose();
                                s3.Dispose();
                                s3.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                })
                {
                    IsBackground = true
                };

                threads[t].Start();
            }

            foreach (var thread in threads)
            {
                bool finished = thread.Join(TimeSpan.FromSeconds(30));
                Assert.True(finished, "Stress thread timed out.");
            }

            Assert.Empty(exceptions);

            long finalMemory = GC.GetTotalMemory(forceFullCollection: true);
            // Verify no massive memory leak (allocations pooled in ArrayPool should not cause unconstrained memory growth)
            long growthBytes = finalMemory - initialMemory;
            // Less than 50MB total growth across 80,000 large array rents
            Assert.True(growthBytes < 50 * 1024 * 1024, $"Unexpected memory growth: {growthBytes / 1024 / 1024} MB");
        }

        [Fact]
        public void ArrayPoolRentScope_NestedUsingAndExplicitPrematureDispose()
        {
            // Test that calling Dispose inside a using block, followed by the compiler-generated Dispose, is 100% idempotent
            for (int i = 0; i < 1000; i++)
            {
                using (var scope = ArrayPoolRentScope<byte>.Rent(1024))
                {
                    scope.Span[0] = 0xFE;
                    scope.Dispose(); // Manual explicit dispose
                    scope.Dispose(); // Second manual dispose
                    // Compiler exits 'using' and calls scope.Dispose() a 3rd time
                }
            }
        }

        [Fact]
        public void NativeMemoryBlock_SuppressFinalize_IdempotentDisposeUnderConcurrency()
        {
            const int threadCount = 8;
            const int iterationsPerThread = 1_000;
            var exceptions = new ConcurrentBag<Exception>();

            Parallel.For(0, threadCount, t =>
            {
                try
                {
                    for (int i = 0; i < iterationsPerThread; i++)
                    {
                        var block = NativeMemoryBlock.Allocate(4096);
                        block.Span[0] = 42;
                        block.Span[4095] = 99;

                        // Double dispose
                        block.Dispose();
                        block.Dispose();
                        block.Dispose();

                        Assert.True(block.IsDisposed);
                        Assert.Throws<ObjectDisposedException>(() => _ = block.Span);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });

            Assert.Empty(exceptions);

            // Trigger GC and pending finalizers to ensure SuppressFinalize prevented double-free crash
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<byte[]>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            public bool Equals(byte[]? x, byte[]? y) => ReferenceEquals(x, y);
            public int GetHashCode(byte[] obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
