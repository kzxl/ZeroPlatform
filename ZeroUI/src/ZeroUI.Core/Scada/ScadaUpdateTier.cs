using System;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Update cadence tier classifications for industrial SCADA architectures.
    /// Strictly decouples 10 kHz field communication from WinForms display rendering.
    /// </summary>
    public enum ScadaUpdateTier
    {
        /// <summary>
        /// 10 kHz (~0.1 ms period) nominal frequency.
        /// Responsibilities: Field PLC ingestion, safety trips/interlocks, ISA-18.2 alarm detection, historian ring buffer.
        /// Rules: Dedicated background threads, zero heap allocations, zero locks, zero UI thread synchronization.
        /// </summary>
        Fast = 10000,

        /// <summary>
        /// 100 Hz - 1000 Hz (1 ms - 10 ms period) nominal frequency.
        /// Responsibilities: Mathematical calculations (mass/flow balance), OEE tracking, PackML state progression, sliding-window aggregations (RMS, Min/Max).
        /// Rules: Dedicated periodic compute loop, zero UI thread synchronization.
        /// </summary>
        Medium = 1000,

        /// <summary>
        /// 30 Hz - 60 Hz (16 ms - 33 ms period) nominal frequency.
        /// Responsibilities: UI controls, Plant Mimic Canvas / ZeroScene, trend chart decimation, animations, operator display.
        /// Rules: Executed strictly on the UI STA Thread via pull-based TripleBuffer acquisition or coalesced dirty batches.
        /// </summary>
        Slow = 60
    }

    /// <summary>
    /// Runtime performance snapshot capturing throughput and latencies across all 3 tiers.
    /// </summary>
    public struct ScadaPipelineMetrics
    {
        public long FastTierIngestCount;
        public double FastTierUpdatesPerSec;
        public double FastTierAvgLatencyMicros;

        public long MediumTierCyclesCount;
        public double MediumTierCyclesPerSec;
        public double MediumTierAvgCycleMs;

        public long SlowTierFramesCount;
        public double SlowTierFps;
        public double SlowTierAvgFrameMs;

        public long SafetyInterlockEvaluations;
        public long SafetyTripsTriggered;

        public override string ToString()
        {
            return $"[FAST] {FastTierUpdatesPerSec:N0} updates/s ({FastTierAvgLatencyMicros:F2} µs) | " +
                   $"[MEDIUM] {MediumTierCyclesPerSec:F1} Hz ({MediumTierAvgCycleMs:F2} ms) | " +
                   $"[SLOW] {SlowTierFps:F1} FPS ({SlowTierAvgFrameMs:F2} ms) | " +
                   $"[SAFETY] {SafetyTripsTriggered} trips";
        }
    }
}
