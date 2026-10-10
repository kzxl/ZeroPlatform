using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace ZeroPrimitives.Simd
{
    /// <summary>
    /// Unified Hardware Intrinsics & Vectorized Operations engine.
    /// Employs AVX2 / SSE2 non-temporal streaming stores on modern runtimes
    /// with graceful fallback to <see cref="Vector{T}"/> and scalar implementations on legacy runtimes.
    /// </summary>
    public static unsafe class SimdOps
    {
        /// <summary>
        /// Gets whether 256-bit AVX2 hardware intrinsics are available on the current CPU and runtime.
        /// </summary>
        public static bool IsAvx2Supported
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if NET8_0_OR_GREATER
                return Avx2.IsSupported;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// Copies memory using non-temporal streaming stores (<c>_mm256_stream_si256</c> / <c>_mm_stream_si128</c>)
        /// when transferring large buffers (e.g., video frames, point clouds) to bypass CPU cache pollution.
        /// </summary>
        /// <param name="source">Source byte span.</param>
        /// <param name="destination">Destination byte span.</param>
        public static void CopyNonTemporal(ReadOnlySpan<byte> source, Span<byte> destination)
        {
            if (source.Length > destination.Length)
                throw new ArgumentException("Destination span is shorter than source span.", nameof(destination));

            int length = source.Length;
            if (length == 0)
                return;

            fixed (byte* pSrc = source)
            fixed (byte* pDst = destination)
            {
#if NET8_0_OR_GREATER
                if (Avx2.IsSupported && length >= 64)
                {
                    byte* src = pSrc;
                    byte* dst = pDst;
                    int remaining = length;

                    // Align destination to 32-byte boundary for Avx.StoreAlignedNonTemporal
                    while (((nuint)dst & 31) != 0 && remaining > 0)
                    {
                        *dst++ = *src++;
                        remaining--;
                    }

                    // Copy 64-byte blocks with non-temporal stores
                    while (remaining >= 64)
                    {
                        Vector256<byte> v0 = Avx.LoadVector256(src);
                        Vector256<byte> v1 = Avx.LoadVector256(src + 32);

                        Avx.StoreAlignedNonTemporal((byte*)dst, v0);
                        Avx.StoreAlignedNonTemporal((byte*)(dst + 32), v1);

                        src += 64;
                        dst += 64;
                        remaining -= 64;
                    }

                    // Copy 32-byte block if remaining
                    if (remaining >= 32)
                    {
                        Vector256<byte> v0 = Avx.LoadVector256(src);
                        Avx.StoreAlignedNonTemporal((byte*)dst, v0);
                        src += 32;
                        dst += 32;
                        remaining -= 32;
                    }

                    // Enforce store fence after non-temporal streaming stores
                    Sse2.StoreFence();

                    // Trailing scalar bytes
                    while (remaining > 0)
                    {
                        *dst++ = *src++;
                        remaining--;
                    }
                    return;
                }
#endif
                // Fallback for smaller buffers or runtimes without AVX2
                source.CopyTo(destination);
            }
        }

        /// <summary>
        /// Vectorized zeroing of a memory span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetZero(Span<byte> destination)
        {
            destination.Clear();
        }

        /// <summary>
        /// Vectorized search for minimum and maximum byte values in a span.
        /// </summary>
        public static void MinMax(ReadOnlySpan<byte> source, out byte min, out byte max)
        {
            if (source.Length == 0)
            {
                min = 0;
                max = 0;
                return;
            }

            int length = source.Length;
            byte localMin = source[0];
            byte localMax = source[0];

            fixed (byte* p = source)
            {
                int i = 0;
#if NET8_0_OR_GREATER
                if (Avx2.IsSupported && length >= 32)
                {
                    Vector256<byte> vMin = Avx.LoadVector256(p);
                    Vector256<byte> vMax = vMin;

                    i = 32;
                    while (i <= length - 32)
                    {
                        Vector256<byte> vCurr = Avx.LoadVector256(p + i);
                        vMin = Avx2.Min(vMin, vCurr);
                        vMax = Avx2.Max(vMax, vCurr);
                        i += 32;
                    }

                    // Horizontal reduction of 32-byte vector
                    byte* pMin = (byte*)&vMin;
                    byte* pMax = (byte*)&vMax;
                    for (int k = 0; k < 32; k++)
                    {
                        if (pMin[k] < localMin) localMin = pMin[k];
                        if (pMax[k] > localMax) localMax = pMax[k];
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count)
                {
                    int vCount = Vector<byte>.Count;
                    Vector<byte> vMin = *(Vector<byte>*)p;
                    Vector<byte> vMax = vMin;

                    i = vCount;
                    while (i <= length - vCount)
                    {
                        Vector<byte> vCurr = *(Vector<byte>*)(p + i);
                        vMin = Vector.Min(vMin, vCurr);
                        vMax = Vector.Max(vMax, vCurr);
                        i += vCount;
                    }

                    for (int k = 0; k < vCount; k++)
                    {
                        if (vMin[k] < localMin) localMin = vMin[k];
                        if (vMax[k] > localMax) localMax = vMax[k];
                    }
                }
#endif
                for (; i < length; i++)
                {
                    byte val = p[i];
                    if (val < localMin) localMin = val;
                    if (val > localMax) localMax = val;
                }
            }

            min = localMin;
            max = localMax;
        }

        /// <summary>
        /// Vectorized dot product of two single-precision float arrays.
        /// </summary>
        public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int length = Math.Min(a.Length, b.Length);
            if (length == 0)
                return 0f;

            float sum = 0f;

            fixed (float* pA = a)
            fixed (float* pB = b)
            {
                int i = 0;
#if NET8_0_OR_GREATER
                if (Avx.IsSupported && length >= 8)
                {
                    Vector256<float> vSum = Vector256<float>.Zero;

                    while (i <= length - 8)
                    {
                        Vector256<float> va = Avx.LoadVector256(pA + i);
                        Vector256<float> vb = Avx.LoadVector256(pB + i);
                        Vector256<float> prod = Avx.Multiply(va, vb);
                        vSum = Avx.Add(vSum, prod);
                        i += 8;
                    }

                    float* pSum = (float*)&vSum;
                    sum += pSum[0] + pSum[1] + pSum[2] + pSum[3] + pSum[4] + pSum[5] + pSum[6] + pSum[7];
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
                {
                    int vCount = Vector<float>.Count;
                    Vector<float> vSum = Vector<float>.Zero;

                    while (i <= length - vCount)
                    {
                        Vector<float> va = *(Vector<float>*)(pA + i);
                        Vector<float> vb = *(Vector<float>*)(pB + i);
                        vSum += va * vb;
                        i += vCount;
                    }

                    sum += Vector.Dot(vSum, Vector<float>.One);
                }
#endif
                for (; i < length; i++)
                {
                    sum += pA[i] * pB[i];
                }
            }

            return sum;
        }
    }
}
