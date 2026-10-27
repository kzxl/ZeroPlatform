using System;
using ZeroVector.Core.Metrics;
using ZeroVector.Core.Results;

namespace ZeroVector.Core.Indices
{
    /// <summary>
    /// Common abstraction for in-memory and embedded vector indices.
    /// </summary>
    public interface IVectorIndex
    {
        /// <summary>
        /// Dimensionality of the vectors stored in this index.
        /// </summary>
        int Dimension { get; }

        /// <summary>
        /// Current number of vector entries stored in this index.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Primary similarity/distance metric configured for this index.
        /// </summary>
        VectorMetricType Metric { get; }

        /// <summary>
        /// Inserts a vector embedding associated with the specified identifier.
        /// </summary>
        void Add(int id, ReadOnlySpan<float> vector);

        /// <summary>
        /// Attempts to copy the vector for the specified identifier into the destination span.
        /// </summary>
        bool TryGet(int id, Span<float> destination);

        /// <summary>
        /// Searches for the top-k nearest neighbors according to the default metric.
        /// </summary>
        VectorSearchResult[] SearchTopK(ReadOnlySpan<float> query, int k);

        /// <summary>
        /// Searches for the top-k nearest neighbors according to the specified metric.
        /// </summary>
        VectorSearchResult[] SearchTopK(ReadOnlySpan<float> query, int k, VectorMetricType metric);

        /// <summary>
        /// Clears all items from the index.
        /// </summary>
        void Clear();
    }
}
