using System;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace ZeroTensor.Core
{
    public static partial class TensorBlas
    {
        /// <summary>
        /// Computes matrix multiplication of two INT8 (sbyte) tensors: C (M x N) = A (M x K) @ B (K x N), accumulating into INT32.
        /// </summary>
        public static Tensor<int> MatMul(Tensor<sbyte> a, Tensor<sbyte> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Rank != 2 || b.Rank != 2) throw new ArgumentException("INT8 MatMul currently supports 2D matrices.");

            int m = a.Shape[0];
            int k = a.Shape[1];
            int n = b.Shape[1];

            if (k != b.Shape[0])
            {
                throw new InvalidOperationException($"Inner matrix dimensions must agree: A is ({m}x{k}), B is ({b.Shape[0]}x{n}).");
            }

            var c = new Tensor<int>(m, n);
            GemmInt8(a, b, c);
            return c;
        }

        /// <summary>
        /// Cache-blocked INT8 General Matrix Multiply with INT32 accumulation: C = A @ B.
        /// Uses AVX2 8/16-element SIMD vectorization on .NET 8+.
        /// </summary>
        public static unsafe void GemmInt8(Tensor<sbyte> a, Tensor<sbyte> b, Tensor<int> c)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (c == null) throw new ArgumentNullException(nameof(c));

            int m = a.Shape[0];
            int k = a.Shape[1];
            int n = b.Shape[1];

            c.Fill(0);

            var aContig = a.ToContiguous();
            var bContig = b.ToContiguous();

            var bufA = aContig.Buffer;
            var bufB = bContig.Buffer;
            var bufC = c.Buffer;

            int offA = aContig.Offset;
            int offB = bContig.Offset;
            int offC = c.Offset;

            int sA0 = aContig.Strides[0], sA1 = aContig.Strides[1];
            int sB0 = bContig.Strides[0], sB1 = bContig.Strides[1];
            int sC0 = c.Strides[0], sC1 = c.Strides[1];

            bool runParallel = (m * n >= 4096) && (m >= BlockM * 2);

            Action<int> processMBlock = (m0) =>
            {
                int mEnd = Math.Min(m0 + BlockM, m);

                fixed (sbyte* pB = bufB)
                fixed (int* pC = bufC)
                {
                    for (int k0 = 0; k0 < k; k0 += BlockK)
                    {
                        int kEnd = Math.Min(k0 + BlockK, k);

                        for (int n0 = 0; n0 < n; n0 += BlockN)
                        {
                            int nEnd = Math.Min(n0 + BlockN, n);

                            for (int i = m0; i < mEnd; i++)
                            {
                                int rowOffA = offA + i * sA0;
                                int rowOffC = offC + i * sC0;

                                for (int p = k0; p < kEnd; p++)
                                {
                                    int aVal = bufA[rowOffA + p * sA1];
                                    if (aVal == 0) continue;

                                    int rowOffB = offB + p * sB0;
                                    int j = n0;

#if NET8_0_OR_GREATER
                                    if (Avx2.IsSupported && sB1 == 1 && sC1 == 1)
                                    {
                                        var vA = Vector256.Create(aVal);
                                        for (; j <= nEnd - 16; j += 16)
                                        {
                                            var vB0 = Avx2.ConvertToVector256Int32(pB + rowOffB + j);
                                            var vB1 = Avx2.ConvertToVector256Int32(pB + rowOffB + j + 8);
                                            var vC0 = Avx.LoadVector256(pC + rowOffC + j);
                                            var vC1 = Avx.LoadVector256(pC + rowOffC + j + 8);
                                            Avx.Store(pC + rowOffC + j, Avx2.Add(vC0, Avx2.MultiplyLow(vA, vB0)));
                                            Avx.Store(pC + rowOffC + j + 8, Avx2.Add(vC1, Avx2.MultiplyLow(vA, vB1)));
                                        }

                                        for (; j <= nEnd - 8; j += 8)
                                        {
                                            var vB0 = Avx2.ConvertToVector256Int32(pB + rowOffB + j);
                                            var vC0 = Avx.LoadVector256(pC + rowOffC + j);
                                            Avx.Store(pC + rowOffC + j, Avx2.Add(vC0, Avx2.MultiplyLow(vA, vB0)));
                                        }
                                    }
#endif

                                    // 4-way unrolled scalar loop
                                    for (; j <= nEnd - 4; j += 4)
                                    {
                                        bufC[rowOffC + j * sC1] += aVal * bufB[rowOffB + j * sB1];
                                        bufC[rowOffC + (j + 1) * sC1] += aVal * bufB[rowOffB + (j + 1) * sB1];
                                        bufC[rowOffC + (j + 2) * sC1] += aVal * bufB[rowOffB + (j + 2) * sB1];
                                        bufC[rowOffC + (j + 3) * sC1] += aVal * bufB[rowOffB + (j + 3) * sB1];
                                    }

                                    for (; j < nEnd; j++)
                                    {
                                        bufC[rowOffC + j * sC1] += aVal * bufB[rowOffB + j * sB1];
                                    }
                                }
                            }
                        }
                    }
                }
            };

            if (runParallel)
            {
                int numMBlocks = (m + BlockM - 1) / BlockM;
                Parallel.For(0, numMBlocks, blockIdx =>
                {
                    processMBlock(blockIdx * BlockM);
                });
            }
            else
            {
                for (int m0 = 0; m0 < m; m0 += BlockM)
                {
                    processMBlock(m0);
                }
            }
        }

        /// <summary>
        /// Quantized INT8 GEMM with zero-point offset and FP32 scale output:
        /// C_float[i, j] = scaleA * scaleB * sum((A[i, p] - zeroPointA) * (B[p, j] - zeroPointB)).
        /// Accumulates INT32 dot products in registers/scalars and writes scaled FP32 directly into C with zero intermediate Tensor allocations.
        /// </summary>
        public static unsafe void GemmInt8(
            Tensor<sbyte> a,
            Tensor<sbyte> b,
            Tensor<float> c,
            float scaleA = 1.0f,
            float scaleB = 1.0f,
            int zeroPointA = 0,
            int zeroPointB = 0)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (c == null) throw new ArgumentNullException(nameof(c));

            int m = a.Shape[0];
            int k = a.Shape[1];
            int n = b.Shape[1];

            float combinedScale = scaleA * scaleB;

            var aContig = a.ToContiguous();
            var bContig = b.ToContiguous();

            var bufA = aContig.Buffer;
            var bufB = bContig.Buffer;
            var bufC = c.Buffer;

            int offA = aContig.Offset;
            int offB = bContig.Offset;
            int offC = c.Offset;

            int sA0 = aContig.Strides[0], sA1 = aContig.Strides[1];
            int sB0 = bContig.Strides[0], sB1 = bContig.Strides[1];
            int sC0 = c.Strides[0], sC1 = c.Strides[1];

            fixed (sbyte* pB = bufB)
            fixed (float* pC = bufC)
            {
                for (int i = 0; i < m; i++)
                {
                    int rowOffA = offA + i * sA0;
                    int rowOffC = offC + i * sC0;
                    int j = 0;

#if NET8_0_OR_GREATER
                    if (Avx2.IsSupported && sB1 == 1 && sC1 == 1)
                    {
                        var vZpB = Vector256.Create(zeroPointB);
                        var vScale = Vector256.Create(combinedScale);

                        for (; j <= n - 16; j += 16)
                        {
                            var vAcc0 = Vector256<int>.Zero;
                            var vAcc1 = Vector256<int>.Zero;

                            for (int p = 0; p < k; p++)
                            {
                                int aVal = bufA[rowOffA + p * sA1] - zeroPointA;
                                if (aVal == 0) continue;

                                var vA = Vector256.Create(aVal);
                                int rowOffB = offB + p * sB0 + j;
                                var vB0 = Avx2.Subtract(Avx2.ConvertToVector256Int32(pB + rowOffB), vZpB);
                                var vB1 = Avx2.Subtract(Avx2.ConvertToVector256Int32(pB + rowOffB + 8), vZpB);
                                vAcc0 = Avx2.Add(vAcc0, Avx2.MultiplyLow(vA, vB0));
                                vAcc1 = Avx2.Add(vAcc1, Avx2.MultiplyLow(vA, vB1));
                            }

                            Avx.Store(pC + rowOffC + j, Avx.Multiply(Avx.ConvertToVector256Single(vAcc0), vScale));
                            Avx.Store(pC + rowOffC + j + 8, Avx.Multiply(Avx.ConvertToVector256Single(vAcc1), vScale));
                        }

                        for (; j <= n - 8; j += 8)
                        {
                            var vAcc0 = Vector256<int>.Zero;

                            for (int p = 0; p < k; p++)
                            {
                                int aVal = bufA[rowOffA + p * sA1] - zeroPointA;
                                if (aVal == 0) continue;

                                var vA = Vector256.Create(aVal);
                                int rowOffB = offB + p * sB0 + j;
                                var vB0 = Avx2.Subtract(Avx2.ConvertToVector256Int32(pB + rowOffB), vZpB);
                                vAcc0 = Avx2.Add(vAcc0, Avx2.MultiplyLow(vA, vB0));
                            }

                            Avx.Store(pC + rowOffC + j, Avx.Multiply(Avx.ConvertToVector256Single(vAcc0), vScale));
                        }
                    }
#endif

                    for (; j < n; j++)
                    {
                        int acc = 0;
                        for (int p = 0; p < k; p++)
                        {
                            int aVal = bufA[rowOffA + p * sA1] - zeroPointA;
                            if (aVal == 0) continue;

                            int bVal = bufB[offB + p * sB0 + j * sB1] - zeroPointB;
                            acc += aVal * bVal;
                        }

                        bufC[rowOffC + j * sC1] = acc * combinedScale;
                    }
                }
            }
        }

        /// <summary>
        /// Packed INT4 (4-bit nibble) General Matrix Multiply: C (M x N) = A (M x K) @ B_int4 (K x N).
        /// Each byte in packedWeights stores two 4-bit weights (low nibble = row 2*l, high nibble = row 2*l+1).
        /// Real weights are computed via: weight = (nibble - zeroPoint) * scale.
        /// Delivers 4x memory compression for edge LLM and vision model inference.
        /// </summary>
        public static unsafe Tensor<float> GemmInt4(
            Tensor<float> activation,
            Tensor<byte> packedWeights,
            Tensor<float> scales,
            Tensor<float>? zeroPoints = null)
        {
            if (activation == null) throw new ArgumentNullException(nameof(activation));
            if (packedWeights == null) throw new ArgumentNullException(nameof(packedWeights));
            if (scales == null) throw new ArgumentNullException(nameof(scales));

            if (activation.Rank != 2 || packedWeights.Rank != 2)
                throw new ArgumentException("GemmInt4 requires rank-2 activation and packed weight tensors.");

            int m = activation.Shape[0];
            int k = activation.Shape[1];
            int packedK = packedWeights.Shape[0];
            int n = packedWeights.Shape[1];

            if (packedK != (k + 1) / 2)
            {
                throw new InvalidOperationException($"Inner dimension mismatch: activation K={k}, but packedWeights K={packedK} (expected {(k + 1) / 2}).");
            }
            if (scales.Length < n)
            {
                throw new InvalidOperationException($"Scales length {scales.Length} must be at least N={n}.");
            }

            var c = new Tensor<float>(m, n);
            var actContig = activation.ToContiguous();
            var actBuf = actContig.Buffer;
            int actOff = actContig.Offset;
            int sAct0 = actContig.Strides[0], sAct1 = actContig.Strides[1];

            var wContig = packedWeights.ToContiguous();
            var wBuf = wContig.Buffer;
            int wOff = wContig.Offset;
            int sW0 = wContig.Strides[0], sW1 = wContig.Strides[1];

            var cBuf = c.Buffer;
            int cOff = c.Offset;
            int sC0 = c.Strides[0], sC1 = c.Strides[1];

            var sBuf = scales.Buffer;
            int sOff = scales.Offset;

            var zpBuf = zeroPoints?.Buffer;
            int zpOff = zeroPoints?.Offset ?? 0;

            Parallel.For(0, m, i =>
            {
                int actRowBase = actOff + i * sAct0;
                int cRowBase = cOff + i * sC0;
                int j = 0;

#if NET8_0_OR_GREATER
                if (Avx2.IsSupported && sW1 == 1 && sC1 == 1 && n >= 8)
                {
                    fixed (byte* pW = wBuf)
                    fixed (float* pC = cBuf)
                    fixed (float* pS = sBuf)
                    fixed (float* pZp = zpBuf)
                    {
                        var vMask0F = Vector256.Create(0x0F);

                        for (; j <= n - 8; j += 8)
                        {
                            var vScale = Avx.LoadVector256(pS + sOff + j);
                            var vZp = pZp != null ? Avx.LoadVector256(pZp + zpOff + j) : Vector256<float>.Zero;
                            var vAcc = Vector256<float>.Zero;

                            for (int l = 0; l < packedK; l++)
                            {
                                var vPacked = Avx2.ConvertToVector256Int32(pW + wOff + l * sW0 + j);
                                var vNib0 = Avx.ConvertToVector256Single(Avx2.And(vPacked, vMask0F));
                                var vW0 = Avx.Multiply(Avx.Subtract(vNib0, vZp), vScale);

                                int k0 = l * 2;
                                var vAct0 = Vector256.Create(actBuf[actRowBase + k0 * sAct1]);
                                vAcc = Fma.IsSupported
                                    ? Fma.MultiplyAdd(vAct0, vW0, vAcc)
                                    : Avx.Add(vAcc, Avx.Multiply(vAct0, vW0));

                                int k1 = k0 + 1;
                                if (k1 < k)
                                {
                                    var vNib1 = Avx.ConvertToVector256Single(Avx2.And(Avx2.ShiftRightLogical(vPacked, 4), vMask0F));
                                    var vW1 = Avx.Multiply(Avx.Subtract(vNib1, vZp), vScale);
                                    var vAct1 = Vector256.Create(actBuf[actRowBase + k1 * sAct1]);
                                    vAcc = Fma.IsSupported
                                        ? Fma.MultiplyAdd(vAct1, vW1, vAcc)
                                        : Avx.Add(vAcc, Avx.Multiply(vAct1, vW1));
                                }
                            }

                            Avx.Store(pC + cRowBase + j, vAcc);
                        }
                    }
                }
#endif

                for (; j < n; j++)
                {
                    float scale = sBuf[sOff + j];
                    float zp = zpBuf != null ? zpBuf[zpOff + j] : 0f;
                    float acc = 0f;
                    int l = 0;

#if NET8_0_OR_GREATER
                    if (Avx2.IsSupported && sAct1 == 1 && k >= 8)
                    {
                        fixed (float* pAct = actBuf)
                        {
                            var vZp = Vector256.Create(zp);
                            var vAcc = Vector256<float>.Zero;
                            int fullQuadLimit = (k / 8) * 4;

                            for (; l < fullQuadLimit; l += 4)
                            {
                                byte p0 = wBuf[wOff + l * sW0 + j * sW1];
                                byte p1 = wBuf[wOff + (l + 1) * sW0 + j * sW1];
                                byte p2 = wBuf[wOff + (l + 2) * sW0 + j * sW1];
                                byte p3 = wBuf[wOff + (l + 3) * sW0 + j * sW1];

                                var vNib = Vector256.Create(
                                    (float)(p0 & 0x0F), (float)((p0 >> 4) & 0x0F),
                                    (float)(p1 & 0x0F), (float)((p1 >> 4) & 0x0F),
                                    (float)(p2 & 0x0F), (float)((p2 >> 4) & 0x0F),
                                    (float)(p3 & 0x0F), (float)((p3 >> 4) & 0x0F));

                                var vAct = Avx.LoadVector256(pAct + actRowBase + l * 2);
                                var vDiff = Avx.Subtract(vNib, vZp);
                                vAcc = Fma.IsSupported
                                    ? Fma.MultiplyAdd(vAct, vDiff, vAcc)
                                    : Avx.Add(vAcc, Avx.Multiply(vAct, vDiff));
                            }

                            acc = Vector256.Sum(vAcc) * scale;
                        }
                    }
#endif

                    for (; l < packedK; l++)
                    {
                        byte packed = wBuf[wOff + l * sW0 + j * sW1];
                        int nibble0 = packed & 0x0F;
                        int nibble1 = (packed >> 4) & 0x0F;

                        int k0 = l * 2;
                        int k1 = k0 + 1;

                        float w0 = (nibble0 - zp) * scale;
                        acc += actBuf[actRowBase + k0 * sAct1] * w0;

                        if (k1 < k)
                        {
                            float w1 = (nibble1 - zp) * scale;
                            acc += actBuf[actRowBase + k1 * sAct1] * w1;
                        }
                    }

                    cBuf[cRowBase + j * sC1] = acc;
                }
            });

            return c;
        }

        /// <summary>
        /// On-the-fly streaming Matrix-Vector multiplication for Q4_0 quantized weights:
        /// y = x @ W^T, where W is raw Q4_0 packed byte array.
        /// </summary>
        public static unsafe Tensor<float> GemvQ4_0(Tensor<float> activation, byte[] packedWeights, int nRows, int kCols)
        {
            if (activation == null) throw new ArgumentNullException(nameof(activation));
            if (packedWeights == null) throw new ArgumentNullException(nameof(packedWeights));

            var actContig = activation.IsContiguous ? activation : activation.ToContiguous();
            var result = new Tensor<float>(nRows);

            fixed (byte* pW = packedWeights)
            fixed (float* pAct = &actContig.Storage.GetPinnableReference(actContig.Offset))
            fixed (float* pDst = &result.Storage.GetPinnableReference(result.Offset))
            {
                GgufDequantizer.GemvQ4_0((IntPtr)pW, pAct, pDst, nRows, kCols);
            }

            return result;
        }

        /// <summary>
        /// On-the-fly streaming Matrix-Vector multiplication for Q8_0 quantized weights:
        /// y = x @ W^T, where W is raw Q8_0 packed byte array.
        /// </summary>
        public static unsafe Tensor<float> GemvQ8_0(Tensor<float> activation, byte[] packedWeights, int nRows, int kCols)
        {
            if (activation == null) throw new ArgumentNullException(nameof(activation));
            if (packedWeights == null) throw new ArgumentNullException(nameof(packedWeights));

            var actContig = activation.IsContiguous ? activation : activation.ToContiguous();
            var result = new Tensor<float>(nRows);

            fixed (byte* pW = packedWeights)
            fixed (float* pAct = &actContig.Storage.GetPinnableReference(actContig.Offset))
            fixed (float* pDst = &result.Storage.GetPinnableReference(result.Offset))
            {
                GgufDequantizer.GemvQ8_0((IntPtr)pW, pAct, pDst, nRows, kCols);
            }

            return result;
        }
    }
}
