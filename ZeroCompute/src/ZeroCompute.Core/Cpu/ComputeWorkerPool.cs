using System;
using System.Diagnostics;
using System.Threading;
using ZeroPrimitives.Concurrency;

namespace ZeroCompute.Core.Cpu
{
    /// <summary>
    /// Dedicated, pinned CPU compute worker thread pool.
    /// Employs dynamic lock-free chunk stealing across heterogeneous cores,
    /// eliminating load imbalance between fast and slow cores.
    /// </summary>
    public sealed class ComputeWorkerPool : IDisposable
    {
        private static readonly Lazy<ComputeWorkerPool> _sharedInstance = new Lazy<ComputeWorkerPool>(
            () => new ComputeWorkerPool(Environment.ProcessorCount),
            LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Gets the singleton shared compute worker pool sized to available CPU cores.
        /// </summary>
        public static ComputeWorkerPool Shared => _sharedInstance.Value;

        private readonly int _workerCount;
        private readonly WorkerSlot[] _slots;
        private readonly FastSpinLock _dispatchLock = new FastSpinLock();

        [ThreadStatic]
        private static bool _isExecutingWorker;

        // Job broadcast state
        private int _currentJobId;
        private int _activeWorkers;
        private int _totalCount;
        private int _chunkSize;
        private int _currentChunkOffset;
        private Action<int, int>? _currentAction;
        private Action<int, int, int>? _currentIndexedAction;

        private int _remainingWorkers;
        private volatile bool _isDisposed;
        private Exception? _dispatchException;

        /// <summary>
        /// Total number of dedicated workers in the pool (including coordinator slot 0).
        /// </summary>
        public int WorkerCount => _workerCount;

        public ComputeWorkerPool(int workerCount)
        {
            if (workerCount <= 0)
                workerCount = Environment.ProcessorCount;

            _workerCount = Math.Max(1, workerCount);
            _slots = new WorkerSlot[_workerCount];

            // Slot 0 is coordinator
            _slots[0] = new WorkerSlot(0);

            // Slots 1..N-1 are dedicated background threads
            for (int i = 1; i < _workerCount; i++)
            {
                int workerIndex = i;
                var slot = new WorkerSlot(workerIndex);
                _slots[i] = slot;

                var thread = new Thread(() => WorkerLoop(slot))
                {
                    Name = $"ZeroCompute-Worker-{workerIndex}",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                slot.Thread = thread;
                thread.Start();
            }
        }

        /// <summary>
        /// Dispatches a range-based parallel workload across up to <paramref name="maxWorkers"/> workers.
        /// Uses dynamic lock-free chunk stealing to prevent slowest-core bottlenecks.
        /// </summary>
        public void DispatchRange(int totalCount, int maxWorkers, Action<int, int> rangeAction)
        {
            if (totalCount <= 0)
                return;
            if (rangeAction == null)
                throw new ArgumentNullException(nameof(rangeAction));

            if (_isExecutingWorker)
            {
                // Reentrant / nested parallel dispatch fallback to inline execution to avoid deadlock
                rangeAction(0, totalCount);
                return;
            }

            int activeWorkers = Math.Min(Math.Min(maxWorkers, _workerCount), totalCount);
            if (activeWorkers <= 1)
            {
                rangeAction(0, totalCount);
                return;
            }

            // Target 4 chunks per worker for dynamic load balancing
            int targetChunk = Math.Max(2048, totalCount / (activeWorkers * 4));

            using (_dispatchLock.EnterScope())
            {
                _isExecutingWorker = true;
                try
                {
                    _dispatchException = null;
                    _totalCount = totalCount;
                    _chunkSize = targetChunk;
                    _currentChunkOffset = 0;
                    _activeWorkers = activeWorkers;
                    _currentAction = rangeAction;
                    _currentIndexedAction = null;
                    _remainingWorkers = activeWorkers - 1;

                    int nextJob = _currentJobId + 1;
                    if (nextJob == 0) nextJob = 1;
                    Volatile.Write(ref _currentJobId, nextJob);

                    for (int w = 1; w < activeWorkers; w++)
                    {
                        _slots[w].Wake.Set();
                    }

                    // Coordinator participates in dynamic chunk execution
                    ExecuteWorkerChunks(rangeAction);

                    WaitForCompletion();

                    if (_dispatchException != null)
                    {
                        throw new AggregateException("An exception occurred during parallel compute execution.", _dispatchException);
                    }
                }
                finally
                {
                    _isExecutingWorker = false;
                }
            }
        }

        /// <summary>
        /// Dispatches a range-based parallel workload with worker index tracking (static balanced partitioning).
        /// </summary>
        public void DispatchRangeIndexed(int totalCount, int maxWorkers, Action<int, int, int> rangeAction)
        {
            if (totalCount <= 0)
                return;
            if (rangeAction == null)
                throw new ArgumentNullException(nameof(rangeAction));

            if (_isExecutingWorker)
            {
                rangeAction(0, totalCount, 0);
                return;
            }

            int activeWorkers = Math.Min(Math.Min(maxWorkers, _workerCount), totalCount);
            if (activeWorkers <= 1)
            {
                rangeAction(0, totalCount, 0);
                return;
            }

            int chunkSize = (totalCount + activeWorkers - 1) / activeWorkers;

            using (_dispatchLock.EnterScope())
            {
                _isExecutingWorker = true;
                try
                {
                    _dispatchException = null;
                    _totalCount = totalCount;
                    _chunkSize = chunkSize;
                    _activeWorkers = activeWorkers;
                    _currentAction = null;
                    _currentIndexedAction = rangeAction;
                    _remainingWorkers = activeWorkers - 1;

                    int nextJob = _currentJobId + 1;
                    if (nextJob == 0) nextJob = 1;
                    Volatile.Write(ref _currentJobId, nextJob);

                    for (int w = 1; w < activeWorkers; w++)
                    {
                        _slots[w].Wake.Set();
                    }

                    int coordEnd = Math.Min(chunkSize, totalCount);
                    try
                    {
                        rangeAction(0, coordEnd, 0);
                    }
                    catch (Exception ex)
                    {
                        _dispatchException = ex;
                    }

                    WaitForCompletion();

                    if (_dispatchException != null)
                    {
                        throw new AggregateException("An exception occurred during parallel compute execution.", _dispatchException);
                    }
                }
                finally
                {
                    _isExecutingWorker = false;
                }
            }
        }

        private void ExecuteWorkerChunks(Action<int, int> action)
        {
            int chunkSize = _chunkSize;
            int total = _totalCount;

            while (true)
            {
                int start = Interlocked.Add(ref _currentChunkOffset, chunkSize) - chunkSize;
                if (start >= total)
                    break;

                int end = Math.Min(start + chunkSize, total);
                try
                {
                    action(start, end);
                }
                catch (Exception ex)
                {
                    _dispatchException = ex;
                }
            }
        }

        private void WaitForCompletion()
        {
            var spinner = new SpinWait();
            while (Volatile.Read(ref _remainingWorkers) > 0)
            {
                spinner.SpinOnce();
                if (spinner.Count > 20)
                {
                    Thread.Yield();
                }
                else if (spinner.Count > 100)
                {
                    Thread.Sleep(0);
                }
            }
        }

        private void WorkerLoop(WorkerSlot slot)
        {
            // Pin worker strictly to designated core and set high scheduling priority
            CpuAffinity.PinCurrentThread(slot.WorkerIndex);
            CpuAffinity.SetHighPriority();

            while (!_isDisposed)
            {
                slot.Wake.WaitOne();
                if (_isDisposed)
                    break;

                int workerIndex = slot.WorkerIndex;
                if (workerIndex < _activeWorkers)
                {
                    _isExecutingWorker = true;
                    try
                    {
                        var action = _currentAction;
                        var idxAction = _currentIndexedAction;

                        if (action != null)
                        {
                            ExecuteWorkerChunks(action);
                        }
                        else if (idxAction != null)
                        {
                            int start = workerIndex * _chunkSize;
                            int end = Math.Min(start + _chunkSize, _totalCount);
                            if (start < end)
                            {
                                try
                                {
                                    idxAction(start, end, workerIndex);
                                }
                                catch (Exception ex)
                                {
                                    _dispatchException = ex;
                                }
                            }
                        }
                    }
                    finally
                    {
                        _isExecutingWorker = false;
                    }

                    Interlocked.Decrement(ref _remainingWorkers);
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            for (int i = 1; i < _slots.Length; i++)
            {
                _slots[i].Wake.Set();
            }

            for (int i = 1; i < _slots.Length; i++)
            {
                _slots[i].Thread?.Join(50);
                _slots[i].Wake.Dispose();
            }
        }

        #region Nested WorkerSlot

        private sealed class WorkerSlot
        {
            public readonly int WorkerIndex;
            public readonly AutoResetEvent Wake = new AutoResetEvent(false);
            public Thread? Thread;

            public WorkerSlot(int workerIndex)
            {
                WorkerIndex = workerIndex;
            }
        }

        #endregion
    }
}
