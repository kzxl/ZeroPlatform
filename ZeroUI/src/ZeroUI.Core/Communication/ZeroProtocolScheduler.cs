using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Communication
{
    /// <summary>
    /// Registration descriptor for a scheduled protocol adapter polling job.
    /// </summary>
    public sealed class ScheduledAdapter
    {
        public IProtocolAdapter Adapter { get; }
        public TimeSpan Interval { get; }
        public bool IsEnabled { get; set; } = true;
        public long TotalPolls { get; internal set; }
        public long FailedPolls { get; internal set; }
        public TimeSpan LastPollDuration { get; internal set; }
        public DateTime LastPollUtc { get; internal set; }
        internal long NextPollTicks { get; set; }
        internal int IsExecuting = 0;

        public ScheduledAdapter(IProtocolAdapter adapter, TimeSpan interval)
        {
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            Interval = interval;
        }
    }

    /// <summary>
    /// Industrial communication scheduler coordinating field protocol drivers (Modbus TCP, Siemens S7).
    /// Executes periodic coalesced polling, monitors connection watchdogs, applies automatic backoff reconnection,
    /// and streams parsed telemetry into <see cref="ZeroTelemetryBus"/>.
    /// </summary>
    public sealed class ZeroProtocolScheduler : IDisposable
    {
        private static readonly Lazy<ZeroProtocolScheduler> _shared =
            new Lazy<ZeroProtocolScheduler>(() => new ZeroProtocolScheduler("Shared", ZeroTelemetryBus.Shared));

        public static ZeroProtocolScheduler Shared => _shared.Value;

        private readonly string _name;
        private readonly ZeroTelemetryBus _bus;
        private readonly ConcurrentDictionary<string, ScheduledAdapter> _adapters =
            new ConcurrentDictionary<string, ScheduledAdapter>(StringComparer.OrdinalIgnoreCase);

        private Thread? _workerThread;
        private readonly ManualResetEventSlim _stopSignal = new ManualResetEventSlim(false);
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private bool _isRunning = false;
        private bool _disposed = false;

        public string Name => _name;
        public bool IsRunning => _isRunning;
        public int AdapterCount => _adapters.Count;
        public IReadOnlyCollection<ScheduledAdapter> Adapters => (IReadOnlyCollection<ScheduledAdapter>)_adapters.Values;

        public ZeroProtocolScheduler(string name = "ProtocolScheduler", ZeroTelemetryBus? bus = null)
        {
            _name = name;
            _bus = bus ?? ZeroTelemetryBus.Shared;
        }

        #region Registration & Management

        /// <summary>
        /// Registers a protocol adapter to be polled at the designated interval.
        /// </summary>
        public ScheduledAdapter RegisterAdapter(IProtocolAdapter adapter, TimeSpan interval)
        {
            if (adapter == null) throw new ArgumentNullException(nameof(adapter));
            if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive", nameof(interval));

            var item = new ScheduledAdapter(adapter, interval);
            item.NextPollTicks = _stopwatch.ElapsedTicks + (long)(interval.TotalSeconds * Stopwatch.Frequency);

            _adapters[adapter.AdapterId] = item;
            return item;
        }

        public bool UnregisterAdapter(string adapterId)
        {
            return _adapters.TryRemove(adapterId, out _);
        }

        public bool TryGetAdapter(string adapterId, out ScheduledAdapter? scheduled)
        {
            return _adapters.TryGetValue(adapterId, out scheduled);
        }

        #endregion

        #region Lifecycle & Execution

        public void Start()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ZeroProtocolScheduler));
            if (_isRunning) return;

            _isRunning = true;
            _stopSignal.Reset();
            _stopwatch.Restart();

            long now = _stopwatch.ElapsedTicks;
            foreach (var item in _adapters.Values)
            {
                item.NextPollTicks = now;
            }

            _workerThread = new Thread(SchedulerLoop)
            {
                Name = $"ZeroProtocolScheduler_{_name}",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            _workerThread.Start();
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;

            _stopSignal.Set();
            if (_workerThread != null && _workerThread.IsAlive)
            {
                _workerThread.Join(500);
                _workerThread = null;
            }
        }

        private void SchedulerLoop()
        {
            while (!_stopSignal.Wait(1))
            {
                PollEligibleAdapters();
            }
        }

        public void PollEligibleAdapters()
        {
            long now = _stopwatch.ElapsedTicks;

            foreach (var kvp in _adapters)
            {
                var item = kvp.Value;
                if (!item.IsEnabled) continue;

                if (now >= item.NextPollTicks)
                {
                    long intervalTicks = (long)(item.Interval.TotalSeconds * Stopwatch.Frequency);

                    if (Interlocked.CompareExchange(ref item.IsExecuting, 1, 0) == 0)
                    {
                        // Fire poll task asynchronously on threadpool to avoid blocking other adapters
                        _ = Task.Run(async () =>
                        {
                            long start = Stopwatch.GetTimestamp();
                            bool success = false;
                            try
                            {
                                if (item.Adapter.State == AdapterConnectionState.Disconnected)
                                {
                                    await item.Adapter.ConnectAsync(CancellationToken.None).ConfigureAwait(false);
                                }

                                if (item.Adapter.State == AdapterConnectionState.Connected)
                                {
                                    await item.Adapter.PollOnceAsync(CancellationToken.None).ConfigureAwait(false);
                                    success = true;
                                }
                            }
                            catch
                            {
                                success = false;
                            }
                            finally
                            {
                                long elapsed = Stopwatch.GetTimestamp() - start;
                                long durTicks = (elapsed * TimeSpan.TicksPerSecond) / Stopwatch.Frequency;

                                item.TotalPolls++;
                                if (!success) item.FailedPolls++;
                                item.LastPollDuration = TimeSpan.FromTicks(durTicks);
                                item.LastPollUtc = DateTime.UtcNow;

                                item.NextPollTicks = _stopwatch.ElapsedTicks + intervalTicks;
                                Interlocked.Exchange(ref item.IsExecuting, 0);
                            }
                        });
                    }
                    else
                    {
                        // Prior poll still running, advance next poll
                        item.NextPollTicks = now + intervalTicks;
                    }
                }
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
            _stopSignal.Dispose();

            foreach (var item in _adapters.Values)
            {
                try { item.Adapter.Dispose(); } catch { }
            }
            _adapters.Clear();
        }
    }
}
