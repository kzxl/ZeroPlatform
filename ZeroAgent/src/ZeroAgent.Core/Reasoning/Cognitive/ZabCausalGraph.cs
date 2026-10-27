using System;
using System.Collections.Generic;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Quantization;

namespace ZeroAgent.Core.Reasoning.Cognitive
{
    /// <summary>
    /// Represents an edge in the Causal Memory Directed Acyclic Graph (DAG).
    /// Maps Condition -> Action -> Observed Outcome with associated Reward signal.
    /// </summary>
    public sealed class ZabCausalEdge
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Condition { get; set; } = string.Empty;
        public string ActionTaken { get; set; } = string.Empty;
        public string ObservedOutcome { get; set; } = string.Empty;
        public float Reward { get; set; } // +1.0 for success, -1.0 for catastrophic failure
        public int ObservationCount { get; set; } = 1;
        public DateTime LastObservedUtc { get; set; } = DateTime.UtcNow;

        public int Dimension { get; set; }
        public float Scale { get; set; }
        public float Offset { get; set; }
        public sbyte[]? ConditionVectorSq8 { get; set; }

        public unsafe float ComputeConditionSimilarity(ReadOnlySpan<float> queryVector, float querySum)
        {
            if (ConditionVectorSq8 == null || ConditionVectorSq8.Length != Dimension || queryVector.Length != Dimension)
                return 0.0f;

            fixed (float* qPtr = queryVector)
            fixed (sbyte* cPtr = ConditionVectorSq8)
            {
                return BinaryQuantizer.CosineSimilaritySq8Hoisted(qPtr, cPtr, Dimension, Scale, Offset, querySum);
            }
        }
    }

    /// <summary>
    /// Directed Causal Memory Graph engine for counterfactual evaluation and anticipatory foresight.
    /// Enables agents to ask: "If I take Action A under Condition C, what outcome and reward have we observed previously?"
    /// </summary>
    public sealed class ZabCausalGraph
    {
        private readonly List<ZabCausalEdge> _edges = new List<ZabCausalEdge>();
        private readonly object _lock = new object();

        public IReadOnlyList<ZabCausalEdge> Edges
        {
            get
            {
                lock (_lock) return _edges.ToArray();
            }
        }

        public int Count
        {
            get
            {
                lock (_lock) return _edges.Count;
            }
        }

        /// <summary>
        /// Records or updates an observed causal transition.
        /// </summary>
        public ZabCausalEdge RecordTransition(
            string condition,
            string actionTaken,
            string observedOutcome,
            float reward,
            ReadOnlySpan<float> conditionEmbedding = default)
        {
            if (string.IsNullOrWhiteSpace(condition)) throw new ArgumentNullException(nameof(condition));
            if (string.IsNullOrWhiteSpace(actionTaken)) throw new ArgumentNullException(nameof(actionTaken));

            lock (_lock)
            {
                // Check if matching condition-action edge exists
                var existing = _edges.Find(e =>
                    string.Equals(e.Condition, condition, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.ActionTaken, actionTaken, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.ObservationCount++;
                    // Exponential Moving Average (EMA) update on reward
                    existing.Reward = existing.Reward * 0.7f + reward * 0.3f;
                    existing.ObservedOutcome = observedOutcome;
                    existing.LastObservedUtc = DateTime.UtcNow;
                    return existing;
                }

                var edge = new ZabCausalEdge
                {
                    Condition = condition,
                    ActionTaken = actionTaken,
                    ObservedOutcome = observedOutcome ?? string.Empty,
                    Reward = reward,
                    ObservationCount = 1,
                    LastObservedUtc = DateTime.UtcNow
                };

                if (!conditionEmbedding.IsEmpty)
                {
                    float[] norm = conditionEmbedding.ToArray();
                    VectorMetrics.NormalizeL2(norm);

                    sbyte[] qVec = new sbyte[norm.Length];
                    BinaryQuantizer.QuantizeSQ8(norm, qVec, out float scale, out float offset);
                    edge.Dimension = norm.Length;
                    edge.Scale = scale;
                    edge.Offset = offset;
                    edge.ConditionVectorSq8 = qVec;
                }

                _edges.Add(edge);
                return edge;
            }
        }

        /// <summary>
        /// Evaluates a candidate action under the current condition vector.
        /// Returns predicted outcome, expected reward, and historical confidence.
        /// </summary>
        public (bool HasPriorExperience, string PredictedOutcome, float ExpectedReward, float Confidence) EvaluateAction(
            ReadOnlySpan<float> conditionEmbedding,
            string candidateAction,
            float minSimilarity = 0.70f)
        {
            if (conditionEmbedding.IsEmpty || string.IsNullOrWhiteSpace(candidateAction))
                return (false, string.Empty, 0.0f, 0.0f);

            float[] norm = conditionEmbedding.ToArray();
            VectorMetrics.NormalizeL2(norm);
            float querySum = BinaryQuantizer.ComputeVectorSum(norm);

            lock (_lock)
            {
                ZabCausalEdge? bestEdge = null;
                float bestSim = float.MinValue;

                for (int i = 0; i < _edges.Count; i++)
                {
                    var edge = _edges[i];
                    if (!string.Equals(edge.ActionTaken, candidateAction, StringComparison.OrdinalIgnoreCase))
                        continue;

                    float sim = edge.ComputeConditionSimilarity(norm, querySum);
                    if (sim >= minSimilarity && sim > bestSim)
                    {
                        bestSim = sim;
                        bestEdge = edge;
                    }
                }

                if (bestEdge != null)
                {
                    float confidence = Math.Min(1.0f, bestSim * (0.8f + Math.Min(bestEdge.ObservationCount, 5) * 0.04f));
                    return (true, bestEdge.ObservedOutcome, bestEdge.Reward, confidence);
                }

                return (false, string.Empty, 0.0f, 0.0f);
            }
        }
    }
}
