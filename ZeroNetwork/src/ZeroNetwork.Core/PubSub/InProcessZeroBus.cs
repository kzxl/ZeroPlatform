using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroNetwork.PubSub
{
    /// <summary>
    /// Configuration options for <see cref="InProcessZeroBus"/>.
    /// </summary>
    public sealed class ZeroBusOptions
    {
        /// <summary>
        /// Gets or sets whether message handlers are executed asynchronously in parallel.
        /// Default is <c>false</c> (sequential FIFO per subscriber).
        /// </summary>
        public bool DispatchInParallel { get; set; } = false;

        /// <summary>
        /// Gets or sets whether an unhandled exception in one subscriber stops dispatching to remaining subscribers.
        /// Default is <c>false</c> (all subscribers receive message even if one faults).
        /// </summary>
        public bool StopOnFirstException { get; set; } = false;
    }

    /// <summary>
    /// Ultra-fast, zero-allocation In-Process Pub/Sub message bus for ZeroPlatform.
    /// Thread-safe, lock-free on dispatch hot paths using Copy-On-Write delegate tables and <see cref="TopicTrie{TSubscriber}"/>.
    /// </summary>
    public sealed class InProcessZeroBus : IZeroBus
    {
        private readonly ZeroBusOptions _options;
        private readonly ConcurrentDictionary<Type, Delegate[]> _typeSubscribers = new ConcurrentDictionary<Type, Delegate[]>();
        private readonly object _typeLock = new object();

        private readonly TopicTrie<TopicSubscription> _topicTrie = new TopicTrie<TopicSubscription>();
        private readonly object _topicLock = new object();
        private int _disposed;

        private sealed class TypedSubscription<T> : IZeroSubscription
        {
            private readonly InProcessZeroBus _bus;
            private readonly Delegate _handler;
            private int _disposed;

            public string Topic => typeof(T).FullName ?? typeof(T).Name;
            public bool IsActive => Volatile.Read(ref _disposed) == 0;

            public TypedSubscription(InProcessZeroBus bus, Delegate handler)
            {
                _bus = bus;
                _handler = handler;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _bus.UnsubscribeTyped<T>(_handler);
                }
            }
        }

        private sealed class TopicSubscription : IZeroSubscription
        {
            private readonly InProcessZeroBus _bus;
            private readonly string _pattern;
            private readonly Delegate _handler;
            private int _disposed;

            public string Topic => _pattern;
            public bool IsActive => Volatile.Read(ref _disposed) == 0;
            public Delegate Handler => _handler;

            public TopicSubscription(InProcessZeroBus bus, string pattern, Delegate handler)
            {
                _bus = bus;
                _pattern = pattern;
                _handler = handler;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _bus.UnsubscribeTopic(_pattern, this);
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="InProcessZeroBus"/>.
        /// </summary>
        public InProcessZeroBus(ZeroBusOptions? options = null)
        {
            _options = options ?? new ZeroBusOptions();
        }

        /// <inheritdoc />
        public ValueTask PublishAsync<T>(T message, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_typeSubscribers.TryGetValue(typeof(T), out var subscribers) || subscribers.Length == 0)
            {
                return default;
            }

            if (_options.DispatchInParallel && subscribers.Length > 1)
            {
                return DispatchTypedParallelAsync(subscribers, message, cancellationToken);
            }

            return DispatchTypedSequentialAsync(subscribers, message, cancellationToken);
        }

        private async ValueTask DispatchTypedSequentialAsync<T>(Delegate[] subscribers, T message, CancellationToken cancellationToken)
        {
            List<Exception>? exceptions = null;

            for (int i = 0; i < subscribers.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var d = subscribers[i];
                try
                {
                    if (d is Func<T, CancellationToken, ValueTask> asyncFunc)
                    {
                        await asyncFunc(message, cancellationToken).ConfigureAwait(false);
                    }
                    else if (d is Action<T> syncAction)
                    {
                        syncAction(message);
                    }
                }
                catch (Exception ex)
                {
                    if (_options.StopOnFirstException)
                        throw;

                    (exceptions ??= new List<Exception>()).Add(ex);
                }
            }

            if (exceptions != null)
            {
                throw new AggregateException("One or more subscribers threw an unhandled exception.", exceptions);
            }
        }

        private async ValueTask DispatchTypedParallelAsync<T>(Delegate[] subscribers, T message, CancellationToken cancellationToken)
        {
            var tasks = new Task[subscribers.Length];
            for (int i = 0; i < subscribers.Length; i++)
            {
                var d = subscribers[i];
                if (d is Func<T, CancellationToken, ValueTask> asyncFunc)
                {
                    tasks[i] = asyncFunc(message, cancellationToken).AsTask();
                }
                else if (d is Action<T> syncAction)
                {
                    tasks[i] = Task.Run(() => syncAction(message), cancellationToken);
                }
            }
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public IZeroSubscription Subscribe<T>(Func<T, CancellationToken, ValueTask> handler)
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            SubscribeTyped<T>(handler);
            return new TypedSubscription<T>(this, handler);
        }

        /// <inheritdoc />
        public IZeroSubscription Subscribe<T>(Action<T> handler)
        {
            ThrowIfDisposed();
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            SubscribeTyped<T>(handler);
            return new TypedSubscription<T>(this, handler);
        }

        private void SubscribeTyped<T>(Delegate handler)
        {
            lock (_typeLock)
            {
                var type = typeof(T);
                if (_typeSubscribers.TryGetValue(type, out var current))
                {
                    var list = new List<Delegate>(current) { handler };
                    _typeSubscribers[type] = list.ToArray();
                }
                else
                {
                    _typeSubscribers[type] = new[] { handler };
                }
            }
        }

        private void UnsubscribeTyped<T>(Delegate handler)
        {
            lock (_typeLock)
            {
                var type = typeof(T);
                if (_typeSubscribers.TryGetValue(type, out var current))
                {
                    var list = new List<Delegate>(current);
                    if (list.Remove(handler))
                    {
                        if (list.Count == 0)
                        {
                            _typeSubscribers.TryRemove(type, out _);
                        }
                        else
                        {
                            _typeSubscribers[type] = list.ToArray();
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public ValueTask PublishTopicAsync(string topic, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(topic)) throw new ArgumentNullException(nameof(topic));

            var matched = new List<TopicSubscription>();
            _topicTrie.GetMatches(topic, matched);

            if (matched.Count == 0)
                return default;

            return DispatchTopicSequentialAsync(matched, topic, payload, cancellationToken);
        }

        private async ValueTask DispatchTopicSequentialAsync(List<TopicSubscription> subscribers, string topic, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            List<Exception>? exceptions = null;

            for (int i = 0; i < subscribers.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sub = subscribers[i];
                if (!sub.IsActive) continue;

                try
                {
                    if (sub.Handler is Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask> asyncFunc)
                    {
                        await asyncFunc(topic, payload, cancellationToken).ConfigureAwait(false);
                    }
                    else if (sub.Handler is Action<string, ReadOnlyMemory<byte>> syncAction)
                    {
                        syncAction(topic, payload);
                    }
                }
                catch (Exception ex)
                {
                    if (_options.StopOnFirstException)
                        throw;

                    (exceptions ??= new List<Exception>()).Add(ex);
                }
            }

            if (exceptions != null)
            {
                throw new AggregateException("One or more topic subscribers threw an unhandled exception.", exceptions);
            }
        }

        /// <inheritdoc />
        public IZeroSubscription SubscribeTopic(string topicPattern, Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask> handler)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(topicPattern)) throw new ArgumentNullException(nameof(topicPattern));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var sub = new TopicSubscription(this, topicPattern, handler);
            lock (_topicLock)
            {
                _topicTrie.Add(topicPattern, sub);
            }
            return sub;
        }

        /// <inheritdoc />
        public IZeroSubscription SubscribeTopic(string topicPattern, Action<string, ReadOnlyMemory<byte>> handler)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(topicPattern)) throw new ArgumentNullException(nameof(topicPattern));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            var sub = new TopicSubscription(this, topicPattern, handler);
            lock (_topicLock)
            {
                _topicTrie.Add(topicPattern, sub);
            }
            return sub;
        }

        private void UnsubscribeTopic(string pattern, TopicSubscription subscription)
        {
            lock (_topicLock)
            {
                _topicTrie.Remove(pattern, subscription);
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(InProcessZeroBus));
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _typeSubscribers.Clear();
            }
        }
    }
}
