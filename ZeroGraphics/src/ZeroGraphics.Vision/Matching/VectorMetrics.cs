using System;
using System.Runtime.CompilerServices;

namespace ZeroGraphics.Vision.Matching
{
    /// <summary>
    /// Hardware-accelerated SIMD metrics for high-dimensional image feature vectors and embeddings.
    /// Backward-compatibility shim delegating to ZeroVector.Core.Metrics.VectorMetrics.
    /// </summary>
    [Obsolete("Use ZeroVector.Core.Metrics.VectorMetrics instead.")]
    public static unsafe class VectorMetrics
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
            => ZeroVector.Core.Metrics.VectorMetrics.DotProduct(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
            => ZeroVector.Core.Metrics.VectorMetrics.CosineSimilarity(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistanceSquared(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
            => ZeroVector.Core.Metrics.VectorMetrics.EuclideanDistanceSquared(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
            => ZeroVector.Core.Metrics.VectorMetrics.EuclideanDistance(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ManhattanDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
            => ZeroVector.Core.Metrics.VectorMetrics.ManhattanDistance(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HammingDistance(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
            => ZeroVector.Core.Metrics.VectorMetrics.HammingDistance(a, b);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NormalizeL2(Span<float> vector)
            => ZeroVector.Core.Metrics.VectorMetrics.NormalizeL2(vector);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProductScalar(float* a, float* b, int len)
            => ZeroVector.Core.Metrics.VectorMetrics.DotProductScalar(a, b, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosineSimilarityScalar(float* a, float* b, int len)
            => ZeroVector.Core.Metrics.VectorMetrics.CosineSimilarityScalar(a, b, len);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistanceSquaredScalar(float* a, float* b, int len)
            => ZeroVector.Core.Metrics.VectorMetrics.EuclideanDistanceSquaredScalar(a, b, len);
    }
}
