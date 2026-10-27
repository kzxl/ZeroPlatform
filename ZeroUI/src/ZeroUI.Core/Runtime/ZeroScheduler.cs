using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Performance and execution metrics for an individual scheduled job.
    /// </summary>
    public sealed class ScheduledJobMetrics
    {
        private long _invocations;
        private long _overruns;
        private long _totalDurationTicks;
        private long _maxDurationTicks;
        private long _lastDurationTicks;
        private long _lastRunUtcTicks;

        public long Invocations => Volatile.Read(ref _invocations);
        public long Overruns => Volatile.Read(ref _overruns);
        public TimeSpan LastDuration => TimeSpan.FromTicks(Volatile.Read(ref _lastDurationTicks));
        public TimeSpan MaxDuration => TimeSpan.FromTicks(Volatile.Read(ref _maxDurationTicks));
        public DateTime LastRunUtc => new DateTime(Volatile.Read(ref _lastRunUtcTicks), DateTimeKind.Utc);

        public double AverageDurationMs
        {
            get
            {
                long inv = Volatile.Read(ref _invocations);
                if (inv == 0) return 0.0;
                long totalTicks = Volatile.Read(ref _totalDurationTicks);
                return (double)totalTicks / TimeSpan.TicksPerMillisecond / inv;
            }
        }

        internal void RecordRun(long durationTicks, bool overrun)
        {
            Interlocked.Increment(ref _invocations);
            if (overrun) Interlocked.Increment(ref _overruns);

            Volatile.Write(ref _lastDurationTicks, durationTicks);
            Interlocked.Add(ref _totalDurationTicks, durationTicks);

            long currentMax;
            do
            {
                currentMax = Volatile.Read(ref _maxDurationTicks);
                if (durationTicks <= currentMax) break;
            } while (Interlocked.CompareExchange(ref _maxDurationTicks, durationTicks, currentMax) != currentMax);

            Volatile.Write(ref _lastRunUtcTicks, DateTime.UtcNow.Ticks);
        }
    }

    /// <summary>
    /// Represents a registered deterministic job inside <see cref="ZeroScheduler"/>.
    /// </summary>
    public sealed class ScheduledJob : IDisposable
    {
        private readonly ZeroScheduler _owner;
        private readonly string _id;
        private readonly string _name;
        private readonly TimeSpan _interval;
        private readonly Action? _syncAction;
        private readonly Func<CancellationToken, Task>? _asyncAction;
        private readonly ScheduledJobMetrics _metrics = new ScheduledJobMetrics();

        private bool _isEnabled = true;
        private int _isExecuting = 0;
        private long _nextTriggerTicks;

        public string Id => _id;
        public string Name => _name;
        public TimeSpan Interval => _interval;
        public bool IsEnabled => _isEnabled;
        public ScheduledJobMetrics Metrics => _metrics;

        internal long NextTriggerTicks
        {
            get => _nextTriggerTicks;
            set => _nextTriggerTicks = value;
        }

        internal ScheduledJob(ZeroScheduler owner, string id, string name, TimeSpan interval, Action action)
        {
            _owner = owner;
            _id = id;
            _name = name;
            _interval = interval;
            _syncAction = action;
        }

        internal ScheduledJob(ZeroScheduler owner, string id, string name, TimeSpan interval, Func<CancellationToken, Task> asyncAction)
        {
            _owner = owner;
            _id = id;
            _name = name;
            _interval = interval;
            _asyncAction = asyncAction;
        }

        public void SetEnabled(bool enabled) => _isEnabled = enabled;

        internal void Execute(long currentTicks, long intervalTicks)
        {
            if (!_isEnabled) return;

            if (Interlocked.CompareExchange(ref _isExecuting, 1, 0) != 0)
            {
                // Job overrun: previous invocation still executing
                _metrics.RecordRun(0, overrun: true);
                _nextTriggerTicks = currentTicks + intervalTicks;
                return;
            }

            long start = Stopwatch.GetTimestamp();
            try
            {
                if (_syncAction != null)
                {
                    _syncAction();
                    long elapsed = Stopwatch.GetTimestamp() - start;
                    long durationTicks = (elapsed * TimeSpan.TicksPerSecond) / Stopwatch.Frequency;
                    _metrics.RecordRun(durationTicks, overrun: false);
                }
                else if (_asyncAction != null)
                {
                    _ = Task.Run(async () =>
                    {
                        long aStart = Stopwatch.GetTimestamp();
                        try
                        {
                            await _asyncAction(CancellationToken.None).ConfigureAwait(false);
                        }
                        finally
                        {
                            long aElapsed = Stopwatch.GetTimestamp() - aStart;
                            long aTicks = (aElapsed * TimeSpan.TicksPerSecond) / Stopwatch.Frequency;
                            _metrics.RecordRun(aTicks, overrun: false);
                            Interlocked.Exchange(ref _isExecuting, 0);
                        }
                    });
                    return;
                }
            }
            finally
            {
                if (_syncAction != null)
                {
                    Interlocked.Exchange(ref _isExecuting, 0);
                }
                _nextTriggerTicks = currentTicks + intervalTicks;
            }
        }

        public void Dispose()
        {
            _owner.RemoveJob(_id);
        }
    }

    /// <summary>
    /// Deterministic multi-schedule coordinator for industrial automation and edge tasks.
    /// Integrates seamlessly with <see cref="ZeroRuntime"/> cycles or runs as an autonomous high-precision timer.
    /// </summary>
    public sealed class ZeroScheduler : IDisposable
    {
        private static readonly Lazy<ZeroScheduler> _shared =
            new Lazy<ZeroScheduler>(() => new ZeroScheduler("Shared", ZeroRuntime.Shared));

        public static ZeroScheduler Shared => _shared.Value;

        private readonly string _name;
        private readonly ZeroRuntime? _runtime;
        private readonly ConcurrentDictionary<string, ScheduledJob> _jobs = new ConcurrentDictionary<string, ScheduledJob>(StringComparer.OrdinalIgnoreCase);

        private Thread? _schedulerThread;
        private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(false);
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private bool _isRunning = false;
        private bool _disposed = false;
        private IDisposable? _runtimeSub;

        public string Name => _name;
        public bool IsRunning => _isRunning;
        public IReadOnlyCollection<ScheduledJob> Jobs => (IReadOnlyCollection<ScheduledJob>)_jobs.Values;

        public ZeroScheduler(string name = "Scheduler", ZeroRuntime? runtime = null)
        {
            _name = name;
            _runtime = runtime;
        }

        /// <summary>
        /// Schedules a recurring synchronous task at the given interval.
        /// </summary>
        public ScheduledJob ScheduleInterval(string id, TimeSpan interval, Action action, string? name = null)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(interval));

            var job = new ScheduledJob(this, id, name ?? id, interval, action);
            job.NextTriggerTicks = _stopwatch.ElapsedTicks + (long)(interval.TotalSeconds * Stopwatch.Frequency);

            _jobs[id] = job;
            return job;
        }

        /// <summary>
        /// Schedules a recurring asynchronous task at the given interval.
        /// </summary>
        public ScheduledJob ScheduleIntervalAsync(string id, TimeSpan interval, Func<CancellationToken, Task> asyncAction, string? name = null)
        {
            if (asyncAction == null) throw new ArgumentNullException(nameof(asyncAction));
            if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(interval));

            var job = new ScheduledJob(this, id, name ?? id, interval, asyncAction);
            job.NextTriggerTicks = _stopwatch.ElapsedTicks + (long)(interval.TotalSeconds * Stopwatch.Frequency);

            _jobs[id] = job;
            return job;
        }

        public bool TryGetJob(string id, out ScheduledJob? job)
        {
            return _jobs.TryGetValue(id, out job);
        }

        public bool RemoveJob(string id)
        {
            return _jobs.TryRemove(id, out _);
        }

        public void Clear()
        {
            _jobs.Clear();
        }

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ZeroScheduler));
            if (_isRunning) return;

            _isRunning = true;
            _stopSignal.Reset();
            _stopwatch.Restart();

            long now = _stopwatch.ElapsedTicks;
            foreach (var job in _jobs.Values)
            {
                job.NextTriggerTicks = now + (long)(job.Interval.TotalSeconds * Stopwatch.Frequency);
            }

            if (_runtime != null)
            {
                // Hook to runtime logic cycle (10ms)
                _runtimeSub = _runtime.Register(RuntimeCycle.Logic, (delta, count) => PollJobs());
                if (!_runtime.IsRunning)
                {
                    _runtime.Start();
                }
            }
            else
            {
                // Autonomous dedicated loop
                _schedulerThread = new Thread(SchedulerLoop)
                {
                    Name = $"ZeroScheduler_{_name}",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _schedulerThread.Start();
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            _runtimeSub?.Dispose();
            _runtimeSub = null;

            _stopSignal.Set();
            if (_schedulerThread != null && _schedulerThread.IsAlive)
            {
                _schedulerThread.Join(500);
                _schedulerThread = null;
            }
        }

        public void PollJobs()
        {
            long now = _stopwatch.ElapsedTicks;

            foreach (var kvp in _jobs)
            {
                var job = kvp.Value;
                if (!job.IsEnabled) continue;

                if (now >= job.NextTriggerTicks)
                {
                    long intervalTicks = (long)(job.Interval.TotalSeconds * Stopwatch.Frequency);
                    job.Execute(now, intervalTicks);
                }
            }
        }

        private void SchedulerLoop()
        {
            while (!_stopSignal.Wait(1))
            {
                PollJobs();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _stopSignal.Dispose();
        }
    }
}
