using System;
using System.Collections.Generic;
using ZeroNeural.Core.Autograd;
using ZeroTensor.Core;

namespace ZeroNeural.Core.Optim
{
    /// <summary>
    /// Base class for all parameter optimizers.
    /// </summary>
    public abstract class Optimizer
    {
        protected readonly List<Variable> _parameters;

        public float LearningRate { get; set; }

        public IReadOnlyList<Variable> Parameters => _parameters;

        protected Optimizer(IEnumerable<Variable> parameters, float learningRate)
        {
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));
            _parameters = new List<Variable>(parameters);
            LearningRate = learningRate;
        }

        /// <summary>
        /// Updates parameter weights using computed gradients.
        /// </summary>
        public abstract void Step();

        /// <summary>
        /// Resets all parameter gradients to null or zeros.
        /// </summary>
        public void ZeroGrad()
        {
            for (int i = 0; i < _parameters.Count; i++)
            {
                _parameters[i].ZeroGrad();
            }
        }
    }

    /// <summary>
    /// Stochastic Gradient Descent with momentum and weight decay.
    /// </summary>
    public class SGD : Optimizer
    {
        public float Momentum { get; set; }
        public float WeightDecay { get; set; }

        private readonly Dictionary<Variable, Tensor<float>> _velocities = new Dictionary<Variable, Tensor<float>>();

        public SGD(IEnumerable<Variable> parameters, float learningRate = 0.01f, float momentum = 0.0f, float weightDecay = 0.0f)
            : base(parameters, learningRate)
        {
            Momentum = momentum;
            WeightDecay = weightDecay;
        }

        public override void Step()
        {
            foreach (var param in _parameters)
            {
                if (param.Grad == null) continue;

                var grad = param.Grad;
                var data = param.Data;

                if (WeightDecay != 0.0f)
                {
                    grad = grad + data * WeightDecay;
                }

                if (Momentum != 0.0f)
                {
                    if (!_velocities.TryGetValue(param, out var v))
                    {
                        v = Tensor.Zeros<float>(param.Data.Shape.ToArray());
                        _velocities[param] = v;
                    }

                    // v = momentum * v + grad
                    var vSpan = v.AsSpan();
                    var gSpan = grad.AsReadOnlySpan();
                    for (int i = 0; i < vSpan.Length; i++)
                    {
                        vSpan[i] = Momentum * vSpan[i] + gSpan[i];
                    }

                    // param = param - lr * v
                    var dSpan = data.AsSpan();
                    for (int i = 0; i < dSpan.Length; i++)
                    {
                        dSpan[i] -= LearningRate * vSpan[i];
                    }
                }
                else
                {
                    // param = param - lr * grad
                    var dSpan = data.AsSpan();
                    var gSpan = grad.AsReadOnlySpan();
                    for (int i = 0; i < dSpan.Length; i++)
                    {
                        dSpan[i] -= LearningRate * gSpan[i];
                    }
                }
            }
        }
    }

    /// <summary>
    /// Adam optimizer with first and second order momentum and bias correction.
    /// </summary>
    public class Adam : Optimizer
    {
        public float Beta1 { get; set; }
        public float Beta2 { get; set; }
        public float Eps { get; set; }
        public float WeightDecay { get; set; }

        private int _stepCount;
        private readonly Dictionary<Variable, Tensor<float>> _m = new Dictionary<Variable, Tensor<float>>();
        private readonly Dictionary<Variable, Tensor<float>> _v = new Dictionary<Variable, Tensor<float>>();

        public Adam(
            IEnumerable<Variable> parameters,
            float learningRate = 0.001f,
            float beta1 = 0.9f,
            float beta2 = 0.999f,
            float eps = 1e-8f,
            float weightDecay = 0.0f)
            : base(parameters, learningRate)
        {
            Beta1 = beta1;
            Beta2 = beta2;
            Eps = eps;
            WeightDecay = weightDecay;
        }

        public override void Step()
        {
            _stepCount++;
            float biasCorrection1 = 1.0f - (float)Math.Pow(Beta1, _stepCount);
            float biasCorrection2 = 1.0f - (float)Math.Pow(Beta2, _stepCount);

            foreach (var param in _parameters)
            {
                if (param.Grad == null) continue;

                var grad = param.Grad;
                var data = param.Data;

                if (WeightDecay != 0.0f)
                {
                    grad = grad + data * WeightDecay;
                }

                if (!_m.TryGetValue(param, out var mTensor))
                {
                    mTensor = Tensor.Zeros<float>(data.Shape.ToArray());
                    _m[param] = mTensor;
                }

                if (!_v.TryGetValue(param, out var vTensor))
                {
                    vTensor = Tensor.Zeros<float>(data.Shape.ToArray());
                    _v[param] = vTensor;
                }

                var dSpan = data.AsSpan();
                var gSpan = grad.AsReadOnlySpan();
                var mSpan = mTensor.AsSpan();
                var vSpan = vTensor.AsSpan();

                for (int i = 0; i < dSpan.Length; i++)
                {
                    float g = gSpan[i];
                    mSpan[i] = Beta1 * mSpan[i] + (1.0f - Beta1) * g;
                    vSpan[i] = Beta2 * vSpan[i] + (1.0f - Beta2) * g * g;

                    float mHat = mSpan[i] / biasCorrection1;
                    float vHat = vSpan[i] / biasCorrection2;

                    dSpan[i] -= LearningRate * mHat / ((float)Math.Sqrt(vHat) + Eps);
                }
            }
        }
    }

    /// <summary>
    /// AdamW optimizer with decoupled weight decay.
    /// </summary>
    public class AdamW : Optimizer
    {
        public float Beta1 { get; set; }
        public float Beta2 { get; set; }
        public float Eps { get; set; }
        public float WeightDecay { get; set; }

        private int _stepCount;
        private readonly Dictionary<Variable, Tensor<float>> _m = new Dictionary<Variable, Tensor<float>>();
        private readonly Dictionary<Variable, Tensor<float>> _v = new Dictionary<Variable, Tensor<float>>();

        public AdamW(
            IEnumerable<Variable> parameters,
            float learningRate = 0.001f,
            float beta1 = 0.9f,
            float beta2 = 0.999f,
            float eps = 1e-8f,
            float weightDecay = 0.01f)
            : base(parameters, learningRate)
        {
            Beta1 = beta1;
            Beta2 = beta2;
            Eps = eps;
            WeightDecay = weightDecay;
        }

        public override void Step()
        {
            _stepCount++;
            float biasCorrection1 = 1.0f - (float)Math.Pow(Beta1, _stepCount);
            float biasCorrection2 = 1.0f - (float)Math.Pow(Beta2, _stepCount);

            foreach (var param in _parameters)
            {
                if (param.Grad == null) continue;

                var grad = param.Grad;
                var data = param.Data;

                // Decoupled weight decay: w = w * (1 - lr * weightDecay)
                if (WeightDecay != 0.0f)
                {
                    var dataSpan = data.AsSpan();
                    float decayFactor = 1.0f - LearningRate * WeightDecay;
                    for (int i = 0; i < dataSpan.Length; i++)
                    {
                        dataSpan[i] *= decayFactor;
                    }
                }

                if (!_m.TryGetValue(param, out var mTensor))
                {
                    mTensor = Tensor.Zeros<float>(data.Shape.ToArray());
                    _m[param] = mTensor;
                }

                if (!_v.TryGetValue(param, out var vTensor))
                {
                    vTensor = Tensor.Zeros<float>(data.Shape.ToArray());
                    _v[param] = vTensor;
                }

                var dSpan = data.AsSpan();
                var gSpan = grad.AsReadOnlySpan();
                var mSpan = mTensor.AsSpan();
                var vSpan = vTensor.AsSpan();

                for (int i = 0; i < dSpan.Length; i++)
                {
                    float g = gSpan[i];
                    mSpan[i] = Beta1 * mSpan[i] + (1.0f - Beta1) * g;
                    vSpan[i] = Beta2 * vSpan[i] + (1.0f - Beta2) * g * g;

                    float mHat = mSpan[i] / biasCorrection1;
                    float vHat = vSpan[i] / biasCorrection2;

                    dSpan[i] -= LearningRate * mHat / ((float)Math.Sqrt(vHat) + Eps);
                }
            }
        }
    }
}
