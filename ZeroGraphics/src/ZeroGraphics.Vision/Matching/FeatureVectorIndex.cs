using System;
using ZeroVector.Core.Indices;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Results;

namespace ZeroGraphics.Vision.Matching
{
    [Obsolete("Use ZeroVector.Core.Metrics.VectorMetricType instead.")]
    public enum VectorMetricType
    {
        Cosine = 0,
        EuclideanSquared = 3,
        DotProduct = 1
    }

    [Obsolete("Use ZeroVector.Core.Results.VectorSearchResult instead.")]
    public readonly struct VectorSearchResult : IComparable<VectorSearchResult>
    {
        public int Id { get; }
        public float Score { get; }

        public VectorSearchResult(int id, float score)
        {
            Id = id;
            Score = score;
        }

        public int CompareTo(VectorSearchResult other) => Score.CompareTo(other.Score);

        public override string ToString() => $"VectorSearchResult(Id={Id}, Score={Score:F4})";
    }

    /// <summary>
    /// Contiguous cache-aligned vector database for high-throughput image feature similarity search.
    /// Backward-compatibility shim wrapping ZeroVector.Core.Indices.FlatVectorIndex.
    /// </summary>
    [Obsolete("Use ZeroVector.Core.Indices.FlatVectorIndex instead.")]
    public sealed class FeatureVectorIndex
    {
        private readonly FlatVectorIndex _inner;

        public int Dimension => _inner.Dimension;
        public int Count => _inner.Count;

        public FeatureVectorIndex(int dimension, int initialCapacity = 64)
        {
            _inner = new FlatVectorIndex(dimension, initialCapacity);
        }

        public void Add(int id, ReadOnlySpan<float> vector) => _inner.Add(id, vector);

        public VectorSearchResult[] SearchTopK(ReadOnlySpan<float> query, int k, VectorMetricType metric = VectorMetricType.Cosine)
        {
            var targetMetric = (ZeroVector.Core.Metrics.VectorMetricType)(int)metric;
            var results = _inner.SearchTopK(query, k, targetMetric);
            var converted = new VectorSearchResult[results.Length];
            for (int i = 0; i < results.Length; i++)
            {
                converted[i] = new VectorSearchResult(results[i].Id, results[i].Score);
            }
            return converted;
        }

        public void Clear() => _inner.Clear();
    }
}
