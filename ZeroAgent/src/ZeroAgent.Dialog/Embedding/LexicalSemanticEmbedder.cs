using System;
using ZeroVector.Core.Metrics;

namespace ZeroAgent.Dialog.Embedding
{
    /// <summary>
    /// Pure C# ultra-fast lexical-semantic feature embedder.
    /// Converts text queries into high-dimensional unit-norm vectors via signed character/word n-gram feature hashing.
    /// Operates in sub-50 microseconds on CPU with zero machine learning dependencies.
    /// </summary>
    public class LexicalSemanticEmbedder : ITextEmbedder
    {
        public int Dimension { get; }

        public LexicalSemanticEmbedder(int dimension = 128)
        {
            if (dimension <= 0 || dimension % 8 != 0)
                throw new ArgumentException("Dimension must be a positive multiple of 8.", nameof(dimension));
            Dimension = dimension;
        }

        public virtual float[] Embed(string text)
        {
            var vector = new float[Dimension];
            Embed(text, vector);
            return vector;
        }

        public virtual void Embed(string text, Span<float> destination)
        {
            if (destination.Length < Dimension)
                throw new ArgumentException($"Destination span must be at least {Dimension} elements.");

            destination.Slice(0, Dimension).Clear();

            if (string.IsNullOrWhiteSpace(text)) return;

            string normalized = text.Trim().ToLowerInvariant();

            // 1. Word tokens (Weight: 2.5)
            var words = normalized.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', '!', '?', ':', '(', ')', '[', ']', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                uint h = Fnv1aHash(word);
                int idx = (int)(h % (uint)Dimension);
                float sign = ((h >> 31) & 1) == 0 ? 1.0f : -1.0f;
                destination[idx] += sign * 2.5f;

                // Word bi-grams for phrase context
                if (i + 1 < words.Length)
                {
                    uint bh = Fnv1aHash(word + "_" + words[i + 1]);
                    int bIdx = (int)(bh % (uint)Dimension);
                    float bSign = ((bh >> 31) & 1) == 0 ? 1.0f : -1.0f;
                    destination[bIdx] += bSign * 2.0f;
                }
            }

            // 2. Character 3-grams for morphological/typo resilience (Weight: 1.0)
            if (normalized.Length >= 3)
            {
                for (int i = 0; i <= normalized.Length - 3; i++)
                {
                    uint ch = Fnv1aHash3Chars(normalized[i], normalized[i + 1], normalized[i + 2]);
                    int idx = (int)(ch % (uint)Dimension);
                    float sign = ((ch >> 31) & 1) == 0 ? 1.0f : -1.0f;
                    destination[idx] += sign * 1.0f;
                }
            }

            // 3. Hardware-accelerated L2 normalization to unit hypersphere
            VectorMetrics.NormalizeL2(destination.Slice(0, Dimension));
        }

        private static uint Fnv1aHash(string s)
        {
            uint hash = 2166136261;
            for (int i = 0; i < s.Length; i++)
            {
                hash ^= s[i];
                hash *= 16777619;
            }
            return hash;
        }

        private static uint Fnv1aHash3Chars(char c1, char c2, char c3)
        {
            uint hash = 2166136261;
            hash = (hash ^ c1) * 16777619;
            hash = (hash ^ c2) * 16777619;
            hash = (hash ^ c3) * 16777619;
            return hash;
        }
    }
}
