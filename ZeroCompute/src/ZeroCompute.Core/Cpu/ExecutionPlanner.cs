using System;

namespace ZeroCompute.Core.Cpu
{
    /// <summary>
    /// Calculated execution strategy determined by the <see cref="ExecutionPlanner"/>.
    /// </summary>
    public readonly struct ExecutionPlan
    {
        /// <summary>
        /// Gets whether the workload should be dispatched across multiple worker threads.
        /// </summary>
        public readonly bool IsParallel;

        /// <summary>
        /// Optimal number of worker threads to allocate.
        /// </summary>
        public readonly int WorkerCount;

        /// <summary>
        /// Calculated chunk size per worker.
        /// </summary>
        public readonly int ChunkSize;

        public ExecutionPlan(bool isParallel, int workerCount, int chunkSize)
        {
            IsParallel = isParallel;
            WorkerCount = workerCount;
            ChunkSize = chunkSize;
        }

        public static ExecutionPlan Sequential(int totalCount) =>
            new ExecutionPlan(false, 1, totalCount);

        public static ExecutionPlan Parallel(int workerCount, int chunkSize) =>
            new ExecutionPlan(true, workerCount, chunkSize);
    }

    /// <summary>
    /// Deterministic execution planner and scheduler.
    /// Determines whether to execute sequentially or across multiple cores,
    /// enforcing small-N cutoffs and memory-bandwidth throttling to avoid memory wall contention.
    /// </summary>
    public static class ExecutionPlanner
    {
        /// <summary>
        /// Cutoff threshold below which thread dispatch overhead exceeds multi-core speedup.
        /// Default is 2,048 iterations.
        /// </summary>
        public const int DefaultSequentialThreshold = 2048;

        /// <summary>
        /// Maximum worker threads allocated for memory-bound kernels to avoid DDR memory bus saturation.
        /// </summary>
        public const int DefaultMemoryBoundMaxWorkers = 4;

        /// <summary>
        /// Default 2D tile dimension (64x64 float32 = 16 KB, perfectly fitting L1 Data Cache).
        /// </summary>
        public const int DefaultTileDimension = 64;

        /// <summary>
        /// Formulates an optimal 1D execution plan for the specified iteration count.
        /// </summary>
        /// <param name="totalCount">Total iterations.</param>
        /// <param name="workload">Workload classification.</param>
        /// <param name="workersOverride">Optional manual worker count override (0 for auto).</param>
        /// <returns>An <see cref="ExecutionPlan"/> specifying worker count and chunk size.</returns>
        public static ExecutionPlan Plan1D(
            int totalCount,
            CpuWorkloadType workload = CpuWorkloadType.Auto,
            int workersOverride = 0)
        {
            if (totalCount <= 0)
                return ExecutionPlan.Sequential(0);

            int availableWorkers = ComputeWorkerPool.Shared.WorkerCount;

            // Manual override takes precedence if positive
            if (workersOverride > 0)
            {
                int workers = Math.Min(workersOverride, availableWorkers);
                if (workers <= 1)
                    return ExecutionPlan.Sequential(totalCount);

                int chunk = (totalCount + workers - 1) / workers;
                return ExecutionPlan.Parallel(workers, chunk);
            }

            // Small-N cutoff: sequential execution avoids thread coordination overhead
            // For memory-bound operations, single-core SIMD processes up to ~1M elements faster than waking threads
            int threshold = (workload == CpuWorkloadType.MemoryBound)
                ? 1_000_000
                : DefaultSequentialThreshold;

            if (totalCount < threshold || availableWorkers <= 1)
            {
                return ExecutionPlan.Sequential(totalCount);
            }

            // Determine worker allocation according to arithmetic intensity
            int targetWorkers;
            switch (workload)
            {
                case CpuWorkloadType.MemoryBound:
                    // Cap at 4 workers to prevent memory controller saturation
                    targetWorkers = Math.Min(DefaultMemoryBoundMaxWorkers, availableWorkers);
                    break;

                case CpuWorkloadType.ComputeBound:
                    // Allocate physical cores to eliminate SMT/Hyper-Threading FPU contention
                    targetWorkers = Math.Min(CpuAffinity.PhysicalCoreCount, availableWorkers);
                    break;

                case CpuWorkloadType.Auto:
                default:
                    // Moderate scaling: scale with count
                    if (totalCount < threshold * 4)
                        targetWorkers = Math.Min(4, availableWorkers);
                    else
                        targetWorkers = availableWorkers;
                    break;
            }

            targetWorkers = Math.Min(targetWorkers, totalCount);
            int chunkSize = (totalCount + targetWorkers - 1) / targetWorkers;

            return ExecutionPlan.Parallel(targetWorkers, chunkSize);
        }

        /// <summary>
        /// Resolves optimal 2D cache tiling dimensions to ensure tiles remain resident within L1/L2 cache.
        /// </summary>
        public static void Plan2D(
            int width,
            int height,
            ref int tileW,
            ref int tileH)
        {
            if (tileW <= 0)
                tileW = Math.Min(width, DefaultTileDimension);
            if (tileH <= 0)
                tileH = Math.Min(height, DefaultTileDimension);

            tileW = Math.Max(1, Math.Min(tileW, width));
            tileH = Math.Max(1, Math.Min(tileH, height));
        }
    }
}
