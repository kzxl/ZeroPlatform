using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Core.Nodes
{
    /// <summary>
    /// Hierarchical macro node encapsulating an entire nested sub-DAG (<see cref="PipelineGraph"/>).
    /// Maps external input/output ports to internal sub-graph endpoints, enabling modular pipeline composition.
    /// </summary>
    public class SubDagNode : PipelineNode
    {
        private readonly PipelineGraph _subGraph;
        private readonly PipelineExecutor _subExecutor;
        private readonly List<Action> _inputDrainers = new List<Action>();

        /// <summary>
        /// The encapsulated inner pipeline graph.
        /// </summary>
        public PipelineGraph SubGraph => _subGraph;

        /// <summary>
        /// Execution engine orchestrating the encapsulated inner sub-graph.
        /// </summary>
        public PipelineExecutor SubExecutor => _subExecutor;

        public SubDagNode(string? name = null, string? id = null, PipelineGraph? subGraph = null)
            : base(name ?? "SubDagNode", id)
        {
            _subGraph = subGraph ?? new PipelineGraph();
            _subExecutor = new PipelineExecutor(_subGraph);
        }

        /// <summary>
        /// Exposes an internal input port on the sub-graph as a public input port of this macro node.
        /// Incoming packets received on this port will be routed to <paramref name="innerTargetInput"/>.
        /// </summary>
        public InputPort<T> ExposeInput<T>(
            string externalPortName,
            InputPort<T> innerTargetInput,
            int capacity = 16,
            BackpressurePolicy policy = BackpressurePolicy.Block)
        {
            if (string.IsNullOrEmpty(externalPortName))
                throw new ArgumentNullException(nameof(externalPortName));
            if (innerTargetInput == null)
                throw new ArgumentNullException(nameof(innerTargetInput));

            var externalInput = AddInputPort<T>(externalPortName, capacity, policy);

            _inputDrainers.Add(() =>
            {
                while (externalInput.TryReceive(out var packet))
                {
                    innerTargetInput.Deliver(packet);
                }
            });

            return externalInput;
        }

        /// <summary>
        /// Exposes an internal output port on the sub-graph as a public output port of this macro node.
        /// Packets emitted by <paramref name="innerSourceOutput"/> will be forwarded to this node's external output port.
        /// </summary>
        public OutputPort<T> ExposeOutput<T>(
            string externalPortName,
            OutputPort<T> innerSourceOutput)
        {
            if (string.IsNullOrEmpty(externalPortName))
                throw new ArgumentNullException(nameof(externalPortName));
            if (innerSourceOutput == null)
                throw new ArgumentNullException(nameof(innerSourceOutput));

            var externalOutput = AddOutputPort<T>(externalPortName);

            // Create a bridge node inside the sub-graph that relays packets to the external port
            var bridge = new SubDagOutputBridge<T>(externalOutput);
            _subGraph.Connect(innerSourceOutput, bridge.Input);

            return externalOutput;
        }

        protected override async Task OnInitializeAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            await _subExecutor.InitializeAsync(context, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            // 1. Drain incoming packets from external inputs into inner targets
            for (int i = 0; i < _inputDrainers.Count; i++)
            {
                _inputDrainers[i]();
            }

            // 2. Execute sub-graph in topological order
            await _subExecutor.ExecuteStepAsync(context, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task OnResetAsync()
        {
            await _subExecutor.ResetAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Internal terminal bridge node inside the sub-graph that forwards packets outward to the macro node's output port.
        /// </summary>
        private sealed class SubDagOutputBridge<T> : PipelineNode
        {
            private readonly OutputPort<T> _externalOutput;
            public InputPort<T> Input { get; }

            public SubDagOutputBridge(OutputPort<T> externalOutput)
                : base($"Bridge_{externalOutput.Name}")
            {
                _externalOutput = externalOutput;
                Input = AddInputPort<T>("Input", capacity: 64, policy: BackpressurePolicy.DropOldest);
            }

            protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            {
                while (Input.TryReceive(out var packet))
                {
                    if (packet.IsEndOfStream)
                    {
                        _externalOutput.EmitEndOfStream(packet.SequenceNumber);
                    }
                    else
                    {
                        _externalOutput.Emit(packet.Payload, packet.SequenceNumber);
                    }
                }

                return Task.CompletedTask;
            }
        }
    }
}
