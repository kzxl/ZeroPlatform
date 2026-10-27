using System;

namespace ZeroPipeline.Core.Nodes
{
    /// <summary>
    /// Lifecycle states of an industrial execution pipeline node.
    /// </summary>
    public enum NodeState
    {
        Idle = 0,
        Configured = 1,
        Running = 2,
        Completed = 3,
        Faulted = 4,
        Paused = 5
    }
}
