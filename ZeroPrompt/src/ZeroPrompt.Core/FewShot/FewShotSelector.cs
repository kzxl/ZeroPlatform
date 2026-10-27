using System;
using System.Collections.Generic;
using System.Text;
using ZeroVector.Core.Metrics;

namespace ZeroPrompt.Core.FewShot
{
    /// <summary>
    /// Dynamic few-shot prompt exemplar selector backed by ZeroVector vector similarity.
    /// </summary>
    public sealed class FewShotSelector
    {
        private readonly List<Exemplar> _exemplars = new List<Exemplar>();

        public IReadOnlyList<Exemplar> Exemplars => _exemplars;

        public void Add(Exemplar exemplar)
        {
            if (exemplar == null) throw new ArgumentNullException(nameof(exemplar));
            _exemplars.Add(exemplar);
        }

        /// <summary>
        /// Retrieves the top-K most semantically relevant exemplars for the given query vector.
        /// </summary>
        public List<Exemplar> SelectTopK(ReadOnlySpan<float> queryEmbedding, int k = 3)
        {
            if (_exemplars.Count == 0 || k <= 0) return new List<Exemplar>();

            var scored = new List<KeyValuePair<Exemplar, float>>(_exemplars.Count);
            for (int i = 0; i < _exemplars.Count; i++)
            {
                var ex = _exemplars[i];
                float sim = 0.0f;
                if (ex.Embedding.Length == queryEmbedding.Length)
                {
                    sim = VectorMetrics.CosineSimilarity(queryEmbedding, ex.Embedding);
                }
                scored.Add(new KeyValuePair<Exemplar, float>(ex, sim));
            }

            scored.Sort((a, b) => b.Value.CompareTo(a.Value));

            int count = Math.Min(k, scored.Count);
            var result = new List<Exemplar>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(scored[i].Key);
            }
            return result;
        }

        /// <summary>
        /// Formats selected top-K exemplars into a single demonstration context string.
        /// </summary>
        public string BuildDemonstrationPrompt(ReadOnlySpan<float> queryEmbedding, int k = 3)
        {
            var selected = SelectTopK(queryEmbedding, k);
            if (selected.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("### Reference Examples:");
            for (int i = 0; i < selected.Count; i++)
            {
                sb.AppendLine($"Example {i + 1}:");
                sb.Append(selected[i].Format());
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
