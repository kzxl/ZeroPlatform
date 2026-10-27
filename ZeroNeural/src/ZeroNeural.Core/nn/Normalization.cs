using System;
using ZeroNeural.Core.Autograd;
using ZeroTensor.Core;

namespace ZeroNeural.Core.nn
{
    /// <summary>
    /// Applies 2D Batch Normalization over a 4D input tensor (B, C, H, W).
    /// Tracks running mean and running variance for deterministic evaluation inference.
    /// </summary>
    public class BatchNorm2d : Module
    {
        public Variable Gamma { get; }
        public Variable Beta { get; }

        public Tensor<float> RunningMean { get; }
        public Tensor<float> RunningVar { get; }

        public int NumFeatures { get; }
        public float Eps { get; }
        public float Momentum { get; }

        public BatchNorm2d(int numFeatures, float eps = 1e-5f, float momentum = 0.1f)
        {
            if (numFeatures <= 0) throw new ArgumentOutOfRangeException(nameof(numFeatures));

            NumFeatures = numFeatures;
            Eps = eps;
            Momentum = momentum;

            Gamma = new Variable(Tensor.Ones(1, numFeatures, 1, 1), requiresGrad: true);
            Beta = new Variable(Tensor.Zeros<float>(1, numFeatures, 1, 1), requiresGrad: true);

            RunningMean = Tensor.Zeros<float>(numFeatures);
            RunningVar = Tensor.Ones(numFeatures);
        }

        public override Variable Forward(Variable input)
        {
            if (input.Rank != 4)
            {
                throw new ArgumentException($"BatchNorm2d expects 4D tensor (B, C, H, W), got rank {input.Rank}.");
            }

            int batch = input.Shape[0];
            int c = input.Shape[1];
            int h = input.Shape[2];
            int w = input.Shape[3];
            int spatialCount = batch * h * w;

            var outData = new Tensor<float>(batch, c, h, w);

            if (IsTraining)
            {
                // Compute batch mean and variance per channel
                for (int ch = 0; ch < c; ch++)
                {
                    float sum = 0f;
                    for (int b = 0; b < batch; b++)
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                                sum += input.Data[b, ch, y, x];

                    float mean = sum / spatialCount;

                    float sumSqDiff = 0f;
                    for (int b = 0; b < batch; b++)
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                float diff = input.Data[b, ch, y, x] - mean;
                                sumSqDiff += diff * diff;
                            }

                    float var = sumSqDiff / spatialCount;

                    // Update running statistics
                    RunningMean[ch] = (1.0f - Momentum) * RunningMean[ch] + Momentum * mean;
                    RunningVar[ch] = (1.0f - Momentum) * RunningVar[ch] + Momentum * var;

                    float invStd = 1.0f / (float)Math.Sqrt(var + Eps);
                    float gamma = Gamma.Data[0, ch, 0, 0];
                    float beta = Beta.Data[0, ch, 0, 0];

                    for (int b = 0; b < batch; b++)
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                float xHat = (input.Data[b, ch, y, x] - mean) * invStd;
                                outData[b, ch, y, x] = gamma * xHat + beta;
                            }
                }
            }
            else
            {
                // Inference mode using running statistics
                for (int ch = 0; ch < c; ch++)
                {
                    float mean = RunningMean[ch];
                    float var = RunningVar[ch];
                    float invStd = 1.0f / (float)Math.Sqrt(var + Eps);
                    float gamma = Gamma.Data[0, ch, 0, 0];
                    float beta = Beta.Data[0, ch, 0, 0];

                    for (int b = 0; b < batch; b++)
                        for (int y = 0; y < h; y++)
                            for (int x = 0; x < w; x++)
                            {
                                float xHat = (input.Data[b, ch, y, x] - mean) * invStd;
                                outData[b, ch, y, x] = gamma * xHat + beta;
                            }
                }
            }

            return new Variable(outData, requiresGrad: input.RequiresGrad || Gamma.RequiresGrad || Beta.RequiresGrad);
        }
    }

    /// <summary>
    /// Applies Layer Normalization over the feature dimension.
    /// </summary>
    public class LayerNorm : Module
    {
        public Variable Gamma { get; }
        public Variable Beta { get; }
        public int NormalizedShape { get; }
        public float Eps { get; }

        public LayerNorm(int normalizedShape, float eps = 1e-5f)
        {
            NormalizedShape = normalizedShape;
            Eps = eps;

            Gamma = new Variable(Tensor.Ones(normalizedShape), requiresGrad: true);
            Beta = new Variable(Tensor.Zeros<float>(normalizedShape), requiresGrad: true);
        }

        public override Variable Forward(Variable input)
        {
            int featureAxis = input.Rank - 1;
            var mean = input.Mean(axis: featureAxis, keepDims: true);
            var diff = input - mean;
            var sqDiff = diff * diff;
            var var = sqDiff.Mean(axis: featureAxis, keepDims: true);

            // invStd = 1 / sqrt(var + eps)
            var varEps = var + Eps;
            var std = new Variable(TensorOps.Sqrt(varEps.Data), requiresGrad: var.RequiresGrad, creator: varEps.Creator);
            var xHat = diff / std;

            return xHat * Gamma + Beta;
        }
    }
}
