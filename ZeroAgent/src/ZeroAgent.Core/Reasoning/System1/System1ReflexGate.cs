using System;
using ZeroAgent.Core.Database;

namespace ZeroAgent.Core.Reasoning.System1
{
    /// <summary>
    /// Non-autoregressive System 1 Cognitive Reflex Gatekeeper modeled after human intuition and Laya.
    /// Evaluates user queries in sub-0.1ms using ZabDatabase's tri-primitive decision policies and semantic plan cache.
    /// Bypasses expensive System 2 LLM deliberations when confident, intercepts unsafe commands, or defers to System 2.
    /// </summary>
    public sealed class System1ReflexGate
    {
        private readonly ZabDatabase _db;
        private readonly float _planCacheThreshold;
        private readonly float _actionConfidenceThreshold;
        private readonly float _riskBlockThreshold;

        public ZabDatabase Database => _db;
        public float PlanCacheThreshold => _planCacheThreshold;
        public float ActionConfidenceThreshold => _actionConfidenceThreshold;
        public float RiskBlockThreshold => _riskBlockThreshold;

        public System1ReflexGate(
            ZabDatabase db,
            float planCacheThreshold = 0.90f,
            float actionConfidenceThreshold = 0.85f,
            float riskBlockThreshold = 0.80f)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _planCacheThreshold = planCacheThreshold;
            _actionConfidenceThreshold = actionConfidenceThreshold;
            _riskBlockThreshold = riskBlockThreshold;
        }

        /// <summary>
        /// Evaluates the user query in single-pass sub-0.1ms reflex time.
        /// </summary>
        public System1ReflexResult EvaluateReflex(string query, ReadOnlySpan<float> queryEmbedding)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return System1ReflexResult.Defer("Empty query");
            }

            // 1. Primitive 2: Score / Guardrail check (Risk Interception)
            if (_db.NeuralPolicy != null && !queryEmbedding.IsEmpty)
            {
                float riskScore = _db.PredictScore(queryEmbedding, "Risk");
                if (riskScore >= _riskBlockThreshold)
                {
                    return System1ReflexResult.Block($"Query flagged as high-risk by System 1 guardrail (Risk: {riskScore:P1})", riskScore);
                }
            }

            // 2. Semantic Plan Cache lookup (Instant Replay)
            if (!queryEmbedding.IsEmpty)
            {
                var cachedPlan = _db.LookupPlan(queryEmbedding, _planCacheThreshold);
                if (cachedPlan != null)
                {
                    return System1ReflexResult.ReplayPlan(cachedPlan, cachedPlan.Confidence);
                }
            }

            // 3. Primitive 1: Choice (Intent / Tool Routing)
            if (_db.NeuralPolicy != null && !queryEmbedding.IsEmpty)
            {
                string? action = _db.PredictChoice(queryEmbedding, out float confidence);
                if (!string.IsNullOrWhiteSpace(action))
                {
                    // If action explicitly indicates deep reasoning or escalation -> defer to System 2
                    if (string.Equals(action, "deep_think", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(action, "escalate_to_human", StringComparison.OrdinalIgnoreCase))
                    {
                        return System1ReflexResult.Defer($"System 1 classified intent as '{action}'.", confidence);
                    }

                    if (confidence >= _actionConfidenceThreshold)
                    {
                        return System1ReflexResult.Dispatch(action!, confidence);
                    }
                }
            }

            return System1ReflexResult.Defer("Uncertain or novel query requires System 2 deliberation.", 0.0f);
        }

        /// <summary>
        /// Distills a successful System 2 deliberative solution into System 1 reflexes.
        /// Caches verified trajectory in ZabDatabase and reinforces policy weights.
        /// </summary>
        public void DistillSuccess(
            string goal,
            string solution,
            int stepsCount,
            ReadOnlySpan<float> goalEmbedding,
            string? verifiedAction = null,
            float confidence = 1.0f)
        {
            if (string.IsNullOrWhiteSpace(goal) || string.IsNullOrWhiteSpace(solution)) return;

            // Cache verified plan into sovereign .zab database
            _db.CachePlan(goal, solution, stepsCount, confidence, goalEmbedding);

            // Adapt neural weights online if embedding and action are provided
            if (!goalEmbedding.IsEmpty && !string.IsNullOrWhiteSpace(verifiedAction))
            {
                _db.AdaptNeuralWeights(goalEmbedding, verifiedAction!, learningRate: 0.05f);
            }
        }
    }
}
