using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Nodes.Inspection;

namespace ZeroPipeline.Nodes.Flow
{
    /// <summary>
    /// Conditional branching node that evaluates each packet and routes it to TrueOutput or FalseOutput.
    /// Used for quality sorting, inspection threshold gating, and dynamic routing.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    public class BranchNode<T> : PipelineNode
    {
        public InputPort<T> Input { get; }
        public OutputPort<T> TrueOutput { get; }
        public OutputPort<T> FalseOutput { get; }

        private readonly Func<T, bool> _predicate;

        public BranchNode(
            Func<T, bool> predicate,
            string? name = null,
            int capacity = 16,
            BackpressurePolicy policy = BackpressurePolicy.Block)
            : base(name ?? $"Branch<{typeof(T).Name}>")
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            Input = AddInputPort<T>("Input", capacity, policy);
            TrueOutput = AddOutputPort<T>("TrueOutput");
            FalseOutput = AddOutputPort<T>("FalseOutput");
        }

        protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            if (Input.TryReceive(out var packet))
            {
                if (packet.IsEndOfStream)
                {
                    TrueOutput.EmitEndOfStream(packet.SequenceNumber);
                    FalseOutput.EmitEndOfStream(packet.SequenceNumber);
                    return Task.CompletedTask;
                }

                bool isTrue = _predicate(packet.Payload);
                if (isTrue)
                {
                    TrueOutput.Emit(packet.Payload, packet.SequenceNumber);
                }
                else
                {
                    FalseOutput.Emit(packet.Payload, packet.SequenceNumber);
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// High-level routing node specifically for quality assurance: splits InspectionResults into Passed and Failed streams.
    /// </summary>
    public sealed class QualityRouterNode : PipelineNode
    {
        public InputPort<InspectionResult> Input { get; }
        public OutputPort<InspectionResult> PassedOutput { get; }
        public OutputPort<InspectionResult> FailedOutput { get; }

        public QualityRouterNode(string? name = null, int capacity = 16)
            : base(name ?? "QualityRouter")
        {
            Input = AddInputPort<InspectionResult>("Input", capacity);
            PassedOutput = AddOutputPort<InspectionResult>("PassedOutput");
            FailedOutput = AddOutputPort<InspectionResult>("FailedOutput");
        }

        protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            if (Input.TryReceive(out var packet))
            {
                if (packet.IsEndOfStream)
                {
                    PassedOutput.EmitEndOfStream(packet.SequenceNumber);
                    FailedOutput.EmitEndOfStream(packet.SequenceNumber);
                    return Task.CompletedTask;
                }

                if (packet.Payload.IsPassed)
                {
                    PassedOutput.Emit(packet.Payload, packet.SequenceNumber);
                }
                else
                {
                    FailedOutput.Emit(packet.Payload, packet.SequenceNumber);
                }
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Accumulates incoming packets into fixed-size batches before emitting downstream.
    /// </summary>
    public sealed class BatchAccumulatorNode<T> : PipelineNode
    {
        private readonly List<T> _buffer;
        private readonly int _batchSize;
        private long _lastSequence;

        public InputPort<T> Input { get; }
        public OutputPort<IReadOnlyList<T>> Output { get; }

        public BatchAccumulatorNode(int batchSize, string? name = null, int capacity = 32)
            : base(name ?? $"Batch<{typeof(T).Name}>[{batchSize}]")
        {
            if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
            _batchSize = batchSize;
            _buffer = new List<T>(batchSize);

            Input = AddInputPort<T>("Input", capacity);
            Output = AddOutputPort<IReadOnlyList<T>>("Output");
        }

        protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            while (Input.TryReceive(out var packet))
            {
                if (packet.IsEndOfStream)
                {
                    FlushBuffer();
                    Output.EmitEndOfStream(packet.SequenceNumber);
                    return Task.CompletedTask;
                }

                _buffer.Add(packet.Payload);
                _lastSequence = packet.SequenceNumber;

                if (_buffer.Count >= _batchSize)
                {
                    FlushBuffer();
                }
            }

            return Task.CompletedTask;
        }

        private void FlushBuffer()
        {
            if (_buffer.Count > 0)
            {
                var batch = _buffer.ToArray();
                _buffer.Clear();
                Output.Emit(batch, _lastSequence);
            }
        }

        protected override Task OnResetAsync()
        {
            _buffer.Clear();
            return base.OnResetAsync();
        }
    }
}
