using System.Collections.Generic;

namespace ZeroAgent.Dialog.Memory
{
    /// <summary>
    /// Contract for context compression, observation masking, and rolling dialogue summarization.
    /// Distills historical conversational turns and lengthy tool outputs into high-density semantic summaries.
    /// </summary>
    public interface IContextCompactor
    {
        /// <summary>
        /// Compacts an evicted or historical sequence of dialogue turns and active slots into a structured summary block.
        /// </summary>
        /// <param name="turnsToCompact">The sequence of turns to compress.</param>
        /// <param name="activeSlots">Active entity slots representing the ground-truth state.</param>
        /// <param name="existingSummary">The prior cumulative summary, if any.</param>
        /// <returns>A condensed, high-density &lt;CONTEXT_SUMMARY&gt; string.</returns>
        string CompactTurns(
            IReadOnlyList<DialogTurn> turnsToCompact,
            IReadOnlyDictionary<string, string> activeSlots,
            string? existingSummary);

        /// <summary>
        /// Compacts a tool observation string to prevent trajectory context blowup in multi-step ReAct loops.
        /// </summary>
        /// <param name="toolName">The name of the tool executed.</param>
        /// <param name="rawObservation">The raw observation returned by the tool.</param>
        /// <param name="maxChars">The maximum character threshold before masking/truncating.</param>
        /// <returns>A condensed observation maintaining critical prefixes and telemetry signals.</returns>
        string CompactObservation(string toolName, string rawObservation, int maxChars = 400);

        /// <summary>
        /// Estimates the token count of a given text using fast CPU heuristics.
        /// </summary>
        int EstimateTokens(string text);
    }
}
