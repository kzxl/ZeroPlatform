using System;
using System.Collections.Generic;
using ZeroTensor.Core;

namespace ZeroNeural.Core.Autograd.Ops
{
    internal sealed class AddOp : IOpNode
    {
        private readonly Variable _a;
        private readonly Variable _b;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public AddOp(Variable a, Variable b)
        {
            _a = a;
            _b = b;
            Inputs = new[] { a, b };
            var outTensor = a.Data + b.Data;
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad || b.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            AutogradEngine.AccumulateGrad(_a, gradOutput);
            AutogradEngine.AccumulateGrad(_b, gradOutput);
        }
    }

    internal sealed class SubOp : IOpNode
    {
        private readonly Variable _a;
        private readonly Variable _b;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public SubOp(Variable a, Variable b)
        {
            _a = a;
            _b = b;
            Inputs = new[] { a, b };
            var outTensor = a.Data - b.Data;
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad || b.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            AutogradEngine.AccumulateGrad(_a, gradOutput);
            AutogradEngine.AccumulateGrad(_b, -gradOutput);
        }
    }

    internal sealed class MulOp : IOpNode
    {
        private readonly Variable _a;
        private readonly Variable _b;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public MulOp(Variable a, Variable b)
        {
            _a = a;
            _b = b;
            Inputs = new[] { a, b };
            var outTensor = a.Data * b.Data;
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad || b.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            AutogradEngine.AccumulateGrad(_a, gradOutput * _b.Data);
            AutogradEngine.AccumulateGrad(_b, gradOutput * _a.Data);
        }
    }

    internal sealed class DivOp : IOpNode
    {
        private readonly Variable _a;
        private readonly Variable _b;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public DivOp(Variable a, Variable b)
        {
            _a = a;
            _b = b;
            Inputs = new[] { a, b };
            var outTensor = a.Data / b.Data;
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad || b.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            AutogradEngine.AccumulateGrad(_a, gradOutput / _b.Data);
            var gradB = -gradOutput * _a.Data / (_b.Data * _b.Data);
            AutogradEngine.AccumulateGrad(_b, gradB);
        }
    }

    internal sealed class NegOp : IOpNode
    {
        private readonly Variable _a;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public NegOp(Variable a)
        {
            _a = a;
            Inputs = new[] { a };
            var outTensor = -a.Data;
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            AutogradEngine.AccumulateGrad(_a, -gradOutput);
        }
    }

    internal sealed class MatMulOp : IOpNode
    {
        private readonly Variable _a;
        private readonly Variable _b;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public MatMulOp(Variable a, Variable b)
        {
            _a = a;
            _b = b;
            Inputs = new[] { a, b };
            var outTensor = TensorBlas.MatMul(a.Data, b.Data);
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad || b.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            // dL/dA = gradOutput @ B^T
            if (_a.RequiresGrad)
            {
                var bT = _b.Data.Transpose(0, 1);
                var gradA = TensorBlas.MatMul(gradOutput, bT);
                AutogradEngine.AccumulateGrad(_a, gradA);
            }

            // dL/dB = A^T @ gradOutput
            if (_b.RequiresGrad)
            {
                var aT = _a.Data.Transpose(0, 1);
                var gradB = TensorBlas.MatMul(aT, gradOutput);
                AutogradEngine.AccumulateGrad(_b, gradB);
            }
        }
    }

    internal sealed class ReLUOp : IOpNode
    {
        private readonly Variable _a;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public ReLUOp(Variable a)
        {
            _a = a;
            Inputs = new[] { a };
            var outTensor = TensorOps.ReLU(a.Data);
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            if (!_a.RequiresGrad) return;

            var gradA = new Tensor<float>(_a.Data.Shape);
            var aData = _a.Data;

            gradA.ForEachCoordinate(coords =>
            {
                gradA[coords] = aData[coords] > 0f ? gradOutput[coords] : 0f;
            });

            AutogradEngine.AccumulateGrad(_a, gradA);
        }
    }

