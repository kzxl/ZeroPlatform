using System;
using ZeroNeural.Core.Autograd;

namespace ZeroNeural.Core.nn
{
    /// <summary>
    /// Residual Dense Transformation Block: Y = LayerNorm(X + Linear2(Activation(LayerNorm(Linear1(X)))))).
    /// Incorporates identity skip connections (ResNet-style) and Layer Normalization to eliminate
    /// vanishing gradients across deep representations while maximizing non-linear expressiveness.
    /// </summary>
    public class ResidualDenseBlock : Module
    {
        public Linear Linear1 { get; }
        public LayerNorm Norm1 { get; }
        public Module Activation { get; }
        public Linear Linear2 { get; }
        public LayerNorm Norm2 { get; }
        public Dropout? Dropout { get; }

        public int Dimension { get; }
        public int HiddenDimension { get; }

        public ResidualDenseBlock(
            int dimension,
            int? hiddenDimension = null,
            Module? activation = null,
            float dropoutProbability = 0.0f,
            int seed = 42)
        {
            if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension));

            Dimension = dimension;
            HiddenDimension = hiddenDimension ?? dimension;

            Linear1 = new Linear(dimension, HiddenDimension, seed: seed);
            Norm1 = new LayerNorm(HiddenDimension);
            Activation = activation ?? new Tanh();
            Linear2 = new Linear(HiddenDimension, dimension, seed: seed + 1);
            Norm2 = new LayerNorm(dimension);

            if (dropoutProbability > 0.0f)
            {
                Dropout = new Dropout(dropoutProbability, seed: seed + 2);
            }
        }

        public override Variable Forward(Variable input)
        {
            var residual = input;

            var h = Linear1.Forward(input);
            h = Norm1.Forward(h);
            h = Activation.Forward(h);

            if (Dropout != null)
            {
                h = Dropout.Forward(h);
            }

            h = Linear2.Forward(h);
            h = Norm2.Forward(h);

            return residual + h;
        }
    }
}
