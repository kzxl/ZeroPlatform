using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Contract for an in-process decoupled event bus.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribes a handler to events of type TEvent.
        /// Returns an IDisposable subscription token for safe unsubscription.
        /// </summary>
        IDisposable Subscribe<TEvent>(Action<TEvent> handler);

        /// <summary>
        /// Subscribes an asynchronous handler to events of type TEvent.
        /// </summary>
        IDisposable SubscribeAsync<TEvent>(Func<TEvent, Task> handler);

        /// <summary>
        /// Publishes an event to all registered subscribers synchronously.
        /// </summary>
        void Publish<TEvent>(TEvent eventData);

        /// <summary>
        /// Publishes an event to all registered subscribers asynchronously.
        /// </summary>
        Task PublishAsync<TEvent>(TEvent eventData);
    }

    /// <summary>
    /// Thread-safe in-process publish/subscribe message bus with subscription lifecycle tokens.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private static readonly Lazy<EventBus> _defaultInstance = new Lazy<EventBus>(() => new EventBus());
        public static EventBus Default => _defaultInstance.Value;

        private readonly ConcurrentDictionary<Type, Delegate[]> _syncHandlers =
            new ConcurrentDictionary<Type, Delegate[]>();

        private readonly ConcurrentDictionary<Type, Delegate[]> _asyncHandlers =
            new ConcurrentDictionary<Type, Delegate[]>();

        private readonly object _lock = new object();

        /// <inheritdoc />
        public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(TEvent);

            lock (_lock)
            {
                if (_syncHandlers.TryGetValue(type, out var current))
                {
                    var updated = new Delegate[current.Length + 1];
                    Array.Copy(current, updated, current.Length);
                    updated[current.Length] = handler;
                    _syncHandlers[type] = updated;
                }
                else
                {
                    _syncHandlers[type] = new Delegate[] { handler };
                }
            }

            return new SubscriptionToken(() =>
            {
                lock (_lock)
                {
                    if (_syncHandlers.TryGetValue(type, out var current))
                    {
                        int index = Array.IndexOf(current, handler);
                        if (index >= 0)
                        {
                            if (current.Length == 1)
                            {
                                _syncHandlers.TryRemove(type, out _);
                            }
                            else
                            {
                                var updated = new Delegate[current.Length - 1];
                                Array.Copy(current, 0, updated, 0, index);
                                Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
                                _syncHandlers[type] = updated;
                            }
                        }
                    }
                }
            });
        }

        /// <inheritdoc />
        public IDisposable SubscribeAsync<TEvent>(Func<TEvent, Task> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(TEvent);

            lock (_lock)
            {
                if (_asyncHandlers.TryGetValue(type, out var current))
                {
                    var updated = new Delegate[current.Length + 1];
                    Array.Copy(current, updated, current.Length);
                    updated[current.Length] = handler;
                    _asyncHandlers[type] = updated;
                }
                else
                {
                    _asyncHandlers[type] = new Delegate[] { handler };
                }
            }

            return new SubscriptionToken(() =>
            {
                lock (_lock)
                {
                    if (_asyncHandlers.TryGetValue(type, out var current))
                    {
                        int index = Array.IndexOf(current, handler);
                        if (index >= 0)
                        {
                            if (current.Length == 1)
                            {
                                _asyncHandlers.TryRemove(type, out _);
                            }
                            else
                            {
                                var updated = new Delegate[current.Length - 1];
                                Array.Copy(current, 0, updated, 0, index);
                                Array.Copy(current, index + 1, updated, index, current.Length - index - 1);
                                _asyncHandlers[type] = updated;
                            }
                        }
                    }
                }
            });
        }

        /// <inheritdoc />
        public void Publish<TEvent>(TEvent eventData)
        {
            var type = typeof(TEvent);

            if (_syncHandlers.TryGetValue(type, out var handlers))
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    try
                    {
                        ((Action<TEvent>)handlers[i])(eventData);
                    }
                    catch
                    {
                        // Guard against individual subscriber failure
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task PublishAsync<TEvent>(TEvent eventData)
        {
            Publish(eventData); // Run synchronous handlers first

            var type = typeof(TEvent);

            if (_asyncHandlers.TryGetValue(type, out var handlers))
            {
                for (int i = 0; i < handlers.Length; i++)
                {
                    try
                    {
                        await ((Func<TEvent, Task>)handlers[i])(eventData).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Guard against individual subscriber failure
                    }
                }
            }
        }

        /// <summary>
        /// Clears all registered subscriptions.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _syncHandlers.Clear();
                _asyncHandlers.Clear();
            }
        }

        private sealed class SubscriptionToken : IDisposable
        {
            private Action? _unsubscribe;

            public SubscriptionToken(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                var action = Interlocked.Exchange(ref _unsubscribe, null);
                action?.Invoke();
            }
        }
    }
}
