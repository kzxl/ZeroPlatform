using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Core.Nodes
{
    /// <summary>
    /// Specialized pipeline node that acts as a data generator or source (has no input ports).
    /// </summary>
    /// <typeparam name="TOut">Output data type.</typeparam>
    public abstract class SourceNode<TOut> : PipelineNode
    {
        public OutputPort<TOut> Output { get; }

        protected SourceNode(string? name = null, string? id = null, string outputPortName = "Output")
            : base(name, id)
        {
            Output = AddOutputPort<TOut>(outputPortName);
        }
    }

    /// <summary>
    /// Specialized pipeline node that transforms a single input stream into a single output stream.
    /// </summary>
    /// <typeparam name="TIn">Input data type.</typeparam>
    /// <typeparam name="TOut">Output data type.</typeparam>
    public abstract class TransformNode<TIn, TOut> : PipelineNode
    {
        public InputPort<TIn> Input { get; }
        public OutputPort<TOut> Output { get; }

        protected TransformNode(
            string? name = null,
            string? id = null,
            string inputPortName = "Input",
            string outputPortName = "Output",
            int capacity = 16,
            BackpressurePolicy policy = BackpressurePolicy.Block)
            : base(name, id)
        {
            Input = AddInputPort<TIn>(inputPortName, capacity, policy);
            Output = AddOutputPort<TOut>(outputPortName);
        }

        protected sealed override async Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            if (Input.TryReceive(out var packet))
            {
                if (packet.IsEndOfStream)
                {
                    Output.EmitEndOfStream(packet.SequenceNumber);
                    return;
                }

                var transformed = await ProcessAsync(packet.Payload, context, cancellationToken).ConfigureAwait(false);
                Output.Emit(transformed, packet.SequenceNumber);
            }
        }

        /// <summary>
        /// Transforms an incoming input payload into an output payload.
        /// </summary>
        protected abstract Task<TOut> ProcessAsync(TIn input, PipelineContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Specialized terminal pipeline node that consumes data without producing downstream outputs.
    /// </summary>
    /// <typeparam name="TIn">Input data type.</typeparam>
    public abstract class SinkNode<TIn> : PipelineNode
    {
        public InputPort<TIn> Input { get; }

        protected SinkNode(
            string? name = null,
            string? id = null,
            string inputPortName = "Input",
            int capacity = 16,
            BackpressurePolicy policy = BackpressurePolicy.Block)
            : base(name, id)
        {
            Input = AddInputPort<TIn>(inputPortName, capacity, policy);
        }

        protected sealed override async Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            if (Input.TryReceive(out var packet))
            {
                if (!packet.IsEndOfStream)
                {
                    await ConsumeAsync(packet.Payload, context, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Consumes an incoming data payload.
        /// </summary>
        protected abstract Task ConsumeAsync(TIn input, PipelineContext context, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Synchronous functional delegate transform node for rapid scripting and lightweight transformations.
    /// </summary>
    public sealed class FuncTransformNode<TIn, TOut> : TransformNode<TIn, TOut>
    {
        private readonly Func<TIn, TOut> _transformFunc;

        public FuncTransformNode(Func<TIn, TOut> transformFunc, string? name = null)
            : base(name ?? $"Transform<{typeof(TIn).Name},{typeof(TOut).Name}>")
        {
            _transformFunc = transformFunc ?? throw new ArgumentNullException(nameof(transformFunc));
        }

        protected override Task<TOut> ProcessAsync(TIn input, PipelineContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(_transformFunc(input));
        }
    }

    /// <summary>
    /// Synchronous action sink node for rapid test inspection and output collection.
    /// </summary>
    public sealed class ActionSinkNode<TIn> : SinkNode<TIn>
    {
        private readonly Action<TIn> _consumerAction;

        public ActionSinkNode(Action<TIn> consumerAction, string? name = null)
            : base(name ?? $"Sink<{typeof(TIn).Name}>")
        {
            _consumerAction = consumerAction ?? throw new ArgumentNullException(nameof(consumerAction));
        }

        protected override Task ConsumeAsync(TIn input, PipelineContext context, CancellationToken cancellationToken)
        {
            _consumerAction(input);
            return Task.CompletedTask;
        }
    }
}
