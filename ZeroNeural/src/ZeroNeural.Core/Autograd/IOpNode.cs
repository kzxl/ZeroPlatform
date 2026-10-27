using System;
using System.Collections.Generic;
using ZeroTensor.Core;

namespace ZeroNeural.Core.Autograd
{
    /// <summary>
    /// Represents a computation node in the dynamic autograd computation graph.
    /// Tracks inputs, records forward operations, and computes input gradients during the backward pass.
    /// </summary>
    public interface IOpNode
    {
        /// <summary>
        /// Gets the list of input variables contributing to this operation.
        /// </summary>
        IReadOnlyList<Variable> Inputs { get; }

        /// <summary>
        /// Gets the output variable produced by this operation.
        /// </summary>
        Variable Output { get; }

        /// <summary>
        /// Propagates the output gradient backward to compute and accumulate gradients for each input.
        /// </summary>
        /// <param name="gradOutput">Incoming gradient tensor with respect to the output.</param>
        void Backward(Tensor<float> gradOutput);
    }
}
