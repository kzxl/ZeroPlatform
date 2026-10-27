using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Discrete data item represented in lookup and search dropdown editors.
    /// Supports multi-attribute display (Key, DisplayText, SubText, Category) and custom user payload object.
    /// </summary>
    public class LookUpItem
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string SubText { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public object? Tag { get; set; }

        public LookUpItem() { }

        public LookUpItem(string key, string displayText, string subText = "", string category = "")
        {
            Key = key ?? string.Empty;
            DisplayText = displayText ?? string.Empty;
            SubText = subText ?? string.Empty;
            Category = category ?? string.Empty;
        }

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="LookUpItem"/>.
    /// </summary>
    [Obsolete("ZeroLookupItem is deprecated. Use LookUpItem instead.")]
    public class ZeroLookupItem : LookUpItem
    {
        public ZeroLookupItem() { }
        public ZeroLookupItem(string key, string displayText, string subText = "", string category = "")
            : base(key, displayText, subText, category) { }
    }

    /// <summary>
    /// High-performance filtering utilities for <see cref="LookUpItem"/> collections.
    /// </summary>
    public static class LookUpFilterHelper
    {
        /// <summary>
        /// Checks whether a <see cref="LookUpItem"/> matches a search query across DisplayText, Key, SubText, and Category.
        /// </summary>
        public static bool Matches(LookUpItem item, string? query)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(query)) return true;

            var q = query!.Trim();
            return (item.DisplayText != null && item.DisplayText.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (item.Key != null && item.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (item.SubText != null && item.SubText.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (item.Category != null && item.Category.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Filters an enumeration of <see cref="LookUpItem"/> elements by search query up to <paramref name="maxResults"/>.
        /// </summary>
        public static List<LookUpItem> Filter(IEnumerable<LookUpItem> items, string? query, int maxResults = int.MaxValue)
        {
            var results = new List<LookUpItem>();
            if (items == null) return results;

            foreach (var item in items)
            {
                if (Matches(item, query))
                {
                    results.Add(item);
                    if (results.Count >= maxResults) break;
                }
            }

            return results;
        }
    }
}
