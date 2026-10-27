using System;
using System.Collections.Generic;
using ZeroAgent.Core.Context;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Core.Reasoning.Cache
{
    /// <summary>
    /// Represents a cached agent solution / trajectory verified by prior execution or critic approval.
    /// </summary>
    public sealed class CachedPlan
    {
        public string Goal { get; }
        public float[] Embedding { get; }
        public string Solution { get; }
        public IReadOnlyList<AgentMessage> ExecutionTrace { get; }
        public int Steps { get; }
        public float Confidence { get; }
        public DateTime CachedAtUtc { get; }
        public int HitCount { get; set; }

        public CachedPlan(
            string goal,
            float[] embedding,
            string solution,
            IReadOnlyList<AgentMessage> executionTrace,
            int steps,
            float confidence = 1.0f)
        {
            Goal = goal ?? string.Empty;
            Embedding = embedding ?? Array.Empty<float>();
            Solution = solution ?? string.Empty;
            ExecutionTrace = executionTrace ?? Array.Empty<AgentMessage>();
            Steps = steps;
            Confidence = confidence;
            CachedAtUtc = DateTime.UtcNow;
            HitCount = 0;
        }

        public AgentResponse ToAgentResponse(TimeSpan lookupElapsed)
        {
            return new AgentResponse(true, Solution, Steps, lookupElapsed, ExecutionTrace);
        }
    }

    /// <summary>
    /// Trajectory & Plan Semantic Cache:
    /// Caches high-confidence, verified agent solutions backed by dense vector similarity.
    /// Provides sub-0.1ms instant solution replay for recurrent or semantically identical user queries,
    /// bypassing expensive multi-step ReAct or Tree-of-Thought deliberations.
    /// </summary>
    public sealed class AgentPlanSemanticCache
    {
        private readonly List<CachedPlan> _cache = new List<CachedPlan>();
        private readonly object _lock = new object();

        public int Count
        {
            get { lock (_lock) return _cache.Count; }
        }

        /// <summary>
        /// Stores a verified agent execution result in the plan semantic cache.
        /// </summary>
        public void CachePlan(
            string goal,
            ReadOnlySpan<float> goalEmbedding,
            AgentResponse response,
            float confidence = 1.0f)
        {
            if (string.IsNullOrWhiteSpace(goal) || response == null || !response.Success) return;

            float[] embCopy = goalEmbedding.ToArray();
            VectorMetrics.NormalizeL2(embCopy);

            var plan = new CachedPlan(
                goal: goal,
                embedding: embCopy,
                solution: response.Output,
                executionTrace: response.ExecutionTrace,
                steps: response.TotalSteps,
                confidence: confidence);

            lock (_lock)
            {
                _cache.Add(plan);
            }
        }

        /// <summary>
        /// Performs sub-millisecond semantic lookup for a matching verified plan.
        /// </summary>
        public CachedPlan? TryGetPlan(ReadOnlySpan<float> queryEmbedding, float minSimilarity = 0.90f)
        {
            if (queryEmbedding.IsEmpty) return null;

            CachedPlan? bestMatch = null;
            float maxSim = minSimilarity;

            lock (_lock)
            {
                for (int i = 0; i < _cache.Count; i++)
                {
                    var plan = _cache[i];
                    if (plan.Embedding.Length != queryEmbedding.Length) continue;

                    float sim = VectorMetrics.CosineSimilarity(queryEmbedding, plan.Embedding);
                    if (sim >= maxSim)
                    {
                        maxSim = sim;
                        bestMatch = plan;
                    }
                }

                if (bestMatch != null)
                {
                    bestMatch.HitCount++;
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Clears all cached plans.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _cache.Clear();
            }
        }
    }
}
