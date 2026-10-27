namespace ZeroVector.Core.Metrics
{
    /// <summary>
    /// Supported similarity and distance metrics for vector search.
    /// </summary>
    public enum VectorMetricType
    {
        /// <summary>
        /// Cosine similarity in range [-1.0, 1.0]. Higher is better.
        /// </summary>
        Cosine = 0,

        /// <summary>
        /// Dot product. Higher is better (for unit-normalized vectors, identical to Cosine).
        /// </summary>
        DotProduct = 1,

        /// <summary>
        /// Euclidean (L2) distance. Lower is better.
        /// </summary>
        Euclidean = 2,

        /// <summary>
        /// Squared Euclidean (L2^2) distance (avoids square root). Lower is better.
        /// </summary>
        EuclideanSquared = 3,

        /// <summary>
        /// Manhattan (L1) distance. Lower is better.
        /// </summary>
        Manhattan = 4,

        /// <summary>
        /// Bitwise Hamming distance for binary descriptors. Lower is better.
        /// </summary>
        Hamming = 5
    }
}
