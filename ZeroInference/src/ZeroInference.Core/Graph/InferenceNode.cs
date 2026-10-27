using System;
using System.Collections.Generic;
using ZeroTensor.Core;

namespace ZeroInference.Core.Graph
{
    /// <summary>
    /// Represents an executable operator node in the static inference graph.
    /// Supports fused execution kernels for zero intermediate write-backs.
    /// </summary>
    public sealed class InferenceNode
    {
        public string Name { get; }
        public NodeKind Kind { get; set; }
        public IReadOnlyList<string> InputNames { get; set; }
        public string OutputName { get; set; }

        public Tensor<float>? Weight { get; set; }
        public Tensor<float>? Bias { get; set; }

        // Hyper-parameters & attributes
        public int Stride { get; set; } = 1;
        public int Padding { get; set; } = 0;
        public int KernelSize { get; set; } = 3;
        public float Alpha { get; set; } = 0.01f; // For LeakyReLU
        public float Epsilon { get; set; } = 1e-5f; // For BatchNorm
        public int Axis { get; set; } = -1; // For Softmax

        // Quantization attributes
        public bool IsQuantized { get; set; }
        public float WeightScale { get; set; } = 1.0f;
        public sbyte WeightZeroPoint { get; set; } = 0;

        public Dictionary<string, object> ExtraAttributes { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public InferenceNode(
            string name,
            NodeKind kind,
            string[] inputNames,
            string outputName,
            Tensor<float>? weight = null,
            Tensor<float>? bias = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind;
            InputNames = inputNames ?? throw new ArgumentNullException(nameof(inputNames));
            OutputName = outputName ?? throw new ArgumentNullException(nameof(outputName));
            Weight = weight;
            Bias = bias;
        }

        public override string ToString() => $"{Name} ({Kind}): [{string.Join(", ", InputNames)}] -> {OutputName}";
    }
}
