using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
#else
using System.Numerics;
#endif

namespace ZeroPrimitives.Simd
{
    /// <summary>
    /// Cross-platform SIMD vector math and signal engine for high-performance tensor and vision computing.
    /// Seamlessly leverages 256-bit AVX2/AVX-512 on x64 and 128-bit NEON on ARM64 architectures
    /// with zero heap allocations and unmanaged span support.
    /// </summary>
    public static unsafe class SimdVector
    {
        /// <summary>
        /// Vectorized element-wise addition: <c>dst[i] = a[i] + b[i]</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Add(Span<float> dst, ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int length = Math.Min(dst.Length, Math.Min(a.Length, b.Length));
            if (length <= 0) return;

            fixed (float* pDst = dst)
            fixed (float* pA = a)
            fixed (float* pB = b)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector256.Load(pA + i);
                        var vb = Vector256.Load(pB + i);
                        Vector256.Store(va + vb, pDst + i);
                    }
                }
                else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
                {
                    int step = Vector128<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector128.Load(pA + i);
                        var vb = Vector128.Load(pB + i);
                        Vector128.Store(va + vb, pDst + i);
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
                {
                    int step = Vector<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = *(Vector<float>*)(pA + i);
                        var vb = *(Vector<float>*)(pB + i);
                        *(Vector<float>*)(pDst + i) = va + vb;
                    }
                }
#endif

                // Scalar remainder
                for (; i < length; i++)
                {
                    pDst[i] = pA[i] + pB[i];
                }
            }
        }

        /// <summary>
        /// Vectorized element-wise subtraction: <c>dst[i] = a[i] - b[i]</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Subtract(Span<float> dst, ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int length = Math.Min(dst.Length, Math.Min(a.Length, b.Length));
            if (length <= 0) return;

            fixed (float* pDst = dst)
            fixed (float* pA = a)
            fixed (float* pB = b)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector256.Load(pA + i);
                        var vb = Vector256.Load(pB + i);
                        Vector256.Store(va - vb, pDst + i);
                    }
                }
                else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
                {
                    int step = Vector128<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector128.Load(pA + i);
                        var vb = Vector128.Load(pB + i);
                        Vector128.Store(va - vb, pDst + i);
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
                {
                    int step = Vector<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = *(Vector<float>*)(pA + i);
                        var vb = *(Vector<float>*)(pB + i);
                        *(Vector<float>*)(pDst + i) = va - vb;
                    }
                }
#endif

                // Scalar remainder
                for (; i < length; i++)
                {
                    pDst[i] = pA[i] - pB[i];
                }
            }
        }

        /// <summary>
        /// Vectorized element-wise multiplication: <c>dst[i] = a[i] * b[i]</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Multiply(Span<float> dst, ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int length = Math.Min(dst.Length, Math.Min(a.Length, b.Length));
            if (length <= 0) return;

            fixed (float* pDst = dst)
            fixed (float* pA = a)
            fixed (float* pB = b)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector256.Load(pA + i);
                        var vb = Vector256.Load(pB + i);
                        Vector256.Store(va * vb, pDst + i);
                    }
                }
                else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
                {
                    int step = Vector128<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector128.Load(pA + i);
                        var vb = Vector128.Load(pB + i);
                        Vector128.Store(va * vb, pDst + i);
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
                {
                    int step = Vector<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = *(Vector<float>*)(pA + i);
                        var vb = *(Vector<float>*)(pB + i);
                        *(Vector<float>*)(pDst + i) = va * vb;
                    }
                }
