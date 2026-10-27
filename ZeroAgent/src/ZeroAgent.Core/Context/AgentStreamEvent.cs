using System;

namespace ZeroAgent.Core.Context
{
    /// <summary>
    /// Type of execution step event emitted during progressive agent inference.
    /// </summary>
    public enum AgentStreamEventType
    {
        GoalStarted,
        ThoughtGenerated,
        ToolCalling,
        ToolCompleted,
        StepVerified,
        VerificationRejected,
        CircuitBreakTriggered,
        AnswerChunk,
        Completed,
        Failed
    }

    /// <summary>
    /// Progressive streaming event emitted in real-time as the agent deliberates,
    /// selects tools, observes results, and verifies reasoning steps.
    /// </summary>
    public sealed class AgentStreamEvent
    {
        public AgentStreamEventType Type { get; }
        public string Content { get; }
        public int StepNumber { get; }
        public string? ToolName { get; }
        public string? ToolArgument { get; }
        public string? ToolResult { get; }
        public TimeSpan Elapsed { get; }

        public AgentStreamEvent(
            AgentStreamEventType type,
            string content,
            int stepNumber = 0,
            string? toolName = null,
            string? toolArgument = null,
            string? toolResult = null,
            TimeSpan? elapsed = null)
        {
            Type = type;
            Content = content ?? string.Empty;
            StepNumber = stepNumber;
            ToolName = toolName;
            ToolArgument = toolArgument;
            ToolResult = toolResult;
            Elapsed = elapsed ?? TimeSpan.Zero;
        }

        public static AgentStreamEvent Started(string goal, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.GoalStarted, goal, 0, elapsed: elapsed);

        public static AgentStreamEvent Thought(string thought, int step, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.ThoughtGenerated, thought, step, elapsed: elapsed);

        public static AgentStreamEvent ToolCall(string tool, string arg, int step, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.ToolCalling, $"Invoking {tool}({arg})", step, toolName: tool, toolArgument: arg, elapsed: elapsed);

        public static AgentStreamEvent ToolDone(string tool, string result, int step, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.ToolCompleted, result, step, toolName: tool, toolResult: result, elapsed: elapsed);

        public static AgentStreamEvent Verified(string details, int step, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.StepVerified, details, step, elapsed: elapsed);

        public static AgentStreamEvent Rejected(string feedback, int step, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.VerificationRejected, feedback, step, elapsed: elapsed);

        public static AgentStreamEvent CircuitBreak(string reason, int step, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.CircuitBreakTriggered, reason, step, elapsed: elapsed);

        public static AgentStreamEvent Completed(string finalAnswer, int steps, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.Completed, finalAnswer, steps, elapsed: elapsed);

        public static AgentStreamEvent Failed(string error, int steps, TimeSpan elapsed) =>
            new AgentStreamEvent(AgentStreamEventType.Failed, error, steps, elapsed: elapsed);
    }
}
