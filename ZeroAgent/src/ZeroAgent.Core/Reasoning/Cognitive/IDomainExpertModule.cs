using System;
using System.Collections.Generic;

namespace ZeroAgent.Core.Reasoning.Cognitive
{
    /// <summary>
    /// Contract for a specialized Domain Expert in a Modular Mixture of Experts (MoE) architecture.
    /// Can be plugged in or unplugged at runtime without retraining the base model or affecting existing experts.
    /// </summary>
    public interface IDomainExpertModule
    {
        string DomainId { get; }
        string DisplayName { get; }
        string Description { get; }
        IReadOnlyList<string> HandledIntents { get; }
        float EvaluateAffinity(ReadOnlySpan<float> inputEmbedding);
    }

    public delegate float DomainAffinityScorer(ReadOnlySpan<float> inputEmbedding);

    /// <summary>
    /// Standard implementation of a Domain Expert Module for MoE clusters.
    /// Supports affinity scoring via centroid embedding cosine similarity or neural gating.
    /// </summary>
    public sealed class DomainExpertModule : IDomainExpertModule
    {
        public string DomainId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public IReadOnlyList<string> HandledIntents { get; }
        public float[]? CentroidEmbedding { get; set; }
        public DomainAffinityScorer? CustomAffinityScorer { get; set; }

        public DomainExpertModule(
            string domainId,
            string displayName,
            string description,
            IEnumerable<string>? handledIntents = null,
            float[]? centroidEmbedding = null)
        {
            DomainId = domainId ?? throw new ArgumentNullException(nameof(domainId));
            DisplayName = displayName ?? domainId;
            Description = description ?? string.Empty;
            HandledIntents = new List<string>(handledIntents ?? Array.Empty<string>());
            CentroidEmbedding = centroidEmbedding;
        }

        public float EvaluateAffinity(ReadOnlySpan<float> inputEmbedding)
        {
            if (CustomAffinityScorer != null)
            {
                return CustomAffinityScorer(inputEmbedding);
            }

            if (CentroidEmbedding == null || CentroidEmbedding.Length != inputEmbedding.Length || inputEmbedding.IsEmpty)
            {
                return 0.0f;
            }

            // Cosine dot product
            float dot = 0f;
            float normA = 0f;
            float normB = 0f;
            for (int i = 0; i < inputEmbedding.Length; i++)
            {
                dot += inputEmbedding[i] * CentroidEmbedding[i];
                normA += inputEmbedding[i] * inputEmbedding[i];
                normB += CentroidEmbedding[i] * CentroidEmbedding[i];
            }

            if (normA <= 1e-7f || normB <= 1e-7f) return 0f;
            return Math.Max(0f, dot / (float)(Math.Sqrt(normA) * Math.Sqrt(normB)));
        }
    }
}
