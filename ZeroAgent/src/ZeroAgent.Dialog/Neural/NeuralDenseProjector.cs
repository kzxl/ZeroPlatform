using System;

namespace ZeroAgent.Dialog.Neural
{
    /// <summary>
    /// Projects sparse lexical embeddings into a dense non-linear latent space (e.g. 64-d)
    /// powered by an underlying 5-layer Deep Residual Vector Projector with LayerNorm and Skip Connections.
    /// Fully backward-compatible while upgrading the internal representation to deep non-linear manifolds.
    /// </summary>
    public sealed class NeuralDenseProjector
    {
        private readonly DeepResidualVectorProjector _projector;

        public int InputDim => _projector.InputDim;
        public int OutputDim => _projector.OutputDim;
        public int HiddenDim => _projector.HiddenDim;
        public int LayerCount => _projector.LayerCount;

        public DeepResidualVectorProjector DeepProjector => _projector;

        public NeuralDenseProjector(int inputDim = 128, int outputDim = 64, int seed = 42)
            : this(inputDim, outputDim, Math.Max(inputDim, outputDim * 2), seed)
        {
        }

        public NeuralDenseProjector(int inputDim, int outputDim, int hiddenDim, int seed = 42)
        {
            _projector = new DeepResidualVectorProjector(inputDim, outputDim, hiddenDim, seed: seed);
        }

        /// <summary>
        /// Projects an input sparse embedding into an L2-normalized dense latent vector.
        /// </summary>
        public float[] Project(ReadOnlySpan<float> inputEmbedding)
        {
            return _projector.Project(inputEmbedding);
        }

        /// <summary>
        /// Zero-allocation projection directly into destination span.
        /// </summary>
        public void Project(ReadOnlySpan<float> inputEmbedding, Span<float> destination)
        {
            _projector.Project(inputEmbedding, destination);
        }
    }
}
