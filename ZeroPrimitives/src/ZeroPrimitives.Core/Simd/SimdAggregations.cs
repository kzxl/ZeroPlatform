using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ZeroPrimitives.Numerics;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
#else
using System.Numerics;
#endif

namespace ZeroPrimitives.Simd
{
    /// <summary>
    /// Hardware-accelerated vectorized aggregations and statistical operations.
    /// Provides AVX2/AVX-512 single-pass MinMax, Sum, Mean, and Variance.
    /// </summary>
    public static unsafe class SimdAggregations
    {
        #region Double Aggregations

        /// <summary>
        /// Computes minimum and maximum elements in a single vectorized pass.
        /// </summary>
        public static void MinMax(ReadOnlySpan<double> src, out double min, out double max)
        {
            int length = src.Length;
            if (length == 0)
            {
                min = double.NaN;
                max = double.NaN;
                return;
            }

            double localMin = src[0];
            double localMax = src[0];

            fixed (double* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
                {
                    int step = Vector256<double>.Count;
                    int limit = length - step;
                    var vMin = Vector256.Load(pSrc);
                    var vMax = vMin;
                    i = step;

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        vMin = Vector256.Min(vMin, v);
                        vMax = Vector256.Max(vMax, v);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        double vKMin = vMin.GetElement(k);
                        double vKMax = vMax.GetElement(k);
                        if (vKMin < localMin) localMin = vKMin;
                        if (vKMax > localMax) localMax = vKMax;
                    }
                }
#endif

                for (; i < length; i++)
                {
                    double val = pSrc[i];
                    if (val < localMin) localMin = val;
                    if (val > localMax) localMax = val;
                }
            }

            min = localMin;
            max = localMax;
        }

        /// <summary>
        /// Computes sum of elements using SIMD vector registers.
        /// </summary>
        public static double Sum(ReadOnlySpan<double> src)
        {
            int length = src.Length;
            if (length == 0) return 0.0;

            double sum = 0.0;
            fixed (double* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
                {
                    int step = Vector256<double>.Count;
                    int limit = length - step;
                    var vAcc = Vector256<double>.Zero;

                    for (; i <= limit; i += step)
                    {
                        vAcc += Vector256.Load(pSrc + i);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        sum += vAcc.GetElement(k);
                    }
                }
#endif

                for (; i < length; i++)
                {
                    sum += pSrc[i];
                }
            }

            return sum;
        }

        /// <summary>
        /// Computes arithmetic mean of elements.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Mean(ReadOnlySpan<double> src)
        {
            if (src.Length == 0) return double.NaN;
            return Sum(src) / src.Length;
        }

        /// <summary>
        /// Computes arithmetic mean and sample variance in a two-pass SIMD execution.
        /// </summary>
        public static void Variance(ReadOnlySpan<double> src, out double mean, out double variance)
        {
            int length = src.Length;
            if (length <= 1)
            {
                mean = length == 1 ? src[0] : double.NaN;
                variance = 0.0;
                return;
            }

            mean = Mean(src);
            double sumSqDiff = 0.0;

            fixed (double* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<double>.Count)
                {
                    int step = Vector256<double>.Count;
                    int limit = length - step;
                    var vMean = Vector256.Create(mean);
                    var vAcc = Vector256<double>.Zero;

                    for (; i <= limit; i += step)
                    {
                        var diff = Vector256.Load(pSrc + i) - vMean;
                        vAcc += diff * diff;
                    }

                    for (int k = 0; k < step; k++)
                    {
                        sumSqDiff += vAcc.GetElement(k);
                    }
                }
#endif

                for (; i < length; i++)
                {
                    double diff = pSrc[i] - mean;
                    sumSqDiff += diff * diff;
                }
            }

            variance = sumSqDiff / (length - 1);
        }

        #endregion

        #region Float Aggregations

        /// <summary>
        /// Computes minimum and maximum elements in a single vectorized pass.
        /// </summary>
        public static void MinMax(ReadOnlySpan<float> src, out float min, out float max)
        {
            int length = src.Length;
            if (length == 0)
            {
                min = float.NaN;
                max = float.NaN;
                return;
            }

            float localMin = src[0];
            float localMax = src[0];

            fixed (float* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    var vMin = Vector256.Load(pSrc);
                    var vMax = vMin;
                    i = step;

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        vMin = Vector256.Min(vMin, v);
                        vMax = Vector256.Max(vMax, v);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        float vKMin = vMin.GetElement(k);
                        float vKMax = vMax.GetElement(k);
                        if (vKMin < localMin) localMin = vKMin;
                        if (vKMax > localMax) localMax = vKMax;
                    }
                }