#endif

                // Scalar remainder
                for (; i < length; i++)
                {
                    pDst[i] = pA[i] * pB[i];
                }
            }
        }

        /// <summary>
        /// Vectorized Fused Multiply-Add (FMA): <c>dst[i] = a[i] * b[i] + c[i]</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyAdd(Span<float> dst, ReadOnlySpan<float> a, ReadOnlySpan<float> b, ReadOnlySpan<float> c)
        {
            int length = Math.Min(dst.Length, Math.Min(a.Length, Math.Min(b.Length, c.Length)));
            if (length <= 0) return;

            fixed (float* pDst = dst)
            fixed (float* pA = a)
            fixed (float* pB = b)
            fixed (float* pC = c)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector256.Load(pA + i);
                        var vb = Vector256.Load(pB + i);
                        var vc = Vector256.Load(pC + i);
                        Vector256.Store((va * vb) + vc, pDst + i);
                    }
                }
                else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
                {
                    int step = Vector128<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector128.Load(pA + i);
                        var vb = Vector128.Load(pB + i);
                        var vc = Vector128.Load(pC + i);
                        Vector128.Store((va * vb) + vc, pDst + i);
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
                {
                    int step = Vector<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = *(Vector<float>*)(pA + i);
                        var vb = *(Vector<float>*)(pB + i);
                        var vc = *(Vector<float>*)(pC + i);
                        *(Vector<float>*)(pDst + i) = (va * vb) + vc;
                    }
                }
#endif

                // Scalar remainder
                for (; i < length; i++)
                {
                    pDst[i] = (pA[i] * pB[i]) + pC[i];
                }
            }
        }

        /// <summary>
        /// Scales all elements by a scalar constant: <c>dst[i] = src[i] * scalar</c>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Scale(Span<float> dst, ReadOnlySpan<float> src, float scalar)
        {
            int length = Math.Min(dst.Length, src.Length);
            if (length <= 0) return;

            fixed (float* pDst = dst)
            fixed (float* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    var vs = Vector256.Create(scalar);
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        Vector256.Store(v * vs, pDst + i);
                    }
                }
                else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
                {
                    var vs = Vector128.Create(scalar);
                    int step = Vector128<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var v = Vector128.Load(pSrc + i);
                        Vector128.Store(v * vs, pDst + i);
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
                {
                    var vs = new Vector<float>(scalar);
                    int step = Vector<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var v = *(Vector<float>*)(pSrc + i);
                        *(Vector<float>*)(pDst + i) = v * vs;
                    }
                }
#endif

                // Scalar remainder
                for (; i < length; i++)
                {
                    pDst[i] = pSrc[i] * scalar;
                }
            }
        }

        /// <summary>
        /// Clamps all elements within <paramref name="min"/> and <paramref name="max"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Clamp(Span<float> dst, ReadOnlySpan<float> src, float min, float max)
        {
            int length = Math.Min(dst.Length, src.Length);
            if (length <= 0) return;

            fixed (float* pDst = dst)
            fixed (float* pSrc = src)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<float>.Count)
                {
                    var vMin = Vector256.Create(min);
                    var vMax = Vector256.Create(max);
                    int step = Vector256<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var v = Vector256.Load(pSrc + i);
                        var clamped = Vector256.Min(Vector256.Max(v, vMin), vMax);
                        Vector256.Store(clamped, pDst + i);
                    }
                }
                else if (Vector128.IsHardwareAccelerated && length >= Vector128<float>.Count)
                {
                    var vMin = Vector128.Create(min);
                    var vMax = Vector128.Create(max);
                    int step = Vector128<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var v = Vector128.Load(pSrc + i);
                        var clamped = Vector128.Min(Vector128.Max(v, vMin), vMax);
                        Vector128.Store(clamped, pDst + i);
                    }
                }
#else
                if (Vector.IsHardwareAccelerated && length >= Vector<float>.Count)
                {
                    var vMin = new Vector<float>(min);
                    var vMax = new Vector<float>(max);
                    int step = Vector<float>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var v = *(Vector<float>*)(pSrc + i);
                        var clamped = Vector.Min(Vector.Max(v, vMin), vMax);
                        *(Vector<float>*)(pDst + i) = clamped;
                    }
                }
