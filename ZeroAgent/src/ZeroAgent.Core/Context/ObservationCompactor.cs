using System;

namespace ZeroAgent.Core.Context
{
    /// <summary>
    /// Micro-level context compactor for tool observations in multi-step ReAct agent loops.
    /// Masks voluminous raw outputs (terminal logs, TSDB telemetry arrays, SQL record sets)
    /// into high-density signatures to preserve KV-cache stability and prevent trajectory window blowup.
    /// </summary>
    public static class ObservationCompactor
    {
        public const int DefaultMaxChars = 400;

        /// <summary>
        /// Compacts a tool observation string if its length exceeds the specified character threshold.
        /// Retains head and tail context lines, reporting truncated character and line counts.
        /// </summary>
        public static string Compact(string toolName, string rawObservation, int maxChars = DefaultMaxChars)
        {
            if (string.IsNullOrEmpty(rawObservation) || rawObservation.Length <= maxChars)
            {
                return rawObservation ?? string.Empty;
            }

            int headLength = Math.Max(50, (maxChars * 3) / 5);
            int tailLength = Math.Max(30, maxChars - headLength);

            if (headLength + tailLength >= rawObservation.Length)
            {
                return rawObservation;
            }

            string head = rawObservation.Substring(0, headLength);
            string tail = rawObservation.Substring(rawObservation.Length - tailLength);
            int omittedChars = rawObservation.Length - (headLength + tailLength);

            int totalLines = 0;
            for (int i = 0; i < rawObservation.Length; i++)
            {
                if (rawObservation[i] == '\n') totalLines++;
            }

            return $"{head}\n[... {toolName} output truncated: {omittedChars} chars (~{totalLines} lines omitted) ...]\n{tail}";
        }
    }
}
