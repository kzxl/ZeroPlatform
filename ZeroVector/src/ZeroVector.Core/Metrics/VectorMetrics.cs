using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace ZeroVector.Core.Metrics
{
    /// <summary>
    /// Hardware-accelerated SIMD (AVX2/FMA) metrics for high-dimensional embeddings and feature vectors.
    /// Provides sub-microsecond DotProduct, CosineSimilarity, EuclideanDistance, ManhattanDistance, and binary HammingDistance.
    /// </summary>
    public static unsafe class VectorMetrics
    {
        /// <summary>
        /// Computes the dot product of two vectors: sum(a[i] * b[i]).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var sumVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            sumVec = Fma.MultiplyAdd(va, vb, sumVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            sumVec += va * vb;
                        }
                    }

                    float sum = Vector256.Sum(sumVec);
                    for (; i < len; i++)
                    {
                        sum += pA[i] * pB[i];
                    }
                    return sum;
                }
#endif
                return DotProductScalar(pA, pB, len);
            }
        }

        /// <summary>
        /// Computes the Cosine Similarity between two vectors in [-1.0, 1.0]: (a . b) / (||a|| * ||b||).
        /// Accumulates dot product, norm(a), and norm(b) simultaneously in a single memory pass.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var dotVec = Vector256<float>.Zero;
                    var normAVec = Vector256<float>.Zero;
                    var normBVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            dotVec = Fma.MultiplyAdd(va, vb, dotVec);
                            normAVec = Fma.MultiplyAdd(va, va, normAVec);
                            normBVec = Fma.MultiplyAdd(vb, vb, normBVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            dotVec += va * vb;
                            normAVec += va * va;
                            normBVec += vb * vb;
                        }
                    }

                    float dot = Vector256.Sum(dotVec);
                    float normA = Vector256.Sum(normAVec);
                    float normB = Vector256.Sum(normBVec);

                    for (; i < len; i++)
                    {
                        float fa = pA[i];
                        float fb = pB[i];
                        dot += fa * fb;
                        normA += fa * fa;
                        normB += fb * fb;
                    }

                    if (normA <= 1e-12f || normB <= 1e-12f) return 0f;
                    float denom = (float)Math.Sqrt(normA * normB);
                    float cos = dot / denom;
                    return cos > 1.0f ? 1.0f : (cos < -1.0f ? -1.0f : cos);
                }
#endif
                return CosineSimilarityScalar(pA, pB, len);
            }
        }

        /// <summary>
        /// Computes the Squared Euclidean (L2) distance: sum((a[i] - b[i])^2).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistanceSquared(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var distVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            var diff = va - vb;
                            distVec = Fma.MultiplyAdd(diff, diff, distVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var va = Vector256.Load(pA + i);
                            var vb = Vector256.Load(pB + i);
                            var diff = va - vb;
                            distVec += diff * diff;
                        }
                    }

                    float distSq = Vector256.Sum(distVec);
                    for (; i < len; i++)
                    {
                        float d = pA[i] - pB[i];
                        distSq += d * d;
                    }
                    return distSq;
                }
#endif
                return EuclideanDistanceSquaredScalar(pA, pB, len);
            }
        }

        /// <summary>
        /// Computes Euclidean (L2) distance: sqrt(sum((a[i] - b[i])^2)).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            return (float)Math.Sqrt(EuclideanDistanceSquared(a, b));
        }

        /// <summary>
        /// Computes Manhattan (L1) distance: sum(|a[i] - b[i]|).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ManhattanDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Vector lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0f;

            fixed (float* pA = a, pB = b)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var sumVec = Vector256<float>.Zero;

                    for (; i < limit; i += 8)
                    {
                        var va = Vector256.Load(pA + i);
                        var vb = Vector256.Load(pB + i);
                        var diff = Vector256.Abs(va - vb);
                        sumVec += diff;
                    }

                    float sum = Vector256.Sum(sumVec);
                    for (; i < len; i++)
                    {
                        sum += Math.Abs(pA[i] - pB[i]);
                    }
                    return sum;
                }
