using System;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Thread-safe global telemetry and accounting for off-heap unmanaged memory.
    /// Provides real-time visibility into native allocations across all ZeroPlatform subsystems.
    /// </summary>
    public static class NativeMemoryTracker
    {
        private static long _allocatedBytes;
        private static long _peakAllocatedBytes;
        private static long _activeBlocksCount;
        private static long _totalAllocationsCount;

        /// <summary>
        /// Gets the current number of bytes allocated off-heap across active allocators.
        /// </summary>
        public static long AllocatedBytes => Volatile.Read(ref _allocatedBytes);

        /// <summary>
        /// Gets the peak watermark of allocated off-heap bytes since process start or last reset.
        /// </summary>
        public static long PeakAllocatedBytes => Volatile.Read(ref _peakAllocatedBytes);

        /// <summary>
        /// Gets the current number of active (checked out) off-heap memory blocks.
        /// </summary>
        public static long ActiveBlocksCount => Volatile.Read(ref _activeBlocksCount);

        /// <summary>
        /// Gets the cumulative total number of blocks allocated since startup.
        /// </summary>
        public static long TotalAllocationsCount => Volatile.Read(ref _totalAllocationsCount);

        /// <summary>
        /// Records a new off-heap allocation.
        /// </summary>
        /// <param name="bytes">Number of bytes allocated.</param>
        public static void TrackAlloc(long bytes)
        {
            if (bytes <= 0) return;

            long current = Interlocked.Add(ref _allocatedBytes, bytes);
            Interlocked.Increment(ref _activeBlocksCount);
            Interlocked.Increment(ref _totalAllocationsCount);

            // Update peak watermark atomically
            long peak = Volatile.Read(ref _peakAllocatedBytes);
            while (current > peak)
            {
                long oldPeak = Interlocked.CompareExchange(ref _peakAllocatedBytes, current, peak);
                if (oldPeak == peak) break;
                peak = oldPeak;
            }
        }

        /// <summary>
        /// Records an off-heap deallocation.
        /// </summary>
        /// <param name="bytes">Number of bytes freed.</param>
        public static void TrackFree(long bytes)
        {
            if (bytes <= 0) return;

            Interlocked.Add(ref _allocatedBytes, -bytes);
            Interlocked.Decrement(ref _activeBlocksCount);
        }

        /// <summary>
        /// Resets the peak watermark and counter metrics (useful in test harnesses).
        /// </summary>
        public static void ResetMetrics()
        {
            Volatile.Write(ref _peakAllocatedBytes, Volatile.Read(ref _allocatedBytes));
            Volatile.Write(ref _totalAllocationsCount, 0);
        }
    }
}
