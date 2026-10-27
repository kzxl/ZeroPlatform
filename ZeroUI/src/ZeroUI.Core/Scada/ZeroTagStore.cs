using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using ZeroUI.Core.Runtime;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Metadata descriptor for an industrial SCADA tag.
    /// </summary>
    public sealed class TagMetadata
    {
        public int TagId { get; }
        public string Path { get; }
        public string Name { get; }
        public string Description { get; }
        public string Unit { get; }
        public ScadaValueType DataType { get; }

        public TagMetadata(int tagId, string path, string name, string unit = "", string description = "", ScadaValueType dataType = ScadaValueType.Double)
        {
            TagId = tagId;
            Path = path ?? string.Empty;
            Name = name ?? path ?? string.Empty;
            Unit = unit ?? string.Empty;
            Description = description ?? string.Empty;
            DataType = dataType;
        }
    }

    /// <summary>
    /// High-performance unboxed tag store for industrial edge SCADA.
    /// Indexes contiguous unboxed values by integer TagId with bitset dirty tracking,
    /// O(1) subscriber dispatch, and zero heap allocation on updates.
    /// </summary>
    public sealed class ZeroTagStore : IDisposable
    {
        private static readonly Lazy<ZeroTagStore> _shared =
            new Lazy<ZeroTagStore>(() => new ZeroTagStore("Shared", 2048));

        public static ZeroTagStore Shared => _shared.Value;

        private readonly string _name;
        private readonly TagStorage _storage;

        private readonly ConcurrentDictionary<string, int> _pathToId =
            new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        private readonly ConcurrentDictionary<int, TagMetadata> _idToMetadata =
            new ConcurrentDictionary<int, TagMetadata>();

        private readonly Dictionary<int, List<Action<int, ScadaValue>>> _subscribers =
            new Dictionary<int, List<Action<int, ScadaValue>>>();
        private readonly object _subscriberLock = new object();

        private IDisposable? _busSubscription;
        private int _nextTagId = 1;
        private bool _disposed = false;

        public string Name => _name;
        public int TagCount => _idToMetadata.Count;
        public TagStorage RawStorage => _storage;

        public ZeroTagStore(string name = "TagStore", int initialCapacity = 2048)
        {
            _name = name;
            _storage = new TagStorage(initialCapacity);
        }

        #region Registration & Lookup

        /// <summary>
        /// Registers or retrieves the integer TagId for a tag path.
        /// </summary>
        public int RegisterTag(string path, string unit = "", string description = "", ScadaValueType dataType = ScadaValueType.Double)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Tag path cannot be empty", nameof(path));

            return _pathToId.GetOrAdd(path, p =>
            {
                int id = Interlocked.Increment(ref _nextTagId);
                _storage.EnsureCapacity(id);

                string name = p;
                int lastSlash = p.LastIndexOf('/');
                if (lastSlash >= 0 && lastSlash < p.Length - 1) name = p.Substring(lastSlash + 1);

                var meta = new TagMetadata(id, p, name, unit, description, dataType);
                _idToMetadata[id] = meta;
                return id;
            });
        }

        public bool TryGetTagId(string path, out int tagId)
        {
            return _pathToId.TryGetValue(path, out tagId);
        }

        public TagMetadata? GetMetadata(int tagId)
        {
            _idToMetadata.TryGetValue(tagId, out var meta);
            return meta;
        }

        #endregion

        #region Value Accessors (Zero Allocation)

        public ScadaValue GetValue(int tagId) => _storage.GetValue(tagId);
        public double GetDouble(int tagId) => _storage.GetValue(tagId).AsDouble();
        public long GetInt64(int tagId) => _storage.GetValue(tagId).AsInt64();
        public bool GetBool(int tagId) => _storage.GetValue(tagId).AsBoolean();
        public ScadaQuality GetQuality(int tagId) => _storage.GetValue(tagId).Quality;
        public long GetTimestampUtcMs(int tagId) => _storage.GetTimestampUtcMs(tagId);

        public void Set(int tagId, in ScadaValue value, long timestampUtcMs = 0)
        {
            if (timestampUtcMs <= 0) timestampUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _storage.Set(tagId, in value, timestampUtcMs);
        }

        public void Set(int tagId, double value, ScadaQuality quality = ScadaQuality.Good)
        {
            Set(tagId, new ScadaValue(value, quality));
        }

        public void Set(int tagId, long value, ScadaQuality quality = ScadaQuality.Good)
        {
            Set(tagId, new ScadaValue(value, quality));
        }

        public void Set(int tagId, bool value, ScadaQuality quality = ScadaQuality.Good)
        {
            Set(tagId, new ScadaValue(value, quality));
        }

        #endregion

        #region Batch Processing & Bus Integration

        /// <summary>
        /// Applies a batch of incoming tag updates with zero heap allocation.
        /// </summary>
        public void ApplyBatch(ReadOnlySpan<TagUpdate> batch)
        {
            for (int i = 0; i < batch.Length; i++)
            {
                ref readonly var update = ref batch[i];
                _storage.Set(update.TagId, in update.Value, update.TimestampUtcMs);
            }
        }

        /// <summary>
        /// Connects this tag store to automatically ingest streaming updates from a <see cref="ZeroTelemetryBus"/>.
        /// </summary>
        public void AttachToBus(ZeroTelemetryBus bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            _busSubscription?.Dispose();
            _busSubscription = bus.SubscribeUpdates(ApplyBatch);
        }

        #endregion

        #region Subscriptions & Dispatch

        /// <summary>
        /// Subscribes a listener callback to value changes for a specific tag.
        /// </summary>
        public IDisposable Subscribe(int tagId, Action<int, ScadaValue> listener)
        {
            if (listener == null) throw new ArgumentNullException(nameof(listener));

            lock (_subscriberLock)
            {
                if (!_subscribers.TryGetValue(tagId, out var list))
                {
                    list = new List<Action<int, ScadaValue>>(2);
                    _subscribers[tagId] = list;
                }
                list.Add(listener);
            }

            return new SubscriptionToken(() =>
            {
                lock (_subscriberLock)
                {
                    if (_subscribers.TryGetValue(tagId, out var list))
                    {
                        list.Remove(listener);
                        if (list.Count == 0) _subscribers.Remove(tagId);
                    }
                }
            });
        }

        /// <summary>
        /// Flushes all dirty tags and notifies subscribed controls in O(1) time.
        /// </summary>
        public void DispatchDirty(Span<int> buffer)
        {
            int dirtyCount = _storage.DrainDirtyTags(buffer);
            if (dirtyCount == 0) return;

            lock (_subscriberLock)
            {
                for (int i = 0; i < dirtyCount; i++)
                {
                    int tagId = buffer[i];
                    if (_subscribers.TryGetValue(tagId, out var list))
                    {
                        var val = _storage.GetValue(tagId);
                        for (int j = 0; j < list.Count; j++)
                        {
                            try
                            {
                                list[j](tagId, val);
                            }
                            catch
                            {
                                // Guard against subscriber crash
                            }
                        }
                    }
                }
            }
        }

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _busSubscription?.Dispose();
            _busSubscription = null;
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
                Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
            }
        }
    }
}
