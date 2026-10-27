using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace ZeroUI.Core.Runtime
{
    /// <summary>
    /// Represents a recorded entry in the StateStore.
    /// </summary>
    public sealed class StateEntry
    {
        public string Key { get; }
        public object? Value { get; }
        public DateTime Timestamp { get; }
        public long Version { get; }

        public StateEntry(string key, object? value, DateTime timestamp, long version)
        {
            Key = key;
            Value = value;
            Timestamp = timestamp;
            Version = version;
        }
    }

    /// <summary>
    /// Central reactive in-memory state repository for industrial machines, stations, and system status.
    /// Thread-safe with atomic Compare-and-Swap and change event notification.
    /// </summary>
    public sealed class StateStore
    {
        private static readonly Lazy<StateStore> _defaultInstance = new Lazy<StateStore>(() => new StateStore());
        public static StateStore Default => _defaultInstance.Value;

        private readonly ConcurrentDictionary<string, StateEntry> _store =
            new ConcurrentDictionary<string, StateEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<string, List<Action<string, object?, object?>>> _listeners =
            new ConcurrentDictionary<string, List<Action<string, object?, object?>>>(StringComparer.OrdinalIgnoreCase);

        private readonly object _listenerLock = new object();
        private long _globalVersion;

        /// <summary>
        /// Global event fired when any state entry changes.
        /// </summary>
        public event Action<string, object?, object?>? AnyStateChanged;

        /// <summary>
        /// Gets the value of a state key, or defaultValue if not present.
        /// </summary>
        public T? GetState<T>(string key, T? defaultValue = default)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            if (_store.TryGetValue(key, out var entry) && entry.Value != null)
            {
                if (entry.Value is T direct) return direct;
                try
                {
                    return (T)Convert.ChangeType(entry.Value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Gets the raw object value of a state key.
        /// </summary>
        public object? GetState(string key, object? defaultValue = null)
        {
            if (string.IsNullOrEmpty(key)) return defaultValue;
            return _store.TryGetValue(key, out var entry) ? entry.Value : defaultValue;
        }

        /// <summary>
        /// Sets a state value. If value is different from current, increments version and notifies listeners.
        /// </summary>
        public bool SetState(string key, object? newValue)
        {
            if (string.IsNullOrEmpty(key)) return false;

            object? oldValue = null;
            bool changed = false;

            _store.AddOrUpdate(
                key,
                k =>
                {
                    changed = true;
                    long ver = Interlocked.Increment(ref _globalVersion);
                    return new StateEntry(k, newValue, DateTime.UtcNow, ver);
                },
                (k, existing) =>
                {
                    oldValue = existing.Value;
                    if (Equals(oldValue, newValue))
                    {
                        return existing;
                    }

                    changed = true;
                    long ver = Interlocked.Increment(ref _globalVersion);
                    return new StateEntry(k, newValue, DateTime.UtcNow, ver);
                });

            if (changed)
            {
                NotifyChanged(key, oldValue, newValue);
            }

            return changed;
        }

        /// <summary>
        /// Atomically updates a state value if current value equals expectedValue.
        /// </summary>
        public bool CompareAndSwap(string key, object? expectedValue, object? newValue)
        {
            if (string.IsNullOrEmpty(key)) return false;

            bool swapped = false;
            object? actualOld = null;

            _store.AddOrUpdate(
                key,
                k =>
                {
                    if (expectedValue == null)
                    {
                        swapped = true;
                        long ver = Interlocked.Increment(ref _globalVersion);
                        return new StateEntry(k, newValue, DateTime.UtcNow, ver);
                    }
                    return new StateEntry(k, null, DateTime.UtcNow, 0);
                },
                (k, existing) =>
                {
                    actualOld = existing.Value;
                    if (Equals(existing.Value, expectedValue))
                    {
                        swapped = true;
                        long ver = Interlocked.Increment(ref _globalVersion);
                        return new StateEntry(k, newValue, DateTime.UtcNow, ver);
                    }
                    return existing;
                });

            if (swapped)
            {
                NotifyChanged(key, actualOld, newValue);
            }

            return swapped;
        }

        /// <summary>
        /// Subscribes to changes on a specific state key.
        /// Returns an IDisposable token for automatic unsubscription.
        /// </summary>
        public IDisposable Subscribe(string key, Action<string, object?, object?> listener)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (listener == null) throw new ArgumentNullException(nameof(listener));

            lock (_listenerLock)
            {
                var list = _listeners.GetOrAdd(key, _ => new List<Action<string, object?, object?>>());
                list.Add(listener);
            }

            return new UnsubscribeToken(() =>
            {
                lock (_listenerLock)
                {
                    if (_listeners.TryGetValue(key, out var list))
                    {
                        list.Remove(listener);
                    }
                }
            });
        }

        /// <summary>
        /// Returns a snapshot dictionary of all current states.
        /// </summary>
        public IReadOnlyDictionary<string, StateEntry> GetAllStates()
        {
            return new Dictionary<string, StateEntry>(_store, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Clears all stored states and listeners.
        /// </summary>
        public void Clear()
        {
            _store.Clear();
            lock (_listenerLock)
            {
                _listeners.Clear();
            }
        }

        private void NotifyChanged(string key, object? oldValue, object? newValue)
        {
            try
            {
                AnyStateChanged?.Invoke(key, oldValue, newValue);
            }
            catch
            {
                // Guard global listener
            }

            List<Action<string, object?, object?>>? keyListeners = null;
            lock (_listenerLock)
            {
                if (_listeners.TryGetValue(key, out var list) && list.Count > 0)
                {
                    keyListeners = new List<Action<string, object?, object?>>(list);
                }
            }

            if (keyListeners != null)
            {
                for (int i = 0; i < keyListeners.Count; i++)
                {
                    try
                    {
                        keyListeners[i](key, oldValue, newValue);
                    }
                    catch
                    {
                        // Guard key listener
                    }
                }
            }
        }

        private sealed class UnsubscribeToken : IDisposable
        {
            private Action? _action;
            public UnsubscribeToken(Action action) => _action = action;
            public void Dispose()
            {
                var act = Interlocked.Exchange(ref _action, null);
                act?.Invoke();
            }
        }
    }
}
