using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Centralized deterministic industrial runtime scheduler for the ZeroUI ecosystem.
    /// Orchestrates all system execution cycles (PLC, Logic, Telemetry, Historian, UI, Animation, Cleanup)
    /// under a single deterministic clock.
    /// Supports both high-precision real-time execution and virtual-time simulation/testing.
    /// </summary>
    public sealed class ZeroRuntime : IDisposable
    {
        private const int TotalCycles = 7;

        private static readonly Lazy<ZeroRuntime> _shared =
            new Lazy<ZeroRuntime>(() => new ZeroRuntime("Shared"));

        /// <summary>
        /// Global shared runtime instance.
        /// </summary>
        public static ZeroRuntime Shared => _shared.Value;

        /// <summary>
        /// Alias for <see cref="Shared"/>.
        /// </summary>
        public static ZeroRuntime Instance => Shared;

        private readonly string _name;
        private readonly CycleSlot[] _slots;
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private Thread? _workerThread;
        private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim _pauseSignal = new ManualResetEventSlim(true);

        private RuntimeMode _mode = RuntimeMode.RealTime;
        private bool _isRunning = false;
        private bool _isPaused = false;
        private bool _disposed = false;

        private long _virtualElapsedTicks = 0;
        private long _lastRealTimeTicks = 0;

        /// <summary>
        /// Event raised whenever a cycle duration exceeds its allotted interval.
        /// </summary>
        public event Action<RuntimeCycle, double, double>? CycleOverrun;

        /// <summary>
        /// Gets the human-readable identifier for this runtime.
        /// </summary>
        public string Name => _name;

        /// <summary>
        /// Gets or sets the operating mode (RealTime or VirtualTime).
        /// </summary>
        public RuntimeMode Mode
        {
            get => _mode;
            set
            {
                if (_isRunning && _mode != value)
                {
                    throw new InvalidOperationException("Cannot change RuntimeMode while ZeroRuntime is actively running. Call Stop() first.");
                }
                _mode = value;
            }
        }

        /// <summary>
        /// Gets whether the runtime is actively ticking.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Gets whether the runtime is currently paused.
        /// </summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Creates a new instance of <see cref="ZeroRuntime"/>.
        /// </summary>
        /// <param name="name">Descriptive name for diagnostics.</param>
        /// <param name="mode">Initial operational mode.</param>
        public ZeroRuntime(string name = "Runtime", RuntimeMode mode = RuntimeMode.RealTime)
        {
            _name = name;
            _mode = mode;
            _slots = new CycleSlot[TotalCycles];

            InitializeSlots();
        }

        private void InitializeSlots()
        {
            // Default industrial cadence:
            // Plc:       10 ms (100 Hz)
            // Logic:     10 ms (100 Hz)
            // Telemetry: 16 ms (~60 Hz)
            // Historian: 100 ms (10 Hz)
            // Ui:        16 ms (60 Hz)
            // Animation: 16 ms (60 Hz)
            // Cleanup:   1000 ms (1 Hz)
            _slots[(int)RuntimeCycle.Plc] = new CycleSlot(RuntimeCycle.Plc, TimeSpan.FromMilliseconds(10), isUiMarshaled: false);
            _slots[(int)RuntimeCycle.Logic] = new CycleSlot(RuntimeCycle.Logic, TimeSpan.FromMilliseconds(10), isUiMarshaled: false);
            _slots[(int)RuntimeCycle.Telemetry] = new CycleSlot(RuntimeCycle.Telemetry, TimeSpan.FromMilliseconds(16), isUiMarshaled: false);
            _slots[(int)RuntimeCycle.Historian] = new CycleSlot(RuntimeCycle.Historian, TimeSpan.FromMilliseconds(100), isUiMarshaled: false);
            _slots[(int)RuntimeCycle.Ui] = new CycleSlot(RuntimeCycle.Ui, TimeSpan.FromMilliseconds(16), isUiMarshaled: true);
            _slots[(int)RuntimeCycle.Animation] = new CycleSlot(RuntimeCycle.Animation, TimeSpan.FromMilliseconds(16), isUiMarshaled: true);
            _slots[(int)RuntimeCycle.Cleanup] = new CycleSlot(RuntimeCycle.Cleanup, TimeSpan.FromMilliseconds(1000), isUiMarshaled: false);
        }

        // ==========================================
        // LIFECYCLE MANAGEMENT
        // ==========================================

        /// <summary>
        /// Starts the centralized runtime engine.
        /// In RealTime mode, spawns the dedicated high-precision master worker thread.
        /// </summary>
        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ZeroRuntime));
            if (_isRunning) return;

            _isRunning = true;
            _isPaused = false;
            _stopSignal.Reset();
            _pauseSignal.Set();

            _stopwatch.Restart();
            _lastRealTimeTicks = _stopwatch.ElapsedTicks;

            long currentTicks = _mode == RuntimeMode.RealTime ? _stopwatch.ElapsedTicks : _virtualElapsedTicks;
            for (int i = 0; i < TotalCycles; i++)
            {
                _slots[i].NextTriggerTicks = currentTicks + _slots[i].IntervalTicks;
                _slots[i].LastRunTicks = currentTicks;
            }

            if (_mode == RuntimeMode.RealTime)
            {
                _workerThread = new Thread(MasterWorkerLoop)
                {
                    Name = $"ZeroRuntime_{_name}_MasterLoop",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _workerThread.Start();
            }
        }

        /// <summary>
        /// Stops the central runtime loop.
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _stopSignal.Set();
            _pauseSignal.Set();

            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join(500);
                _workerThread = null;
            }

            _stopwatch.Stop();
        }

        /// <summary>
        /// Pauses execution of all scheduled cycles without resetting counters.
        /// </summary>
        public void Pause()
        {
            if (!_isRunning || _isPaused) return;
            _isPaused = true;
            _pauseSignal.Reset();
        }

        /// <summary>
        /// Resumes execution after being paused.
        /// </summary>
        public void Resume()
        {
            if (!_isRunning || !_isPaused) return;
            _isPaused = false;

            // Shift trigger baselines so cycles don't fire rapidly to catch up during pause
            long currentTicks = _mode == RuntimeMode.RealTime ? _stopwatch.ElapsedTicks : _virtualElapsedTicks;
            for (int i = 0; i < TotalCycles; i++)
            {
                _slots[i].NextTriggerTicks = currentTicks + _slots[i].IntervalTicks;
                _slots[i].LastRunTicks = currentTicks;
            }

            _pauseSignal.Set();
        }

        /// <summary>
        /// Resets all metrics, task registrations, and clock state to defaults.
        /// </summary>
        public void Reset()
        {
            Stop();
            _virtualElapsedTicks = 0;
            _lastRealTimeTicks = 0;
            InitializeSlots();
        }

        // ==========================================
        // REGISTRATION APIS
        // ==========================================

        /// <summary>
        /// Subscribes a parameterless action callback to the specified cycle.
        /// </summary>
        public IDisposable Register(RuntimeCycle cycle, Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            return Register(cycle, (delta, count) => callback());
        }

        /// <summary>
        /// Subscribes an action callback receiving cycle elapsed delta and monotonic cycle index.
        /// </summary>
        public IDisposable Register(RuntimeCycle cycle, Action<TimeSpan, long> callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));

            var slot = _slots[(int)cycle];
            lock (slot.Lock)
            {
                if (!slot.ActionListeners.Contains(callback))
                {
                    slot.ActionListeners.Add(callback);
                    slot.RebuildActionSnapshot();
                }
            }

            return new RuntimeSubscriptionToken(() => Unregister(cycle, callback));
        }

        /// <summary>
        /// Subscribes a zero-allocation <see cref="IRuntimeTask"/> implementation.
        /// </summary>
        public IDisposable Register(RuntimeCycle cycle, IRuntimeTask task)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var slot = _slots[(int)cycle];
            lock (slot.Lock)
            {
                if (!slot.TaskListeners.Contains(task))
                {
                    slot.TaskListeners.Add(task);
                    slot.RebuildTaskSnapshot();
                }
            }

            return new RuntimeSubscriptionToken(() => Unregister(cycle, task));
        }

        /// <summary>
        /// Unregisters an action callback from the specified cycle.
        /// </summary>
        public bool Unregister(RuntimeCycle cycle, Action<TimeSpan, long> callback)
        {
            if (callback == null) return false;

            var slot = _slots[(int)cycle];
            lock (slot.Lock)
            {
                bool removed = slot.ActionListeners.Remove(callback);
                if (removed) slot.RebuildActionSnapshot();
                return removed;
            }
        }

        /// <summary>
        /// Unregisters an <see cref="IRuntimeTask"/> from the specified cycle.
        /// </summary>
        public bool Unregister(RuntimeCycle cycle, IRuntimeTask task)
        {
            if (task == null) return false;

            var slot = _slots[(int)cycle];
            lock (slot.Lock)
            {
                bool removed = slot.TaskListeners.Remove(task);
                if (removed) slot.RebuildTaskSnapshot();
                return removed;
            }
        }

        // ==========================================
        // CONFIGURATION APIS
        // ==========================================

        /// <summary>
        /// Gets the configured period interval for the specified cycle.
        /// </summary>
        public TimeSpan GetCycleInterval(RuntimeCycle cycle)
        {
            return _slots[(int)cycle].Interval;
        }

        /// <summary>
        /// Updates the configured period interval for the specified cycle.
        /// </summary>
        public void SetCycleInterval(RuntimeCycle cycle, TimeSpan interval)
        {
            if (interval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(interval), "Cycle interval must be positive.");

            var slot = _slots[(int)cycle];
            slot.Interval = interval;
            slot.IntervalTicks = (long)(interval.TotalSeconds * Stopwatch.Frequency);
        }

        /// <summary>
        /// Enables or disables execution for a specific cycle.
        /// </summary>
        public void SetCycleEnabled(RuntimeCycle cycle, bool enabled)
        {
            _slots[(int)cycle].IsEnabled = enabled;
        }

        /// <summary>
        /// Configures whether callbacks for this cycle are marshaled to the UI thread.
        /// </summary>
        public void SetCycleUiMarshaled(RuntimeCycle cycle, bool uiMarshaled)
        {
            _slots[(int)cycle].IsUiMarshaled = uiMarshaled;
        }

        // ==========================================
        // VIRTUAL TIME / SIMULATION & TESTING APIS
        // ==========================================

        /// <summary>
        /// Advances virtual time by the specified duration, deterministically executing all cycles
        /// that qualify within this step in strict phase order:
        /// Plc -> Logic -> Telemetry -> Historian -> Ui -> Animation -> Cleanup.
        /// </summary>
        /// <param name="delta">Virtual time delta to advance.</param>
        public void Step(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delta), "Delta cannot be negative.");
            if (_isPaused) return;

            long stepTicks = (long)(delta.TotalSeconds * Stopwatch.Frequency);
            _virtualElapsedTicks += stepTicks;

            ProcessCycles(_virtualElapsedTicks);
        }

        /// <summary>
        /// Manually triggers a specific cycle slice.
        /// Useful when hosting custom UI rendering pumps or synchronizing with external game loops.
        /// </summary>
        public void StepCycle(RuntimeCycle cycle, TimeSpan delta)
        {
            var slot = _slots[(int)cycle];
            ExecuteSlot(slot, delta, (long)(delta.TotalSeconds * Stopwatch.Frequency));
        }

        /// <summary>
        /// Synchronously pumps the UI cycle on the caller's thread (typically UI STA thread).
        /// </summary>
        public void PumpUiCycle()
        {
            StepCycle(RuntimeCycle.Ui, _slots[(int)RuntimeCycle.Ui].Interval);
        }

        /// <summary>
        /// Synchronously pumps the Animation cycle on the caller's thread.
        /// </summary>
        public void PumpAnimationCycle()
        {
            StepCycle(RuntimeCycle.Animation, _slots[(int)RuntimeCycle.Animation].Interval);
        }

        // ==========================================
        // REALTIME MASTER LOOP
        // ==========================================

        private void MasterWorkerLoop()
        {
            while (_isRunning)
            {
                if (_stopSignal.Wait(0)) break;

                _pauseSignal.Wait();
                if (!_isRunning) break;

                long nowTicks = _stopwatch.ElapsedTicks;
                ProcessCycles(nowTicks);

                // Compute sleep duration to next scheduled cycle boundary
                long minTicksToNext = long.MaxValue;
                for (int i = 0; i < TotalCycles; i++)
                {
                    if (!_slots[i].IsEnabled) continue;
                    long diff = _slots[i].NextTriggerTicks - nowTicks;
                    if (diff < minTicksToNext) minTicksToNext = diff;
                }

                if (minTicksToNext > 0)
                {
                    double msToNext = (double)minTicksToNext / Stopwatch.Frequency * 1000.0;
                    if (msToNext >= 2.0)
                    {
                        // Sleep for integer ms minus 1ms headroom for scheduling precision
                        int sleepMs = Math.Max(1, (int)msToNext - 1);
                        _stopSignal.Wait(sleepMs);
                    }
                    else
                    {
                        // Microsecond fine-grained spinning/yielding
                        Thread.Yield();
                    }
                }
            }
        }

        // ==========================================
        // DETERMINISTIC PHASE PROCESSING
        // ==========================================

        private void ProcessCycles(long currentTicks)
        {
            // Guaranteed strictly deterministic order:
            // 0: Plc -> 1: Logic -> 2: Telemetry -> 3: Historian -> 4: Ui -> 5: Animation -> 6: Cleanup
            for (int i = 0; i < TotalCycles; i++)
            {
                var slot = _slots[i];
                if (!slot.IsEnabled) continue;

                if (currentTicks >= slot.NextTriggerTicks)
                {
                    long elapsedSinceLast = currentTicks - slot.LastRunTicks;
                    if (elapsedSinceLast <= 0) elapsedSinceLast = slot.IntervalTicks;

                    TimeSpan delta = TimeSpan.FromSeconds((double)elapsedSinceLast / Stopwatch.Frequency);

                    // Track scheduling jitter
                    long expectedTicks = slot.NextTriggerTicks;
                    long jitterTicks = currentTicks - expectedTicks;
                    slot.LastJitterMicros = (double)jitterTicks / Stopwatch.Frequency * 1_000_000.0;

                    // Advance schedule
                    slot.NextTriggerTicks = currentTicks + slot.IntervalTicks;
                    slot.LastRunTicks = currentTicks;

                    // Execute cycle
                    if (slot.IsUiMarshaled && _mode == RuntimeMode.RealTime && UiDispatcher.IsInitialized)
                    {
                        UiDispatcher.Post(() => ExecuteSlot(slot, delta, elapsedSinceLast));
                    }
                    else
                    {
                        ExecuteSlot(slot, delta, elapsedSinceLast);
                    }
                }
            }
        }

        private void ExecuteSlot(CycleSlot slot, TimeSpan delta, long elapsedTicks)
        {
            long start = Stopwatch.GetTimestamp();
            long cycleIndex = Interlocked.Increment(ref slot.CycleCount);

            // 1. Execute Action Delegates from immutable snapshot
            var actionSnap = slot.ActionSnapshot;
            if (actionSnap != null)
            {
                for (int i = 0; i < actionSnap.Length; i++)
                {
                    try { actionSnap[i](delta, cycleIndex); }
                    catch { /* Shield runtime from individual listener exceptions */ }
                }
            }

            // 2. Execute IRuntimeTask Contracts from immutable snapshot
            var taskSnap = slot.TaskSnapshot;
            if (taskSnap != null)
            {
                for (int i = 0; i < taskSnap.Length; i++)
                {
                    try { taskSnap[i].Execute(delta, cycleIndex); }
                    catch { /* Shield runtime */ }
                }
            }

            long durationTicks = Stopwatch.GetTimestamp() - start;
            slot.RecordExecution(durationTicks);

            // Check for overrun
            double durationMs = (double)durationTicks / Stopwatch.Frequency * 1000.0;
            if (durationMs > slot.Interval.TotalMilliseconds)
            {
                Interlocked.Increment(ref slot.OverrunCount);
                CycleOverrun?.Invoke(slot.Cycle, durationMs, slot.Interval.TotalMilliseconds);
            }
        }

        // ==========================================
        // DIAGNOSTICS APIS
        // ==========================================

        /// <summary>
        /// Retrieves telemetry statistics for an individual cycle.
        /// </summary>
        public CycleExecutionStats GetCycleStats(RuntimeCycle cycle)
        {
            var slot = _slots[(int)cycle];
            return slot.ToStats();
        }

        /// <summary>
        /// Retrieves consolidated diagnostics across all 7 cycles.
        /// </summary>
        public RuntimeDiagnostics GetDiagnostics()
        {
            var stats = new CycleExecutionStats[TotalCycles];
            for (int i = 0; i < TotalCycles; i++)
            {
                stats[i] = _slots[i].ToStats();
            }

            TimeSpan totalRuntime = _mode == RuntimeMode.RealTime
                ? _stopwatch.Elapsed
                : TimeSpan.FromSeconds((double)_virtualElapsedTicks / Stopwatch.Frequency);

            return new RuntimeDiagnostics
            {
                Mode = _mode,
                IsRunning = _isRunning,
                IsPaused = _isPaused,
                TotalRuntime = totalRuntime,
                Cycles = stats
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            _stopSignal.Dispose();
            _pauseSignal.Dispose();
        }

        // ==========================================
        // INTERNAL CYCLE SLOT & SUBSCRIPTION TOKEN
        // ==========================================

        private sealed class CycleSlot
        {
            public readonly RuntimeCycle Cycle;
            public readonly object Lock = new object();

            public TimeSpan Interval;
            public long IntervalTicks;
            public long NextTriggerTicks;
            public long LastRunTicks;
            public bool IsEnabled = true;
            public bool IsUiMarshaled;

            public long CycleCount;
            public long TotalElapsedTicks;
            public long MaxElapsedTicks;
            public long LastElapsedTicks;
            public long OverrunCount;
            public double LastJitterMicros;

            public readonly List<Action<TimeSpan, long>> ActionListeners = new List<Action<TimeSpan, long>>(16);
            public readonly List<IRuntimeTask> TaskListeners = new List<IRuntimeTask>(16);

            public Action<TimeSpan, long>[]? ActionSnapshot;
            public IRuntimeTask[]? TaskSnapshot;

            public CycleSlot(RuntimeCycle cycle, TimeSpan defaultInterval, bool isUiMarshaled)
            {
                Cycle = cycle;
                Interval = defaultInterval;
                IntervalTicks = (long)(defaultInterval.TotalSeconds * Stopwatch.Frequency);
                IsUiMarshaled = isUiMarshaled;
            }

            public void RebuildActionSnapshot()
            {
                ActionSnapshot = ActionListeners.Count > 0 ? ActionListeners.ToArray() : null;
            }

            public void RebuildTaskSnapshot()
            {
                TaskSnapshot = TaskListeners.Count > 0 ? TaskListeners.ToArray() : null;
            }

            public void RecordExecution(long durationTicks)
            {
                Interlocked.Add(ref TotalElapsedTicks, durationTicks);
                Interlocked.Exchange(ref LastElapsedTicks, durationTicks);

                long currentMax = Interlocked.Read(ref MaxElapsedTicks);
                while (durationTicks > currentMax)
                {
                    long old = Interlocked.CompareExchange(ref MaxElapsedTicks, durationTicks, currentMax);
                    if (old == currentMax) break;
                    currentMax = old;
                }
            }

            public CycleExecutionStats ToStats()
            {
                long count = Interlocked.Read(ref CycleCount);
                long totalTicks = Interlocked.Read(ref TotalElapsedTicks);
                long maxTicks = Interlocked.Read(ref MaxElapsedTicks);
                long lastTicks = Interlocked.Read(ref LastElapsedTicks);
                long overruns = Interlocked.Read(ref OverrunCount);

                double freq = Stopwatch.Frequency;
                double avgMicros = count > 0 ? (double)totalTicks / count / (freq / 1_000_000.0) : 0.0;
                double maxMicros = (double)maxTicks / (freq / 1_000_000.0);
                double lastMicros = (double)lastTicks / (freq / 1_000_000.0);

                return new CycleExecutionStats
                {
                    Cycle = Cycle,
                    IntervalMs = Interval.TotalMilliseconds,
                    IsEnabled = IsEnabled,
                    IsUiMarshaled = IsUiMarshaled,
                    CycleCount = count,
                    AvgDurationMicros = avgMicros,
                    MaxDurationMicros = maxMicros,
                    LastDurationMicros = lastMicros,
                    OverrunCount = overruns,
                    JitterMicros = LastJitterMicros
                };
            }
        }

        private sealed class RuntimeSubscriptionToken : IDisposable
        {
            private Action? _unsubscribe;

            public RuntimeSubscriptionToken(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                var action = Interlocked.Exchange(ref _unsubscribe, null);
                action?.Invoke();
            }
        }
    }
}
