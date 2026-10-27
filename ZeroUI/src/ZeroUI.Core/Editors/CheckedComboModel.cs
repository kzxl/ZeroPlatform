using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Presentation format for selected items within checked combo boxes.
    /// </summary>
    public enum CheckedComboDisplayMode
    {
        /// <summary>
        /// Comma-separated or summary text (e.g. "Item 1, Item 2" or "3 items selected").
        /// </summary>
        Text,

        /// <summary>
        /// Discrete chips/tokens with individual dismissal close buttons.
        /// </summary>
        Tokens
    }

    /// <summary>
    /// Shared business calculations and formatting utilities for checked combo box editors.
    /// </summary>
    public static class CheckedComboHelper
    {
        /// <summary>
        /// Formats display text from a collection of checked item strings using placeholder and summary fallback.
        /// </summary>
        public static string FormatDisplayText(
            IEnumerable<string>? checkedTexts,
            string placeholder,
            string summaryFormat,
            int maxInlineCount = 2)
        {
            if (checkedTexts == null) return placeholder ?? string.Empty;

            var list = checkedTexts as IList<string> ?? new List<string>(checkedTexts);
            if (list.Count == 0) return placeholder ?? string.Empty;

            if (list.Count <= maxInlineCount)
            {
                return string.Join(", ", list);
            }

            try
            {
                return string.Format(summaryFormat ?? "{0} items selected", list.Count);
            }
            catch
            {
                return $"{list.Count} items selected";
            }
        }

        /// <summary>
        /// Calculates the tri-state nullable boolean value for a "Select All" master checkbox.
        /// Returns <c>false</c> if no items checked (or total is 0), <c>true</c> if all items checked,
        /// or <c>null</c> (indeterminate) if partially checked.
        /// </summary>
        public static bool? CalculateSelectAllState(int checkedCount, int totalCount)
        {
            if (totalCount <= 0 || checkedCount <= 0) return false;
            if (checkedCount >= totalCount) return true;
            return null; // Indeterminate
        }
    }
}
