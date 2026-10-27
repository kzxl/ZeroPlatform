using System;
using System.Collections.Concurrent;
using System.Drawing;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Icons
{
    /// <summary>
    /// Thread-safe high-performance cache for rendered icon bitmaps.
    /// Eliminates redundant bitmap allocations and manages GDI resources cleanly across theme changes.
    /// </summary>
    public static class ZeroIconCache
    {
        private readonly struct CacheKey : IEquatable<CacheKey>
        {
            public readonly string Key;
            public readonly int Size;
            public readonly int Argb;

            public CacheKey(string key, int size, int argb)
            {
                Key = key;
                Size = size;
                Argb = argb;
            }

            public bool Equals(CacheKey other) =>
                Size == other.Size && Argb == other.Argb && string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Key ?? string.Empty);
                    hash = (hash * 397) ^ Size;
                    hash = (hash * 397) ^ Argb;
                    return hash;
                }
            }
        }

        private static readonly ConcurrentDictionary<CacheKey, Bitmap> _cache = new ConcurrentDictionary<CacheKey, Bitmap>();

        static ZeroIconCache()
        {
            ZeroTheme.ThemeChanged += (s, e) => Clear();
        }

        /// <summary>
        /// Gets the current number of cached bitmap icons in memory.
        /// </summary>
        public static int Count => _cache.Count;

        /// <summary>
        /// Retrieves an existing cached bitmap or creates, caches, and returns a new bitmap using the specified factory.
        /// </summary>
        public static Bitmap GetOrCreate(string key, int size, Color color, Func<Bitmap> factory)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            var cacheKey = new CacheKey(key, size, color.ToArgb());
            return _cache.GetOrAdd(cacheKey, _ => factory());
        }

        /// <summary>
        /// Disposes all cached GDI bitmaps and clears the icon cache to free system resources.
        /// </summary>
        public static void Clear()
        {
            var keys = _cache.Keys;
            foreach (var k in keys)
            {
                if (_cache.TryRemove(k, out var bmp) && bmp != null)
                {
                    try
                    {
                        bmp.Dispose();
                    }
                    catch
                    {
                        // Ignore already disposed handles
                    }
                }
            }
        }
    }
}
