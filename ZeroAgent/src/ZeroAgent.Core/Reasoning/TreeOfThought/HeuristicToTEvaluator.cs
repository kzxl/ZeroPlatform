using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;

namespace ZeroAgent.Core.Reasoning.TreeOfThought
{
    /// <summary>
    /// Default heuristic evaluator for Tree-of-Thought search nodes.
    /// Analyzes terminal status, observation success/error signals, and user-provided custom heuristics.
    /// </summary>
    public sealed class HeuristicToTEvaluator : IToTEvaluator
    {
        public Func<AgentContext, ThoughtNode, float?>? CustomScorer { get; set; }

        public Task<float> EvaluateNodeAsync(
            AgentContext context,
            ThoughtNode node,
            CancellationToken cancellationToken = default)
        {
            if (node == null) return Task.FromResult(0.0f);

            // 1. Custom scorer override
            if (CustomScorer != null)
            {
                var custom = CustomScorer(context, node);
                if (custom.HasValue)
                {
                    return Task.FromResult(Math.Max(0.0f, Math.Min(1.0f, custom.Value)));
                }
            }

            // 2. Terminal answer evaluation
            if (node.IsTerminal)
            {
                return Task.FromResult(!string.IsNullOrWhiteSpace(node.FinalAnswer) ? 0.95f : 0.30f);
            }

            // 3. Observation error signals
            if (!string.IsNullOrEmpty(node.Observation))
            {
                string obs = node.Observation;
                if (obs.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    obs.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    obs.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    obs.IndexOf("circuit breaker", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return Task.FromResult(0.20f); // Penalize dead end / error
                }

                // Successful non-empty observation
                return Task.FromResult(0.75f);
            }

            // Default intermediate score
            return Task.FromResult(0.50f);
        }
    }
}
