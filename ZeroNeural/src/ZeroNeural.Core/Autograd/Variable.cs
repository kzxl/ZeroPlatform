using System;
using System.Threading;
using ZeroNeural.Core.Autograd.Ops;
using ZeroTensor.Core;

namespace ZeroNeural.Core.Autograd
{
    /// <summary>
    /// Represents a dynamic tensor variable in the autograd computation graph.
    /// Tracks forward data, reverse gradient, generating operation node, and computation graph dependencies.
    /// </summary>
    public class Variable : IEquatable<Variable>
    {
        private static int _globalIdCounter;
        private readonly int _id;

        /// <summary>
        /// Gets the unique identifier of this variable.
        /// </summary>
        public int Id => _id;

        /// <summary>
        /// Gets or sets the underlying data tensor.
        /// </summary>
        public Tensor<float> Data { get; set; }

        /// <summary>
        /// Gets or sets the accumulated gradient tensor.
        /// </summary>
        public Tensor<float>? Grad { get; set; }

        /// <summary>
        /// Gets or sets whether this variable tracks gradients for backpropagation.
        /// </summary>
        public bool RequiresGrad { get; set; }

        /// <summary>
        /// Gets or sets the operation node that generated this variable during the forward pass.
        /// Null for leaf variables (e.g. model weights, inputs).
        /// </summary>
        public IOpNode? Creator { get; set; }

        /// <summary>
        /// Gets the shape of the underlying tensor.
        /// </summary>
        public TensorShape Shape => Data.Shape;

        /// <summary>
        /// Gets the rank of the underlying tensor.
        /// </summary>
        public int Rank => Data.Rank;

        /// <summary>
        /// Gets the total number of elements.
        /// </summary>
        public int Length => Data.Length;

        /// <summary>
        /// Initializes a new Variable with the specified data tensor.
        /// </summary>
        public Variable(Tensor<float> data, bool requiresGrad = false, IOpNode? creator = null)
        {
            _id = Interlocked.Increment(ref _globalIdCounter);
            Data = data ?? throw new ArgumentNullException(nameof(data));
            RequiresGrad = requiresGrad;
            Creator = creator;
            Grad = null;
        }

        /// <summary>
        /// Resets the accumulated gradient to null.
        /// </summary>
        public void ZeroGrad()
        {
            Grad = null;
        }

        /// <summary>
        /// Executes reverse-mode automatic differentiation starting from this variable.
        /// </summary>
        public void Backward(Tensor<float>? initialGrad = null)
        {
            AutogradEngine.Backward(this, initialGrad);
        }

        #region Standard Neural Operations

        /// <summary>
        /// Computes matrix multiplication: this @ other.
        /// </summary>
        public Variable MatMul(Variable other) => new MatMulOp(this, other).Output;

        /// <summary>
        /// Computes Rectified Linear Unit: max(0, x).
        /// </summary>
        public Variable ReLU() => new ReLUOp(this).Output;

        /// <summary>
        /// Computes Sigmoid activation: 1 / (1 + exp(-x)).
        /// </summary>
        public Variable Sigmoid() => new SigmoidOp(this).Output;

        /// <summary>
        /// Computes Hyperbolic Tangent: tanh(x).
        /// </summary>
        public Variable Tanh() => new TanhOp(this).Output;

        /// <summary>
        /// Computes sum of elements along an axis.
        /// </summary>
        public Variable Sum(int axis = -1, bool keepDims = false) => new SumOp(this, axis, keepDims).Output;

        /// <summary>
        /// Computes arithmetic mean of elements along an axis.
        /// </summary>
        public Variable Mean(int axis = -1, bool keepDims = false) => new MeanOp(this, axis, keepDims).Output;

        #endregion

        #region Operator Overloads

        public static Variable operator +(Variable a, Variable b) => new AddOp(a, b).Output;
        public static Variable operator -(Variable a, Variable b) => new SubOp(a, b).Output;
        public static Variable operator *(Variable a, Variable b) => new MulOp(a, b).Output;
        public static Variable operator /(Variable a, Variable b) => new DivOp(a, b).Output;
        public static Variable operator -(Variable a) => new NegOp(a).Output;

        public static Variable operator +(Variable a, float scalar)
        {
            var scalarVar = new Variable(Tensor.Full(scalar, a.Data.Shape.ToArray()), requiresGrad: false);
            return new AddOp(a, scalarVar).Output;
        }

        public static Variable operator -(Variable a, float scalar)
        {
            var scalarVar = new Variable(Tensor.Full(scalar, a.Data.Shape.ToArray()), requiresGrad: false);
            return new SubOp(a, scalarVar).Output;
        }

        public static Variable operator *(Variable a, float scalar)
        {
            var scalarVar = new Variable(Tensor.Full(scalar, a.Data.Shape.ToArray()), requiresGrad: false);
            return new MulOp(a, scalarVar).Output;
        }

        public static Variable operator /(Variable a, float scalar)
        {
            var scalarVar = new Variable(Tensor.Full(scalar, a.Data.Shape.ToArray()), requiresGrad: false);
            return new DivOp(a, scalarVar).Output;
        }

        public static Variable operator +(float scalar, Variable a) => a + scalar;

        public static Variable operator -(float scalar, Variable a)
        {
            var scalarVar = new Variable(Tensor.Full(scalar, a.Data.Shape.ToArray()), requiresGrad: false);
            return new SubOp(scalarVar, a).Output;
        }

        public static Variable operator *(float scalar, Variable a) => a * scalar;

        public static Variable operator /(float scalar, Variable a)
        {
            var scalarVar = new Variable(Tensor.Full(scalar, a.Data.Shape.ToArray()), requiresGrad: false);
            return new DivOp(scalarVar, a).Output;
        }

        #endregion

        #region Equality & Formatting

        public bool Equals(Variable? other)
        {
            if (other is null) return false;
            return _id == other._id;
        }

        public override bool Equals(object? obj) => obj is Variable other && Equals(other);

        public override int GetHashCode() => _id;

        public override string ToString()
        {
            return $"Variable(id={_id}, shape={Shape}, requiresGrad={RequiresGrad}, creator={Creator?.GetType().Name ?? "leaf"})";
        }

        #endregion
    }
}
