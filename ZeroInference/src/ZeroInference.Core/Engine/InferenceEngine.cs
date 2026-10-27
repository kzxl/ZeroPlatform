using System;
using ZeroInference.Core.Graph;

namespace ZeroInference.Core.Engine
{
    /// <summary>
    /// Factory for creating and optimizing edge AI execution sessions.
    /// </summary>
    public static class InferenceEngine
    {
        public static ExecutionSession CreateSession(
            InferenceGraph graph,
            bool optimize = true,
            int arenaSlotCapacity = 2 * 1024 * 1024)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var executionGraph = optimize ? GraphOptimizer.Optimize(graph) : graph;
            return new ExecutionSession(executionGraph, arenaSlotCapacity);
        }
    }
}
