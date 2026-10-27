using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Common;
using ZeroUI.Core.Mes;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada.Analytics;
using ZeroUI.Core.Scada.Safety;

namespace ZeroUI.Core.Scada.Pipeline
{
    /// <summary>
    /// Central coordinator enforcing the 3-Tier Update Architecture for industrial SCADA.
    /// Strictly separates:
    /// - Tier 1: FAST (10 kHz) Field PLC Ingestion, Safety/Interlock, Alarm Logic, Historian buffer.
    /// - Tier 2: MEDIUM (100–1000 Hz) Analytical calculations, OEE, State Machines, Sliding-window aggregations.
    /// - Tier 3: SLOW (30–60 Hz) UI Controls, Plant Mimic Canvas, Scene Graph, Trend charts, animations.
    /// </summary>
    public sealed class ScadaPipelineCoordinator : IDisposable
    {
        private static readonly Lazy<ScadaPipelineCoordinator> _shared =
            new Lazy<ScadaPipelineCoordinator>(() => new ScadaPipelineCoordinator());

        public static ScadaPipelineCoordinator Shared => _shared.Value;

        private readonly ScadaSafetyInterlockEngine _safetyEngine;
        private readonly ScadaAggregationEngine _aggregationEngine;
        private readonly List<Action<ScadaPipelineCoordinator>> _mediumCalculations = new List<Action<ScadaPipelineCoordinator>>();
        private readonly object _mediumCalcLock = new object();

        private IDisposable? _runtimeLogicSub;
        private int _mediumIntervalMs = 10; // 100 Hz default
        private int _isMediumExecuting = 0;
        private bool _isRunning = false;
        private bool _disposed = false;

        // Metrics Tracking
        private long _fastIngestCount;
        private long _fastTotalTicks;
        private long _mediumCyclesCount;
        private long _mediumTotalTicks;
        private long _slowFramesCount;
        private long _slowTotalTicks;
        private long _lastMetricsResetTick = MonotonicClock.ElapsedMilliseconds;

        public ScadaSafetyInterlockEngine Safety => _safetyEngine;
        public ScadaAggregationEngine Aggregation => _aggregationEngine;

        public bool IsRunning => _isRunning;
        public int MediumIntervalMs => _mediumIntervalMs;

        public ScadaPipelineCoordinator(int mediumFrequencyHz = 100)
        {
            _safetyEngine = ScadaSafetyInterlockEngine.Shared;
            _aggregationEngine = new ScadaAggregationEngine();
            SetMediumFrequency(mediumFrequencyHz);
        }

        /// <summary>
        /// Sets the nominal frequency for the Medium analytical tier (100 Hz to 1000 Hz).
        /// </summary>
        public void SetMediumFrequency(int frequencyHz)
        {
            frequencyHz = Math.Max(10, Math.Min(1000, frequencyHz));
            _mediumIntervalMs = Math.Max(1, 1000 / frequencyHz);

            ZeroRuntime.Shared.SetCycleInterval(RuntimeCycle.Logic, TimeSpan.FromMilliseconds(_mediumIntervalMs));
        }

        /// <summary>
        /// Starts the Medium analytical compute tier background loop.
        /// </summary>
        public void Start()
        {
            if (_isRunning || _disposed) return;
            _isRunning = true;

            ZeroRuntime.Shared.SetCycleInterval(RuntimeCycle.Logic, TimeSpan.FromMilliseconds(_mediumIntervalMs));
            _runtimeLogicSub ??= ZeroRuntime.Shared.Register(RuntimeCycle.Logic, (delta, count) => ExecuteMediumTierCycle());

            if (!ZeroRuntime.Shared.IsRunning)
            {
                ZeroRuntime.Shared.Start();
            }
        }

        /// <summary>
        /// Stops the background analytical loops.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            _runtimeLogicSub?.Dispose();
            _runtimeLogicSub = null;
        }

