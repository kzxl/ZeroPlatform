using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroPlatform.Concurrency
{
    /// <summary>
    /// Represents a zero-allocation, awaitable asynchronous manual-reset event.
    /// Once set, all current and future awaiters complete immediately until <see cref="Reset"/> is called.
    /// </summary>
    public sealed class AsyncManualResetEvent
    {
        private readonly object _syncLock = new();
        private readonly ConcurrentQueue<ZeroPromise> _waiters = new();
        private int _state; // 1 = signaled, 0 = unsignaled

        /// <summary>
        /// Gets whether the event is currently in a signaled state.
        /// </summary>
        public bool IsSet => Volatile.Read(ref _state) == 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncManualResetEvent"/> class.
        /// </summary>
        /// <param name="initialState">True if the initial state is signaled; false otherwise.</param>
        public AsyncManualResetEvent(bool initialState = false)
        {
            _state = initialState ? 1 : 0;
        }

        /// <summary>
        /// Asynchronously waits for the event to be signaled.
        /// Returns a zero-allocation <see cref="ValueTask"/> if already signaled.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask WaitAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return new ValueTask(Task.FromCanceled(cancellationToken));

            if (Volatile.Read(ref _state) == 1)
                return default; // Fast-path: already signaled, 0 allocation

            lock (_syncLock)
            {
                if (Volatile.Read(ref _state) == 1)
                    return default;

                var promise = ZeroPromisePool.Rent();
                _waiters.Enqueue(promise);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(static s =>
                    {
                        var p = (ZeroPromise)s!;
                        p.TrySetException(new OperationCanceledException());
                    }, promise);
                }

                return promise.Task;
            }
        }

        /// <summary>
        /// Sets the state of the event to signaled, allowing one or more awaiters to proceed.
        /// </summary>
        public void Set()
        {
            if (Interlocked.Exchange(ref _state, 1) == 1)
                return; // Already set

            lock (_syncLock)
            {
                while (_waiters.TryDequeue(out var promise))
                {
                    promise.TrySetResult();
                }
            }
        }

        /// <summary>
        /// Sets the state of the event to unsignaled, causing awaiters to suspend.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref _state, 0);
        }
    }

    /// <summary>
    /// Represents a zero-allocation, awaitable asynchronous auto-reset event.
    /// When signaled, exactly one awaiting caller is unblocked and the event automatically resets to unsignaled.
    /// </summary>
    public sealed class AsyncAutoResetEvent
    {
        private readonly object _syncLock = new();
        private readonly ConcurrentQueue<ZeroPromise> _waiters = new();
        private int _state; // 1 = signaled, 0 = unsignaled

        /// <summary>
        /// Gets whether the event is currently in a signaled state.
        /// </summary>
        public bool IsSet => Volatile.Read(ref _state) == 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> class.
        /// </summary>
        /// <param name="initialState">True if the initial state is signaled; false otherwise.</param>
        public AsyncAutoResetEvent(bool initialState = false)
        {
            _state = initialState ? 1 : 0;
        }

        /// <summary>
        /// Asynchronously waits for the event to be signaled.
        /// Automatically transitions back to unsignaled once an awaiter unblocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask WaitAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return new ValueTask(Task.FromCanceled(cancellationToken));

            lock (_syncLock)
            {
                if (Volatile.Read(ref _state) == 1)
                {
                    Volatile.Write(ref _state, 0); // Consume signal immediately
                    return default;
                }

                var promise = ZeroPromisePool.Rent();
                _waiters.Enqueue(promise);

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationToken.Register(static s =>
                    {
                        var p = (ZeroPromise)s!;
                        p.TrySetException(new OperationCanceledException());
                    }, promise);
                }

                return promise.Task;
            }
        }

        /// <summary>
        /// Sets the state of the event to signaled.
        /// If an awaiter is queued, it is unblocked immediately and the event remains unsignaled.
        /// </summary>
        public void Set()
        {
            lock (_syncLock)
            {
                while (_waiters.TryDequeue(out var promise))
                {
                    if (promise.TrySetResult())
                    {
                        return; // Unblocked one waiter, keep state at 0
                    }
                }

                // No waiter was unblocked, store the signal
                Volatile.Write(ref _state, 1);
            }
        }

        /// <summary>
        /// Sets the state of the event to unsignaled without releasing any awaiters.
        /// </summary>
        public void Reset()
        {
            lock (_syncLock)
            {
                Volatile.Write(ref _state, 0);
            }
        }
    }
}
