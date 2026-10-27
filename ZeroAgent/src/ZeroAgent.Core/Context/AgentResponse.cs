using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Context
{
    public sealed class AgentResponse
    {
        public bool Success { get; }
        public string Output { get; }
        public string? ErrorMessage => !Success ? Output : null;
        public int TotalSteps { get; }
        public TimeSpan Elapsed { get; }
        public IReadOnlyList<AgentMessage> ExecutionTrace { get; }

        public AgentResponse(bool success, string output, int totalSteps, TimeSpan elapsed, IReadOnlyList<AgentMessage> trace)
        {
            Success = success;
            Output = output ?? string.Empty;
            TotalSteps = totalSteps;
            Elapsed = elapsed;
            ExecutionTrace = trace ?? Array.Empty<AgentMessage>();
        }

        public static AgentResponse Failed(string error, int steps, TimeSpan elapsed, IReadOnlyList<AgentMessage> trace)
        {
            return new AgentResponse(false, $"Error: {error}", steps, elapsed, trace);
        }

        public static AgentResponse Succeeded(string answer, int steps, TimeSpan elapsed, IReadOnlyList<AgentMessage> trace)
        {
            return new AgentResponse(true, answer, steps, elapsed, trace);
        }
    }
}
