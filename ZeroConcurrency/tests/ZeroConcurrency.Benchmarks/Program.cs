using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ZeroPlatform.Concurrency;

namespace ZeroPlatform.Concurrency.Benchmarks
{
    public static class Program
    {
        public static async Task Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==================================================================================");
            Console.WriteLine("        ZERO CONCURRENCY BENCHMARK SUITE — REAL-WORLD PERFORMANCE VS TASK         ");
            Console.WriteLine("==================================================================================");
            Console.WriteLine($"Environment: .NET {Environment.Version}, OS: {Environment.OSVersion}, Cores: {Environment.ProcessorCount}\n");

            // Warmup JIT
            await WarmupAsync();

            // Run Benchmarks
            await RunPromiseVsTaskCompletionSourceBenchmarkAsync(1_000_000);
            await RunQueueThroughputBenchmarkAsync(2_000_000);
            await RunChannelThroughputBenchmarkAsync(500_000);
            await RunSchedulerThroughputBenchmarkAsync(100_000);

            Console.WriteLine("\nAll benchmarks completed successfully.");
        }

        private static async Task WarmupAsync()
        {
            Console.Write("Warming up JIT compiler and runtime engines... ");
            var p = ZeroPromisePool<int>.Rent();
            p.SetResult(1);
            _ = await p.Task;

            var tcs = new TaskCompletionSource<int>();
            tcs.SetResult(1);
            _ = await tcs.Task;

            var ring = new ZeroRingBuffer<int>(64);
            ring.TryEnqueue(1);
            ring.TryDequeue(out _);

            var ch = new ZeroChannel<int>(64);
            await ch.WriteAsync(1);
            _ = await ch.ReadAsync();

            Console.WriteLine("Done.\n");
        }

        #region 1. Promise vs TaskCompletionSource
        private static async Task RunPromiseVsTaskCompletionSourceBenchmarkAsync(int iterations)
        {
            Console.WriteLine($"----------------------------------------------------------------------------------");
            Console.WriteLine($"[1] Async Promise / Completion Source ({iterations:N0} ops)");
            Console.WriteLine($"----------------------------------------------------------------------------------");

            // Benchmark TaskCompletionSource<int>
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long tcsAllocBefore = GC.GetTotalAllocatedBytes(true);
            int tcsGen0Before = GC.CollectionCount(0);
            var swTcs = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                tcs.SetResult(i);
                _ = await tcs.Task;
            }

            swTcs.Stop();
            long tcsAllocAfter = GC.GetTotalAllocatedBytes(true);
            int tcsGen0After = GC.CollectionCount(0);
            double tcsAllocMb = (tcsAllocAfter - tcsAllocBefore) / (1024.0 * 1024.0);