        /// <summary>
        /// Registers a custom mathematical or logic calculation to be executed during the Medium Tier cycle.
        /// </summary>
        public void RegisterMediumCalculation(Action<ScadaPipelineCoordinator> calculation)
        {
            if (calculation == null) throw new ArgumentNullException(nameof(calculation));
            lock (_mediumCalcLock)
            {
                _mediumCalculations.Add(calculation);
            }
        }

        // ==========================================
        // TIER 1: FAST PATH (10 kHz / 0.1 ms nominal)
        // ==========================================

        /// <summary>
        /// Ingests a numeric telemetry tag on the Fast Tier (10 kHz).
        /// Evaluates safety interlocks, updates flat storage, publishes to TripleBuffer.
        /// Guarantees zero allocations and zero UI synchronization.
        /// </summary>
        public bool IngestFast(int tagId, double value, ScadaQuality quality = ScadaQuality.Good)
        {
            long start = Stopwatch.GetTimestamp();

            // 1. Evaluate Safety Interlock (< 1 µs)
            _safetyEngine.EvaluateTag(tagId, value);

            // 2. Ingest into Flat Storage with Decoupled UI (dispatchUi: false)
            bool updated = ZeroTagEngine.SetNumeric(tagId, value, quality, timestampUtcMs: 0, dispatchUi: false);

            // 3. Mark TripleBuffer Dirty for Slow Tier pull
            if (updated)
            {
                ZeroTagEngine.TripleBuffer.PublishWrite();
            }

            long elapsedTicks = Stopwatch.GetTimestamp() - start;
            Interlocked.Increment(ref _fastIngestCount);
            Interlocked.Add(ref _fastTotalTicks, elapsedTicks);

            return updated;
        }

        /// <summary>
        /// Ingests a numeric telemetry tag by tag path on the Fast Tier.
        /// </summary>
        public bool IngestFast(string tagPath, double value, ScadaQuality quality = ScadaQuality.Good)
        {
            int tagId = ZeroTagEngine.GetOrRegisterTag(tagPath);
            return IngestFast(tagId, value, quality);
        }

        /// <summary>
        /// Ingests a boolean telemetry tag on the Fast Tier.
        /// </summary>
        public bool IngestFastBoolean(int tagId, bool value, ScadaQuality quality = ScadaQuality.Good)
        {
            long start = Stopwatch.GetTimestamp();

            _safetyEngine.EvaluateTag(tagId, value ? 1.0 : 0.0);
            bool updated = ZeroTagEngine.SetBoolean(tagId, value, quality, timestampUtcMs: 0, dispatchUi: false);

            if (updated)
            {
                ZeroTagEngine.TripleBuffer.PublishWrite();
            }

            long elapsedTicks = Stopwatch.GetTimestamp() - start;
            Interlocked.Increment(ref _fastIngestCount);
            Interlocked.Add(ref _fastTotalTicks, elapsedTicks);

            return updated;
        }

        // ==========================================
        // TIER 2: MEDIUM PATH (100 Hz - 1000 Hz)
        // ==========================================

        private void OnMediumTimerTick(object? state)
        {
            ExecuteMediumTierCycle();
        }

        /// <summary>
        /// Executes a single Medium Tier cycle (aggregations + custom logic/OEE).
        /// Can be called directly by ZeroRuntime or dedicated loop.
        /// </summary>
        public void ExecuteMediumTierCycle()
        {
            if (!_isRunning || _disposed) return;
            if (Interlocked.CompareExchange(ref _isMediumExecuting, 1, 0) != 0)
                return; // Previous cycle still executing, skip

            long start = Stopwatch.GetTimestamp();

            try
            {
                long currentTick = MonotonicClock.ElapsedMilliseconds;

                // 1. Execute sliding-window aggregations (SMA, RMS, Min/Max)
                _aggregationEngine.ExecuteAggregationCycle(currentTick);

                // 2. Execute registered custom calculations (OEE, PackML, Mass Balance)
                List<Action<ScadaPipelineCoordinator>>? calcs = null;
                lock (_mediumCalcLock)
                {
                    if (_mediumCalculations.Count > 0)
                    {
                        calcs = new List<Action<ScadaPipelineCoordinator>>(_mediumCalculations);
                    }
                }

                if (calcs != null)
                {
                    for (int i = 0; i < calcs.Count; i++)
                    {
                        try { calcs[i](this); } catch { }
                    }
                }
            }
            finally
            {
                long elapsedTicks = Stopwatch.GetTimestamp() - start;
                Interlocked.Increment(ref _mediumCyclesCount);
                Interlocked.Add(ref _mediumTotalTicks, elapsedTicks);
                Interlocked.Exchange(ref _isMediumExecuting, 0);
            }
        }

