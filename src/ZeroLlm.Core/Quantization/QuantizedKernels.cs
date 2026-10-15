using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace ZeroLlm.Core.Quantization
{
    /// <summary>
    /// Hardware-optimized fused dot-product and matrix-vector multiplication kernels
    /// operating directly on GGML quantized blocks without heap allocations or full dequantization.
    /// </summary>
    public static unsafe class QuantizedKernels
    {
        /// <summary>
        /// Computes the fused dot product between a float input vector and Q8_0 quantized weight blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProductQ8_0(float* pX, BlockQ8_0* pW, int blockCount)
        {
            float totalSum = 0.0f;

#if NET8_0_OR_GREATER
            if (Avx2.IsSupported)
            {
                for (int b = 0; b < blockCount; b++)
                {
                    BlockQ8_0* block = pW + b;
                    float scale = block->GetScale();
                    float* bX = pX + (b * BlockQ8_0.BlockSize);
                    sbyte* qs = block->Qs;

                    var vW0 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs));
                    var vW1 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs + 8));
                    var vW2 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs + 16));
                    var vW3 = Avx.ConvertToVector256Single(Avx2.ConvertToVector256Int32(qs + 24));

                    var vAct0 = Vector256.Load(bX);
                    var vAct1 = Vector256.Load(bX + 8);
                    var vAct2 = Vector256.Load(bX + 16);
                    var vAct3 = Vector256.Load(bX + 24);

                    var vDot0 = Avx.Multiply(vW0, vAct0);
                    var vDot1 = Avx.Multiply(vW1, vAct1);
                    var vDot2 = Fma.IsSupported ? Fma.MultiplyAdd(vW2, vAct2, vDot0) : Avx.Add(Avx.Multiply(vW2, vAct2), vDot0);
                    var vDot3 = Fma.IsSupported ? Fma.MultiplyAdd(vW3, vAct3, vDot1) : Avx.Add(Avx.Multiply(vW3, vAct3), vDot1);

                    totalSum += Vector256.Sum(vDot2 + vDot3) * scale;
                }
                return totalSum;
            }
#endif

            for (int b = 0; b < blockCount; b++)
            {
                BlockQ8_0* block = pW + b;
                float scale = block->GetScale();
                float* bX = pX + (b * BlockQ8_0.BlockSize);
                sbyte* qs = block->Qs;

                float s0 = (bX[0] * qs[0]) + (bX[1] * qs[1]) + (bX[2] * qs[2]) + (bX[3] * qs[3])
                         + (bX[4] * qs[4]) + (bX[5] * qs[5]) + (bX[6] * qs[6]) + (bX[7] * qs[7]);

                float s1 = (bX[8] * qs[8]) + (bX[9] * qs[9]) + (bX[10] * qs[10]) + (bX[11] * qs[11])
                         + (bX[12] * qs[12]) + (bX[13] * qs[13]) + (bX[14] * qs[14]) + (bX[15] * qs[15]);

                float s2 = (bX[16] * qs[16]) + (bX[17] * qs[17]) + (bX[18] * qs[18]) + (bX[19] * qs[19])
                         + (bX[20] * qs[20]) + (bX[21] * qs[21]) + (bX[22] * qs[22]) + (bX[23] * qs[23]);

                float s3 = (bX[24] * qs[24]) + (bX[25] * qs[25]) + (bX[26] * qs[26]) + (bX[27] * qs[27])
                         + (bX[28] * qs[28]) + (bX[29] * qs[29]) + (bX[30] * qs[30]) + (bX[31] * qs[31]);

                totalSum += ((s0 + s1) + (s2 + s3)) * scale;
            }
            return totalSum;
        }

        /// <summary>
        /// Computes the fused dot product between a float input vector and Q8_0 quantized weight blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProductQ8_0(ReadOnlySpan<float> x, ReadOnlySpan<BlockQ8_0> w)
        {
            int blockCount = w.Length;
            if (x.Length < blockCount * BlockQ8_0.BlockSize)
                throw new ArgumentException("Vector x is shorter than required for the quantized weight blocks.");

            fixed (float* pX = x)
            fixed (BlockQ8_0* pW = w)
            {
                return DotProductQ8_0(pX, pW, blockCount);
            }
        }

        /// <summary>
        /// Computes the fused dot product between a float input vector and Q4_0 quantized weight blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProductQ4_0(float* pX, BlockQ4_0* pW, int blockCount)
        {
            float totalSum = 0.0f;

#if NET8_0_OR_GREATER
            if (Avx2.IsSupported)
            {
                var vMask = Vector256.Create(0x0F);
                var vOffset = Vector256.Create(8);

                for (int b = 0; b < blockCount; b++)
                {
                    BlockQ4_0* block = pW + b;
                    float scale = block->GetScale();
                    float* bX = pX + (b * BlockQ4_0.BlockSize);
                    byte* qs = block->Qs;

                    var raw0 = Avx2.ConvertToVector256Int32(qs);
                    var raw1 = Avx2.ConvertToVector256Int32(qs + 8);

                    var vW0_0 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(raw0, vMask), vOffset));
                    var vW1_0 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(Avx2.ShiftRightLogical(raw0, 4), vMask), vOffset));

                    var vW0_1 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(raw1, vMask), vOffset));
                    var vW1_1 = Avx.ConvertToVector256Single(Avx2.Subtract(Avx2.And(Avx2.ShiftRightLogical(raw1, 4), vMask), vOffset));

                    var vDot0 = Avx.Multiply(vW0_0, Vector256.Load(bX));
                    var vDot1 = Avx.Multiply(vW0_1, Vector256.Load(bX + 8));
                    var vDot2 = Fma.IsSupported ? Fma.MultiplyAdd(vW1_0, Vector256.Load(bX + 16), vDot0) : Avx.Add(Avx.Multiply(vW1_0, Vector256.Load(bX + 16)), vDot0);
                    var vDot3 = Fma.IsSupported ? Fma.MultiplyAdd(vW1_1, Vector256.Load(bX + 24), vDot1) : Avx.Add(Avx.Multiply(vW1_1, Vector256.Load(bX + 24)), vDot1);

                    totalSum += Vector256.Sum(vDot2 + vDot3) * scale;
                }
                return totalSum;
            }
