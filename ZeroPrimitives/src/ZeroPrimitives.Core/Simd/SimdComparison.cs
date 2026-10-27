using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics;
#else
using System.Numerics;
#endif

namespace ZeroPrimitives.Simd
{
    /// <summary>
    /// Comparison operations for SIMD vector evaluation.
    /// </summary>
    public enum VectorCompareOp : byte
    {
        Equal = 0,
        NotEqual = 1,
        GreaterThan = 2,
        GreaterThanOrEqual = 3,
        LessThan = 4,
        LessThanOrEqual = 5
    }

    /// <summary>
    /// Hardware-accelerated vectorized comparison and index selection engine (DuckDB / ClickHouse columnar scan architecture).
    /// Extracts matching row indices using SIMD register bitmasks with zero per-row branching and zero heap allocations.
    /// </summary>
    public static unsafe class SimdComparison
    {
        #region Bit Utilities (Cross-runtime)

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int TrailingZeroCount(uint value)
        {
#if NET8_0_OR_GREATER
            return BitOperations.TrailingZeroCount(value);
#else
            if (value == 0) return 32;
            int count = 0;
            while ((value & 1) == 0)
            {
                value >>= 1;
                count++;
            }
            return count;
#endif
        }

        #endregion

        #region Double Comparisons

