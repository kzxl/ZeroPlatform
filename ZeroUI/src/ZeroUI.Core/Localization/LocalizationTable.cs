using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif

namespace ZeroUI.Core.Localization
{
    /// <summary>
    /// Thread-safe immutable string dictionary table for a specific culture.
    /// Utilizes FrozenDictionary on .NET 8+ and highly-optimized Ordinal Dictionary on .NET Framework.
    /// Supports self-healing memoization cache for hierarchical fallback entries.
    /// </summary>
    public sealed class LocalizationTable
    {
#if NET8_0_OR_GREATER
        private readonly FrozenDictionary<string, string> _frozenMap;
#else
        private readonly Dictionary<string, string> _map;
#endif
        private readonly ConcurrentDictionary<string, string> _memoizedFallback = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        public string CultureCode { get; }

        public LocalizationTable(string cultureCode, IDictionary<string, string> source)
        {
            CultureCode = cultureCode ?? string.Empty;

#if NET8_0_OR_GREATER
            _frozenMap = source.ToFrozenDictionary(StringComparer.Ordinal);
#else
            _map = new Dictionary<string, string>(source, StringComparer.Ordinal);
#endif
        }

        public bool TryGetValue(string key, out string value)
        {
#if NET8_0_OR_GREATER
            if (_frozenMap.TryGetValue(key, out value!)) return true;
#else
            if (_map.TryGetValue(key, out value!)) return true;
#endif
            if (_memoizedFallback.TryGetValue(key, out value!)) return true;

            value = string.Empty;
            return false;
        }

        /// <summary>
        /// Caches a resolved fallback key into this table for instant O(1) retrieval on future lookups.
        /// </summary>
        internal void Memoize(string key, string value)
        {
            _memoizedFallback.TryAdd(key, value);
        }

        public int Count
        {
            get
            {
#if NET8_0_OR_GREATER
                return _frozenMap.Count + _memoizedFallback.Count;
#else
                return _map.Count + _memoizedFallback.Count;
#endif
            }
        }
    }
}