#endif

            for (int b = 0; b < blockCount; b++)
            {
                BlockQ4_0* block = pW + b;
                float scale = block->GetScale();
                float* bX = pX + (b * BlockQ4_0.BlockSize);
                byte* qs = block->Qs;

                float s0 = (bX[0] * ((qs[0] & 0x0F) - 8)) + (bX[16] * ((qs[0] >> 4) - 8))
                         + (bX[1] * ((qs[1] & 0x0F) - 8)) + (bX[17] * ((qs[1] >> 4) - 8))
                         + (bX[2] * ((qs[2] & 0x0F) - 8)) + (bX[18] * ((qs[2] >> 4) - 8))
                         + (bX[3] * ((qs[3] & 0x0F) - 8)) + (bX[19] * ((qs[3] >> 4) - 8));

                float s1 = (bX[4] * ((qs[4] & 0x0F) - 8)) + (bX[20] * ((qs[4] >> 4) - 8))
                         + (bX[5] * ((qs[5] & 0x0F) - 8)) + (bX[21] * ((qs[5] >> 4) - 8))
                         + (bX[6] * ((qs[6] & 0x0F) - 8)) + (bX[22] * ((qs[6] >> 4) - 8))
                         + (bX[7] * ((qs[7] & 0x0F) - 8)) + (bX[23] * ((qs[7] >> 4) - 8));

                float s2 = (bX[8] * ((qs[8] & 0x0F) - 8)) + (bX[24] * ((qs[8] >> 4) - 8))
                         + (bX[9] * ((qs[9] & 0x0F) - 8)) + (bX[25] * ((qs[9] >> 4) - 8))
                         + (bX[10] * ((qs[10] & 0x0F) - 8)) + (bX[26] * ((qs[10] >> 4) - 8))
                         + (bX[11] * ((qs[11] & 0x0F) - 8)) + (bX[27] * ((qs[11] >> 4) - 8));

                float s3 = (bX[12] * ((qs[12] & 0x0F) - 8)) + (bX[28] * ((qs[12] >> 4) - 8))
                         + (bX[13] * ((qs[13] & 0x0F) - 8)) + (bX[29] * ((qs[13] >> 4) - 8))
                         + (bX[14] * ((qs[14] & 0x0F) - 8)) + (bX[30] * ((qs[14] >> 4) - 8))
                         + (bX[15] * ((qs[15] & 0x0F) - 8)) + (bX[31] * ((qs[15] >> 4) - 8));

                totalSum += ((s0 + s1) + (s2 + s3)) * scale;
            }
            return totalSum;
        }

        /// <summary>
        /// Computes the fused dot product between a float input vector and Q4_0 quantized weight blocks.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProductQ4_0(ReadOnlySpan<float> x, ReadOnlySpan<BlockQ4_0> w)
        {
            int blockCount = w.Length;
            if (x.Length < blockCount * BlockQ4_0.BlockSize)
                throw new ArgumentException("Vector x is shorter than required for the quantized weight blocks.");

            fixed (float* pX = x)
            fixed (BlockQ4_0* pW = w)
            {
                return DotProductQ4_0(pX, pW, blockCount);
            }
        }

        /// <summary>
        /// Multiplies a matrix of Q8_0 blocks with a float vector x, writing into destination vector y: y = W * x.
        /// </summary>
        public static void MatVecMulQ8_0(
            ReadOnlySpan<BlockQ8_0> weightMatrix,
            ReadOnlySpan<float> x,
            Span<float> y,
            int rows,
            int cols)
        {
            int blocksPerRow = cols / BlockQ8_0.BlockSize;
            if (weightMatrix.Length < rows * blocksPerRow)
                throw new ArgumentException("Weight matrix dimension mismatch.");

            fixed (BlockQ8_0* pW = weightMatrix)
            fixed (float* pX = x)
            fixed (float* pY = y)
            {
                if (rows >= 1024)
                {
                    IntPtr ptrW = (IntPtr)pW;
                    IntPtr ptrX = (IntPtr)pX;
                    IntPtr ptrY = (IntPtr)pY;
                    Parallel.For(0, rows, r =>
                    {
                        var curW = (BlockQ8_0*)ptrW + (r * blocksPerRow);
                        var curX = (float*)ptrX;
                        var curY = (float*)ptrY;
                        curY[r] = DotProductQ8_0(curX, curW, blocksPerRow);
                    });
                }
                else
                {
                    for (int r = 0; r < rows; r++)
                    {
                        pY[r] = DotProductQ8_0(pX, pW + (r * blocksPerRow), blocksPerRow);
                    }
                }
            }
        }

        /// <summary>
        /// Multiplies a matrix of Q4_0 blocks with a float vector x, writing into destination vector y: y = W * x.
        /// </summary>
        public static void MatVecMulQ4_0(
            ReadOnlySpan<BlockQ4_0> weightMatrix,
            ReadOnlySpan<float> x,
            Span<float> y,
            int rows,
            int cols)
        {
            int blocksPerRow = cols / BlockQ4_0.BlockSize;
            if (weightMatrix.Length < rows * blocksPerRow)
                throw new ArgumentException("Weight matrix dimension mismatch.");

            fixed (BlockQ4_0* pW = weightMatrix)
            fixed (float* pX = x)
            fixed (float* pY = y)
            {
                if (rows >= 1024)
                {
                    IntPtr ptrW = (IntPtr)pW;
                    IntPtr ptrX = (IntPtr)pX;
                    IntPtr ptrY = (IntPtr)pY;
                    Parallel.For(0, rows, r =>
                    {
                        var curW = (BlockQ4_0*)ptrW + (r * blocksPerRow);
                        var curX = (float*)ptrX;
                        var curY = (float*)ptrY;
                        curY[r] = DotProductQ4_0(curX, curW, blocksPerRow);
                    });
                }
                else
                {
                    for (int r = 0; r < rows; r++)
                    {
                        pY[r] = DotProductQ4_0(pX, pW + (r * blocksPerRow), blocksPerRow);
                    }
                }
            }
        }

        /// <summary>
        /// Multiplies a matrix of Q8_0 blocks with a float vector x, accumulating into destination vector y: y += W * x.
        /// </summary>
        public static void MatVecAddQ8_0(
            ReadOnlySpan<BlockQ8_0> weightMatrix,
            ReadOnlySpan<float> x,
            Span<float> y,
            int rows,
            int cols)
        {
            int blocksPerRow = cols / BlockQ8_0.BlockSize;
            if (weightMatrix.Length < rows * blocksPerRow)
                throw new ArgumentException("Weight matrix dimension mismatch.");

            fixed (BlockQ8_0* pW = weightMatrix)
            fixed (float* pX = x)
            fixed (float* pY = y)
            {
                if (rows >= 1024)
                {
                    IntPtr ptrW = (IntPtr)pW;
                    IntPtr ptrX = (IntPtr)pX;
                    IntPtr ptrY = (IntPtr)pY;
                    Parallel.For(0, rows, r =>
                    {
                        var curW = (BlockQ8_0*)ptrW + (r * blocksPerRow);
                        var curX = (float*)ptrX;
                        var curY = (float*)ptrY;
                        curY[r] += DotProductQ8_0(curX, curW, blocksPerRow);
                    });
                }
                else
                {
                    for (int r = 0; r < rows; r++)
                    {
                        pY[r] += DotProductQ8_0(pX, pW + (r * blocksPerRow), blocksPerRow);
                    }
                }
            }
        }

        /// <summary>
        /// Multiplies a matrix of Q4_0 blocks with a float vector x, accumulating into destination vector y: y += W * x.
        /// </summary>
        public static void MatVecAddQ4_0(
            ReadOnlySpan<BlockQ4_0> weightMatrix,
            ReadOnlySpan<float> x,
            Span<float> y,
            int rows,
            int cols)
        {
            int blocksPerRow = cols / BlockQ4_0.BlockSize;
            if (weightMatrix.Length < rows * blocksPerRow)
                throw new ArgumentException("Weight matrix dimension mismatch.");

            fixed (BlockQ4_0* pW = weightMatrix)
            fixed (float* pX = x)
            fixed (float* pY = y)
            {
                if (rows >= 1024)
                {
                    IntPtr ptrW = (IntPtr)pW;
                    IntPtr ptrX = (IntPtr)pX;
                    IntPtr ptrY = (IntPtr)pY;
                    Parallel.For(0, rows, r =>
                    {
                        var curW = (BlockQ4_0*)ptrW + (r * blocksPerRow);
                        var curX = (float*)ptrX;
                        var curY = (float*)ptrY;
                        curY[r] += DotProductQ4_0(curX, curW, blocksPerRow);
                    });
                }
                else
                {
                    for (int r = 0; r < rows; r++)
                    {
                        pY[r] += DotProductQ4_0(pX, pW + (r * blocksPerRow), blocksPerRow);
                    }
                }
            }
        }
    }
}
