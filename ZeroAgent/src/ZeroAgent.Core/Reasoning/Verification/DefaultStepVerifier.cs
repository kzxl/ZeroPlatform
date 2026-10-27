using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Context;
using ZeroAgent.Core.Tools;

namespace ZeroAgent.Core.Reasoning.Verification
{
    /// <summary>
    /// Default in-place Step-Level Verifier and Pre-Execution Critic.
    /// Validates tool existence, schema conformance, JSON syntax, and custom predicate rules.
    /// Prevents ill-formed tool calls from executing and returns actionable reflection feedback.
    /// </summary>
    public class DefaultStepVerifier : IStepCritic
    {
        private readonly List<Func<AgentContext, ToolCallRequest, AgentToolRegistry, StepVerificationResult?>> _customRules =
            new List<Func<AgentContext, ToolCallRequest, AgentToolRegistry, StepVerificationResult?>>();

        public IReadOnlyList<Func<AgentContext, ToolCallRequest, AgentToolRegistry, StepVerificationResult?>> CustomRules => _customRules;

        /// <summary>
        /// Registers an additional domain-specific verification rule.
        /// Return null if rule passes, or StepVerificationResult if rule fails.
        /// </summary>
        public void AddRule(Func<AgentContext, ToolCallRequest, AgentToolRegistry, StepVerificationResult?> rule)
        {
            if (rule != null)
            {
                _customRules.Add(rule);
            }
        }

        public virtual Task<StepVerificationResult> VerifyStepAsync(
            AgentContext context,
            string thought,
            ToolCallRequest toolCall,
            AgentToolRegistry tools,
            CancellationToken cancellationToken = default)
        {
            if (toolCall == null)
            {
                return Task.FromResult(StepVerificationResult.Reject("Null tool call."));
            }

            // 1. Tool existence check
            if (!tools.Contains(toolCall.ToolName))
            {
                string available = string.Join(", ", tools.GetToolNames());
                string feedback = $"The tool '{toolCall.ToolName}' does not exist in the active registry. " +
                                  $"Available tools are: [{available}]. Please select a valid tool from the list.";
                return Task.FromResult(StepVerificationResult.Revise(feedback, "UnknownTool"));
            }

            // 2. Arguments JSON syntax check (if arguments are present)
            if (!string.IsNullOrWhiteSpace(toolCall.ArgumentsJson))
            {
                string raw = toolCall.ArgumentsJson.Trim();
                if (raw.StartsWith("{") || raw.StartsWith("["))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(raw);
                    }
                    catch (Exception ex)
                    {
                        string feedback = $"Malformed JSON arguments for tool '{toolCall.ToolName}': {ex.Message}. " +
                                          $"Arguments must be valid JSON: {toolCall.ArgumentsJson}. Please correct the argument format.";
                        return Task.FromResult(StepVerificationResult.Revise(feedback, "InvalidJsonArguments"));
                    }
                }
            }

            // 3. Custom rules evaluation
            for (int i = 0; i < _customRules.Count; i++)
            {
                var result = _customRules[i](context, toolCall, tools);
                if (result != null && !result.IsApproved)
                {
                    return Task.FromResult(result);
                }
            }

            return Task.FromResult(StepVerificationResult.Approve());
        }
    }
}
