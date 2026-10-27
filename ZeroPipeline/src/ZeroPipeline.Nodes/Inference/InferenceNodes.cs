using System;
using System.Threading;
using System.Threading.Tasks;
using ZeroInference.Core.Engine;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroTensor.Core;

namespace ZeroPipeline.Nodes.Inference
{
    /// <summary>
    /// Pipeline node running neural network inference on input tensors using ZeroInference.Core.
    /// Supports pre-allocated static tensor arenas for allocation-free high-frequency classification.
    /// </summary>
    public sealed class InferenceClassifierNode : TransformNode<Tensor<float>, Tensor<float>>
    {
        private readonly ExecutionSession _session;

        public ExecutionSession Session => _session;

        public InferenceClassifierNode(ExecutionSession session, string? name = null)
            : base(name ?? "InferenceClassifier")
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        protected override Task<Tensor<float>> ProcessAsync(Tensor<float> input, PipelineContext context, CancellationToken cancellationToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            var output = _session.Run(input);
            return Task.FromResult(output);
        }
    }
}