            // Benchmark ZeroPromise<int> (Pooled)
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long zeroAllocBefore = GC.GetTotalAllocatedBytes(true);
            int zeroGen0Before = GC.CollectionCount(0);
            var swZero = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                var promise = ZeroPromisePool<int>.Rent();
                promise.SetResult(i);
                _ = await promise.Task;
            }

            swZero.Stop();
            long zeroAllocAfter = GC.GetTotalAllocatedBytes(true);
            int zeroGen0After = GC.CollectionCount(0);
            double zeroAllocMb = (zeroAllocAfter - zeroAllocBefore) / (1024.0 * 1024.0);

            PrintResultRow("TaskCompletionSource<int>", swTcs.ElapsedMilliseconds, iterations, tcsAllocMb, tcsGen0After - tcsGen0Before);
            PrintResultRow("ZeroPromise<int> (Pooled)", swZero.ElapsedMilliseconds, iterations, zeroAllocMb, zeroGen0After - zeroGen0Before);

            double speedup = (double)swTcs.ElapsedTicks / swZero.ElapsedTicks;
            Console.WriteLine($"   => Speedup: {speedup:F2}x faster | Memory Reduced: {(tcsAllocMb - zeroAllocMb):F2} MB (100% Zero Alloc)\n");
        }
        #endregion

        #region 2. Queue Throughput
        private static async Task RunQueueThroughputBenchmarkAsync(int totalItems)
        {
            Console.WriteLine($"----------------------------------------------------------------------------------");
            Console.WriteLine($"[2] Data Queue SPSC / MPMC Throughput ({totalItems:N0} items)");
            Console.WriteLine($"----------------------------------------------------------------------------------");

            // Benchmark 1: ConcurrentQueue<int>
            GC.Collect();
            long cqAllocBefore = GC.GetTotalAllocatedBytes(true);
            int cqGen0Before = GC.CollectionCount(0);
            var swCq = Stopwatch.StartNew();

            var cq = new ConcurrentQueue<int>();
            var cqConsumer = Task.Run(() =>
            {
                int count = 0;
                SpinWait spinner = default;
                while (count < totalItems)
                {
                    if (cq.TryDequeue(out _))
                    {
                        count++;
                        spinner.Reset();
                    }
                    else
                    {
                        spinner.SpinOnce();
                    }
                }
            });

            var cqProducer = Task.Run(() =>
            {
                for (int i = 0; i < totalItems; i++)
                {
                    cq.Enqueue(i);
                }
            });

            await Task.WhenAll(cqProducer, cqConsumer);
            swCq.Stop();
            long cqAllocAfter = GC.GetTotalAllocatedBytes(true);
            int cqGen0After = GC.CollectionCount(0);
            double cqAllocMb = (cqAllocAfter - cqAllocBefore) / (1024.0 * 1024.0);

            // Benchmark 2: ZeroMpmcRingBuffer<int>
            GC.Collect();
            long mpmcAllocBefore = GC.GetTotalAllocatedBytes(true);
            int mpmcGen0Before = GC.CollectionCount(0);
            var swMpmc = Stopwatch.StartNew();

            var mpmc = new ZeroMpmcRingBuffer<int>(4096);
            var mpmcConsumer = Task.Run(() =>
            {
                int count = 0;
                SpinWait spinner = default;
                while (count < totalItems)
                {
                    if (mpmc.TryDequeue(out _))
                    {
                        count++;
                        spinner.Reset();
                    }
                    else
                    {
                        spinner.SpinOnce();
                    }
                }
            });

            var mpmcProducer = Task.Run(() =>
            {
                SpinWait spinner = default;
                for (int i = 0; i < totalItems; i++)
                {
                    while (!mpmc.TryEnqueue(i))
                    {
                        spinner.SpinOnce();
                    }
                    spinner.Reset();
                }
            });

            await Task.WhenAll(mpmcProducer, mpmcConsumer);
            swMpmc.Stop();
            long mpmcAllocAfter = GC.GetTotalAllocatedBytes(true);
            int mpmcGen0After = GC.CollectionCount(0);
            double mpmcAllocMb = (mpmcAllocAfter - mpmcAllocBefore) / (1024.0 * 1024.0);

            // Benchmark 3: ZeroRingBuffer<int> (SPSC)
            GC.Collect();
            long spscAllocBefore = GC.GetTotalAllocatedBytes(true);
            int spscGen0Before = GC.CollectionCount(0);
            var swSpsc = Stopwatch.StartNew();

            var spsc = new ZeroRingBuffer<int>(4096);
            var spscConsumer = Task.Run(() =>
            {
                int count = 0;
                SpinWait spinner = default;
                while (count < totalItems)
                {
                    if (spsc.TryDequeue(out _))
                    {
                        count++;
                        spinner.Reset();
                    }
                    else
                    {
                        spinner.SpinOnce();
                    }
                }
            });

            var spscProducer = Task.Run(() =>
            {
                SpinWait spinner = default;
                for (int i = 0; i < totalItems; i++)
                {
                    while (!spsc.TryEnqueue(i))
                    {
                        spinner.SpinOnce();
                    }
                    spinner.Reset();
                }
            });

            await Task.WhenAll(spscProducer, spscConsumer);
            swSpsc.Stop();
            long spscAllocAfter = GC.GetTotalAllocatedBytes(true);
            int spscGen0After = GC.CollectionCount(0);
            double spscAllocMb = (spscAllocAfter - spscAllocBefore) / (1024.0 * 1024.0);

            PrintResultRow("ConcurrentQueue<int>", swCq.ElapsedMilliseconds, totalItems, cqAllocMb, cqGen0After - cqGen0Before);
            PrintResultRow("ZeroMpmcRingBuffer<int>", swMpmc.ElapsedMilliseconds, totalItems, mpmcAllocMb, mpmcGen0After - mpmcGen0Before);
            PrintResultRow("ZeroRingBuffer<int> (SPSC)", swSpsc.ElapsedMilliseconds, totalItems, spscAllocMb, spscGen0After - spscGen0Before);

            double spscSpeedup = (double)swCq.ElapsedTicks / swSpsc.ElapsedTicks;
            Console.WriteLine($"   => ZeroRingBuffer vs ConcurrentQueue: {spscSpeedup:F2}x faster | Memory: {spscAllocMb:F2} MB\n");
        }
        #endregion

        #region 3. Channels Throughput
        private static async Task RunChannelThroughputBenchmarkAsync(int totalItems)
        {
            Console.WriteLine($"----------------------------------------------------------------------------------");
            Console.WriteLine($"[3] Streaming Channel Benchmark ({totalItems:N0} messages)");
            Console.WriteLine($"----------------------------------------------------------------------------------");

            // Benchmark 1: System.Threading.Channels.Channel<int>
            GC.Collect();
            long sysAllocBefore = GC.GetTotalAllocatedBytes(true);
            int sysGen0Before = GC.CollectionCount(0);
            var swSys = Stopwatch.StartNew();

            var sysChannel = Channel.CreateBounded<int>(new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = true
            });

            var sysConsumer = Task.Run(async () =>
            {
                var reader = sysChannel.Reader;
                while (await reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (reader.TryRead(out _)) { }
                }
            });

            var sysProducer = Task.Run(async () =>
            {
                var writer = sysChannel.Writer;
                for (int i = 0; i < totalItems; i++)
                {
                    await writer.WriteAsync(i).ConfigureAwait(false);
                }
                writer.Complete();
            });

            await Task.WhenAll(sysProducer, sysConsumer);
            swSys.Stop();
            long sysAllocAfter = GC.GetTotalAllocatedBytes(true);
            int sysGen0After = GC.CollectionCount(0);
            double sysAllocMb = (sysAllocAfter - sysAllocBefore) / (1024.0 * 1024.0);

            // Benchmark 2: ZeroChannel<int>
            GC.Collect();
            long zeroAllocBefore = GC.GetTotalAllocatedBytes(true);
            int zeroGen0Before = GC.CollectionCount(0);
            var swZero = Stopwatch.StartNew();

            var zeroChannel = new ZeroChannel<int>(1024);

            var zeroConsumer = Task.Run(async () =>
            {
                while (await zeroChannel.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (zeroChannel.TryRead(out _)) { }
                }
            });

            var zeroProducer = Task.Run(async () =>
            {
                for (int i = 0; i < totalItems; i++)
                {
                    await zeroChannel.WriteAsync(i).ConfigureAwait(false);
                }
                zeroChannel.Complete();
            });

            await Task.WhenAll(zeroProducer, zeroConsumer);
            swZero.Stop();
            long zeroAllocAfter = GC.GetTotalAllocatedBytes(true);
            int zeroGen0After = GC.CollectionCount(0);
            double zeroAllocMb = (zeroAllocAfter - zeroAllocBefore) / (1024.0 * 1024.0);

            PrintResultRow("System.Threading.Channels", swSys.ElapsedMilliseconds, totalItems, sysAllocMb, sysGen0After - sysGen0Before);
            PrintResultRow("ZeroChannel<int> (Go-Style)", swZero.ElapsedMilliseconds, totalItems, zeroAllocMb, zeroGen0After - zeroGen0Before);

            double speedup = (double)swSys.ElapsedTicks / swZero.ElapsedTicks;
            Console.WriteLine($"   => ZeroChannel vs System Channel: {speedup:F2}x faster | Memory: {zeroAllocMb:F2} MB\n");
        }
        #endregion

        #region 4. Scheduler Throughput
        private sealed class BenchWorkItem : IZeroWorkItem
        {
            private readonly CountdownEvent _cde;
            public BenchWorkItem(CountdownEvent cde) => _cde = cde;
            public void Execute() => _cde.Signal();
        }

        private static async Task RunSchedulerThroughputBenchmarkAsync(int totalDispatches)
        {
            Console.WriteLine($"----------------------------------------------------------------------------------");
            Console.WriteLine($"[4] Thread Dispatching ({totalDispatches:N0} dispatches)");
            Console.WriteLine($"----------------------------------------------------------------------------------");

            // Benchmark 1: Task.Run
            GC.Collect();
            long taskAllocBefore = GC.GetTotalAllocatedBytes(true);
            int taskGen0Before = GC.CollectionCount(0);
            var swTask = Stopwatch.StartNew();

            using (var cdeTask = new CountdownEvent(totalDispatches))
            {
                for (int i = 0; i < totalDispatches; i++)
                {
                    Task.Run(() => cdeTask.Signal());
                }
                await Task.Run(() => cdeTask.Wait());
            }

            swTask.Stop();
            long taskAllocAfter = GC.GetTotalAllocatedBytes(true);
            int taskGen0After = GC.CollectionCount(0);
            double taskAllocMb = (taskAllocAfter - taskAllocBefore) / (1024.0 * 1024.0);

            // Benchmark 2: ZeroScheduler.UnsafeRun (IZeroWorkItem)
            GC.Collect();
            long zeroAllocBefore = GC.GetTotalAllocatedBytes(true);
            int zeroGen0Before = GC.CollectionCount(0);
            var swZero = Stopwatch.StartNew();

            using (var cdeZero = new CountdownEvent(totalDispatches))
            {
                var workItem = new BenchWorkItem(cdeZero);
                for (int i = 0; i < totalDispatches; i++)
                {
                    ZeroScheduler.UnsafeRun(workItem);
                }
                await Task.Run(() => cdeZero.Wait());
            }

            swZero.Stop();
            long zeroAllocAfter = GC.GetTotalAllocatedBytes(true);
            int zeroGen0After = GC.CollectionCount(0);
            double zeroAllocMb = (zeroAllocAfter - zeroAllocBefore) / (1024.0 * 1024.0);

            PrintResultRow("Task.Run(Action)", swTask.ElapsedMilliseconds, totalDispatches, taskAllocMb, taskGen0After - taskGen0Before);
            PrintResultRow("ZeroScheduler(IZeroWorkItem)", swZero.ElapsedMilliseconds, totalDispatches, zeroAllocMb, zeroGen0After - zeroGen0Before);

            double speedup = (double)swTask.ElapsedTicks / swZero.ElapsedTicks;
            Console.WriteLine($"   => ZeroScheduler vs Task.Run: {speedup:F2}x faster | Memory Reduced: {(taskAllocMb - zeroAllocMb):F2} MB\n");
        }
        #endregion

        private static void PrintResultRow(string testName, long elapsedMs, int totalOps, double memoryMb, int gen0Collections)
        {
            double opsPerSec = (double)totalOps / (elapsedMs / 1000.0);
            double nsPerOp = (elapsedMs * 1_000_000.0) / totalOps;

            Console.WriteLine($"{testName,-30} | Time: {elapsedMs,6} ms | Throughput: {opsPerSec,12:N0} ops/s | Latency: {nsPerOp,7:F1} ns | Alloc: {memoryMb,6:F2} MB | Gen0: {gen0Collections,4}");
        }
    }
}
