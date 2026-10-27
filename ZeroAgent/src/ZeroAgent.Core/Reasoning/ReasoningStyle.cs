namespace ZeroAgent.Core.Reasoning
{
    /// <summary>
    /// Governs the cognitive verbosity and drafting style of the agent's internal thought generation.
    /// </summary>
    public enum ReasoningStyle
    {
        /// <summary>
        /// Standard Chain-of-Thought (CoT) with comprehensive exploratory reasoning.
        /// </summary>
        DetailedCoT = 0,

        /// <summary>
        /// Chain-of-Draft (CoD): Ultra-compact telegraphic thoughts (under 30 words).
        /// Optimizes CPU inference speed by cutting intermediate reasoning tokens by 50-70%.
        /// </summary>
        ChainOfDraft = 1,

        /// <summary>
        /// Direct execution: Bypasses verbose reasoning and proceeds directly to action formulation.
        /// </summary>
        SilentAction = 2
    }
}
