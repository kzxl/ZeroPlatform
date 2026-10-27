using System;
using System.Collections.Generic;
using ZeroNeural.Core.Autograd;
using ZeroTensor.Core;

namespace ZeroNeural.Core.Loss
{
    /// <summary>
    /// Mean Squared Error loss function for regression tasks.
    /// </summary>
    public static class MSELoss
    {
        public static Variable Compute(Variable prediction, Variable target)
        {
            var diff = prediction - target;
            var sqDiff = diff * diff;
            return sqDiff.Mean();
        }
    }

    /// <summary>
    /// Binary Cross Entropy with integrated Sigmoid for numerically stable binary classification.
    /// Loss = max(x, 0) - x * y + log(1 + exp(-|x|)).
    /// </summary>
    public static class BCEWithLogitsLoss
    {
        public static Variable Compute(Variable logits, Variable targets)
        {
            if (logits.Shape != targets.Shape)
            {
                throw new ArgumentException($"Logits shape {logits.Shape} does not match targets shape {targets.Shape}.");
            }

            var lossTensor = new Tensor<float>(logits.Shape);
            var logData = logits.Data;
            var tarData = targets.Data;

            lossTensor.ForEachCoordinate(coords =>
            {
                float x = logData[coords];
                float y = tarData[coords];

                float maxVal = Math.Max(x, 0f);
                float negAbs = -Math.Abs(x);
                float log1pExp = (float)Math.Log(1.0 + Math.Exp(negAbs));

                lossTensor[coords] = maxVal - x * y + log1pExp;
            });

            var lossVar = new Variable(lossTensor, requiresGrad: logits.RequiresGrad || targets.RequiresGrad);

            // Backward node
            return new BCELossOp(logits, targets, lossVar).Output;
        }

        private sealed class BCELossOp : IOpNode
        {
            private readonly Variable _logits;
            private readonly Variable _targets;
            public IReadOnlyList<Variable> Inputs { get; }
            public Variable Output { get; }

            public BCELossOp(Variable logits, Variable targets, Variable lossTensor)
            {
                _logits = logits;
                _targets = targets;
                Inputs = new[] { logits, targets };
                Output = lossTensor.Mean();
                Output.Creator = this;
            }

            public void Backward(Tensor<float> gradOutput)
            {
                if (!_logits.RequiresGrad) return;

                // d(BCE)/dx = (sigmoid(x) - y) / N
                float scalarGrad = gradOutput.Scalar / (float)_logits.Length;
                var gradLogits = new Tensor<float>(_logits.Data.Shape);

                gradLogits.ForEachCoordinate(coords =>
                {
                    float x = _logits.Data[coords];
                    float y = _targets.Data[coords];
                    float sig = 1.0f / (1.0f + (float)Math.Exp(-x));
                    gradLogits[coords] = (sig - y) * scalarGrad;
                });

                AutogradEngine.AccumulateGrad(_logits, gradLogits);
            }
        }
    }

    /// <summary>
    /// Multi-class Cross Entropy loss with integrated Log-Softmax.
    /// </summary>
    public static class CrossEntropyLoss
    {
        /// <summary>
        /// Computes cross entropy loss given prediction logits (B, C) and target class indices (B).
        /// </summary>
        public static Variable Compute(Variable logits, int[] targetClassIndices)
        {
            if (logits.Rank != 2) throw new ArgumentException("Logits must be 2D tensor (Batch, Classes).", nameof(logits));
            int batch = logits.Shape[0];
            int classes = logits.Shape[1];

            if (targetClassIndices.Length != batch)
            {
                throw new ArgumentException($"Target indices length {targetClassIndices.Length} does not match batch size {batch}.");
            }

            // Compute Log-Softmax and negative log-likelihood
            var logSoftmax = new Tensor<float>(batch, classes);
            float totalLoss = 0f;

            for (int b = 0; b < batch; b++)
            {
                float maxVal = float.NegativeInfinity;
                for (int c = 0; c < classes; c++)
                {
                    if (logits.Data[b, c] > maxVal) maxVal = logits.Data[b, c];
                }

                float sumExp = 0f;
                for (int c = 0; c < classes; c++)
                {
                    sumExp += (float)Math.Exp(logits.Data[b, c] - maxVal);
                }

                float logSumExp = maxVal + (float)Math.Log(sumExp);

                for (int c = 0; c < classes; c++)
                {
                    logSoftmax[b, c] = logits.Data[b, c] - logSumExp;
                }

                int targetClass = targetClassIndices[b];
                totalLoss -= logSoftmax[b, targetClass];
            }

            float meanLoss = totalLoss / batch;
            var lossVar = new Variable(Tensor.FromArray(new[] { meanLoss }), requiresGrad: logits.RequiresGrad);

            return new CrossEntropyOp(logits, targetClassIndices, logSoftmax, lossVar).Output;
        }

        private sealed class CrossEntropyOp : IOpNode
        {
            private readonly Variable _logits;
            private readonly int[] _targetClassIndices;
            private readonly Tensor<float> _logSoftmax;
            public IReadOnlyList<Variable> Inputs { get; }
            public Variable Output { get; }

            public CrossEntropyOp(Variable logits, int[] targetClassIndices, Tensor<float> logSoftmax, Variable output)
            {
                _logits = logits;
                _targetClassIndices = targetClassIndices;
                _logSoftmax = logSoftmax;
                Inputs = new[] { logits };
                Output = output;
                Output.Creator = this;
            }

            public void Backward(Tensor<float> gradOutput)
            {
                if (!_logits.RequiresGrad) return;

                int batch = _logits.Shape[0];
                int classes = _logits.Shape[1];
                float scalarGrad = gradOutput.Scalar / (float)batch;

                var gradLogits = new Tensor<float>(batch, classes);

                for (int b = 0; b < batch; b++)
                {
                    int targetClass = _targetClassIndices[b];
                    for (int c = 0; c < classes; c++)
                    {
                        float prob = (float)Math.Exp(_logSoftmax[b, c]);
                        float grad = (c == targetClass) ? (prob - 1.0f) : prob;
                        gradLogits[b, c] = grad * scalarGrad;
                    }
                }

                AutogradEngine.AccumulateGrad(_logits, gradLogits);
            }
        }
    }
}
