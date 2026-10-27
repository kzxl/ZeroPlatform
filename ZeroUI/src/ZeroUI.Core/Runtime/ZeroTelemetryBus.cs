using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Zero-allocation delegate for handling streamed batches of telemetry tag updates.
    /// </summary>
    public delegate void TagUpdateBatchHandler(ReadOnlySpan<TagUpdate> batch);

    /// <summary>
    /// High-throughput, lock-free telemetry bus decoupling field protocol drivers,
    /// analytical processors, and real-time UI render passes.
    /// </summary>
    public sealed class ZeroTelemetryBus
    {
        private static readonly Lazy<ZeroTelemetryBus> _shared =
            new Lazy<ZeroTelemetryBus>(() => new ZeroTelemetryBus("Shared"));

        public static ZeroTelemetryBus Shared => _shared.Value;

        private readonly string _name;
        private readonly object _lock = new object();

        // Copy-On-Write array for raw tag updates
        private TagUpdateBatchHandler[] _batchHandlers = Array.Empty<TagUpdateBatchHandler>();

        // Topic subscriptions for structured events (Alarms, System Status, OEE)
        private readonly ConcurrentDictionary<string, List<object>> _topicSubscribers =
            new ConcurrentDictionary<string, List<object>>(StringComparer.OrdinalIgnoreCase);

        private long _totalUpdatesPublished;
        private long _totalBatchesPublished;

        public string Name => _name;
        public long TotalUpdatesPublished => Volatile.Read(ref _totalUpdatesPublished);
        public long TotalBatchesPublished => Volatile.Read(ref _totalBatchesPublished);
        public int BatchSubscriberCount => Volatile.Read(ref _batchHandlers).Length;

        public ZeroTelemetryBus(string name = "TelemetryBus")
        {
            _name = name;
        }

        #region Tag Updates Stream

        /// <summary>
        /// Subscribes to the raw telemetry batch stream with zero heap allocation.
        /// </summary>
        public IDisposable SubscribeUpdates(TagUpdateBatchHandler handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var oldArr = _batchHandlers;
                var newArr = new TagUpdateBatchHandler[oldArr.Length + 1];
                Array.Copy(oldArr, newArr, oldArr.Length);
                newArr[oldArr.Length] = handler;
                Volatile.Write(ref _batchHandlers, newArr);
            }

            return new SubscriptionToken(() =>
            {
                lock (_lock)
                {
                    var current = _batchHandlers;
                    int idx = Array.IndexOf(current, handler);
                    if (idx < 0) return;

                    var newArr = new TagUpdateBatchHandler[current.Length - 1];
                    Array.Copy(current, 0, newArr, 0, idx);
                    Array.Copy(current, idx + 1, newArr, idx, current.Length - idx - 1);
                    Volatile.Write(ref _batchHandlers, newArr);
                }
            });
        }

        /// <summary>
        /// Publishes a batch of telemetry updates to all active subscribers.
        /// Operates entirely via Span without heap allocations.
        /// </summary>
        public void Publish(ReadOnlySpan<TagUpdate> batch)
        {
            if (batch.IsEmpty) return;

            Interlocked.Add(ref _totalUpdatesPublished, batch.Length);
            Interlocked.Increment(ref _totalBatchesPublished);

            var handlers = Volatile.Read(ref _batchHandlers);
            for (int i = 0; i < handlers.Length; i++)
            {
                try
                {
                    handlers[i](batch);
                }
                catch
                {
                    // Isolate subscriber exceptions from streaming publisher
                }
            }
        }

        /// <summary>
        /// Publishes a single telemetry update.
        /// </summary>
        public void Publish(in TagUpdate update)
        {
            Span<TagUpdate> single = stackalloc TagUpdate[1];
            single[0] = update;
            Publish(single);
        }

        #endregion

        #region Structured Topic Messaging

        /// <summary>
        /// Subscribes to a typed topic channel (e.g. Alarms, Diagnostics, Events).
        /// </summary>
        public IDisposable Subscribe<T>(string topic, Action<T> handler)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            _topicSubscribers.AddOrUpdate(
                topic,
                _ => new List<object> { handler },
                (_, list) =>
                {
                    lock (list)
                    {
                        var copy = new List<object>(list) { handler };
                        return copy;
                    }
                });

            return new SubscriptionToken(() =>
            {
                if (_topicSubscribers.TryGetValue(topic, out var list))
                {
                    lock (list)
                    {
                        list.Remove(handler);
                    }
                }
            });
        }

        /// <summary>
        /// Publishes a message to a typed topic.
        /// </summary>
        public void Publish<T>(string topic, in T message)
        {
            if (topic == null) return;

            if (_topicSubscribers.TryGetValue(topic, out var list))
            {
                List<object> snapshot;
                lock (list)
                {
                    snapshot = list;
                }

                for (int i = 0; i < snapshot.Count; i++)
                {
                    if (snapshot[i] is Action<T> typed)
                    {
                        try
                        {
                            typed(message);
                        }
                        catch
                        {
                            // Guard against faulty subscriber
                        }
                    }
                }
            }
        }

        #endregion

        private sealed class SubscriptionToken : IDisposable
        {
            private Action? _unsubscribe;

            public SubscriptionToken(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
            }
        }
    }
}
