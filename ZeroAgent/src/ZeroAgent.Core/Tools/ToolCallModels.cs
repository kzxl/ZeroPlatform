using System;
using System.Text.Json;

namespace ZeroAgent.Core.Tools
{
    /// <summary>
    /// Represents a structured invocation request for an AgentTool.
    /// Compatible with OpenAI/Anthropic/Gemini function-calling protocols and local deterministic planners.
    /// </summary>
    public sealed class ToolCallRequest
    {
        public string CallId { get; set; }
        public string ToolName { get; set; }
        public string ArgumentsJson { get; set; }

        public ToolCallRequest()
        {
            CallId = $"call_{Guid.NewGuid():N}";
            ToolName = string.Empty;
            ArgumentsJson = "{}";
        }

        public ToolCallRequest(string toolName, string argumentsJson, string? callId = null)
        {
            ToolName = toolName ?? throw new ArgumentNullException(nameof(toolName));
            ArgumentsJson = argumentsJson ?? "{}";
            CallId = callId ?? $"call_{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Safely parses the ArgumentsJson into a strongly-typed parameter model.
        /// </summary>
        public T? ParseArguments<T>()
        {
            if (string.IsNullOrWhiteSpace(ArgumentsJson) || ArgumentsJson == "{}")
                return default;

            try
            {
                return JsonSerializer.Deserialize<T>(ArgumentsJson);
            }
            catch
            {
                return default;
            }
        }

        public override string ToString() => $"[ToolCall: {ToolName}({ArgumentsJson}) id={CallId}]";
    }

    /// <summary>
    /// Represents the execution outcome of an AgentTool invocation.
    /// Preserves execution metrics, status flags, and error diagnostics for agent reasoning loops.
    /// </summary>
    public sealed class ToolCallResponse
    {
        public string CallId { get; set; }
        public string ToolName { get; set; }
        public bool Success { get; set; }
        public string Content { get; set; }
        public string? Error { get; set; }
        public string? ErrorMessage => Error;
        public TimeSpan Duration { get; set; }

        public ToolCallResponse()
        {
            CallId = string.Empty;
            ToolName = string.Empty;
            Content = string.Empty;
        }

        public static ToolCallResponse CreateSuccess(string callId, string toolName, string content, TimeSpan duration)
        {
            return new ToolCallResponse
            {
                CallId = callId,
                ToolName = toolName,
                Success = true,
                Content = content,
                Error = null,
                Duration = duration
            };
        }

        public static ToolCallResponse CreateFailure(string callId, string toolName, string error, TimeSpan duration)
        {
            return new ToolCallResponse
            {
                CallId = callId,
                ToolName = toolName,
                Success = false,
                Content = string.Empty,
                Error = error,
                Duration = duration
            };
        }

        public override string ToString() => Success
            ? $"[ToolResponse: {ToolName} OK ({Duration.TotalMilliseconds:F2}ms)]"
            : $"[ToolResponse: {ToolName} ERROR: {Error}]";
    }
}
