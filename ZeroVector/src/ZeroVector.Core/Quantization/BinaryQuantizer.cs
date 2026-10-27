using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif
using ZeroVector.Core.Metrics;

namespace ZeroVector.Core.Quantization
{
    /// <summary>
    /// High-throughput 1-Bit Binary Quantization (BQ) and 8-Bit Scalar Quantization (SQ8) engine.
    /// Enables 32x memory compression for 1-bit binary representations and ultra-fast hardware POPCNT filtering.
    /// </summary>
    public static unsafe class BinaryQuantizer
    {
        /// <summary>
        /// Gets the number of 64-bit unsigned integers required to hold a 1-bit quantized vector of specified dimensionality.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetRequiredUlongCount(int dimension)
        {
            return (dimension + 63) >> 6; // (dimension + 63) / 64
        }

        /// <summary>
        /// Quantizes an FP32 continuous vector into a 1-bit packed binary representation (1 bit per dimension).
        /// Bit is 1 if component >= 0.0, else 0.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Quantize1Bit(ReadOnlySpan<float> source, Span<ulong> destination)
        {
            int requiredUlongs = (source.Length + 63) >> 6;
            if (destination.Length < requiredUlongs)
                throw new ArgumentException($"Destination span requires at least {requiredUlongs} ulongs, got {destination.Length}.", nameof(destination));

            destination.Clear();

            int len = source.Length;
            for (int i = 0; i < len; i++)
            {
                if (source[i] >= 0.0f)
                {
                    int ulongIdx = i >> 6;
                    int bitIdx = i & 63;
                    destination[ulongIdx] |= (1UL << bitIdx);
                }
            }
        }

        /// <summary>
        /// Computes exact bitwise Hamming distance between two 1-bit quantized vector bitsets using hardware POPCNT.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeHammingDistance(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b)
        {
            return VectorMetrics.HammingDistance(a, b);
        }

        /// <summary>
        /// Computes normalized Hamming Similarity in [0.0, 1.0].
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeHammingSimilarity(ReadOnlySpan<ulong> a, ReadOnlySpan<ulong> b, int totalDimensions)
        {
            if (totalDimensions <= 0) return 0.0f;
            int distance = VectorMetrics.HammingDistance(a, b);
            float similarity = 1.0f - ((float)distance / totalDimensions);
            return similarity < 0.0f ? 0.0f : (similarity > 1.0f ? 1.0f : similarity);
        }

        /// <summary>
        /// Quantizes an FP32 vector into Int8 (sbyte) values using per-vector MinMax scaling.
        /// </summary>
        public static void QuantizeSQ8(
            ReadOnlySpan<float> source,
            Span<sbyte> destination,
            out float scale,
            out float offset)
        {
            if (source.Length != destination.Length)
                throw new ArgumentException("Source and destination lengths must match.");

            float minVal = float.MaxValue;
            float maxVal = float.MinValue;

            for (int i = 0; i < source.Length; i++)
            {
                float v = source[i];
                if (v < minVal) minVal = v;
                if (v > maxVal) maxVal = v;
            }

            float range = maxVal - minVal;
            scale = range > 1e-7f ? range / 254.0f : 1.0f;
            offset = minVal;

            float invScale = 1.0f / scale;
            for (int i = 0; i < source.Length; i++)
            {
                int q = (int)Math.Round((source[i] - offset) * invScale) - 127;
                destination[i] = (sbyte)Math.Max(-127, Math.Min(127, q));
            }
        }

        /// <summary>
        /// Dequantizes an Int8 (sbyte) vector back into FP32 floats.
        /// </summary>
        public static void DequantizeSQ8(
            ReadOnlySpan<sbyte> source,
            Span<float> destination,
            float scale,
            float offset)
        {
            if (source.Length != destination.Length)
                throw new ArgumentException("Source and destination lengths must match.");

            for (int i = 0; i < source.Length; i++)
            {
                destination[i] = (source[i] + 127) * scale + offset;
            }
        }

        /// <summary>
        /// Computes the sum of all elements in an FP32 vector with SIMD acceleration.
        /// Precomputed once per query to enable Affine Hoisting in SQ8 dot products.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeVectorSum(ReadOnlySpan<float> vector)
        {
            float sum = 0.0f;
            int length = vector.Length;
            int i = 0;

            fixed (float* p = vector)
            {
#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= 8)
                {
                    var acc256 = Vector256<float>.Zero;
                    int limit = length - 7;
                    for (; i < limit; i += 8)
                    {
                        acc256 += Vector256.Load(p + i);
                    }
                    sum += Vector256.Sum(acc256);
                }
#endif
                for (; i < length; i++)
                {
                    sum += p[i];
                }
            }

            return sum;
        }

        /// <summary>
        /// Direct pointer-based SIMD fused cosine similarity kernel with Affine Hoisting.
        /// Reduces inner loop from 4 SIMD instructions (Add->Mul->Add->FMA) to 1 FMA instruction per 8 dimensions.
        /// Identity: sum(q * ((t + 127)*scale + offset)) = scale * sum(q * t) + (127*scale + offset) * sum(q)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CosineSimilaritySq8Hoisted(
            float* qPtr,
            sbyte* tPtr,
            int length,
            float targetScale,
            float targetOffset,
            float querySum)
        {
            float rawDot = 0.0f;
            int i = 0;

#if NET8_0_OR_GREATER
            if (Avx2.IsSupported && length >= 16)
            {
                var acc0 = Vector256<float>.Zero;
                var acc1 = Vector256<float>.Zero;
                int limit = length - 15;

                for (; i < limit; i += 16)
                {
                    // Chunk 0..7
                    var q0 = Vector256.Load(qPtr + i);
                    var rawBytes0 = Vector128.Load(tPtr + i);
                    var ints0 = Avx2.ConvertToVector256Int32(rawBytes0);
                    var floats0 = Avx.ConvertToVector256Single(ints0);

                    // Chunk 8..15
                    var q1 = Vector256.Load(qPtr + i + 8);
                    var rawBytes1 = Vector128.Load(tPtr + i + 8);
                    var ints1 = Avx2.ConvertToVector256Int32(rawBytes1);
                    var floats1 = Avx.ConvertToVector256Single(ints1);

                    if (Fma.IsSupported)
                    {
                        acc0 = Fma.MultiplyAdd(q0, floats0, acc0);
                        acc1 = Fma.MultiplyAdd(q1, floats1, acc1);
                    }
                    else
                    {
                        acc0 += q0 * floats0;
                        acc1 += q1 * floats1;
                    }
                }

                rawDot = Vector256.Sum(acc0 + acc1);
            }
#endif

            for (; i < length; i++)
            {
                rawDot += qPtr[i] * tPtr[i];
            }

            // Hoisted scalar dequantization application:
            // sum(q * ((t + 127)*scale + offset)) = scale * rawDot + (127*scale + offset) * querySum
            float dotSum = (targetScale * rawDot) + ((127.0f * targetScale + targetOffset) * querySum);
            return dotSum < -1.0f ? -1.0f : (dotSum > 1.0f ? 1.0f : dotSum);
        }
    }
}
