using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Engine
{
    public enum LoopGuardStatus
    {
        Allow,
        WarnAndReflect,
        CircuitBreak
    }

    /// <summary>
    /// Circuit-breaker and anti-hallucination guard for ReAct agent tool invocation loops.
    /// Detects consecutive identical calls, oscillating tool loops, and injects reflection prompts.
    /// </summary>
    public sealed class ReActLoopGuard
    {
        private readonly List<string> _callHistory = new List<string>();
        private readonly Dictionary<string, int> _callCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int MaxConsecutiveDuplicates { get; set; } = 2;
        public int MaxTotalDuplicates { get; set; } = 3;

        public LoopGuardStatus EvaluateCall(string toolName, string argument, out string feedback)
        {
            if (string.IsNullOrWhiteSpace(toolName))
            {
                feedback = string.Empty;
                return LoopGuardStatus.Allow;
            }

            string sig = $"{toolName.Trim().ToLowerInvariant()}:{argument?.Trim() ?? string.Empty}";

            // 1. Check total duplicate occurrences
            _callCounts.TryGetValue(sig, out int currentCount);
            currentCount++;
            _callCounts[sig] = currentCount;

            // 2. Check consecutive identical calls
            int consecutiveCount = 1;
            for (int i = _callHistory.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_callHistory[i], sig, StringComparison.OrdinalIgnoreCase))
                {
                    consecutiveCount++;
                }
                else
                {
                    break;
                }
            }

            _callHistory.Add(sig);

            // 3. Evaluate circuit-breaker conditions
            if (consecutiveCount >= MaxConsecutiveDuplicates || currentCount >= MaxTotalDuplicates)
            {
                feedback = $"[CIRCUIT BREAKER TRIGGERED] You have called tool '{toolName}' with the exact same argument {consecutiveCount} time(s) consecutively (total {currentCount} times). Duplicate execution is blocked. You MUST analyze existing observations or provide your Final Answer.";
                return LoopGuardStatus.CircuitBreak;
            }

            if (consecutiveCount > 1)
            {
                feedback = $"[REFLEXION WARNING] You previously called '{toolName}' with identical arguments. Re-executing will yield identical results. Consider what additional information you need or formulate your Final Answer.";
                return LoopGuardStatus.WarnAndReflect;
            }

            // 4. Check for 2-tool oscillation (A -> B -> A -> B)
            if (_callHistory.Count >= 4)
            {
                int len = _callHistory.Count;
                if (_callHistory[len - 1] == _callHistory[len - 3] &&
                    _callHistory[len - 2] == _callHistory[len - 4] &&
                    _callHistory[len - 1] != _callHistory[len - 2])
                {
                    feedback = "[OSCILLATION DETECTED] You are alternating between two tools repeatedly without progress. Synthesize the findings and output your Final Answer.";
                    return LoopGuardStatus.WarnAndReflect;
                }
            }

            feedback = string.Empty;
            return LoopGuardStatus.Allow;
        }

        public void Reset()
        {
            _callHistory.Clear();
            _callCounts.Clear();
        }
    }
}
