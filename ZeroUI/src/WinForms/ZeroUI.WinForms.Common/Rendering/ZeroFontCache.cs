using System;
using System.Collections.Concurrent;
using System.Drawing;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Rendering
{
    /// <summary>
    /// High-performance thread-safe GDI+ Font Cache.
    /// Eliminates Win32 HFONT allocation and managed Font GC heap churn on hot rendering paths.
    /// </summary>
    public static class ZeroFontCache
    {
        private static readonly ConcurrentDictionary<FontKey, Font> _cache = new ConcurrentDictionary<FontKey, Font>();

        static ZeroFontCache()
        {
            ZeroUIConfig.FontChanged += (s, e) => Clear();
        }

        private readonly struct FontKey : IEquatable<FontKey>
        {
            public readonly string Family;
            public readonly float Size;
            public readonly FontStyle Style;
            private readonly int _hashCode;

            public FontKey(string family, float size, FontStyle style)
            {
                Family = family;
                Size = size;
                Style = style;
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + (family != null ? family.GetHashCode() : 0);
                    hash = hash * 31 + size.GetHashCode();
                    hash = hash * 31 + (int)style;
                    _hashCode = hash;
                }
            }

            public bool Equals(FontKey other) =>
                string.Equals(Family, other.Family, StringComparison.OrdinalIgnoreCase) &&
                Size.Equals(other.Size) &&
                Style == other.Style;

            public override bool Equals(object? obj) => obj is FontKey other && Equals(other);
            public override int GetHashCode() => _hashCode;
        }

        /// <summary>
        /// Retrieves or creates a cached Font instance.
        /// </summary>
        public static Font Get(string family, float size, FontStyle style = FontStyle.Regular)
        {
            var key = new FontKey(family ?? "Segoe UI", size, style);
            if (_cache.TryGetValue(key, out var cachedFont))
            {
                try
                {
                    _ = cachedFont.Height;
                    return cachedFont;
                }
                catch (Exception)
                {
                    _cache.TryRemove(key, out _);
                }
            }
            return _cache.GetOrAdd(key, k => new Font(k.Family, k.Size, k.Style));
        }

        /// <summary>
        /// Retrieves or creates a cached Font instance using the global ZeroUIConfig default family.
        /// </summary>
        public static Font Get(float size, FontStyle style = FontStyle.Regular)
        {
            return Get(ZeroUIConfig.DefaultFont.FontFamily.Name, size, style);
        }

        /// <summary>
        /// Clears all cached fonts and disposes native GDI handles.
        /// </summary>
        public static void Clear()
        {
            foreach (var kvp in _cache)
            {
                try { kvp.Value.Dispose(); } catch { }
            }
            _cache.Clear();
        }
    }
}
