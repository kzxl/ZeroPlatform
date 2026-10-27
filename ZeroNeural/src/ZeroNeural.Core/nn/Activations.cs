using System;
using ZeroNeural.Core.Autograd;

namespace ZeroNeural.Core.nn
{
    /// <summary>
    /// Rectified Linear Unit activation layer: max(0, x).
    /// </summary>
    public class ReLU : Module
    {
        public override Variable Forward(Variable input) => input.ReLU();
    }

    /// <summary>
    /// Sigmoid activation layer: 1 / (1 + exp(-x)).
    /// </summary>
    public class Sigmoid : Module
    {
        public override Variable Forward(Variable input) => input.Sigmoid();
    }

    /// <summary>
    /// Hyperbolic Tangent activation layer: tanh(x).
    /// </summary>
    public class Tanh : Module
    {
        public override Variable Forward(Variable input) => input.Tanh();
    }

    /// <summary>
    /// Inverted Dropout layer: during training, zeros out elements with probability p and scales the rest by 1 / (1 - p).
    /// </summary>
    public class Dropout : Module
    {
        private readonly float _p;
        private readonly Random _rng;

        public float Probability => _p;

        public Dropout(float p = 0.5f, int seed = 42)
        {
            if (p < 0f || p >= 1.0f) throw new ArgumentOutOfRangeException(nameof(p), "Dropout probability must be in [0, 1).");
            _p = p;
            _rng = new Random(seed);
        }

        public override Variable Forward(Variable input)
        {
            if (!IsTraining || _p == 0f)
            {
                return input;
            }

            float scale = 1.0f / (1.0f - _p);
            var maskData = input.Data.Clone();
            maskData.ForEachCoordinate(coords =>
            {
                maskData[coords] = _rng.NextDouble() < _p ? 0f : scale;
            });

            var maskVar = new Variable(maskData, requiresGrad: false);
            return input * maskVar;
        }
    }
}