    internal sealed class SigmoidOp : IOpNode
    {
        private readonly Variable _a;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public SigmoidOp(Variable a)
        {
            _a = a;
            Inputs = new[] { a };
            var outTensor = TensorOps.Sigmoid(a.Data);
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            if (!_a.RequiresGrad) return;

            // d(sigmoid)/dx = sigmoid(x) * (1 - sigmoid(x))
            var s = Output.Data;
            var oneMinusS = -s + 1.0f;
            var gradA = gradOutput * s * oneMinusS;
            AutogradEngine.AccumulateGrad(_a, gradA);
        }
    }

    internal sealed class TanhOp : IOpNode
    {
        private readonly Variable _a;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public TanhOp(Variable a)
        {
            _a = a;
            Inputs = new[] { a };
            var outTensor = TensorOps.Tanh(a.Data);
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            if (!_a.RequiresGrad) return;

            // d(tanh)/dx = 1 - tanh^2(x)
            var t = Output.Data;
            var gradA = gradOutput * (-(t * t) + 1.0f);
            AutogradEngine.AccumulateGrad(_a, gradA);
        }
    }

    internal sealed class SumOp : IOpNode
    {
        private readonly Variable _a;
        private readonly int _axis;
        private readonly bool _keepDims;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public SumOp(Variable a, int axis = -1, bool keepDims = false)
        {
            _a = a;
            _axis = axis;
            _keepDims = keepDims;
            Inputs = new[] { a };
            var outTensor = TensorOps.Sum(a.Data, axis, keepDims);
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            if (!_a.RequiresGrad) return;

            // Broadcast gradOutput back to _a.Data.Shape
            var gradA = new Tensor<float>(_a.Data.Shape);
            if (_axis == -1)
            {
                float scalarGrad = gradOutput.Scalar;
                gradA.Fill(scalarGrad);
            }
            else
            {
                var broadcastGrad = gradOutput;
                if (!_keepDims)
                {
                    int ax = _axis < 0 ? _axis + _a.Data.Rank : _axis;
                    broadcastGrad = broadcastGrad.Unsqueeze(ax);
                }

                _a.Data.ForEachCoordinate(coords =>
                {
                    int ax = _axis < 0 ? _axis + _a.Data.Rank : _axis;
                    var gradCoords = (int[])coords.Clone();
                    gradCoords[ax] = 0;
                    gradA[coords] = broadcastGrad[gradCoords];
                });
            }

            AutogradEngine.AccumulateGrad(_a, gradA);
        }
    }

    internal sealed class MeanOp : IOpNode
    {
        private readonly Variable _a;
        private readonly int _axis;
        private readonly bool _keepDims;
        public IReadOnlyList<Variable> Inputs { get; }
        public Variable Output { get; }

        public MeanOp(Variable a, int axis = -1, bool keepDims = false)
        {
            _a = a;
            _axis = axis;
            _keepDims = keepDims;
            Inputs = new[] { a };
            var outTensor = TensorOps.Mean(a.Data, axis, keepDims);
            Output = new Variable(outTensor, requiresGrad: a.RequiresGrad, creator: this);
        }

        public void Backward(Tensor<float> gradOutput)
        {
            if (!_a.RequiresGrad) return;

            int count = _axis == -1 ? _a.Data.Length : _a.Data.Shape[_axis < 0 ? _axis + _a.Data.Rank : _axis];
            float factor = 1.0f / (float)count;

            var gradA = new Tensor<float>(_a.Data.Shape);
            if (_axis == -1)
            {
                float scalarGrad = gradOutput.Scalar * factor;
                gradA.Fill(scalarGrad);
            }
            else
            {
                var broadcastGrad = gradOutput * factor;
                if (!_keepDims)
                {
                    int ax = _axis < 0 ? _axis + _a.Data.Rank : _axis;
                    broadcastGrad = broadcastGrad.Unsqueeze(ax);
                }

                _a.Data.ForEachCoordinate(coords =>
                {
                    int ax = _axis < 0 ? _axis + _a.Data.Rank : _axis;
                    var gradCoords = (int[])coords.Clone();
                    gradCoords[ax] = 0;
                    gradA[coords] = broadcastGrad[gradCoords];
                });
            }

            AutogradEngine.AccumulateGrad(_a, gradA);
        }
    }
}
