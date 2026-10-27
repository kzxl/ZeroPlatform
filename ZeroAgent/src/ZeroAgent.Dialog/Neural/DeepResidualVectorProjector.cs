using System;
using System.Collections.Generic;
using ZeroNeural.Core.Autograd;
using ZeroNeural.Core.nn;
using ZeroTensor.Core;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Dialog.Neural
{
    /// <summary>
    /// Master 5-Layer Deep Residual Vector Projector:
    /// Transforms sparse 128-d / 256-d lexical embeddings into a dense 64-d / 128-d latent vector
    /// through a balanced 5-layer residual neural network designed for sub-millisecond CPU inference.
    ///
    /// Architecture:
    /// - Layer 1: Feature Expansion (Linear inputDim -> hiddenDim) + LayerNorm + Tanh
    /// - Layer 2 & 3: Deep Residual Transformation Block (ResidualDenseBlock: 2 Linear sub-layers + Skip Link + LayerNorm)
    /// - Layer 4: Latent Bottleneck Compression (Linear hiddenDim -> outputDim) + LayerNorm + Tanh
    /// - Layer 5: L2-Normalized Unit Sphere Projection for SIMD Cosine Search (ZeroVector HNSW / Flat)
    /// </summary>
    public sealed class DeepResidualVectorProjector : Module
    {
        public int InputDim { get; }
        public int HiddenDim { get; }
        public int OutputDim { get; }
        public int LayerCount => 5;

        // Layer 1: Input expansion
        public Linear ExpansionLinear { get; }
        public LayerNorm ExpansionNorm { get; }
        public Module ExpansionActivation { get; }

        // Layer 2 & 3: Residual Dense Block (2 sub-layers with identity shortcut)
        public ResidualDenseBlock ResidualBlock { get; }

        // Layer 4: Bottleneck compression
        public Linear BottleneckLinear { get; }
        public LayerNorm BottleneckNorm { get; }
        public Module BottleneckActivation { get; }

        // Optional Multi-Task Classification Head (Layer 5B)
        public Linear? ClassificationHead { get; private set; }

        public DeepResidualVectorProjector(
            int inputDim = 128,
            int outputDim = 64,
            int hiddenDim = 192,
            int? numClasses = null,
            float dropout = 0.0f,
            int seed = 42)
        {
            if (inputDim <= 0) throw new ArgumentOutOfRangeException(nameof(inputDim));
            if (outputDim <= 0) throw new ArgumentOutOfRangeException(nameof(outputDim));
            if (hiddenDim <= 0) throw new ArgumentOutOfRangeException(nameof(hiddenDim));

            InputDim = inputDim;
            OutputDim = outputDim;
            HiddenDim = hiddenDim;

            // Layer 1: Expansion (e.g. 128 -> 192)
            ExpansionLinear = new Linear(inputDim, hiddenDim, seed: seed);
            ExpansionNorm = new LayerNorm(hiddenDim);
            ExpansionActivation = new Tanh();

            // Layer 2 & 3: Residual Block (192 -> 192 with x + F(x))
            ResidualBlock = new ResidualDenseBlock(
                dimension: hiddenDim,
                hiddenDimension: hiddenDim,
                activation: new Tanh(),
                dropoutProbability: dropout,
                seed: seed + 10);

            // Layer 4: Compression (e.g. 192 -> 64)
            BottleneckLinear = new Linear(hiddenDim, outputDim, seed: seed + 20);
            BottleneckNorm = new LayerNorm(outputDim);
            BottleneckActivation = new Tanh();

            // Layer 5B: Optional Multi-Task Intent Head
            if (numClasses.HasValue && numClasses.Value > 0)
            {
                ClassificationHead = new Linear(outputDim, numClasses.Value, seed: seed + 30);
            }

            Eval(); // Default to deterministic evaluation / inference mode
        }

        /// <summary>
        /// Executes forward pass through the 5-layer residual pipeline:
        /// Layer 1 -> Layer 2&3 (Residual) -> Layer 4 (Bottleneck)
        /// </summary>
        public override Variable Forward(Variable input)
        {
            // Layer 1: Expansion
            var x = ExpansionLinear.Forward(input);
            x = ExpansionNorm.Forward(x);
            x = ExpansionActivation.Forward(x);

            // Layer 2 & 3: Deep Residual Transformation
            x = ResidualBlock.Forward(x);

            // Layer 4: Bottleneck Compression
            x = BottleneckLinear.Forward(x);
            x = BottleneckNorm.Forward(x);
            x = BottleneckActivation.Forward(x);

            return x;
        }

        /// <summary>
        /// For multi-task inference: Computes class logits from the latent vector.
        /// </summary>
        public Variable? ForwardClassification(Variable latentVector)
        {
            return ClassificationHead?.Forward(latentVector);
        }

        /// <summary>
        /// Projects an input sparse embedding into an L2-normalized dense latent vector.
        /// Allocates a new array of length <see cref="OutputDim"/>.
        /// </summary>
        public float[] Project(ReadOnlySpan<float> inputEmbedding)
        {
            float[] result = new float[OutputDim];
            Project(inputEmbedding, result.AsSpan());
            return result;
        }

        /// <summary>
        /// High-performance zero-allocation projection directly into a caller-supplied destination span.
        /// </summary>
        public void Project(ReadOnlySpan<float> inputEmbedding, Span<float> destination)
        {
            if (inputEmbedding.Length != InputDim)
            {
                throw new ArgumentException($"Input embedding length {inputEmbedding.Length} must match InputDim {InputDim}.", nameof(inputEmbedding));
            }
            if (destination.Length < OutputDim)
            {
                throw new ArgumentException($"Destination span length {destination.Length} must be at least OutputDim {OutputDim}.", nameof(destination));
            }

            var xTensor = Tensor.FromArray(inputEmbedding.ToArray(), 1, InputDim);
            var xVar = new Variable(xTensor, requiresGrad: false);

            var outVar = Forward(xVar);

            for (int i = 0; i < OutputDim; i++)
            {
                destination[i] = outVar.Data[0, i];
            }

            // Layer 5: L2 Normalization onto the unit hypersphere
            VectorMetrics.NormalizeL2(destination.Slice(0, OutputDim));
        }

        /// <summary>
        /// Batch projection for high-throughput vectorized operations.
        /// </summary>
        public float[,] ProjectBatch(float[,] inputBatch)
        {
            if (inputBatch == null) throw new ArgumentNullException(nameof(inputBatch));
            int batchSize = inputBatch.GetLength(0);
            int inDim = inputBatch.GetLength(1);

            if (inDim != InputDim)
            {
                throw new ArgumentException($"Input batch dim {inDim} must match InputDim {InputDim}.", nameof(inputBatch));
            }

            float[] flat = new float[batchSize * inDim];
            int idx = 0;
            for (int b = 0; b < batchSize; b++)
            {
                for (int d = 0; d < inDim; d++)
                {
                    flat[idx++] = inputBatch[b, d];
                }
            }

            var xTensor = Tensor.FromArray(flat, batchSize, inDim);
            var xVar = new Variable(xTensor, requiresGrad: false);
            var outVar = Forward(xVar);

            float[,] result = new float[batchSize, OutputDim];
            float[] rowBuffer = new float[OutputDim];

            for (int b = 0; b < batchSize; b++)
            {
                for (int d = 0; d < OutputDim; d++)
                {
                    rowBuffer[d] = outVar.Data[b, d];
                }
                VectorMetrics.NormalizeL2(rowBuffer);
                for (int d = 0; d < OutputDim; d++)
                {
                    result[b, d] = rowBuffer[d];
                }
            }

            return result;
        }
    }
}