#endif

                // Scalar remainder
                for (; i < length; i++)
                {
                    float val = pSrc[i];
                    pDst[i] = val < min ? min : (val > max ? max : val);
                }
            }
        }

        /// <summary>
        /// Normalizes raw byte values [0, 255] to float range [0.0f, 1.0f] for machine learning tensor ingestion.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NormalizeByteToFloat(ReadOnlySpan<byte> src, Span<float> dst)
        {
            int length = Math.Min(src.Length, dst.Length);
            if (length <= 0) return;

            const float Inv255 = 1.0f / 255.0f;

            fixed (byte* pSrc = src)
            fixed (float* pDst = dst)
            {
                int i = 0;

                // Process in chunks of 8 floats
                int limit = length - 8;
                for (; i <= limit; i += 8)
                {
                    pDst[i + 0] = pSrc[i + 0] * Inv255;
                    pDst[i + 1] = pSrc[i + 1] * Inv255;
                    pDst[i + 2] = pSrc[i + 2] * Inv255;
                    pDst[i + 3] = pSrc[i + 3] * Inv255;
                    pDst[i + 4] = pSrc[i + 4] * Inv255;
                    pDst[i + 5] = pSrc[i + 5] * Inv255;
                    pDst[i + 6] = pSrc[i + 6] * Inv255;
                    pDst[i + 7] = pSrc[i + 7] * Inv255;
                }

                // Remainder
                for (; i < length; i++)
                {
                    pDst[i] = pSrc[i] * Inv255;
                }
            }
        }

        /// <summary>
        /// Quantizes float values [0.0f, 1.0f] back to byte [0, 255] with saturation for vision and image output.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void QuantizeFloatToByte(ReadOnlySpan<float> src, Span<byte> dst)
        {
            int length = Math.Min(src.Length, dst.Length);
            if (length <= 0) return;

            fixed (float* pSrc = src)
            fixed (byte* pDst = dst)
            {
                int i = 0;
                int limit = length - 8;
                for (; i <= limit; i += 8)
                {
                    pDst[i + 0] = ClampFloatToByte(pSrc[i + 0] * 255.0f);
                    pDst[i + 1] = ClampFloatToByte(pSrc[i + 1] * 255.0f);
                    pDst[i + 2] = ClampFloatToByte(pSrc[i + 2] * 255.0f);
                    pDst[i + 3] = ClampFloatToByte(pSrc[i + 3] * 255.0f);
                    pDst[i + 4] = ClampFloatToByte(pSrc[i + 4] * 255.0f);
                    pDst[i + 5] = ClampFloatToByte(pSrc[i + 5] * 255.0f);
                    pDst[i + 6] = ClampFloatToByte(pSrc[i + 6] * 255.0f);
                    pDst[i + 7] = ClampFloatToByte(pSrc[i + 7] * 255.0f);
                }

                for (; i < length; i++)
                {
                    pDst[i] = ClampFloatToByte(pSrc[i] * 255.0f);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ClampFloatToByte(float val)
        {
            if (val <= 0.0f) return 0;
            if (val >= 255.0f) return 255;
            return (byte)(val + 0.5f);
        }

        /// <summary>
        /// High-throughput vectorized sequence equality check across two byte spans.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool SequenceEqual(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            if (a.Length != b.Length) return false;
            int length = a.Length;
            if (length == 0) return true;

            fixed (byte* pA = a)
            fixed (byte* pB = b)
            {
                int i = 0;

#if NET8_0_OR_GREATER
                if (Vector256.IsHardwareAccelerated && length >= Vector256<byte>.Count)
                {
                    int step = Vector256<byte>.Count;
                    int limit = length - step;
                    for (; i <= limit; i += step)
                    {
                        var va = Vector256.Load(pA + i);
                        var vb = Vector256.Load(pB + i);
                        if (va != vb) return false;
                    }
                }
#endif
                // Check 8-byte (ulong) chunks
                int ulongLimit = length - sizeof(ulong);
                for (; i <= ulongLimit; i += sizeof(ulong))
                {
                    if (*(ulong*)(pA + i) != *(ulong*)(pB + i)) return false;
                }

                // Remaining bytes
                for (; i < length; i++)
                {
                    if (pA[i] != pB[i]) return false;
                }

                return true;
            }
        }
    }
}
