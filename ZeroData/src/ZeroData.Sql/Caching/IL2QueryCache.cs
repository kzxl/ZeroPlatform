using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ZeroData.Sql.Caching
{
    /// <summary>
    /// Second-level (L2) query cache interface for caching query results across database operations.
    /// Supports automatic tag-based invalidation when entity tables are modified.
    /// </summary>
    public interface IL2QueryCache
    {
        /// <summary>
        /// Attempts to get a cached query result.
        /// </summary>
        bool TryGet<T>(string key, out T value);

        /// <summary>
        /// Stores a query result in cache with the specified expiration and associated entity types.
        /// </summary>
        void Set<T>(string key, T value, TimeSpan duration, IEnumerable<Type> impactedEntityTypes = null);

        /// <summary>
        /// Invalidates all cached queries associated with the specified entity type.
        /// </summary>
        void Invalidate(Type entityType);

        /// <summary>
        /// Invalidates all cached query entries.
        /// </summary>
        void InvalidateAll();

        /// <summary>
        /// Gets the current number of cached entries.
        /// </summary>
        int Count { get; }
    }

    /// <summary>
    /// In-memory thread-safe implementation of <see cref="IL2QueryCache"/> with sliding/absolute TTL,
    /// bounded capacity, and tag tracking.
    /// </summary>
    public class MemoryL2QueryCache : IL2QueryCache
    {
        private class CacheEntry
        {
            public object Value { get; set; }
            public DateTime ExpiresAtUtc { get; set; }
            public List<Type> EntityTypes { get; set; }
        }

        /// <summary>
        /// Maximum number of cache entries to retain in memory before triggering eviction.
        /// Default is 10,000 entries.
        /// </summary>
        public int MaxCapacity { get; set; } = 10000;

        /// <summary>
        /// Gets the current count of active cache entries.
        /// </summary>
        public int Count => _cache.Count;

        private readonly ConcurrentDictionary<string, CacheEntry> _cache
            = new ConcurrentDictionary<string, CacheEntry>(StringComparer.Ordinal);

        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, byte>> _typeKeys
            = new ConcurrentDictionary<Type, ConcurrentDictionary<string, byte>>();

        public bool TryGet<T>(string key, out T value)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (DateTime.UtcNow < entry.ExpiresAtUtc)
                {
                    value = (T)entry.Value;
                    return true;
                }

                // Expired
                _cache.TryRemove(key, out _);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Scans and purges all expired entries from memory.
        /// Returns the number of purged entries.
        /// </summary>
        public int PruneExpired()
        {
            var now = DateTime.UtcNow;
            int pruned = 0;
            foreach (var kvp in _cache)
            {
                if (now >= kvp.Value.ExpiresAtUtc)
                {
                    if (_cache.TryRemove(kvp.Key, out _))
                        pruned++;
                }
            }
            return pruned;
        }

        public void Set<T>(string key, T value, TimeSpan duration, IEnumerable<Type> impactedEntityTypes = null)
        {
            if (_cache.Count >= MaxCapacity)
            {
                PruneExpired();

                // If still exceeding or at capacity, evict earliest expiring entry
                if (_cache.Count >= MaxCapacity)
                {
                    string oldestKey = null;
                    DateTime earliest = DateTime.MaxValue;
                    foreach (var kvp in _cache)
                    {
                        if (kvp.Value.ExpiresAtUtc < earliest)
                        {
                            earliest = kvp.Value.ExpiresAtUtc;
                            oldestKey = kvp.Key;
                        }
                    }
                    if (oldestKey != null)
                    {
                        _cache.TryRemove(oldestKey, out _);
                    }
                }
            }

            var entry = new CacheEntry
            {
                Value = value,
                ExpiresAtUtc = DateTime.UtcNow.Add(duration),
                EntityTypes = impactedEntityTypes != null ? new List<Type>(impactedEntityTypes) : new List<Type>()
            };

            _cache[key] = entry;

            if (impactedEntityTypes != null)
            {
                foreach (var type in impactedEntityTypes)
                {
                    var keys = _typeKeys.GetOrAdd(type, _ => new ConcurrentDictionary<string, byte>());
                    keys[key] = 1;
                }
            }
        }

        public void Invalidate(Type entityType)
        {
            if (entityType == null) return;

            if (_typeKeys.TryRemove(entityType, out var keys))
            {
                foreach (var key in keys.Keys)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }

        public void InvalidateAll()
        {
            _cache.Clear();
            _typeKeys.Clear();
        }
    }
}
