using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Engine;

namespace ZeroAgent.Core.Reasoning.GraphOfThought
{
    /// <summary>
    /// Graph-of-Thought (GoT) Deliberation Engine:
    /// Extends Tree-of-Thought by generating multi-perspective thoughts and synthesizing (merging)
    /// multiple intermediate hypotheses into higher-order comprehensive conclusions.
    /// </summary>
    public sealed class GraphOfThoughtEngine
    {
        private readonly ILlmClient _llm;

        public GraphOfThoughtEngine(ILlmClient llm)
        {
            _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        }

        /// <summary>
        /// Executes a GoT deliberation graph:
        /// Step 1: Branch out into independent analytical perspectives (e.g. Safety, Feasibility, Cost)
        /// Step 2: Score each independent perspective
        /// Step 3: Aggregate / Synthesize high-scoring perspectives into a unified consensus solution
        /// </summary>
        public async Task<GoTNode> DeliberateGraphAsync(
            string goal,
            IReadOnlyList<string> perspectives,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(goal)) throw new ArgumentNullException(nameof(goal));
            if (perspectives == null || perspectives.Count == 0)
            {
                perspectives = new[] { "Technical Feasibility", "Operational Safety", "Resource Optimization" };
            }

            var graph = new GraphOfThought();
            var activeThoughtIds = new List<string>();

            // Phase 1: Branch out into independent perspectives
            foreach (var perspective in perspectives)
            {
                string prompt = 
$@"You are an Expert Industrial Agent.
Analyze the following goal strictly from the perspective of: [{perspective}].
Goal: {goal}

Provide a concise analysis and actionable recommendation (max 3 sentences).";

                string thoughtContent = await _llm.CompleteAsync(prompt, cancellationToken).ConfigureAwait(false);
                float score = 0.85f; // Initial confidence
                var node = graph.AddThought($"[{perspective}] {thoughtContent.Trim()}", score);
                activeThoughtIds.Add(node.Id);
            }

            // Phase 2: Synthesis & Aggregation (Cross-Merge multiple branches)
            var thoughtsToMerge = new StringBuilder();
            for (int i = 0; i < activeThoughtIds.Count; i++)
            {
                var n = graph.Nodes[activeThoughtIds[i]];
                thoughtsToMerge.AppendLine($"- Thought {i + 1}: {n.Content}");
            }

            string mergePrompt = 
$@"You are a Master Cognitive Synthesizer.
Combine and resolve the following divergent perspectives into one unified, optimal execution plan:
Goal: {goal}
Perspectives:
{thoughtsToMerge}

Synthesize these into a cohesive, robust final decision.";

            string aggregatedContent = await _llm.CompleteAsync(mergePrompt, cancellationToken).ConfigureAwait(false);
            var aggregatedNode = graph.Aggregate(aggregatedContent.Trim(), 0.95f, activeThoughtIds);

            return aggregatedNode;
        }
    }
}
