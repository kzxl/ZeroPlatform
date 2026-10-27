using System;
using ZeroAgent.Core.Database;

namespace ZeroAgent.Core.Reasoning.System1
{
    /// <summary>
    /// Categorical decision output from the System 1 Fast Reflex Engine.
    /// </summary>
    public enum System1DecisionType
    {
        /// <summary>
        /// Verified trajectory plan cached in .zab database matches query with high similarity. Bypasses LLM.
        /// </summary>
        DirectPlanReplay,

        /// <summary>
        /// Neural policy classified a deterministic tool action with confidence above threshold.
        /// </summary>
        DirectToolDispatch,

        /// <summary>
        /// Safety guardrail classified the action as destructive or unauthorized. Halts execution.
        /// </summary>
        BlockedByGuardrail,

        /// <summary>
        /// Low confidence, high ambiguity, or novel task requiring System 2 multi-step LLM reasoning.
        /// </summary>
        DeferToSystem2
    }

    /// <summary>
    /// Concrete result evaluated by the System 1 Reflex Gate in sub-0.1ms.
    /// </summary>
    public sealed class System1ReflexResult
    {
        public System1DecisionType Decision { get; }
        public string Action { get; }
        public float Confidence { get; }
        public float RiskScore { get; }
        public ZabPlanRecord? CachedPlan { get; }
        public string? Reason { get; }

        public System1ReflexResult(
            System1DecisionType decision,
            string action,
            float confidence,
            float riskScore = 0.0f,
            ZabPlanRecord? cachedPlan = null,
            string? reason = null)
        {
            Decision = decision;
            Action = action ?? string.Empty;
            Confidence = confidence;
            RiskScore = riskScore;
            CachedPlan = cachedPlan;
            Reason = reason;
        }

        public static System1ReflexResult ReplayPlan(ZabPlanRecord plan, float confidence) =>
            new System1ReflexResult(System1DecisionType.DirectPlanReplay, "replay_plan", confidence, 0.0f, plan, "Instant plan cache hit.");

        public static System1ReflexResult Block(string reason, float riskScore) =>
            new System1ReflexResult(System1DecisionType.BlockedByGuardrail, "block", 1.0f, riskScore, null, reason);

        public static System1ReflexResult Dispatch(string toolAction, float confidence) =>
            new System1ReflexResult(System1DecisionType.DirectToolDispatch, toolAction, confidence, 0.0f, null, "High-confidence System 1 tool dispatch.");

        public static System1ReflexResult Defer(string reason, float confidence = 0.0f) =>
            new System1ReflexResult(System1DecisionType.DeferToSystem2, "defer_system2", confidence, 0.0f, null, reason);
    }
}
