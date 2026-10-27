using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPlatform.Concurrency
{
    /// <summary>
    /// Represents a high-throughput, low-latency work-stealing thread pool.
    /// Each worker thread owns a dedicated lock-free queue and attempts to steal work
    /// from peer workers when idle, eliminating thread contention on a single shared queue.
    /// </summary>
    public sealed class ZeroWorkStealingPool : IDisposable
    {
        private readonly int _workerCount;
        private readonly Thread[] _workers;
        private readonly ConcurrentQueue<Action>[] _queues;
        private readonly AutoResetEvent[] _waitHandles;
        private readonly CancellationTokenSource _cts = new();
        private int _roundRobinIndex;
        private int _disposed;

        /// <summary>
        /// Gets the number of worker threads managed by the pool.
        /// </summary>
        public int WorkerCount => _workerCount;

        /// <summary>
        /// Gets whether the pool has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZeroWorkStealingPool"/> class.
        /// </summary>
        /// <param name="workerCount">Number of dedicated worker threads (defaults to logical processor count).</param>
        /// <param name="threadNamePrefix">Optional name prefix for worker threads.</param>
        public ZeroWorkStealingPool(int workerCount = 0, string threadNamePrefix = "ZeroWorker")
        {
            _workerCount = workerCount > 0 ? workerCount : Math.Max(1, Environment.ProcessorCount);
            _workers = new Thread[_workerCount];
            _queues = new ConcurrentQueue<Action>[_workerCount];
            _waitHandles = new AutoResetEvent[_workerCount];

            for (int i = 0; i < _workerCount; i++)
            {
                _queues[i] = new ConcurrentQueue<Action>();
                _waitHandles[i] = new AutoResetEvent(false);
            }

            for (int i = 0; i < _workerCount; i++)
            {
                int workerIndex = i;
                _workers[i] = new Thread(() => WorkerLoop(workerIndex))
                {
                    Name = $"{threadNamePrefix}-{workerIndex}",
                    IsBackground = true,
                    Priority = ThreadPriority.AboveNormal
                };
                _workers[i].Start();
            }
        }

        /// <summary>
        /// Dispatches a work item to the pool with minimal scheduling overhead.
        /// </summary>
        /// <param name="action">The delegate to execute.</param>
        public void Post(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ThrowIfDisposed();

            int targetIndex = (int)((uint)Interlocked.Increment(ref _roundRobinIndex) % (uint)_workerCount);
            _queues[targetIndex].Enqueue(action);
            _waitHandles[targetIndex].Set();
        }

        /// <summary>
        /// Dispatches a stateful work item to the pool without allocating closure objects or delegates.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public void Post<TState>(Action<TState> action, TState state)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ThrowIfDisposed();

            var workItem = PooledStateWorkItem<TState>.Rent(action, state);
            int targetIndex = (int)((uint)Interlocked.Increment(ref _roundRobinIndex) % (uint)_workerCount);
            _queues[targetIndex].Enqueue(workItem.InvokeAction);
            _waitHandles[targetIndex].Set();
        }

        /// <summary>
        /// Executes a work item asynchronously, returning a zero-allocation awaitable <see cref="ValueTask"/>.
        /// </summary>
        public ValueTask ExecuteAsync(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            ThrowIfDisposed();

            var promise = ZeroPromisePool.Rent();
            Post(static s =>
            {
                try
                {
                    s.action();
                    s.promise.TrySetResult();
                }
                catch (Exception ex)
                {
                    s.promise.TrySetException(ex);
                }
            }, (action, promise));

            return promise.Task;
        }

        /// <summary>
        /// Executes a function asynchronously and returns its result via a pooled <see cref="ValueTask{T}"/>.
        /// </summary>
        public ValueTask<T> ExecuteAsync<T>(Func<T> function)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            ThrowIfDisposed();

            var promise = ZeroPromisePool<T>.Rent();
            Post(static s =>
            {
                try
                {
                    T result = s.function();
                    s.promise.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    s.promise.TrySetException(ex);
                }
            }, (function, promise));

            return promise.Task;
        }

        internal interface IWorkItem
        {
            void Execute();
        }

        private sealed class PooledStateWorkItem<TState> : IWorkItem, IZeroWorkItem
        {
            private const int MaxPoolSize = 1024;
            private static PooledStateWorkItem<TState>? s_fastItem;
            private static readonly PooledStateWorkItem<TState>?[] s_pool = new PooledStateWorkItem<TState>?[MaxPoolSize];
            private static int s_head;
            private static int s_lock;

            public Action<TState>? Action;
            public TState? State;
            public readonly Action InvokeAction;

            private PooledStateWorkItem()
            {
                InvokeAction = Execute;
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            public static PooledStateWorkItem<TState> Rent(Action<TState> action, TState state)
            {
                var item = Interlocked.Exchange(ref s_fastItem, null);
                if (item == null && !TryPopPool(out item))
                {
                    item = new PooledStateWorkItem<TState>();
                }

                item!.Action = action;
                item.State = state;
                return item;
            }

            public void Execute()
            {
                var action = Action;
                var state = State!;
                Action = null;
                State = default;

                Return(this);
                action?.Invoke(state);
            }

            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
            private static void Return(PooledStateWorkItem<TState> item)
            {
                if (Interlocked.CompareExchange(ref s_fastItem, item, null) != null)
                {
                    TryPushPool(item);
                }
            }

            private static bool TryPopPool(out PooledStateWorkItem<TState>? item)
            {
                if (Volatile.Read(ref s_head) == 0)
                {
                    item = null;
                    return false;
                }

                var spinner = new SpinWait();
                while (Interlocked.CompareExchange(ref s_lock, 1, 0) != 0)
                {
                    spinner.SpinOnce();
                }

                int head = s_head - 1;
                if (head >= 0)
                {
                    item = s_pool[head];
                    s_pool[head] = null;
                    s_head = head;
                    Volatile.Write(ref s_lock, 0);
                    return true;
                }

                Volatile.Write(ref s_lock, 0);
                item = null;
                return false;
            }

            private static void TryPushPool(PooledStateWorkItem<TState> item)
            {
                var spinner = new SpinWait();
                while (Interlocked.CompareExchange(ref s_lock, 1, 0) != 0)
                {
                    spinner.SpinOnce();
                }

                int head = s_head;
                if ((uint)head < (uint)MaxPoolSize)
                {
                    s_pool[head] = item;
                    s_head = head + 1;
                }

                Volatile.Write(ref s_lock, 0);
            }
        }

        private void WorkerLoop(int workerIndex)
        {
            var myQueue = _queues[workerIndex];
            var myWaitHandle = _waitHandles[workerIndex];
            var token = _cts.Token;

            while (!token.IsCancellationRequested)
            {
                // 1. Process own queue
                if (myQueue.TryDequeue(out var action))
                {
                    ExecuteActionSafely(action);
                    continue;
                }

                // 2. Work Stealing: try stealing from peer workers
                bool stole = false;
                for (int offset = 1; offset < _workerCount; offset++)
                {
                    int peerIndex = (workerIndex + offset) % _workerCount;
                    if (_queues[peerIndex].TryDequeue(out action))
                    {
                        ExecuteActionSafely(action);
                        stole = true;
                        break;
                    }
                }

                if (stole)
                    continue;

                // 3. No work available: sleep until signaled
                myWaitHandle.WaitOne(10);
            }

            // Drain remaining items on shutdown
            while (myQueue.TryDequeue(out var action))
            {
                ExecuteActionSafely(action);
            }
        }

        private static void ExecuteActionSafely(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // Isolate unhandled exceptions from crashing the worker thread
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(ZeroWorkStealingPool));
        }

        /// <summary>
        /// Shuts down all worker threads gracefully.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            _cts.Cancel();

            // Wake up all workers
            for (int i = 0; i < _workerCount; i++)
            {
                _waitHandles[i].Set();
            }

            for (int i = 0; i < _workerCount; i++)
            {
                _workers[i].Join(500);
                _waitHandles[i].Dispose();
            }

            _cts.Dispose();
        }
    }
}
