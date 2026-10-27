using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Represents an individual command item in the command palette.
    /// </summary>
    public class CommandItem
    {
        /// <summary>
        /// Gets or sets the unique identifier for the command.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the label to display for the command.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category of the command.
        /// </summary>
        public string? Category { get; set; }

        /// <summary>
        /// Gets or sets the shortcut text to display (e.g., "Ctrl+K").
        /// </summary>
        public string? ShortcutText { get; set; }

        /// <summary>
        /// Gets or sets the glyph character or identifier for the command.
        /// </summary>
        public string? Glyph { get; set; }

        /// <summary>
        /// Gets or sets the description of the command.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the action to execute when the command is selected.
        /// </summary>
        public Action? Execute { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the command is enabled.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets additional tokens to use for searching.
        /// </summary>
        public string[] SearchTokens { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Event arguments for when a command is selected.
    /// </summary>
    public class CommandSelectedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the command that was selected.
        /// </summary>
        public CommandItem Command { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandSelectedEventArgs"/> class.
        /// </summary>
        /// <param name="command">The selected command.</param>
        public CommandSelectedEventArgs(CommandItem command)
        {
            Command = command;
        }
    }

    /// <summary>
    /// Helper class for filtering commands.
    /// </summary>
    public static class CommandFilterHelper
    {
        /// <summary>
        /// Filters a sequence of commands based on a query string.
        /// </summary>
        /// <param name="items">The items to filter.</param>
        /// <param name="query">The search query.</param>
        /// <param name="maxResults">The maximum number of results to return.</param>
        /// <returns>A list of filtered commands.</returns>
        public static List<CommandItem> Filter(IEnumerable<CommandItem> items, string? query, int maxResults = 30)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return items.Take(maxResults).ToList();
            }

            var lowerQuery = query.ToLowerInvariant();
            
            return items
                .Select(item => new { Item = item, Score = CalculateScore(item, lowerQuery) })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Item.Label)
                .Take(maxResults)
                .Select(x => x.Item)
                .ToList();
        }

        private static int CalculateScore(CommandItem item, string query)
        {
            if (!item.IsEnabled) return 0;
            
            int score = 0;
            var lowerLabel = item.Label?.ToLowerInvariant() ?? string.Empty;
            var lowerCategory = item.Category?.ToLowerInvariant() ?? string.Empty;
            var lowerDescription = item.Description?.ToLowerInvariant() ?? string.Empty;

            if (lowerLabel.StartsWith(query))
                score += 100;
            else if (lowerLabel.Contains(query))
                score += 50;

            if (lowerCategory.StartsWith(query))
                score += 30;
            else if (lowerCategory.Contains(query))
                score += 15;
                
            if (lowerDescription.Contains(query))
                score += 10;
                
            foreach(var token in item.SearchTokens)
            {
                var lowerToken = token?.ToLowerInvariant() ?? string.Empty;
                if (lowerToken.StartsWith(query))
                    score += 25;
                else if (lowerToken.Contains(query))
                    score += 10;
            }

            return score;
        }
    }
}
