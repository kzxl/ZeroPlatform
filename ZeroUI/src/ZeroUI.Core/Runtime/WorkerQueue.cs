using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Backpressure and conflation strategy when the WorkerQueue processes high-throughput data.
    /// </summary>
    public enum QueueBackpressureMode
    {
        /// <summary>
        /// Drops the oldest item in the queue to make room for the new item.
        /// Ideal for realtime telemetry where recent data is more relevant than stale data.
        /// </summary>
        DropOldest,

        /// <summary>
        /// Discards the incoming item when full.
        /// </summary>
        DropWrite,

        /// <summary>
        /// Asynchronously waits until capacity becomes available.
        /// </summary>
        Wait,

        /// <summary>
        /// Conflates items by key (latest-value semantics). If multiple updates for the same key
        /// arrive before the worker processes them, intermediate stale updates are discarded,
        /// and only the newest update per key is processed.
        /// </summary>
        LatestPerKey
    }

    /// <summary>
    /// High-throughput, bounded asynchronous background processing queue backed by System.Threading.Channels.
    /// Supports configurable backpressure, concurrency, automatic retries, and industrial LatestPerKey conflation.
    /// </summary>
    /// <typeparam name="T">Payload type.</typeparam>
    public sealed class WorkerQueue<T> : IDisposable
    {
        private readonly Channel<T> _channel;
        private readonly Func<T, CancellationToken, Task> _handler;
        private readonly int _concurrency;
        private readonly int _maxRetries;
        private readonly Action<T, Exception>? _onError;
        private readonly ConcurrentDictionary<object, T>? _latestKeyMap;
        private readonly Func<T, object>? _keySelector;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly Task[] _workerTasks;

        private long _processedCount;
        private long _droppedCount;
        private long _failedCount;
        private bool _isDisposed;

        /// <summary>
        /// Gets the total number of items successfully processed.
        /// </summary>
        public long ProcessedCount => Interlocked.Read(ref _processedCount);

        /// <summary>
        /// Gets the total number of items dropped or conflated due to backpressure.
        /// </summary>
        public long DroppedCount => Interlocked.Read(ref _droppedCount);

        /// <summary>
        /// Gets the total number of items that failed after exceeding max retries.
        /// </summary>
        public long FailedCount => Interlocked.Read(ref _failedCount);

        /// <summary>
        /// Initializes a new instance of the WorkerQueue backed by System.Threading.Channels.
        /// </summary>
        /// <param name="handler">Asynchronous processor for each item.</param>
        /// <param name="capacity">Maximum bounded queue capacity.</param>
        /// <param name="backpressure">Strategy when capacity is reached or when conflating keys.</param>
        /// <param name="concurrency">Number of concurrent worker tasks.</param>
        /// <param name="maxRetries">Maximum retry attempts upon processing exception.</param>
        /// <param name="onError">Callback invoked when an item fails permanently.</param>
        /// <param name="keySelector">Extractor function for key-based conflation when using QueueBackpressureMode.LatestPerKey.</param>
        public WorkerQueue(
            Func<T, CancellationToken, Task> handler,
            int capacity = 10000,
            QueueBackpressureMode backpressure = QueueBackpressureMode.DropOldest,
            int concurrency = 1,
            int maxRetries = 0,
            Action<T, Exception>? onError = null,
            Func<T, object>? keySelector = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");
            if (concurrency <= 0) throw new ArgumentOutOfRangeException(nameof(concurrency), "Concurrency must be positive.");

            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _concurrency = concurrency;
            _maxRetries = maxRetries;
            _onError = onError;

            if (backpressure == QueueBackpressureMode.LatestPerKey)
            {
                _keySelector = keySelector ?? (item => item!);
                _latestKeyMap = new ConcurrentDictionary<object, T>();
            }

            var channelMode = backpressure switch
            {
                QueueBackpressureMode.DropWrite => BoundedChannelFullMode.DropWrite,
                QueueBackpressureMode.Wait => BoundedChannelFullMode.Wait,
                _ => BoundedChannelFullMode.DropOldest
            };

            var options = new BoundedChannelOptions(capacity)
            {
                FullMode = channelMode,
                SingleReader = concurrency == 1,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<T>(options, item =>
            {
                Interlocked.Increment(ref _droppedCount);
            });

            _workerTasks = new Task[_concurrency];
            for (int i = 0; i < _concurrency; i++)
            {
                _workerTasks[i] = Task.Run(WorkerLoopAsync);
            }
        }

        /// <summary>
        /// Enqueues an item asynchronously. If backpressure mode is Wait, awaits buffer space.
        /// In LatestPerKey mode, updates the key store and enqueues.
        /// </summary>
        public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(WorkerQueue<T>));

            if (_latestKeyMap != null && _keySelector != null)
            {
                object key = _keySelector(item);
                _latestKeyMap[key] = item;
            }

            return _channel.Writer.WriteAsync(item, cancellationToken);
        }

        /// <summary>
        /// Tries to enqueue an item synchronously without blocking.
        /// In LatestPerKey mode, updates the key store with the newest value.
        /// </summary>
        public bool TryEnqueue(T item)
        {
            if (_isDisposed) return false;

            if (_latestKeyMap != null && _keySelector != null)
            {
                object key = _keySelector(item);
                _latestKeyMap[key] = item;
            }

            return _channel.Writer.TryWrite(item);
        }

        private async Task WorkerLoopAsync()
        {
            var reader = _channel.Reader;
            var token = _cts.Token;

            try
            {
                while (await reader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    while (reader.TryRead(out var item))
                    {
                        if (_latestKeyMap != null && _keySelector != null)
                        {
                            object key = _keySelector(item);
                            // Only process if this is still the newest value for this key
                            if (_latestKeyMap.TryRemove(key, out var latestItem))
                            {
                                await ProcessItemWithRetryAsync(latestItem, token).ConfigureAwait(false);
                            }
                            else
                            {
                                // Stale intermediate item dropped by conflation
                                Interlocked.Increment(ref _droppedCount);
                            }
                        }
                        else
                        {
                            await ProcessItemWithRetryAsync(item, token).ConfigureAwait(false);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Clean exit on cancellation
            }
        }

        private async Task ProcessItemWithRetryAsync(T item, CancellationToken token)
        {
            int attempt = 0;
            while (true)
            {
                try
                {
                    await _handler(item, token).ConfigureAwait(false);
                    Interlocked.Increment(ref _processedCount);
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    attempt++;
                    if (attempt > _maxRetries)
                    {
                        Interlocked.Increment(ref _failedCount);
                        try
                        {
                            _onError?.Invoke(item, ex);
                        }
                        catch
                        {
                            // Guard against error handler exceptions
                        }
                        return;
                    }

                    await Task.Delay(attempt * 10, token).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Signals that no more items will be written and awaits processing of remaining items.
        /// </summary>
        public async Task CompleteAsync()
        {
            _channel.Writer.TryComplete();
            await Task.WhenAll(_workerTasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Disposes the queue, canceling running workers.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _channel.Writer.TryComplete();
            _cts.Cancel();
            _cts.Dispose();
        }
    }
}
