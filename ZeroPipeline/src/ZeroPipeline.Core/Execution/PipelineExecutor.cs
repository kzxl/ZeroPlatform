using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;

namespace ZeroPipeline.Core.Execution
{
    /// <summary>
    /// High-throughput execution engine that orchestrates pipeline graph execution.
    /// Supports deterministic single-step evaluation and continuous non-blocking streaming execution.
    /// </summary>
    public sealed class PipelineExecutor
    {
        private readonly PipelineGraph _graph;
        private IReadOnlyList<IPipelineNode>? _cachedExecutionOrder;
        private bool _isInitialized;
        private readonly object _lock = new object();

        public PipelineGraph Graph => _graph;
        public bool IsInitialized => _isInitialized;

        public event Action<IPipelineNode, PipelineContext>? NodeExecuting;
        public event Action<IPipelineNode, double>? NodeCompleted;
        public event Action<IPipelineNode, Exception>? NodeFaulted;

        public PipelineExecutor(PipelineGraph graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        /// <summary>
        /// Validates graph topology, caches topological order, and initializes all nodes.
        /// </summary>
        public async Task InitializeAsync(PipelineContext? context = null, CancellationToken cancellationToken = default)
        {
            var ctx = context ?? new PipelineContext(cancellationToken);

            lock (_lock)
            {
                _cachedExecutionOrder = _graph.GetTopologicalOrder();
            }

            foreach (var node in _cachedExecutionOrder)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await node.InitializeAsync(ctx, cancellationToken).ConfigureAwait(false);
            }

            _isInitialized = true;
        }

        /// <summary>
        /// Executes a single discrete processing cycle across all graph nodes in topological dependency order.
        /// </summary>
        public async Task ExecuteStepAsync(PipelineContext? context = null, CancellationToken cancellationToken = default)
        {
            if (!_isInitialized || _cachedExecutionOrder == null)
            {
                await InitializeAsync(context, cancellationToken).ConfigureAwait(false);
            }

            var ctx = context ?? new PipelineContext(cancellationToken);
            var nodes = _cachedExecutionOrder!;

            for (int i = 0; i < nodes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var node = nodes[i];

                try
                {
                    NodeExecuting?.Invoke(node, ctx);
                    await node.ExecuteAsync(ctx, cancellationToken).ConfigureAwait(false);
                    NodeCompleted?.Invoke(node, node.LastExecutionDurationMs);
                }
                catch (Exception ex)
                {
                    NodeFaulted?.Invoke(node, ex);
                    throw;
                }
            }

            ctx.CycleId++;
        }

        /// <summary>
        /// Runs a continuous streaming execution loop until cancellation is requested.
        /// </summary>
        public async Task StartStreamingAsync(
            PipelineContext? context = null,
            int loopIntervalMs = 0,
            CancellationToken cancellationToken = default)
        {
            var ctx = context ?? new PipelineContext(cancellationToken);

            if (!_isInitialized)
            {
                await InitializeAsync(ctx, cancellationToken).ConfigureAwait(false);
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                await ExecuteStepAsync(ctx, cancellationToken).ConfigureAwait(false);

                if (loopIntervalMs > 0)
                {
                    await Task.Delay(loopIntervalMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Resets all nodes in the graph, clearing all buffers and error states.
        /// </summary>
        public async Task ResetAsync()
        {
            IReadOnlyList<IPipelineNode> nodes;
            lock (_lock)
            {
                nodes = _graph.Nodes;
            }

            foreach (var node in nodes)
            {
                await node.ResetAsync().ConfigureAwait(false);
            }

            _isInitialized = false;
        }
    }
}
