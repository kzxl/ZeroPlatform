using System;
using System.Collections.Generic;
using ZeroAgent.Dialog.Embedding;
using ZeroNeural.Core.Autograd;
using ZeroNeural.Core.nn;
using ZeroTensor.Core;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Dialog.Neural
{
    /// <summary>
    /// Item candidate scored and reranked by the Cross-Encoder.
    /// </summary>
    public sealed class RerankResult<T>
    {
        public T Item { get; }
        public int OriginalRank { get; }
        public float InitialScore { get; }
        public float RerankScore { get; }

        public RerankResult(T item, int originalRank, float initialScore, float rerankScore)
        {
            Item = item;
            OriginalRank = originalRank;
            InitialScore = initialScore;
            RerankScore = rerankScore;
        }
    }

    /// <summary>
    /// Deep Neural Cross-Encoder Reranker:
    /// Takes candidate items retrieved by Bi-Encoder / Vector Search (ZeroVector HNSW / Flat)
    /// and performs fine-grained non-linear cross-attention modeling between query and candidate text.
    ///
    /// Cross-Feature Representation:
    /// Constructs relational interaction vector: z = [u, v, |u - v|, u * v] (dimension: 4 * D)
    /// Passes through a 3-layer neural network with Residual connection and Sigmoid activation
    /// to output a high-precision relevance score in [0.0, 1.0].
    /// </summary>
    public sealed class SemanticCrossEncoderReranker : Module
    {
        private readonly LexicalSemanticEmbedder _embedder;
        private readonly Linear _inputLinear;
        private readonly LayerNorm _inputNorm;
        private readonly Module _inputActivation;
        private readonly ResidualDenseBlock _residualBlock;
        private readonly Linear _outputLinear;
        private readonly Sigmoid _outputActivation;

        public int Dimension { get; }
        public int FeatureDim => Dimension * 4;

        public SemanticCrossEncoderReranker(
            int dimension = 128,
            int hiddenDim = 128,
            LexicalSemanticEmbedder? embedder = null,
            int seed = 42)
        {
            if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension));
            if (hiddenDim <= 0) throw new ArgumentOutOfRangeException(nameof(hiddenDim));

            Dimension = dimension;
            _embedder = embedder ?? new HybridSemanticEmbedder(dimension);

            // Layer 1: Relational projection (4*D -> hiddenDim)
            _inputLinear = new Linear(FeatureDim, hiddenDim, seed: seed);
            _inputNorm = new LayerNorm(hiddenDim);
            _inputActivation = new Tanh();

            // Layer 2: Deep Residual interaction block (hiddenDim -> hiddenDim)
            _residualBlock = new ResidualDenseBlock(
                dimension: hiddenDim,
                hiddenDimension: hiddenDim,
                activation: new Tanh(),
                seed: seed + 10);

            // Layer 3: Relevance scoring head (hiddenDim -> 1) with Sigmoid
            _outputLinear = new Linear(hiddenDim, 1, seed: seed + 20);
            _outputActivation = new Sigmoid();

            Eval(); // Default to evaluation mode
        }

        /// <summary>
        /// Forward pass calculating raw relevance score from interactive feature vector z.
        /// </summary>
        public override Variable Forward(Variable input)
        {
            var h = _inputLinear.Forward(input);
            h = _inputNorm.Forward(h);
            h = _inputActivation.Forward(h);

            h = _residualBlock.Forward(h);

            var logits = _outputLinear.Forward(h);
            return _outputActivation.Forward(logits);
        }

        /// <summary>
        /// Computes Cross-Encoder relevance score between query and document text.
        /// </summary>
        public float Score(string query, string document)
        {
            if (string.IsNullOrWhiteSpace(query) || string.IsNullOrWhiteSpace(document)) return 0.0f;

            var u = _embedder.Embed(query);
            var v = _embedder.Embed(document);

            return Score(u, v);
        }

        /// <summary>
        /// Computes Cross-Encoder relevance score directly from query and document embeddings.
        /// </summary>
        public float Score(ReadOnlySpan<float> queryEmbedding, ReadOnlySpan<float> documentEmbedding)
        {
            if (queryEmbedding.Length != Dimension || documentEmbedding.Length != Dimension)
            {
                throw new ArgumentException($"Embeddings must match configured dimension {Dimension}.");
            }

            // Construct interaction vector: [u, v, |u - v|, u * v]
            float[] featureBuffer = new float[FeatureDim];
            ConstructInteractionVector(queryEmbedding, documentEmbedding, featureBuffer);

            var xTensor = Tensor.FromArray(featureBuffer, 1, FeatureDim);
            var xVar = new Variable(xTensor, requiresGrad: false);

            var outVar = Forward(xVar);
            return outVar.Data[0, 0];
        }

        /// <summary>
        /// Reranks a list of candidate items using deep cross-attention interaction.
        /// </summary>
        public IReadOnlyList<RerankResult<T>> Rerank<T>(
            string query,
            IReadOnlyList<T> candidates,
            Func<T, string> textExtractor,
            int topK = 3,
            float minRerankScore = 0.0f)
        {
            if (string.IsNullOrWhiteSpace(query) || candidates == null || candidates.Count == 0)
            {
                return Array.Empty<RerankResult<T>>();
            }

            var queryEmb = _embedder.Embed(query);
            var results = new List<RerankResult<T>>(candidates.Count);

            float[] featureBuffer = new float[FeatureDim];

            for (int i = 0; i < candidates.Count; i++)
            {
                var item = candidates[i];
                string docText = textExtractor(item);
                var docEmb = _embedder.Embed(docText);

                ConstructInteractionVector(queryEmb, docEmb, featureBuffer);

                var xTensor = Tensor.FromArray(featureBuffer, 1, FeatureDim);
                var xVar = new Variable(xTensor, requiresGrad: false);
                var outVar = Forward(xVar);
                float score = outVar.Data[0, 0];

                float initialCosine = VectorMetrics.CosineSimilarity(queryEmb, docEmb);
                float fusedRerankScore = (0.6f * Math.Max(0f, initialCosine)) + (0.4f * score);

                if (fusedRerankScore >= minRerankScore)
                {
                    results.Add(new RerankResult<T>(item, i, initialCosine, fusedRerankScore));
                }
            }

            // Sort descending by Cross-Encoder score
            results.Sort((a, b) => b.RerankScore.CompareTo(a.RerankScore));

            if (topK > 0 && results.Count > topK)
            {
                results.RemoveRange(topK, results.Count - topK);
            }

            return results;
        }

        private static void ConstructInteractionVector(
            ReadOnlySpan<float> u,
            ReadOnlySpan<float> v,
            Span<float> destination)
        {
            int d = u.Length;

            // 1. u
            u.CopyTo(destination.Slice(0, d));

            // 2. v
            v.CopyTo(destination.Slice(d, d));

            // 3. |u - v|
            for (int i = 0; i < d; i++)
            {
                destination[2 * d + i] = Math.Abs(u[i] - v[i]);
            }

            // 4. u * v
            for (int i = 0; i < d; i++)
            {
                destination[3 * d + i] = u[i] * v[i];
            }
        }
    }
}