#endif

                for (; i < length; i++)
                {
                    float val = pSrc[i];
                    if (val < localMin) localMin = val;
                    if (val > localMax) localMax = val;
                }
            }

            min = localMin;
            max = localMax;
        }

        /// <summary>
        /// Computes sum of elements using SIMD vector registers.
        /// </summary>
        public static float Sum(ReadOnlySpan<float> src)
        {
            int length = src.Length;
            if (length == 0) return 0f;

            float sum = 0f;
            fixed (float* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    var vAcc = Vector256<float>.Zero;

                    for (; i <= limit; i += step)
                    {
                        vAcc += Vector256.Load(pSrc + i);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        sum += vAcc.GetElement(k);
                    }
                }
#endif

                for (; i < length; i++)
                {
                    sum += pSrc[i];
                }
            }

            return sum;
        }

        /// <summary>
        /// Computes arithmetic mean of elements.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Mean(ReadOnlySpan<float> src)
        {
            if (src.Length == 0) return float.NaN;
            return Sum(src) / src.Length;
        }

        /// <summary>
        /// Computes arithmetic mean and sample variance in a two-pass SIMD execution.
        /// </summary>
        public static void Variance(ReadOnlySpan<float> src, out float mean, out float variance)
        {
            int length = src.Length;
            if (length <= 1)
            {
                mean = length == 1 ? src[0] : float.NaN;
                variance = 0f;
                return;
            }

            mean = Mean(src);
            float sumSqDiff = 0f;

            fixed (float* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    var vMean = Vector256.Create(mean);
                    var vAcc = Vector256<float>.Zero;

                    for (; i <= limit; i += step)
                    {
                        var diff = Vector256.Load(pSrc + i) - vMean;
                        vAcc += diff * diff;
                    }

                    for (int k = 0; k < step; k++)
                    {
                        sumSqDiff += vAcc.GetElement(k);
                    }
                }
#endif

                for (; i < length; i++)
                {
                    float diff = pSrc[i] - mean;
                    sumSqDiff += diff * diff;
                }
            }

            variance = sumSqDiff / (length - 1);
        }

        #endregion

        #region Int32 Aggregations

        /// <summary>
        /// Computes minimum and maximum elements in a single vectorized pass.
        /// </summary>
        public static void MinMax(ReadOnlySpan<int> src, out int min, out int max)
        {
            int length = src.Length;
            if (length == 0)
            {
                min = 0;
                max = 0;
                return;
            }

            int localMin = src[0];
            int localMax = src[0];

            fixed (int* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
                {
                    int step = Vector256<int>.Count;
                    int limit = length - step;
                    var vMin = Vector256.Load(pSrc);
                    var vMax = vMin;
                    i = step;

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        vMin = Vector256.Min(vMin, v);
                        vMax = Vector256.Max(vMax, v);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        int vKMin = vMin.GetElement(k);
                        int vKMax = vMax.GetElement(k);
                        if (vKMin < localMin) localMin = vKMin;
                        if (vKMax > localMax) localMax = vKMax;
                    }
                }
#endif

                for (; i < length; i++)
                {
                    int val = pSrc[i];
                    if (val < localMin) localMin = val;
                    if (val > localMax) localMax = val;
                }
            }

            min = localMin;
            max = localMax;
        }

        #endregion

        #region Int64 Aggregations

        /// <summary>
        /// Computes minimum and maximum elements in a single vectorized pass.
        /// </summary>
        public static void MinMax(ReadOnlySpan<long> src, out long min, out long max)
        {
            int length = src.Length;
            if (length == 0)
            {
                min = 0L;
                max = 0L;
                return;
            }

            long localMin = src[0];
            long localMax = src[0];

            fixed (long* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
                {
                    int step = Vector256<long>.Count;
                    int limit = length - step;
                    var vMin = Vector256.Load(pSrc);
                    var vMax = vMin;
                    i = step;

                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        vMin = Vector256.Min(vMin, v);
                        vMax = Vector256.Max(vMax, v);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        long vKMin = vMin.GetElement(k);
                        long vKMax = vMax.GetElement(k);
                        if (vKMin < localMin) localMin = vKMin;
                        if (vKMax > localMax) localMax = vKMax;
                    }
                }
#endif

                for (; i < length; i++)
                {
                    long val = pSrc[i];
                    if (val < localMin) localMin = val;
                    if (val > localMax) localMax = val;
                }
            }

            min = localMin;
            max = localMax;
        }

        /// <summary>
        /// Computes exact 64-bit integer sum over <paramref name="src"/> using SIMD vector accumulation.
        /// </summary>
        public static long Sum(ReadOnlySpan<long> src)
        {
            int length = src.Length;
            if (length == 0) return 0L;

            long sum = 0L;
            fixed (long* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<long>.Count)
                {
                    int step = Vector256<long>.Count;
                    int limit = length - step;
                    var vAcc = Vector256<long>.Zero;

                    for (; i <= limit; i += step)
                    {
                        vAcc += Vector256.Load(pSrc + i);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        sum += vAcc.GetElement(k);
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<long>.Count)
                {
                    int step = Vector<long>.Count;
                    int limit = length - step;
                    var vAcc = Vector<long>.Zero;

                    for (; i <= limit; i += step)
                    {
                        vAcc += Unsafe.ReadUnaligned<Vector<long>>(pSrc + i);
                    }

                    for (int k = 0; k < step; k++)
                    {
                        sum += vAcc[k];
                    }
                }
#endif

                for (; i < length; i++)
                {
                    sum += pSrc[i];
                }
            }

            return sum;
        }

        /// <summary>
        /// Computes exact 64-bit widened sum over 32-bit integers without 32-bit overflow.
        /// </summary>
        public static long Sum(ReadOnlySpan<int> src)
        {
            int length = src.Length;
            if (length == 0) return 0L;

            long sum = 0L;
            fixed (int* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<int>.Count)
                {
                    int step = Vector256<int>.Count;
                    int limit = length - step;
                    var vAccLo = Vector256<long>.Zero;
                    var vAccHi = Vector256<long>.Zero;

                    for (; i <= limit; i += step)
                    {
                        var vInt = Vector256.Load(pSrc + i);
                        var (lo, hi) = Vector256.Widen(vInt);
                        vAccLo += lo;
                        vAccHi += hi;
                    }

                    var vAcc = vAccLo + vAccHi;
                    for (int k = 0; k < Vector256<long>.Count; k++)
                    {
                        sum += vAcc.GetElement(k);
                    }
                }
#else
                long s0 = 0L, s1 = 0L, s2 = 0L, s3 = 0L;
                int limit4 = length - 4;
                for (; i <= limit4; i += 4)
                {
                    s0 += pSrc[i];
                    s1 += pSrc[i + 1];
                    s2 += pSrc[i + 2];
                    s3 += pSrc[i + 3];
                }
                sum = s0 + s1 + s2 + s3;
#endif

                for (; i < length; i++)
                {
                    sum += pSrc[i];
                }
            }

            return sum;
        }

        /// <summary>
        /// Computes exact SIMD sum over a contiguous span of 8-byte <see cref="ScaledInt64"/> fixed-point values without copy or allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ScaledInt64 Sum(ReadOnlySpan<ScaledInt64> src)
        {
            ReadOnlySpan<long> raw = MemoryMarshal.Cast<ScaledInt64, long>(src);
            return ScaledInt64.FromRaw(Sum(raw));
        }

        /// <summary>
        /// Computes minimum and maximum over <see cref="ScaledInt64"/> values in a single vectorized pass.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MinMax(ReadOnlySpan<ScaledInt64> src, out ScaledInt64 min, out ScaledInt64 max)
        {
            ReadOnlySpan<long> raw = MemoryMarshal.Cast<ScaledInt64, long>(src);
            MinMax(raw, out long rMin, out long rMax);
            min = ScaledInt64.FromRaw(rMin);
            max = ScaledInt64.FromRaw(rMax);
        }

        #endregion
    }
}
