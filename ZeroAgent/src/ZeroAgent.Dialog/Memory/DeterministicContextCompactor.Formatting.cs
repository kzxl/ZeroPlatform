using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeroAgent.Dialog.Memory
{
    public sealed partial class DeterministicContextCompactor
    {
        private static List<string> ExtractPriorMilestones(string? existingSummary, out int historicalCollapsedTurns)
        {
            historicalCollapsedTurns = 0;
            var milestones = new List<string>();
            if (string.IsNullOrWhiteSpace(existingSummary)) return milestones;

            string[] lines = existingSummary!.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            bool inMilestonesSection = false;

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("# Milestones", StringComparison.OrdinalIgnoreCase))
                {
                    inMilestonesSection = true;
                    continue;
                }
                if (inMilestonesSection && line.StartsWith("#", StringComparison.OrdinalIgnoreCase))
                {
                    inMilestonesSection = false;
                    continue;
                }

                if (inMilestonesSection && line.StartsWith("-"))
                {
                    // Check for prior collapsed count
                    var match = Regex.Match(line, @"Aggregated\s+(\d+)\s+earlier", RegexOptions.IgnoreCase);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
                    {
                        historicalCollapsedTurns = count;
                    }
                    else
                    {
                        milestones.Add(line);
                    }
                }
            }

            return milestones;
        }

        private static string FormatTurnMilestone(DialogTurn turn)
        {
            if (turn == null) return string.Empty;

            string query = TruncateAndFlatten(turn.UserMessage, 60);
            string response = TruncateAndFlatten(turn.BotResponse, 75);
            string intent = string.IsNullOrEmpty(turn.Intent) ? "UNKNOWN" : turn.Intent;

            return $"- [{intent}] User: \"{query}\" -> Result: \"{response}\"";
        }

        private static string FormatActiveEntities(IReadOnlyDictionary<string, string>? activeSlots)
        {
            if (activeSlots == null || activeSlots.Count == 0) return string.Empty;

            var sb = new StringBuilder("- ");
            bool first = true;
            foreach (var kvp in activeSlots)
            {
                if (!first) sb.Append(" | ");
                sb.Append($"{kvp.Key}: {kvp.Value}");
                first = false;
            }
            return sb.ToString();
        }

        private static string TruncateAndFlatten(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string flat = text.Replace("\r", " ").Replace("\n", " ").Trim();
            // Collapse multiple spaces
            flat = Regex.Replace(flat, @"\s+", " ");
            if (flat.Length <= maxChars) return flat;
            return flat.Substring(0, maxChars - 3) + "...";
        }
    }
}
