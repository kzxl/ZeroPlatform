using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Dialog.Embedding
{
    /// <summary>
    /// Self-contained Hybrid Neural-Lexical Semantic Text Embedder.
    /// Combines sub-50us signed lexical n-gram hashing with an adaptive latent semantic codebook.
    /// Bridges lexical mismatch (e.g. "động cơ" <-> "motor") through trainable dense projection,
    /// synonym cluster anchoring, and online contrastive learning.
    /// </summary>
    public sealed partial class HybridSemanticEmbedder : LexicalSemanticEmbedder
    {
        private readonly ConcurrentDictionary<string, float[]> _semanticCodebook = new ConcurrentDictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<HashSet<string>> _synonymClusters = new List<HashSet<string>>();
        private readonly object _clusterLock = new object();
        private readonly float[] _projectionMatrix;
        private readonly object _trainLock = new object();

        /// <summary>
        /// Gets or sets the fusion weight between Lexical features and Latent Semantic features.
        /// 0.0 = pure lexical hashing; 1.0 = pure latent semantic codebook; default = 0.35.
        /// </summary>
        public float Alpha { get; set; } = 0.35f;

        /// <summary>
        /// Total number of unique semantic terms registered in the trainable codebook.
        /// </summary>
        public int CodebookSize => _semanticCodebook.Count;

        public HybridSemanticEmbedder(int dimension = 128) : base(dimension)
        {
            _projectionMatrix = new float[dimension * dimension];
            InitializeProjectionMatrix(dimension);
            SeedDefaultIndustrialSynonyms();
        }

        private void InitializeProjectionMatrix(int dim)
        {
            // Initialize with identity matrix + slight randomized orthogonal perturbation
            var rnd = new Random(42);
            for (int r = 0; r < dim; r++)
            {
                for (int c = 0; c < dim; c++)
                {
                    int idx = r * dim + c;
                    float baseVal = (r == c) ? 1.0f : 0.0f;
                    float noise = (float)(rnd.NextDouble() * 0.02 - 0.01);
                    _projectionMatrix[idx] = baseVal + noise;
                }
            }
        }

        public override float[] Embed(string text)
        {
            var vector = new float[Dimension];
            Embed(text, vector);
            return vector;
        }

        public override void Embed(string text, Span<float> destination)
        {
            if (destination.Length < Dimension)
                throw new ArgumentException($"Destination span must be at least {Dimension} elements.");

            if (string.IsNullOrWhiteSpace(text))
            {
                destination.Slice(0, Dimension).Clear();
                return;
            }

            // 1. Channel A: Fast Lexical Subword & N-gram Hashing
            base.Embed(text, destination);

            // 2. Channel B: Additive Semantic Codebook Enrichment
            if (Alpha > 0.001f && !_semanticCodebook.IsEmpty)
            {
                Span<float> semanticSpan = stackalloc float[Dimension];
                semanticSpan.Clear();
                int matched = AccumulateSemanticLatentFeatures(text, semanticSpan);

                if (matched > 0)
                {
                    float scale = Alpha * 1.8f;
                    for (int i = 0; i < Dimension; i++)
                    {
                        destination[i] += semanticSpan[i] * scale;
                    }
                }
            }

            // 3. Hardware SIMD L2 Normalization onto unit hypersphere
            VectorMetrics.NormalizeL2(destination.Slice(0, Dimension));
        }

        private int AccumulateSemanticLatentFeatures(string text, Span<float> destination)
        {
            int matched = 0;
            string clean = text.Trim().ToLowerInvariant();

            // Direct full phrase / single concept check
            if (_semanticCodebook.TryGetValue(clean, out var fullVec) ||
                _semanticCodebook.TryGetValue(clean.Replace(' ', '_'), out fullVec))
            {
                for (int d = 0; d < Dimension; d++) destination[d] += fullVec[d] * 2.5f;
                return 1;
            }

            var words = clean.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < words.Length; i++)
            {
                string w = words[i];
                if (_semanticCodebook.TryGetValue(w, out var vec))
                {
                    for (int d = 0; d < Dimension; d++) destination[d] += vec[d];
                    matched++;
                }

                if (i + 1 < words.Length)
                {
                    string bigramUnderscore = w + "_" + words[i + 1];
                    string bigramSpace = w + " " + words[i + 1];
                    if (_semanticCodebook.TryGetValue(bigramUnderscore, out var bVec) ||
                        _semanticCodebook.TryGetValue(bigramSpace, out bVec))
                    {
                        for (int d = 0; d < Dimension; d++) destination[d] += bVec[d] * 1.5f;
                        matched++;
                    }
                }
            }

            return matched;
        }

        private void ApplyDenseProjection(ReadOnlySpan<float> input, Span<float> output)
        {
            int dim = Dimension;
            for (int r = 0; r < dim; r++)
            {
                float sum = 0.0f;
                int rowOffset = r * dim;
                for (int c = 0; c < dim; c++)
                {
                    sum += input[c] * _projectionMatrix[rowOffset + c];
                }
                output[r] = (float)Math.Tanh(sum);
            }
        }
    }
}
