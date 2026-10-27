using System;
using System.Collections.Generic;
using ZeroTensor.Core;

namespace ZeroInference.Core.Engine
{
    /// <summary>
    /// Unified execution contract for edge AI inference sessions across pure C# and hardware-accelerated providers.
    /// Enables seamless switching between zero-dependency pure C# fallback and native ONNX Runtime / DirectML engines.
    /// </summary>
    public interface IInferenceSession : IDisposable
    {
        /// <summary>
        /// Gets the names of the declared input feeds in the execution graph.
        /// </summary>
        IReadOnlyList<string> InputNames { get; }

        /// <summary>
        /// Gets the names of the declared output nodes in the execution graph.
        /// </summary>
        IReadOnlyList<string> OutputNames { get; }

        /// <summary>
        /// Runs single-input inference and returns the primary output tensor.
        /// </summary>
        /// <param name="input">The input tensor matching the primary input node shape.</param>
        /// <returns>The computed output tensor.</returns>
        Tensor<float> Run(Tensor<float> input);

        /// <summary>
        /// Runs multi-input inference and returns the primary output tensor.
        /// </summary>
        /// <param name="inputs">Dictionary mapping input node names to their respective tensors.</param>
        /// <returns>The primary computed output tensor.</returns>
        Tensor<float> Run(IReadOnlyDictionary<string, Tensor<float>> inputs);
    }
}
