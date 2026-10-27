using System;
using ZeroNeural.Core.Autograd;
using ZeroTensor.Core;

namespace ZeroNeural.Core.nn
{
    /// <summary>
    /// Fully connected linear (dense) transformation layer: Y = X @ W + B.
    /// Weights are initialized using Kaiming Uniform distribution.
    /// </summary>
    public class Linear : Module
    {
        public Variable Weight { get; }
        public Variable? Bias { get; }

        public int InFeatures { get; }
        public int OutFeatures { get; }

        public Linear(int inFeatures, int outFeatures, bool hasBias = true, int seed = 42)
        {
            if (inFeatures <= 0) throw new ArgumentOutOfRangeException(nameof(inFeatures));
            if (outFeatures <= 0) throw new ArgumentOutOfRangeException(nameof(outFeatures));

            InFeatures = inFeatures;
            OutFeatures = outFeatures;

            float k = 1.0f / inFeatures;
            float bound = (float)Math.Sqrt(k);

            var wTensor = Tensor.Uniform(-bound, bound, seed, inFeatures, outFeatures);
            Weight = new Variable(wTensor, requiresGrad: true);

            if (hasBias)
            {
                var bTensor = Tensor.Uniform(-bound, bound, seed + 1, 1, outFeatures);
                Bias = new Variable(bTensor, requiresGrad: true);
            }
        }

        public override Variable Forward(Variable input)
        {
            // If input is 1D vector, unsqueeze to (1, inFeatures)
            bool is1D = input.Rank == 1;
            var x = input;
            if (is1D)
            {
                x = new Variable(input.Data.Unsqueeze(0), requiresGrad: input.RequiresGrad, creator: input.Creator);
            }

            var output = x.MatMul(Weight);

            if (Bias != null)
            {
                output = output + Bias;
            }

            return output;
        }
    }
}