        /// <summary>
        /// Selects all row indices satisfying <c>src[i] op threshold</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        /// <returns>The number of matching indices written to <paramref name="dstIndices"/>.</returns>
        public static int SelectIndices(ReadOnlySpan<double> src, VectorCompareOp op, double threshold, Span<int> dstIndices)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (double* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
                {
                    int step = Vector256<double>.Count;
                    int limit = length - step;
                    var vThreshold = Vector256.Create(threshold);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<double> vCmp;

                        switch (op)
                        {
                            case VectorCompareOp.Equal:
                                vCmp = Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.NotEqual:
                                vCmp = ~Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThan:
                                vCmp = Vector256.GreaterThan(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThanOrEqual:
                                vCmp = Vector256.GreaterThanOrEqual(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThan:
                                vCmp = Vector256.LessThan(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThanOrEqual:
                                vCmp = Vector256.LessThanOrEqual(v, vThreshold);
                                break;
                            default:
                                vCmp = Vector256<double>.Zero;
                                break;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1; // Clear lowest set bit
                        }
                    }
                }
#endif

                // Scalar remainder / fallback
                for (; i < length; i++)
                {
                    double val = pSrc[i];
                    bool matched = op switch
                    {
                        VectorCompareOp.Equal => val == threshold,
                        VectorCompareOp.NotEqual => val != threshold,
                        VectorCompareOp.GreaterThan => val > threshold,
                        VectorCompareOp.GreaterThanOrEqual => val >= threshold,
                        VectorCompareOp.LessThan => val < threshold,
                        VectorCompareOp.LessThanOrEqual => val <= threshold,
                        _ => false
                    };

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        /// <summary>
        /// Selects all row indices satisfying <c>low &lt;= src[i] &lt;= high</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        public static int SelectBetween(ReadOnlySpan<double> src, double low, double high, Span<int> dstIndices, bool inclusive = true)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (double* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
                {
                    int step = Vector256<double>.Count;
                    int limit = length - step;
                    var vLow = Vector256.Create(low);
                    var vHigh = Vector256.Create(high);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<double> vCmp;

                        if (inclusive)
                        {
                            var cLow = Vector256.GreaterThanOrEqual(v, vLow);
                            var cHigh = Vector256.LessThanOrEqual(v, vHigh);
                            vCmp = cLow & cHigh;
                        }
                        else
                        {
                            var cLow = Vector256.GreaterThan(v, vLow);
                            var cHigh = Vector256.LessThan(v, vHigh);
                            vCmp = cLow & cHigh;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1;
                        }
                    }
                }
#endif

                for (; i < length; i++)
                {
                    double val = pSrc[i];
                    bool matched = inclusive
                        ? (val >= low && val <= high)
                        : (val > low && val < high);

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        #endregion

        #region Float Comparisons

        /// <summary>
        /// Selects all row indices satisfying <c>src[i] op threshold</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        public static int SelectIndices(ReadOnlySpan<float> src, VectorCompareOp op, float threshold, Span<int> dstIndices)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (float* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    var vThreshold = Vector256.Create(threshold);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<float> vCmp;

                        switch (op)
                        {
                            case VectorCompareOp.Equal:
                                vCmp = Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.NotEqual:
                                vCmp = ~Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThan:
                                vCmp = Vector256.GreaterThan(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThanOrEqual:
                                vCmp = Vector256.GreaterThanOrEqual(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThan:
                                vCmp = Vector256.LessThan(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThanOrEqual:
                                vCmp = Vector256.LessThanOrEqual(v, vThreshold);
                                break;
                            default:
                                vCmp = Vector256<float>.Zero;
                                break;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1;
                        }
                    }
                }
#endif

                for (; i < length; i++)
                {
                    float val = pSrc[i];
                    bool matched = op switch
                    {
                        VectorCompareOp.Equal => val == threshold,
                        VectorCompareOp.NotEqual => val != threshold,
                        VectorCompareOp.GreaterThan => val > threshold,
                        VectorCompareOp.GreaterThanOrEqual => val >= threshold,
                        VectorCompareOp.LessThan => val < threshold,
                        VectorCompareOp.LessThanOrEqual => val <= threshold,
                        _ => false
                    };

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        /// <summary>
        /// Selects all row indices satisfying <c>low &lt;= src[i] &lt;= high</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        public static int SelectBetween(ReadOnlySpan<float> src, float low, float high, Span<int> dstIndices, bool inclusive = true)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (float* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    var vLow = Vector256.Create(low);
                    var vHigh = Vector256.Create(high);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<float> vCmp;

                        if (inclusive)
                        {
                            var cLow = Vector256.GreaterThanOrEqual(v, vLow);
                            var cHigh = Vector256.LessThanOrEqual(v, vHigh);
                            vCmp = cLow & cHigh;
                        }
                        else
                        {
                            var cLow = Vector256.GreaterThan(v, vLow);
                            var cHigh = Vector256.LessThan(v, vHigh);
                            vCmp = cLow & cHigh;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1;
                        }
                    }
                }
#endif

                for (; i < length; i++)
                {
                    float val = pSrc[i];
                    bool matched = inclusive
                        ? (val >= low && val <= high)
                        : (val > low && val < high);

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        #endregion

        #region Int32 Comparisons

        /// <summary>
        /// Selects all row indices satisfying <c>src[i] op threshold</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        public static int SelectIndices(ReadOnlySpan<int> src, VectorCompareOp op, int threshold, Span<int> dstIndices)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (int* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
                {
                    int step = Vector256<int>.Count;
                    int limit = length - step;
                    var vThreshold = Vector256.Create(threshold);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<int> vCmp;

                        switch (op)
                        {
                            case VectorCompareOp.Equal:
                                vCmp = Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.NotEqual:
                                vCmp = ~Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThan:
                                vCmp = Vector256.GreaterThan(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThanOrEqual:
                                vCmp = Vector256.GreaterThanOrEqual(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThan:
                                vCmp = Vector256.LessThan(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThanOrEqual:
                                vCmp = Vector256.LessThanOrEqual(v, vThreshold);
                                break;
                            default:
                                vCmp = Vector256<int>.Zero;
                                break;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1;
                        }
                    }
                }
#endif

                for (; i < length; i++)
                {
                    int val = pSrc[i];
                    bool matched = op switch
                    {
                        VectorCompareOp.Equal => val == threshold,
                        VectorCompareOp.NotEqual => val != threshold,
                        VectorCompareOp.GreaterThan => val > threshold,
                        VectorCompareOp.GreaterThanOrEqual => val >= threshold,
                        VectorCompareOp.LessThan => val < threshold,
                        VectorCompareOp.LessThanOrEqual => val <= threshold,
                        _ => false
                    };

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        /// <summary>
        /// Selects all row indices satisfying <c>low &lt;= src[i] &lt;= high</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        public static int SelectBetween(ReadOnlySpan<int> src, int low, int high, Span<int> dstIndices, bool inclusive = true)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (int* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
                {
                    int step = Vector256<int>.Count;
                    int limit = length - step;
                    var vLow = Vector256.Create(low);
                    var vHigh = Vector256.Create(high);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<int> vCmp;

                        if (inclusive)
                        {
                            var cLow = Vector256.GreaterThanOrEqual(v, vLow);
                            var cHigh = Vector256.LessThanOrEqual(v, vHigh);
                            vCmp = cLow & cHigh;
                        }
                        else
                        {
                            var cLow = Vector256.GreaterThan(v, vLow);
                            var cHigh = Vector256.LessThan(v, vHigh);
                            vCmp = cLow & cHigh;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1;
                        }
                    }
                }
#endif

                for (; i < length; i++)
                {
                    int val = pSrc[i];
                    bool matched = inclusive
                        ? (val >= low && val <= high)
                        : (val > low && val < high);

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        #endregion

        #region Int64 Comparisons

        /// <summary>
        /// Selects all row indices satisfying <c>src[i] op threshold</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        public static int SelectIndices(ReadOnlySpan<long> src, VectorCompareOp op, long threshold, Span<int> dstIndices)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (long* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
                {
                    int step = Vector256<long>.Count;
                    int limit = length - step;
                    var vThreshold = Vector256.Create(threshold);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<long> vCmp;

                        switch (op)
                        {
                            case VectorCompareOp.Equal:
                                vCmp = Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.NotEqual:
                                vCmp = ~Vector256.Equals(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThan:
                                vCmp = Vector256.GreaterThan(v, vThreshold);
                                break;
                            case VectorCompareOp.GreaterThanOrEqual:
                                vCmp = Vector256.GreaterThanOrEqual(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThan:
                                vCmp = Vector256.LessThan(v, vThreshold);
                                break;
                            case VectorCompareOp.LessThanOrEqual:
                                vCmp = Vector256.LessThanOrEqual(v, vThreshold);
                                break;
                            default:
                                vCmp = Vector256<long>.Zero;
                                break;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1;
                        }
                    }
                }
#endif

                for (; i < length; i++)
                {
                    long val = pSrc[i];
                    bool matched = op switch
                    {
                        VectorCompareOp.Equal => val == threshold,
                        VectorCompareOp.NotEqual => val != threshold,
                        VectorCompareOp.GreaterThan => val > threshold,
                        VectorCompareOp.GreaterThanOrEqual => val >= threshold,
                        VectorCompareOp.LessThan => val < threshold,
                        VectorCompareOp.LessThanOrEqual => val <= threshold,
                        _ => false
                    };

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        /// <summary>
        /// Selects all row indices satisfying <c>low &lt;= src[i] &lt;= high</c> into <paramref name="dstIndices"/> using SIMD hardware vectorization.
        /// </summary>
        public static int SelectBetween(ReadOnlySpan<long> src, long low, long high, Span<int> dstIndices, bool inclusive = true)
        {
            int length = src.Length;
            if (length == 0) return 0;

            int matchCount = 0;
            fixed (long* pSrc = src)
            fixed (int* pDst = dstIndices)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
                {
                    int step = Vector256<long>.Count;
                    int limit = length - step;
                    var vLow = Vector256.Create(low);
                    var vHigh = Vector256.Create(high);

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256<long> vCmp;

                        if (inclusive)
                        {
                            var cLow = Vector256.GreaterThanOrEqual(v, vLow);
                            var cHigh = Vector256.LessThanOrEqual(v, vHigh);
                            vCmp = cLow & cHigh;
                        }
                        else
                        {
                            var cLow = Vector256.GreaterThan(v, vLow);
                            var cHigh = Vector256.LessThan(v, vHigh);
                            vCmp = cLow & cHigh;
                        }

                        uint mask = (uint)Vector256.ExtractMostSignificantBits(vCmp);
                        while (mask != 0)
                        {
                            int tz = TrailingZeroCount(mask);
                            pDst[matchCount++] = i + tz;
                            mask &= mask - 1;
                        }
                    }
                }
#endif

                for (; i < length; i++)
                {
                    long val = pSrc[i];
                    bool matched = inclusive
                        ? (val >= low && val <= high)
                        : (val > low && val < high);

                    if (matched)
                    {
                        pDst[matchCount++] = i;
                    }
                }
            }

            return matchCount;
        }

        #endregion
    }
}
