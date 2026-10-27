using System;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Telemetry and diagnostics snapshot for an individual <see cref="RuntimeCycle"/>.
    /// </summary>
    public struct CycleExecutionStats
    {
        /// <summary>
        /// The runtime cycle this telemetry represents.
        /// </summary>
        public RuntimeCycle Cycle { get; set; }

        /// <summary>
        /// Configured interval between cycle triggers in milliseconds.
        /// </summary>
        public double IntervalMs { get; set; }

        /// <summary>
        /// Whether this cycle is actively enabled.
        /// </summary>
        public bool IsEnabled { get; set; }

        /// <summary>
        /// Whether callbacks are dispatched to the UI STA thread via UiDispatcher.
        /// </summary>
        public bool IsUiMarshaled { get; set; }

        /// <summary>
        /// Total number of successful cycle executions.
        /// </summary>
        public long CycleCount { get; set; }

        /// <summary>
        /// Average execution duration in microseconds.
        /// </summary>
        public double AvgDurationMicros { get; set; }

        /// <summary>
        /// Peak execution duration in microseconds.
        /// </summary>
        public double MaxDurationMicros { get; set; }

        /// <summary>
        /// Duration of the most recent cycle execution in microseconds.
        /// </summary>
        public double LastDurationMicros { get; set; }

        /// <summary>
        /// Total number of detected overruns where cycle execution time exceeded the configured period.
        /// </summary>
        public long OverrunCount { get; set; }

        /// <summary>
        /// Estimated jitter / scheduling deviation in microseconds.
        /// </summary>
        public double JitterMicros { get; set; }

        public override string ToString()
        {
            return $"{Cycle,-10} | Count: {CycleCount,8:N0} | Avg: {AvgDurationMicros,6:F2} µs | Max: {MaxDurationMicros,6:F2} µs | Overruns: {OverrunCount,4} | Interval: {IntervalMs:F1} ms";
        }
    }

    /// <summary>
    /// Consolidated runtime diagnostics snapshot across all 7 industrial cycles.
    /// </summary>
    public sealed class RuntimeDiagnostics
    {
        public RuntimeMode Mode { get; set; }
        public bool IsRunning { get; set; }
        public bool IsPaused { get; set; }
        public TimeSpan TotalRuntime { get; set; }
        public CycleExecutionStats[] Cycles { get; set; } = Array.Empty<CycleExecutionStats>();

        public CycleExecutionStats GetStats(RuntimeCycle cycle)
        {
            for (int i = 0; i < Cycles.Length; i++)
            {
                if (Cycles[i].Cycle == cycle) return Cycles[i];
            }
            return default;
        }
    }
}
