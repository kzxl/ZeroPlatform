using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Graph;

namespace ZeroPipeline.Core.Execution
{
    /// <summary>
    /// Thread-safe double-buffered pipeline runner supporting dynamic graph and recipe hot-reloading at runtime.
    /// Safely swaps DAG topologies and node parameters on live streams at cycle boundaries with zero packet loss.
    /// </summary>
    public sealed class HotReloadPipelineRunner
    {
        private PipelineGraph _activeGraph;
        private PipelineExecutor _activeExecutor;

        private PipelineGraph? _pendingGraph;
        private PipelineExecutor? _pendingExecutor;

        private readonly object _swapLock = new object();
        private readonly Stopwatch _cycleStopwatch = new Stopwatch();

        private bool _isRunning;
        private long _currentCycle;
        private int _reloadCount;

        /// <summary>
        /// Currently active pipeline graph DAG topology.
        /// </summary>
        public PipelineGraph ActiveGraph
        {
            get
            {
                lock (_swapLock) return _activeGraph;
            }
        }

        /// <summary>
        /// Executor driving the active pipeline graph.
        /// </summary>
        public PipelineExecutor ActiveExecutor
        {
            get
            {
                lock (_swapLock) return _activeExecutor;
            }
        }

        /// <summary>
        /// Indicates whether continuous streaming execution is active.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Total completed execution cycle counter.
        /// </summary>
        public long CurrentCycle => _currentCycle;

        /// <summary>
        /// Number of successful runtime graph hot-reloads completed.
        /// </summary>
        public int ReloadCount => _reloadCount;

        /// <summary>
        /// Triggered when an active pipeline graph is successfully swapped for a new graph at a cycle boundary.
        /// </summary>
        public event Action<PipelineGraph, PipelineGraph>? GraphSwapped;

        /// <summary>
        /// Triggered when a requested hot-reload fails validation or initialization.
        /// </summary>
        public event Action<Exception>? ReloadFailed;

        /// <summary>
        /// Triggered upon completion of each pipeline execution cycle.
        /// </summary>
        public event Action<long, double>? CycleCompleted;

        public HotReloadPipelineRunner(PipelineGraph initialGraph)
        {
            _activeGraph = initialGraph ?? throw new ArgumentNullException(nameof(initialGraph));
            _activeExecutor = new PipelineExecutor(_activeGraph);
        }

        /// <summary>
        /// Prepares and initializes the initial active pipeline graph.
        /// </summary>
        public async Task InitializeAsync(PipelineContext? context = null, CancellationToken cancellationToken = default)
        {
            await _activeExecutor.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Requests a runtime hot-reload of the active pipeline topology.
        /// The new graph is validated and pre-initialized before being atomically swapped at the next cycle boundary.
        /// </summary>
        public async Task ReloadAsync(
            PipelineGraph newGraph,
            bool preInitialize = true,
            CancellationToken cancellationToken = default)
        {
            if (newGraph == null) throw new ArgumentNullException(nameof(newGraph));

            try
            {
                // 1. Validate DAG acyclic invariants
                newGraph.Validate();

                // 2. Pre-initialize new executor ahead of swap
                var newExecutor = new PipelineExecutor(newGraph);
                if (preInitialize)
                {
                    var ctx = new PipelineContext(cancellationToken);
                    await newExecutor.InitializeAsync(ctx, cancellationToken).ConfigureAwait(false);
                }

                // 3. Stage for atomic swap
                lock (_swapLock)
                {
                    _pendingGraph = newGraph;
                    _pendingExecutor = newExecutor;

                    // If not running in a continuous streaming loop, perform swap immediately
                    if (!_isRunning)
                    {
                        ApplyPendingSwapUnsafe();
                    }
                }
            }
            catch (Exception ex)
            {
                ReloadFailed?.Invoke(ex);
                throw;
            }
        }

        /// <summary>
        /// Executes a single discrete cycle, atomically applying any pending hot-reload swaps at the cycle boundary.
        /// </summary>
        public async Task ExecuteSingleCycleAsync(PipelineContext? context = null, CancellationToken cancellationToken = default)
        {
            PipelineExecutor executor;
            lock (_swapLock)
            {
                CheckAndApplyPendingSwapUnsafe();
                executor = _activeExecutor;
            }

            _cycleStopwatch.Restart();
            await executor.ExecuteStepAsync(context, cancellationToken).ConfigureAwait(false);
            _cycleStopwatch.Stop();

            long cycle = Interlocked.Increment(ref _currentCycle);
            CycleCompleted?.Invoke(cycle, _cycleStopwatch.Elapsed.TotalMilliseconds);

            lock (_swapLock)
            {
                CheckAndApplyPendingSwapUnsafe();
            }
        }

        /// <summary>
        /// Runs a continuous streaming loop on the active pipeline until cancellation is requested.
        /// </summary>
        public async Task StartAsync(
            PipelineContext? context = null,
            int intervalMs = 0,
            CancellationToken cancellationToken = default)
        {
            var ctx = context ?? new PipelineContext(cancellationToken);

            lock (_swapLock)
            {
                if (!_activeExecutor.IsInitialized)
                {
                    _activeExecutor.InitializeAsync(ctx, cancellationToken).GetAwaiter().GetResult();
                }
                _isRunning = true;
            }

            try
            {
                while (!cancellationToken.IsCancellationRequested && _isRunning)
                {
                    await ExecuteSingleCycleAsync(ctx, cancellationToken).ConfigureAwait(false);

                    if (intervalMs > 0)
                    {
                        await Task.Delay(intervalMs, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// Signals the continuous streaming runner loop to stop.
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
        }

        /// <summary>
        /// Resets the active pipeline graph, clearing buffered queues.
        /// </summary>
        public async Task ResetAsync()
        {
            lock (_swapLock)
            {
                _isRunning = false;
                _pendingGraph = null;
                _pendingExecutor = null;
            }

            await _activeExecutor.ResetAsync().ConfigureAwait(false);
        }

        private void CheckAndApplyPendingSwapUnsafe()
        {
            if (_pendingGraph != null && _pendingExecutor != null)
            {
                ApplyPendingSwapUnsafe();
            }
        }

        private void ApplyPendingSwapUnsafe()
        {
            if (_pendingGraph == null || _pendingExecutor == null) return;

            var oldGraph = _activeGraph;
            _activeGraph = _pendingGraph;
            _activeExecutor = _pendingExecutor;

            _pendingGraph = null;
            _pendingExecutor = null;
            _reloadCount++;

            GraphSwapped?.Invoke(oldGraph, _activeGraph);
        }
    }
}
