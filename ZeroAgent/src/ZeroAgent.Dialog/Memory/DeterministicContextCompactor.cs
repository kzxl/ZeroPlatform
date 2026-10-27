using System;
using System.Collections.Generic;
using System.Text;
using ZeroAgent.Core.Context;

namespace ZeroAgent.Dialog.Memory
{
    /// <summary>
    /// Deterministic, sub-millisecond context compactor written in 100% pure C#.
    /// Extracts active entities, chronological milestones, and verified actions without requiring external LLM inference.
    /// Operates at Tier 1 (Reflex) speed (&lt;0.05ms) with zero GPU overhead.
    /// </summary>
    public sealed partial class DeterministicContextCompactor : IContextCompactor
    {
        public static DeterministicContextCompactor Instance { get; } = new DeterministicContextCompactor();

        public const int MaxPreservedMilestones = 12;

        public string CompactObservation(string toolName, string rawObservation, int maxChars = 400)
        {
            return ObservationCompactor.Compact(toolName, rawObservation, maxChars);
        }

        public int EstimateTokens(string text)
        {
            return ContextBudgetManager.FastTokenEstimator(text);
        }

        public string CompactTurns(
            IReadOnlyList<DialogTurn> turnsToCompact,
            IReadOnlyDictionary<string, string> activeSlots,
            string? existingSummary)
        {
            if (turnsToCompact == null || turnsToCompact.Count == 0)
            {
                return existingSummary ?? string.Empty;
            }

            var priorMilestones = ExtractPriorMilestones(existingSummary, out int historicalCollapsedTurns);

            // Add new milestones from turnsToCompact
            foreach (var turn in turnsToCompact)
            {
                string summaryLine = FormatTurnMilestone(turn);
                if (!string.IsNullOrWhiteSpace(summaryLine))
                {
                    priorMilestones.Add(summaryLine);
                }
            }

            // Bound milestones to prevent summary blowup
            if (priorMilestones.Count > MaxPreservedMilestones)
            {
                int excess = priorMilestones.Count - MaxPreservedMilestones;
                priorMilestones.RemoveRange(0, excess);
                historicalCollapsedTurns += excess;
            }

            var sb = new StringBuilder();
            sb.AppendLine("<CONTEXT_SUMMARY>");

            // 1. Active Entities & State
            sb.AppendLine("# Active Entities & State:");
            string entityLine = FormatActiveEntities(activeSlots);
            sb.AppendLine(string.IsNullOrEmpty(entityLine) ? "- No active entities currently tracked." : entityLine);

            // 2. Milestones & Verified Decisions
            sb.AppendLine("# Milestones & Verified Decisions:");
            if (historicalCollapsedTurns > 0)
            {
                sb.AppendLine($"- [Prior History]: Aggregated {historicalCollapsedTurns} earlier dialogue turns and actions.");
            }

            foreach (var line in priorMilestones)
            {
                sb.AppendLine(line);
            }

            // 3. Status Directives
            sb.AppendLine("# Directives & Session Status:");
            sb.AppendLine($"- Compacted: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Active milestones: {priorMilestones.Count}");
            sb.Append("</CONTEXT_SUMMARY>");

            return sb.ToString();
        }
    }
}
