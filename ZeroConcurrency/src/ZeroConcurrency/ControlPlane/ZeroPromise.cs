using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ZeroPlatform.Concurrency
{
    /// <summary>
    /// Reusable <see cref="IValueTaskSource{T}"/> implementation that eliminates GC allocations for asynchronous completions.
    /// Equipped with thread-safe single-transition guards (<see cref="TrySetResult"/>, <see cref="TrySetException"/>)
    /// to safely handle concurrent completions, cancellations, and race conditions.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    public sealed class ZeroPromise<T> : IValueTaskSource<T>, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<T> _core;
        private readonly Action<ZeroPromise<T>>? _returnToPool;
        private int _completedState; // 0 = pending, 1 = completed

        internal ZeroPromise(Action<ZeroPromise<T>>? returnToPool = null)
        {
            _returnToPool = returnToPool;
            _core.RunContinuationsAsynchronously = true; // Prevent stack overflows on long continuation chains
            _completedState = 0;
        }

        /// <summary>
        /// Gets a <see cref="ValueTask{T}"/> wrapping this promise.
        /// </summary>
        public ValueTask<T> Task => new ValueTask<T>(this, _core.Version);

        /// <summary>
        /// Transitions the promise to a completed state with the specified result.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(T result)
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetResult(result);
            }
        }

        /// <summary>
        /// Attempts to transition the promise to a completed state. Returns true if this call completed it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetResult(T result)
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetResult(result);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Transitions the promise to a faulted state with the specified exception.
        /// </summary>
        public void SetException(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetException(exception);
            }
        }

        /// <summary>
        /// Attempts to transition the promise to a faulted state. Returns true if this call faulted it.
        /// </summary>
        public bool TrySetException(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetException(exception);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the current status of the operation.
        /// </summary>
        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        /// <summary>
        /// Schedules the continuation action that will be invoked when the operation completes.
        /// </summary>
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _core.OnCompleted(continuation, state, token, flags);
        }

        /// <summary>
        /// Optional cancellation registration automatically disposed when the promise completes.
        /// </summary>
        public CancellationTokenRegistration Registration;

        /// <summary>
        /// Retrieves the result and recycles the instance back to the object pool.
        /// </summary>
        public T GetResult(short token)
        {
            try
            {
                return _core.GetResult(token);
            }
            finally
            {
                Registration.Dispose();
                Registration = default;
                _completedState = 0;
                _core.Reset();
                _returnToPool?.Invoke(this);
            }
        }

        void IValueTaskSource.GetResult(short token) => GetResult(token);
    }

    /// <summary>
    /// Reusable <see cref="IValueTaskSource"/> implementation for non-generic (void) asynchronous operations.
    /// </summary>
    public sealed class ZeroPromise : IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> _core;
        private readonly Action<ZeroPromise>? _returnToPool;
        private int _completedState;

        internal ZeroPromise(Action<ZeroPromise>? returnToPool = null)
        {
            _returnToPool = returnToPool;
            _core.RunContinuationsAsynchronously = true;
            _completedState = 0;
        }

        /// <summary>
        /// Gets a <see cref="ValueTask"/> wrapping this promise.
        /// </summary>
        public ValueTask Task => new ValueTask(this, _core.Version);

        /// <summary>
        /// Transitions the promise to a completed state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult()
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetResult(true);
            }
        }

        /// <summary>
        /// Attempts to transition the promise to a completed state.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrySetResult()
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetResult(true);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Transitions the promise to a faulted state with an exception.
        /// </summary>
        public void SetException(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetException(exception);
            }
        }

        /// <summary>
        /// Attempts to transition the promise to a faulted state.
        /// </summary>
        public bool TrySetException(Exception exception)
        {
            if (Interlocked.CompareExchange(ref _completedState, 1, 0) == 0)
            {
                _core.SetException(exception);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the current status of the operation.
        /// </summary>
        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        /// <summary>
        /// Schedules the continuation action.
        /// </summary>
        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _core.OnCompleted(continuation, state, token, flags);
        }

        /// <summary>
        /// Optional cancellation registration automatically disposed when the promise completes.
        /// </summary>
        public CancellationTokenRegistration Registration;

        /// <summary>
        /// Retrieves the result and recycles the instance back to the object pool.
        /// </summary>
        public void GetResult(short token)
        {
            try
            {
                _core.GetResult(token);
            }
            finally
            {
                Registration.Dispose();
                Registration = default;
                _completedState = 0;
                _core.Reset();
                _returnToPool?.Invoke(this);
            }
        }
    }

    /// <summary>
    /// Lock-free pool managing recyclable <see cref="ZeroPromise{T}"/> instances to eliminate GC allocations.
    /// Equipped with a single-slot fast path (sub-3ns latency) and a concurrent queue fallback.
    /// </summary>
    public static class ZeroPromisePool<T>
    {
        private static ZeroPromise<T>? s_fastItem;
        private static readonly ConcurrentQueue<ZeroPromise<T>> s_pool = new ConcurrentQueue<ZeroPromise<T>>();

        /// <summary>
        /// Rents a reusable promise from the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZeroPromise<T> Rent()
        {
            var fast = Interlocked.Exchange(ref s_fastItem, null);
            if (fast != null)
            {
                return fast;
            }

            if (s_pool.TryDequeue(out var promise))
            {
                return promise;
            }

            return new ZeroPromise<T>(Return);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Return(ZeroPromise<T> promise)
        {
            if (Interlocked.CompareExchange(ref s_fastItem, promise, null) != null)
            {
                s_pool.Enqueue(promise);
            }
        }
    }

    /// <summary>
    /// Lock-free pool managing recyclable non-generic <see cref="ZeroPromise"/> instances.
    /// </summary>
    public static class ZeroPromisePool
    {
        private static ZeroPromise? s_fastItem;
        private static readonly ConcurrentQueue<ZeroPromise> s_pool = new ConcurrentQueue<ZeroPromise>();

        /// <summary>
        /// Rents a reusable non-generic promise from the pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZeroPromise Rent()
        {
            var fast = Interlocked.Exchange(ref s_fastItem, null);
            if (fast != null)
            {
                return fast;
            }

            if (s_pool.TryDequeue(out var promise))
            {
                return promise;
            }

            return new ZeroPromise(Return);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Return(ZeroPromise promise)
        {
            if (Interlocked.CompareExchange(ref s_fastItem, promise, null) != null)
            {
                s_pool.Enqueue(promise);
            }
        }
    }
}
