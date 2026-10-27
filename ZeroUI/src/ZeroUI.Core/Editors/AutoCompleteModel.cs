using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Data item for <see cref="IAutoCompleteSource"/> suggestion results.
    /// Supports rich display with primary text, sub-text, and category grouping.
    /// </summary>
    public class AutoCompleteItem
    {
        /// <summary>Display text shown as primary label in the suggestion dropdown.</summary>
        public string DisplayText { get; set; } = string.Empty;

        /// <summary>Optional secondary text shown below or beside the display text.</summary>
        public string SubText { get; set; } = string.Empty;

        /// <summary>Optional category for visual grouping in the dropdown.</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>Optional icon glyph character rendered before the display text.</summary>
        public string Glyph { get; set; } = string.Empty;

        /// <summary>User-defined payload object associated with this suggestion.</summary>
        public object? Tag { get; set; }

        public AutoCompleteItem() { }

        public AutoCompleteItem(string displayText, string subText = "", string category = "")
        {
            DisplayText = displayText ?? string.Empty;
            SubText = subText ?? string.Empty;
            Category = category ?? string.Empty;
        }

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// Event arguments raised when an <see cref="AutoCompleteItem"/> is selected from the suggestion dropdown.
    /// </summary>
    public class AutoCompleteItemSelectedEventArgs : EventArgs
    {
        /// <summary>The selected suggestion item.</summary>
        public AutoCompleteItem SelectedItem { get; }

        /// <summary>The current search text at the time of selection.</summary>
        public string SearchText { get; }

        public AutoCompleteItemSelectedEventArgs(AutoCompleteItem selectedItem, string searchText)
        {
            SelectedItem = selectedItem ?? throw new ArgumentNullException(nameof(selectedItem));
            SearchText = searchText ?? string.Empty;
        }
    }

    /// <summary>
    /// High-performance substring filter for <see cref="AutoCompleteItem"/> collections.
    /// </summary>
    public static class AutoCompleteFilterHelper
    {
        /// <summary>
        /// Checks whether an <see cref="AutoCompleteItem"/> matches a search query
        /// across DisplayText, SubText, and Category fields (case-insensitive).
        /// </summary>
        public static bool Matches(AutoCompleteItem item, string? query)
        {
            if (item == null) return false;
            if (string.IsNullOrWhiteSpace(query)) return true;

            return item.DisplayText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || item.SubText.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || item.Category.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Filters a collection of <see cref="AutoCompleteItem"/> using substring matching.
        /// Prioritizes: exact prefix match first, then contains match.
        /// </summary>
        public static List<AutoCompleteItem> Filter(IEnumerable<AutoCompleteItem> items, string? query, int maxResults = 20)
        {
            if (items == null) return new List<AutoCompleteItem>();
            if (string.IsNullOrWhiteSpace(query))
            {
                var all = new List<AutoCompleteItem>();
                foreach (var item in items)
                {
                    if (all.Count >= maxResults) break;
                    all.Add(item);
                }
                return all;
            }

            var prefixMatches = new List<AutoCompleteItem>();
            var containsMatches = new List<AutoCompleteItem>();

            foreach (var item in items)
            {
                if (prefixMatches.Count + containsMatches.Count >= maxResults) break;

                if (item.DisplayText.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                    prefixMatches.Add(item);
                else if (Matches(item, query))
                    containsMatches.Add(item);
            }

            prefixMatches.AddRange(containsMatches);
            return prefixMatches;
        }
    }
}
