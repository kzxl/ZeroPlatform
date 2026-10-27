using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Context
{
    /// <summary>
    /// Knapsack-inspired token budgeting engine for agent context windows.
    /// Evaluates token density and bounds dialogue turns and system payloads within strict LLM token limits.
    /// </summary>
    public sealed class ContextBudgetManager
    {
        private readonly Func<string, int> _tokenEstimator;

        public ContextBudgetManager(Func<string, int>? tokenEstimator = null)
        {
            _tokenEstimator = tokenEstimator ?? FastTokenEstimator;
        }

        /// <summary>
        /// Fast heuristic token estimator for English and Vietnamese UTF-8 text (~3.5 chars/token).
        /// </summary>
        public static int FastTokenEstimator(string? text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            return Math.Max(1, (text!.Length + 2) / 3);
        }

        public int EstimateTokens(string? text) => _tokenEstimator(text ?? string.Empty);

        public int EstimateContextTokens(AgentContext context)
        {
            if (context == null) return 0;
            int total = EstimateTokens(context.Goal);
            for (int i = 0; i < context.History.Count; i++)
            {
                total += EstimateTokens(context.History[i].Content);
            }
            return total;
        }

        /// <summary>
        /// Prunes history items from the head when total context tokens exceed maxTokens.
        /// Retains System instructions and the latest messages.
        /// </summary>
        public int PruneContextToBudget(AgentContext context, int maxTokens, out List<AgentMessage> evictedMessages)
        {
            evictedMessages = new List<AgentMessage>();
            if (context == null || maxTokens <= 0 || context.History.Count <= 1) return 0;

            int currentTokens = EstimateContextTokens(context);
            int evictedCount = 0;

            int i = 0;
            while (currentTokens > maxTokens && i < context.History.Count)
            {
                var msg = context.History[i];
                // Keep leading system directives if possible
                if (msg.Role == AgentRole.System && context.History.Count > 2)
                {
                    i++;
                    continue;
                }

                context.History.RemoveAt(i);
                evictedMessages.Add(msg);
                currentTokens -= EstimateTokens(msg.Content);
                evictedCount++;
            }

            return evictedCount;
        }

        public int PruneContextToBudget(AgentContext context, int maxTokens)
        {
            return PruneContextToBudget(context, maxTokens, out _);
        }
    }
}
