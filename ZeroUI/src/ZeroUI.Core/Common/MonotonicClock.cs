using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ZeroUI.Core.Common
{
    /// <summary>
    /// Provides a 64-bit monotonic millisecond clock based on <see cref="Stopwatch"/>.
    /// Unlike <see cref="System.Environment.TickCount"/> (Int32), this clock never overflows
    /// and is safe for continuous 24/7 industrial SCADA operation.
    /// </summary>
    public static class MonotonicClock
    {
        private static readonly long _startTimestamp = Stopwatch.GetTimestamp();
        private static readonly double _ticksPerMs = Stopwatch.Frequency / 1000.0;

        /// <summary>
        /// Returns monotonic elapsed milliseconds since process start.
        /// Uses 64-bit arithmetic — never overflows.
        /// </summary>
        public static long ElapsedMilliseconds
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (long)((Stopwatch.GetTimestamp() - _startTimestamp) / _ticksPerMs);
        }
    }
}
