using System;

namespace ZeroAgent.Core.Context
{
    public enum AgentRole
    {
        System = 0,
        User = 1,
        Assistant = 2,
        Tool = 3
    }

    public sealed class AgentMessage
    {
        public AgentRole Role { get; }
        public string Content { get; }
        public string? Name { get; }
        public DateTime Timestamp { get; }

        public AgentMessage(AgentRole role, string content, string? name = null)
        {
            Role = role;
            Content = content ?? string.Empty;
            Name = name;
            Timestamp = DateTime.UtcNow;
        }

        public override string ToString() => $"[{Role}{(Name != null ? $": {Name}" : "")}] {Content}";
    }
}