        // ==========================================
        // TIER 3: SLOW PATH (30 Hz - 60 Hz / UI Thread)
        // ==========================================

        /// <summary>
        /// Flushes pending coalesced updates to WinForms controls and acquires new render frames.
        /// Called strictly on the UI STA Thread at 30–60 Hz (e.g. from Canvas Animation Timer).
        /// Completely shields the Windows Message Loop from 10 kHz field data.
        /// </summary>
        /// <param name="maxBatchSize">Maximum dirty tags to flush per frame.</param>
        /// <returns>Number of dirty tags dispatched to visual controls.</returns>
        public int PumpUiFrame(int maxBatchSize = 1024)
        {
            long start = Stopwatch.GetTimestamp();

            // 1. Check TripleBuffer for newest snapshot
            ZeroTagEngine.TripleBuffer.AcquireRenderBuffer(out bool hasUpdate);

            // 2. Dispatch coalesced dirty tags to bound controls
            int dispatched = ZeroTagEngine.FlushUiBatch(maxBatchSize);

            long elapsedTicks = Stopwatch.GetTimestamp() - start;
            Interlocked.Increment(ref _slowFramesCount);
            Interlocked.Add(ref _slowTotalTicks, elapsedTicks);

            return dispatched;
        }

        /// <summary>
        /// Retrieves runtime metrics snapshot across all 3 tiers.
        /// </summary>
        public ScadaPipelineMetrics GetMetrics()
        {
            long now = MonotonicClock.ElapsedMilliseconds;
            double elapsedSec = Math.Max(0.001, (now - _lastMetricsResetTick) / 1000.0);

            long fastCount = Interlocked.Read(ref _fastIngestCount);
            long fastTicks = Interlocked.Read(ref _fastTotalTicks);

            long medCount = Interlocked.Read(ref _mediumCyclesCount);
            long medTicks = Interlocked.Read(ref _mediumTotalTicks);

            long slowCount = Interlocked.Read(ref _slowFramesCount);
            long slowTicks = Interlocked.Read(ref _slowTotalTicks);

            double fastAvgLatencyMicros = fastCount > 0
                ? (double)fastTicks / fastCount / (Stopwatch.Frequency / 1_000_000.0)
                : 0.0;

            double medAvgMs = medCount > 0
                ? (double)medTicks / medCount / (Stopwatch.Frequency / 1_000.0)
                : 0.0;

            double slowAvgMs = slowCount > 0
                ? (double)slowTicks / slowCount / (Stopwatch.Frequency / 1_000.0)
                : 0.0;

            return new ScadaPipelineMetrics
            {
                FastTierIngestCount = fastCount,
                FastTierUpdatesPerSec = fastCount / elapsedSec,
                FastTierAvgLatencyMicros = fastAvgLatencyMicros,

                MediumTierCyclesCount = medCount,
                MediumTierCyclesPerSec = medCount / elapsedSec,
                MediumTierAvgCycleMs = medAvgMs,

                SlowTierFramesCount = slowCount,
                SlowTierFps = slowCount / elapsedSec,
                SlowTierAvgFrameMs = slowAvgMs,

                SafetyInterlockEvaluations = _safetyEngine.EvaluationsCount,
                SafetyTripsTriggered = _safetyEngine.TripsCount
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _isRunning = false;
            _runtimeLogicSub?.Dispose();
            _runtimeLogicSub = null;
        }
    }
}
