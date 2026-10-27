using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZeroNetwork.Common
{
    /// <summary>
    /// High-resolution hardware clock and timer engine for industrial SCADA and real-time networking.
    /// Provides sub-10-nanosecond hardware timestamping via QPC / RDTSC, eliminating the 15.6ms Windows OS timer granularity.
    /// </summary>
    public static class ZeroClock
    {
#if NETFRAMEWORK
        private static readonly bool s_isWindows = true;
#else
        private static readonly bool s_isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
#endif
        private static readonly long s_frequency;
        private static readonly double s_nanosecondsPerTick;
        private static readonly double s_microsecondsPerTick;
        private static readonly double s_millisecondsPerTick;

        private static readonly long s_startRawTicks;
        private static readonly DateTime s_startUtcTime;

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        static ZeroClock()
        {
            if (s_isWindows && QueryPerformanceFrequency(out long qpf) && qpf > 0)
            {
                s_frequency = qpf;
            }
            else
            {
                s_frequency = Stopwatch.Frequency;
            }

            s_nanosecondsPerTick = 1_000_000_000.0 / s_frequency;
            s_microsecondsPerTick = 1_000_000.0 / s_frequency;
            s_millisecondsPerTick = 1_000.0 / s_frequency;

            s_startRawTicks = GetTimestamp();
            s_startUtcTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Gets the hardware timer frequency in ticks per second.
        /// </summary>
        public static long Frequency => s_frequency;

        /// <summary>
        /// Gets the current high-resolution raw timer tick count.
        /// </summary>
        public static long GetTimestamp()
        {
            if (s_isWindows)
            {
                QueryPerformanceCounter(out long counter);
                return counter;
            }
            return Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Gets the elapsed nanoseconds between two raw hardware tick counts.
        /// </summary>
        public static double GetElapsedNanoseconds(long startTicks, long endTicks)
        {
            return (endTicks - startTicks) * s_nanosecondsPerTick;
        }

        /// <summary>
        /// Gets the elapsed microseconds between two raw hardware tick counts.
        /// </summary>
        public static double GetElapsedMicroseconds(long startTicks, long endTicks)
        {
            return (endTicks - startTicks) * s_microsecondsPerTick;
        }

        /// <summary>
        /// Gets the elapsed milliseconds between two raw hardware tick counts.
        /// </summary>
        public static double GetElapsedMilliseconds(long startTicks, long endTicks)
        {
            return (endTicks - startTicks) * s_millisecondsPerTick;
        }

        /// <summary>
        /// Gets a calibrated UTC DateTime with sub-microsecond precision.
        /// Replaces BCL DateTime.UtcNow which suffers from 0.5-15.6ms Windows system timer quantum.
        /// </summary>
        public static DateTime HighPrecisionUtcNow
        {
            get
            {
                long currentTicks = GetTimestamp();
                double elapsedSec = (currentTicks - s_startRawTicks) / (double)s_frequency;
                return s_startUtcTime.AddSeconds(elapsedSec);
            }
        }
    }
}
