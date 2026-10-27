using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Internal;

namespace ZeroPrimitives.Diagnostics
{
    /// <summary>
    /// A zero-allocation, lightweight value-type stopwatch for microsecond latency profiling in hot paths.
    /// Eliminates heap allocation compared to System.Diagnostics.Stopwatch.
    /// </summary>
    public readonly struct ValueStopwatch
    {
        private static readonly double TimestampToTicks = TimeSpan.TicksPerSecond / (double)Stopwatch.Frequency;
        private readonly long _startTimestamp;

        /// <summary>
        /// Gets a value indicating whether this stopwatch has been initialized and started.
        /// </summary>
        public bool IsActive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _startTimestamp != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValueStopwatch(long startTimestamp)
        {
            _startTimestamp = startTimestamp;
        }

        /// <summary>
        /// Starts and returns a new active ValueStopwatch.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueStopwatch StartNew()
        {
            return new ValueStopwatch(Stopwatch.GetTimestamp());
        }

        /// <summary>
        /// Gets the raw starting timestamp.
        /// </summary>
        public long RawStartTimestamp => _startTimestamp;

        /// <summary>
        /// Gets the total elapsed time measured by the current instance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSpan GetElapsedTime()
        {
            if (!IsActive)
            {
                ThrowHelper.ThrowInvalidOperationException("An uninitialized ValueStopwatch cannot be used to measure elapsed time.");
            }

            long end = Stopwatch.GetTimestamp();
            long delta = end - _startTimestamp;
            long ticks = (long)(TimestampToTicks * delta);
            return new TimeSpan(ticks);
        }

        /// <summary>
        /// Gets the total elapsed milliseconds measured by the current instance.
        /// </summary>
        public long ElapsedMilliseconds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (long)GetElapsedTime().TotalMilliseconds;
        }

        /// <summary>
        /// Computes the elapsed time from a raw timestamp returned by Stopwatch.GetTimestamp().
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TimeSpan GetElapsedTime(long startingTimestamp)
        {
            long end = Stopwatch.GetTimestamp();
            long delta = end - startingTimestamp;
            long ticks = (long)(TimestampToTicks * delta);
            return new TimeSpan(ticks);
        }
    }
}
