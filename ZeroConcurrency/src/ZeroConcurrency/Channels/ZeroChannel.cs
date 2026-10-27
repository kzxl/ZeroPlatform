using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace ZeroPlatform.Concurrency
{
    /// <summary>
    /// Exception thrown when attempting to write to or read from a closed <see cref="ZeroChannel{T}"/>.
    /// </summary>
    public sealed class ZeroChannelClosedException : InvalidOperationException
    {
        public ZeroChannelClosedException(string message) : base(message) { }
        public ZeroChannelClosedException(string message, Exception? innerException) : base(message, innerException) { }
    }

    // Recyclable promise holding an item for direct-handoff backpressure from waiting writers.
    internal sealed class ZeroWriterPromise<T> : IValueTaskSource<bool>, IValueTaskSource
    {
        public T Item = default!;
        public CancellationTokenRegistration Registration;
        private ManualResetValueTaskSourceCore<bool> _core;
        private readonly Action<ZeroWriterPromise<T>> _returnToPool;

        public ZeroWriterPromise(Action<ZeroWriterPromise<T>> returnToPool)
        {
            _returnToPool = returnToPool;
            _core.RunContinuationsAsynchronously = true;
        }

        public ValueTask Task => new ValueTask(this, _core.Version);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetResult(bool result) => _core.SetResult(result);

        public void SetException(Exception exception) => _core.SetException(exception);

        public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);

        public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => _core.OnCompleted(continuation, state, token, flags);

        public bool GetResult(short token)
        {
            try
            {
                return _core.GetResult(token);
            }
            finally
            {
                Registration.Dispose();
                Registration = default;
                Item = default!;
                _core.Reset();
                _returnToPool(this);
            }
        }

        void IValueTaskSource.GetResult(short token) => GetResult(token);
    }

    internal static class ZeroWriterPromisePool<T>
    {
        private static ZeroWriterPromise<T>? s_fastItem;
        private static readonly Queue<ZeroWriterPromise<T>> s_pool = new Queue<ZeroWriterPromise<T>>();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ZeroWriterPromise<T> Rent(T item)
        {
            var fast = Interlocked.Exchange(ref s_fastItem, null);
            if (fast != null)
            {
                fast.Item = item;
                return fast;
            }

            lock (s_pool)
            {
                if (s_pool.Count > 0)
                {
                    var promise = s_pool.Dequeue();
                    promise.Item = item;
                    return promise;
                }
            }

            var newPromise = new ZeroWriterPromise<T>(Return);
            newPromise.Item = item;
            return newPromise;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Return(ZeroWriterPromise<T> promise)
        {
            if (Interlocked.CompareExchange(ref s_fastItem, promise, null) != null)
            {
                lock (s_pool)
                {
                    s_pool.Enqueue(promise);
                }
            }
        }
    }

    /// <summary>
    /// High-performance CSP (Communicating Sequential Processes) channel inspired by Go channels (chan T).
    /// Uses <see cref="ZeroRingBuffer{T}"/> as its ring storage and a Direct-Handoff synchronization mechanism
    /// to achieve true Zero-Allocation asynchronous streaming without compiler state machine boxing.
    /// </summary>
    /// <typeparam name="T">The type of elements flowing through the channel.</typeparam>
    public sealed class ZeroChannel<T>
    {
        private readonly object _syncObj = new object();
        private readonly ZeroRingBuffer<T> _buffer;
        private readonly Queue<ZeroPromise<T>> _waitingReaders = new Queue<ZeroPromise<T>>();
        private readonly Queue<ZeroWriterPromise<T>> _waitingWriters = new Queue<ZeroWriterPromise<T>>();

        private volatile bool _isCompleted;
        private Exception? _completionError;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZeroChannel{T}"/> class with a power-of-two buffer capacity.
        /// </summary>
        /// <param name="capacityPowerOfTwo">The ring buffer capacity (must be a power of two, e.g. 64, 256, 1024...).</param>
        public ZeroChannel(int capacityPowerOfTwo = 1024)
        {
            _buffer = new ZeroRingBuffer<T>(capacityPowerOfTwo);
        }

        /// <summary>
        /// Gets the maximum capacity of the channel's underlying ring buffer.
        /// </summary>
        public int Capacity => _buffer.Capacity;

        /// <summary>
        /// Gets the approximate number of items currently buffered in the channel.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_syncObj)
                {
                    return _buffer.Count;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the channel is marked completed and all buffered items have been consumed.
        /// </summary>
        public bool IsCompleted
        {
            get
            {
                lock (_syncObj)
                {
                    return _isCompleted && _buffer.IsEmpty && _waitingWriters.Count == 0;
                }
            }
        }

        /// <summary>
        /// Attempts to write an item into the channel synchronously without blocking.
        /// First attempts direct handoff to a waiting reader; otherwise buffers in the ring queue.
        /// </summary>
        /// <param name="item">The item to write.</param>
        /// <returns><c>true</c> if the item was written or handed off; otherwise <c>false</c>.</returns>
        public bool TryWrite(T item)
        {
            lock (_syncObj)
            {
                return TryWriteCore(item);
            }
        }

        private bool TryWriteCore(T item)
        {
            if (_isCompleted)
            {
                throw new ZeroChannelClosedException("Cannot write to a closed channel.", _completionError);
            }

            // Direct handoff: pass item straight to waiting reader without entering ring buffer
            while (_waitingReaders.Count > 0)
            {
                var reader = _waitingReaders.Dequeue();
                if (reader.TrySetResult(item))
                {
                    return true;
                }
            }

            return _buffer.TryEnqueue(item);
        }

        /// <summary>
        /// Attempts to read an item from the channel synchronously without blocking.
        /// </summary>
        /// <param name="item">The item received.</param>
        /// <returns><c>true</c> if an item was read; otherwise <c>false</c>.</returns>
        public bool TryRead(out T item)
        {
            lock (_syncObj)
            {
                return TryReadCore(out item);
            }
        }

        private bool TryReadCore(out T item)
        {
            // First try reading from ring buffer
            if (_buffer.TryDequeue(out item))
            {
                // Refill freed buffer slot directly from waiting writer if any
                while (_waitingWriters.Count > 0)
                {
                    var writer = _waitingWriters.Dequeue();
                    if (_buffer.TryEnqueue(writer.Item))
                    {
                        writer.SetResult(true);
                        break;
                    }
                    else
                    {
                        item = writer.Item;
                        writer.SetResult(true);
                        return true;
                    }
                }
                return true;
            }

            // If buffer is empty, check for direct handoff from waiting writer
            while (_waitingWriters.Count > 0)
            {
                var directWriter = _waitingWriters.Dequeue();
                item = directWriter.Item;
                directWriter.SetResult(true);
                return true;
            }

            item = default!;
            return false;
        }

        /// <summary>
        /// Attempts to write a batch of items synchronously from a <see cref="ReadOnlySpan{T}"/>.
        /// Acquires the synchronization lock once for the entire batch.
        /// </summary>
        /// <param name="items">The items to write.</param>
        /// <returns>The number of items successfully written.</returns>
        public int TryWriteBatch(ReadOnlySpan<T> items)
        {
            if (items.IsEmpty) return 0;

            lock (_syncObj)
            {
                if (_isCompleted) return 0;

                int written = 0;
                // First satisfy any waiting readers directly
                while (_waitingReaders.Count > 0 && written < items.Length)
                {
                    var reader = _waitingReaders.Dequeue();
                    reader.SetResult(items[written]);
                    written++;
                }

                // If remaining items, write directly to ring buffer in a single batch
                if (written < items.Length)
                {
                    written += _buffer.TryEnqueueBatch(items.Slice(written));
                }

                return written;
            }
        }

        /// <summary>
        /// Attempts to read a batch of items synchronously into a destination <see cref="Span{T}"/>.
        /// Acquires the synchronization lock once for the entire batch.
        /// </summary>
        /// <param name="destination">The destination span to populate.</param>
        /// <returns>The number of items successfully read.</returns>
        public int TryReadBatch(Span<T> destination)
        {
            if (destination.IsEmpty) return 0;

            lock (_syncObj)
            {
                int read = _buffer.TryDequeueBatch(destination);

                // If buffer is drained and there is still space in destination, drain waiting writers directly
                while (_waitingWriters.Count > 0 && read < destination.Length)
                {
                    var writer = _waitingWriters.Dequeue();
                    destination[read++] = writer.Item;
                    writer.SetResult(true);
                }

                // Refill freed buffer slots from waiting writers
                while (_waitingWriters.Count > 0)
                {
                    var writer = _waitingWriters.Peek();
                    if (_buffer.TryEnqueue(writer.Item))
                    {
                        _waitingWriters.Dequeue();
                        writer.SetResult(true);
                    }
                    else
                    {
                        break;
                    }
                }

                return read;
            }
        }

        /// <summary>
        /// Asynchronously writes an item to the channel (analogous to 'ch &lt;- val' in Go).
        /// Achieves 0-allocation by avoiding compiler async state machine boxing.
        /// </summary>
        /// <param name="item">The item to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public ValueTask WriteAsync(T item, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
#if NET8_0_OR_GREATER
                return ValueTask.FromCanceled(cancellationToken);
#else
                return new ValueTask(Task.FromCanceled(cancellationToken));
#endif
            }

            ZeroWriterPromise<T> writerPromise;
            lock (_syncObj)
            {
                // Fast path: immediate write succeeds with 0 allocation
                if (TryWriteCore(item))
                {
                    return default;
                }

                if (_isCompleted)
                {
                    throw new ZeroChannelClosedException("Cannot write to a closed channel.", _completionError);
                }

                // Slow path: Rent a writer promise and return its ValueTask directly
                writerPromise = ZeroWriterPromisePool<T>.Rent(item);
                _waitingWriters.Enqueue(writerPromise);
            }

            if (cancellationToken.CanBeCanceled)
            {
#if NET8_0_OR_GREATER
                writerPromise.Registration = cancellationToken.UnsafeRegister(static s =>
                {
                    var p = (ZeroWriterPromise<T>)s!;
                    p.SetException(new OperationCanceledException());
                }, writerPromise);
#else
                writerPromise.Registration = cancellationToken.Register(static s =>
                {
                    var p = (ZeroWriterPromise<T>)s!;
                    p.SetException(new OperationCanceledException());
                }, writerPromise);
#endif
            }

            return writerPromise.Task;
        }

        /// <summary>
        /// Asynchronously reads an item from the channel (analogous to 'val := &lt;-ch' in Go).
        /// Achieves 0-allocation by avoiding compiler async state machine boxing.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public ValueTask<T> ReadAsync(CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
#if NET8_0_OR_GREATER
                return ValueTask.FromCanceled<T>(cancellationToken);
#else
                return new ValueTask<T>(Task.FromCanceled<T>(cancellationToken));
#endif
            }

            ZeroPromise<T> readerPromise;
            lock (_syncObj)
            {
                // Fast path: immediate read succeeds with 0 allocation
                if (TryReadCore(out var item))
                {
                    return new ValueTask<T>(item);
                }

                if (_isCompleted)
                {
                    throw new ZeroChannelClosedException("Channel is closed and empty.", _completionError);
                }

                // Slow path: Rent reader promise and return its ValueTask directly
                readerPromise = ZeroPromisePool<T>.Rent();
                _waitingReaders.Enqueue(readerPromise);
            }

            if (cancellationToken.CanBeCanceled)
            {
#if NET8_0_OR_GREATER
                readerPromise.Registration = cancellationToken.UnsafeRegister(static s =>
                {
                    var p = (ZeroPromise<T>)s!;
                    p.SetException(new OperationCanceledException());
                }, readerPromise);
#else
                readerPromise.Registration = cancellationToken.Register(static s =>
                {
                    var p = (ZeroPromise<T>)s!;
                    p.SetException(new OperationCanceledException());
                }, readerPromise);
#endif
            }

            return readerPromise.Task;
        }

        /// <summary>
        /// Asynchronously waits until data is available for reading or the channel completes.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><c>true</c> if data is available; <c>false</c> if the channel is closed and drained.</returns>
        public async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                lock (_syncObj)
                {
                    if (!_buffer.IsEmpty || _waitingWriters.Count > 0) return true;
                    if (_isCompleted) return false;
                }

                try
                {
                    T incoming = await ReadAsync(cancellationToken).ConfigureAwait(false);
                    lock (_syncObj)
                    {
                        // Push back into buffer for subsequent TryRead consumption
                        _buffer.TryEnqueue(incoming);
                    }
                    return true;
                }
                catch (ZeroChannelClosedException)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Streams all elements from the channel as an <see cref="IAsyncEnumerable{T}"/>.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async IAsyncEnumerable<T> ReadAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                T item;
                try
                {
                    item = await ReadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (ZeroChannelClosedException)
                {
                    break;
                }

                yield return item;
            }
        }

        /// <summary>
        /// Marks the channel as complete, preventing further writes. Remaining buffered items can still be read.
        /// </summary>
        /// <param name="error">Optional completion exception.</param>
        public void Complete(Exception? error = null)
        {
            if (_isCompleted) return;

            lock (_syncObj)
            {
                if (_isCompleted) return;

                _completionError = error;
                _isCompleted = true;

                // Wake up waiting writers with closed error
                while (_waitingWriters.Count > 0)
                {
                    var writer = _waitingWriters.Dequeue();
                    if (error != null) writer.SetException(error);
                    else writer.SetException(new ZeroChannelClosedException("Channel has been closed."));
                }

                // Wake up all waiting readers with closed exception
                while (_waitingReaders.Count > 0)
                {
                    var reader = _waitingReaders.Dequeue();
                    reader.SetException(error ?? new ZeroChannelClosedException("Channel has been closed."));
                }
            }
        }
    }
}
