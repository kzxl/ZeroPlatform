using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Context
{
    public sealed class AgentContext
    {
        public string Goal { get; }
        public List<AgentMessage> History { get; } = new List<AgentMessage>();
        public Dictionary<string, object> Variables { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        public int MaxSteps { get; set; } = 10;

        public AgentContext(string goal, int maxSteps = 10)
        {
            Goal = goal ?? string.Empty;
            MaxSteps = maxSteps;
            if (!string.IsNullOrWhiteSpace(goal))
            {
                History.Add(new AgentMessage(AgentRole.User, goal));
            }
        }

        public void AddMessage(AgentRole role, string content, string? name = null)
        {
            History.Add(new AgentMessage(role, content, name));
        }
    }
}