#endif
                float scalarSum = 0f;
                for (int i = 0; i < len; i++)
                {
                    scalarSum += Math.Abs(pA[i] - pB[i]);
                }
                return scalarSum;
            }
        }

        /// <summary>
        /// Computes the bitwise Hamming distance between two 64-bit word buffers (e.g. 1-bit quantized vector embeddings).
        /// Uses 64-bit hardware POPCNT instructions for maximum throughput.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HammingDistance(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Buffer lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0;

            int totalDistance = 0;
            for (int i = 0; i < len; i++)
            {
                totalDistance += PopCount64(a[i] ^ b[i]);
            }

            return totalDistance;
        }

        /// <summary>
        /// Computes the bitwise Hamming distance between two byte buffers (e.g. 256-bit ORB/BRIEF binary descriptors).
        /// Uses 64-bit hardware POPCNT instructions for maximum throughput.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HammingDistance(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException($"Buffer lengths must match: a={a.Length}, b={b.Length}");

            int len = a.Length;
            if (len == 0) return 0;

            int totalDistance = 0;
            int i = 0;

            fixed (byte* pA = a, pB = b)
            {
                int ulongLimit = len - 7;
                for (; i < ulongLimit; i += 8)
                {
                    ulong wordA = *(ulong*)(pA + i);
                    ulong wordB = *(ulong*)(pB + i);
                    totalDistance += PopCount64(wordA ^ wordB);
                }

                for (; i < len; i++)
                {
                    byte diff = (byte)(pA[i] ^ pB[i]);
                    totalDistance += PopCountByte(diff);
                }
            }

            return totalDistance;
        }

        /// <summary>
        /// Normalizes a vector in-place to unit length (L2 norm = 1.0).
        /// </summary>
        public static void NormalizeL2(Span<float> vector)
        {
            int len = vector.Length;
            if (len == 0) return;

            float normSq = 0.0f;
            fixed (float* p = vector)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var sumVec = Vector256<float>.Zero;

                    if (Fma.IsSupported)
                    {
                        for (; i < limit; i += 8)
                        {
                            var v = Vector256.Load(p + i);
                            sumVec = Fma.MultiplyAdd(v, v, sumVec);
                        }
                    }
                    else
                    {
                        for (; i < limit; i += 8)
                        {
                            var v = Vector256.Load(p + i);
                            sumVec += v * v;
                        }
                    }

                    normSq = Vector256.Sum(sumVec);
                    for (; i < len; i++)
                    {
                        normSq += p[i] * p[i];
                    }
                }
                else
#endif
                {
                    for (int i = 0; i < len; i++)
                    {
                        normSq += p[i] * p[i];
                    }
                }

                if (normSq <= 1e-12f) return;
                float invNorm = 1.0f / (float)Math.Sqrt(normSq);

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && len >= 8)
                {
                    int i = 0;
                    int limit = len - 7;
                    var scaleVec = Vector256.Create(invNorm);
                    for (; i < limit; i += 8)
                    {
                        var v = Vector256.Load(p + i);
                        Vector256.Store(v * scaleVec, p + i);
                    }
                    for (; i < len; i++)
                    {
                        p[i] *= invNorm;
                    }
                    return;
                }
#endif
                for (int i = 0; i < len; i++)
                {
                    p[i] *= invNorm;
                }
            }
        }

        #region Public Scalar Helpers

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProductScalar(float* a, float* b, int len)
        {
            float sum = 0f;
            for (int i = 0; i < len; i++)
            {
                sum += a[i] * b[i];
            }
            return sum;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosineSimilarityScalar(float* a, float* b, int len)
        {
            float dot = 0f;
            float normA = 0f;
            float normB = 0f;

            for (int i = 0; i < len; i++)
            {
                float fa = a[i];
                float fb = b[i];
                dot += fa * fb;
                normA += fa * fa;
                normB += fb * fb;
            }

            if (normA <= 1e-12f || normB <= 1e-12f) return 0f;
            float denom = (float)Math.Sqrt(normA * normB);
            float cos = dot / denom;
            return cos > 1.0f ? 1.0f : (cos < -1.0f ? -1.0f : cos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EuclideanDistanceSquaredScalar(float* a, float* b, int len)
        {
            float distSq = 0f;
            for (int i = 0; i < len; i++)
            {
                float d = a[i] - b[i];
                distSq += d * d;
            }
            return distSq;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount64(ulong value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)((value * 0x0101010101010101UL) >> 56);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCountByte(byte value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.PopCount(value);
#else
            int v = value;
            v = v - ((v >> 1) & 0x55);
            v = (v & 0x33) + ((v >> 2) & 0x33);
            return (v + (v >> 4)) & 0x0F;
#endif
        }

        #endregion
    }
}
