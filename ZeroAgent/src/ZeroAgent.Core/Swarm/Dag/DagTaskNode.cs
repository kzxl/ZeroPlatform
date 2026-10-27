using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Swarm.Dag
{
    public enum DagTaskStatus
    {
        Pending = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Skipped = 4
    }

    /// <summary>
    /// Represents an individual node in a multi-agent execution DAG.
    /// Tracks agent assignment, dependencies, execution state, and output.
    /// </summary>
    public sealed class DagTaskNode
    {
        public string Id { get; }
        public string AgentName { get; }
        public string Goal { get; }
        public HashSet<string> Dependencies { get; }
        public DagTaskStatus Status { get; set; } = DagTaskStatus.Pending;
        public string? Output { get; set; }
        public string? ErrorMessage { get; set; }
        public int TotalSteps { get; set; }

        public DagTaskNode(string id, string agentName, string goal, IEnumerable<string>? dependencies = null)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            AgentName = agentName ?? throw new ArgumentNullException(nameof(agentName));
            Goal = goal ?? throw new ArgumentNullException(nameof(goal));
            Dependencies = dependencies != null
                ? new HashSet<string>(dependencies, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
