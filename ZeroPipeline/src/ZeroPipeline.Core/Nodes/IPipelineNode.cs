using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Core.Nodes
{
    /// <summary>
    /// Contract defining an executable vertex in the pipeline directed acyclic graph.
    /// Handles typed inputs, processes payload logic, and produces downstream outputs.
    /// </summary>
    public interface IPipelineNode
    {
        /// <summary>
        /// Globally unique identifier for this node instance.
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Human-readable display and logging name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Current lifecycle state of the node.
        /// </summary>
        NodeState State { get; }

        /// <summary>
        /// Captured exception if the node transitioned to NodeState.Faulted.
        /// </summary>
        Exception? LastException { get; }

        /// <summary>
        /// Number of successful execution cycles completed.
        /// </summary>
        long ExecutionCount { get; }

        /// <summary>
        /// Elapsed execution duration of the most recent execution cycle in milliseconds.
        /// </summary>
        double LastExecutionDurationMs { get; }

        /// <summary>
        /// Total cumulative execution duration across all cycles in milliseconds.
        /// </summary>
        double TotalExecutionDurationMs { get; }

        /// <summary>
        /// Input ports accepting data from upstream producer nodes.
        /// </summary>
        IReadOnlyList<IPort> InputPorts { get; }

        /// <summary>
        /// Output ports publishing data to downstream consumer nodes.
        /// </summary>
        IReadOnlyList<IPort> OutputPorts { get; }

        /// <summary>
        /// Prepares resources, preallocates buffers, and validates input configurations before execution begins.
        /// </summary>
        Task InitializeAsync(PipelineContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes one processing cycle of the node logic.
        /// </summary>
        Task ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the node state, clears buffered queues, and resets error conditions.
        /// </summary>
        Task ResetAsync();
    }
}
