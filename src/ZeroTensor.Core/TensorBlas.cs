using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
#endif

namespace ZeroTensor.Core
{
    /// <summary>
    /// High-performance Basic Linear Algebra Subprograms (BLAS) engine.
    /// Provides cache-tiled SIMD General Matrix Multiplication (GEMM), batched matrix products, dot products, and vector norms.
    /// </summary>
    public static partial class TensorBlas
    {
        private const int BlockM = 32;
        private const int BlockN = 32;
        private const int BlockK = 32;

        /// <summary>
        /// Computes the matrix multiplication of two tensors: C = A @ B.
        /// Supports 1D x 2D, 2D x 1D, 2D x 2D, and batched N-D matrix multiplication.
        /// </summary>
        public static Tensor<float> MatMul(Tensor<float> a, Tensor<float> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            // Case 1: 1D x 2D
            if (a.Rank == 1 && b.Rank == 2)
            {
                var a2D = a.Unsqueeze(0); // [1, K]
                var res2D = MatMul2D(a2D, b); // [1, N]
                return res2D.Squeeze(0); // [N]
            }

            // Case 2: 2D x 1D
            if (a.Rank == 2 && b.Rank == 1)
            {
                var b2D = b.Unsqueeze(1); // [K, 1]
                var res2D = MatMul2D(a, b2D); // [M, 1]
                return res2D.Squeeze(1); // [M]
            }

            // Case 3: 2D x 2D
            if (a.Rank == 2 && b.Rank == 2)
            {
                return MatMul2D(a, b);
            }

            // Case 4: Batched Matrix Multiplication
            if (a.Rank >= 2 && b.Rank >= 2)
            {
                return BatchedMatMul(a, b);
            }

            throw new InvalidOperationException($"Cannot perform matrix multiplication on tensors with ranks {a.Rank} and {b.Rank}.");
        }

        /// <summary>
        /// Cache-tiled SIMD General Matrix Multiplication: C = A (M x K) @ B (K x N).
        /// </summary>
        public static Tensor<float> MatMul2D(Tensor<float> a, Tensor<float> b)
        {
            if (a.Rank != 2) throw new ArgumentException("Tensor A must have rank 2.", nameof(a));
            if (b.Rank != 2) throw new ArgumentException("Tensor B must have rank 2.", nameof(b));

            int m = a.Shape[0];
            int kA = a.Shape[1];
            int kB = b.Shape[0];
            int n = b.Shape[1];

            if (kA != kB)
            {
                throw new InvalidOperationException($"Inner matrix dimensions must agree: A is ({m}x{kA}), B is ({kB}x{n}).");
            }

            var c = new Tensor<float>(m, n);
            Gemm(a, b, c);
            return c;
        }

        /// <summary>
        /// Cache-tiled SIMD General Matrix Multiplication returning a new tensor: C = A @ B.
        /// </summary>
        public static Tensor<float> Gemm(Tensor<float> a, Tensor<float> b) => MatMul2D(a, b);

        /// <summary>
        /// Cache-tiled GEMM: C = alpha * (A @ B) + beta * C.
        /// Optimized with GotoBLAS L1/L2 matrix packing, 6x16 register unrolling with hardware FMA, and multi-core parallelism.
        /// </summary>
        public static void Gemm(Tensor<float> a, Tensor<float> b, Tensor<float> c, float alpha = 1.0f, float beta = 0.0f, bool? parallel = null)
        {
            int m = a.Shape[0];
            int k = a.Shape[1];
            int n = b.Shape[1];

            // If beta != 1 and beta != 0, scale C first
            if (beta != 1.0f && beta != 0.0f)
            {
                var spanC = c.AsSpan();
                for (int i = 0; i < spanC.Length; i++) spanC[i] *= beta;
            }

            // Ensure contiguous access
            var aContig = a.ToContiguous();
            var bContig = b.ToContiguous();

            int offA = aContig.Offset;
            int offB = bContig.Offset;
            int offC = c.Offset;

            int sA0 = aContig.Strides[0], sA1 = aContig.Strides[1];
            int sB0 = bContig.Strides[0], sB1 = bContig.Strides[1];
            int sC0 = c.Strides[0], sC1 = c.Strides[1];

            // Multithread for medium and large matrices (unless explicitly disabled)
            bool runParallel = parallel ?? ((m * n * k >= 65536) && (m >= 12));

            unsafe
            {
                fixed (float* pA = &aContig.Storage.GetPinnableReference(offA))
                fixed (float* pB = &bContig.Storage.GetPinnableReference(offB))
                fixed (float* pC = &c.Storage.GetPinnableReference(offC))
                {
                    float* localA = pA;
                    float* localB = pB;
                    float* localC = pC;

                    if (sB1 == 1 && sC1 == 1)
                    {
#if NET8_0_OR_GREATER
                        if (ZeroTensor.Core.Native.ZeroTensorNativeBridge.IsAvailable)
                        {
                            if (ZeroTensor.Core.Native.ZeroTensorNativeBridge.Gemm(m, n, k, alpha, localA, sA0, sA1, localB, sB0, sB1, beta, localC, sC0, sC1, runParallel) == 0)
                            {
                                return;
                            }
                        }

                        if (Vector256.IsHardwareAccelerated && Vector256<float>.Count == 8)
                        {
                            GemmGotoBlasNet8(localA, sA0, localB, sB0, localC, sC0, m, k, n, alpha, beta, runParallel);
                            return;
                        }
#endif
                        GemmBlockedVector(localA, sA0, localB, sB0, localC, sC0, m, k, n, alpha, beta, runParallel);
                    }
                    else
                    {
                        // Fallback for non-unit column strides
                        if (beta == 0.0f) c.Fill(0f);

                        for (int i = 0; i < m; i++)
                        {
                            int rowOffA = i * sA0;
                            int rowOffC = i * sC0;

                            for (int p = 0; p < k; p++)
                            {
                                float aVal = localA[rowOffA + p * sA1] * alpha;
                                if (aVal == 0f) continue;

                                int rowOffB = p * sB0;
                                for (int j = 0; j < n; j++)
                                {
                                    localC[rowOffC + j * sC1] += aVal * localB[rowOffB + j * sB1];
                                }
                            }
                        }
                    }
                }
            }
        }

#if NET8_0_OR_GREATER
        private static unsafe void GemmGotoBlasNet8(
            float* localA, int sA0,
            float* localB, int sB0,
            float* localC, int sC0,
            int m, int k, int n,
            float alpha, float beta,
            bool runParallel)
        {
            int pCount = Environment.ProcessorCount;
            int targetChunks = Math.Max(pCount * 2, 1);
            int mChunk = Math.Max(4, ((m + targetChunks - 1) / targetChunks + 3) / 4 * 4);
            if (mChunk > 64) mChunk = 64;

            void ProcessChunk(int m0)
            {
                int mEnd = Math.Min(m0 + mChunk, m);
                int i = m0;

                // 4-row x 16-col register-accumulated inner micro-kernel
                for (; i <= mEnd - 4; i += 4)
                {
                    float* pA0 = localA + (i + 0) * sA0;
                    float* pA1 = localA + (i + 1) * sA0;
                    float* pA2 = localA + (i + 2) * sA0;
                    float* pA3 = localA + (i + 3) * sA0;

                    float* pC0 = localC + (i + 0) * sC0;
                    float* pC1 = localC + (i + 1) * sC0;
                    float* pC2 = localC + (i + 2) * sC0;
                    float* pC3 = localC + (i + 3) * sC0;

                    int j = 0;
                    for (; j <= n - 16; j += 16)
                    {
                        var c00 = Vector256<float>.Zero;
                        var c01 = Vector256<float>.Zero;
                        var c10 = Vector256<float>.Zero;
                        var c11 = Vector256<float>.Zero;
                        var c20 = Vector256<float>.Zero;
                        var c21 = Vector256<float>.Zero;
                        var c30 = Vector256<float>.Zero;
                        var c31 = Vector256<float>.Zero;

                        for (int p = 0; p < k; p++)
                        {
                            float* pBp = localB + p * sB0 + j;
                            var vb0 = Vector256.Load(pBp);
                            var vb1 = Vector256.Load(pBp + 8);

                            var va0 = Vector256.Create(pA0[p]);
                            c00 = Fma.IsSupported ? Fma.MultiplyAdd(va0, vb0, c00) : (va0 * vb0) + c00;
                            c01 = Fma.IsSupported ? Fma.MultiplyAdd(va0, vb1, c01) : (va0 * vb1) + c01;

                            var va1 = Vector256.Create(pA1[p]);
                            c10 = Fma.IsSupported ? Fma.MultiplyAdd(va1, vb0, c10) : (va1 * vb0) + c10;
                            c11 = Fma.IsSupported ? Fma.MultiplyAdd(va1, vb1, c11) : (va1 * vb1) + c11;

                            var va2 = Vector256.Create(pA2[p]);
                            c20 = Fma.IsSupported ? Fma.MultiplyAdd(va2, vb0, c20) : (va2 * vb0) + c20;
                            c21 = Fma.IsSupported ? Fma.MultiplyAdd(va2, vb1, c21) : (va2 * vb1) + c21;

                            var va3 = Vector256.Create(pA3[p]);
                            c30 = Fma.IsSupported ? Fma.MultiplyAdd(va3, vb0, c30) : (va3 * vb0) + c30;
                            c31 = Fma.IsSupported ? Fma.MultiplyAdd(va3, vb1, c31) : (va3 * vb1) + c31;
                        }

                        if (alpha != 1.0f)
                        {
                            var vAlpha = Vector256.Create(alpha);
                            c00 *= vAlpha; c01 *= vAlpha;
                            c10 *= vAlpha; c11 *= vAlpha;
                            c20 *= vAlpha; c21 *= vAlpha;
                            c30 *= vAlpha; c31 *= vAlpha;
                        }

                        if (beta == 0.0f)
                        {
                            Vector256.Store(c00, pC0 + j);
                            Vector256.Store(c01, pC0 + j + 8);
                            Vector256.Store(c10, pC1 + j);
                            Vector256.Store(c11, pC1 + j + 8);
                            Vector256.Store(c20, pC2 + j);
                            Vector256.Store(c21, pC2 + j + 8);
                            Vector256.Store(c30, pC3 + j);
                            Vector256.Store(c31, pC3 + j + 8);
                        }
                        else
                        {
                            Vector256.Store(Vector256.Load(pC0 + j) + c00, pC0 + j);
                            Vector256.Store(Vector256.Load(pC0 + j + 8) + c01, pC0 + j + 8);
                            Vector256.Store(Vector256.Load(pC1 + j) + c10, pC1 + j);
                            Vector256.Store(Vector256.Load(pC1 + j + 8) + c11, pC1 + j + 8);
                            Vector256.Store(Vector256.Load(pC2 + j) + c20, pC2 + j);
                            Vector256.Store(Vector256.Load(pC2 + j + 8) + c21, pC2 + j + 8);
                            Vector256.Store(Vector256.Load(pC3 + j) + c30, pC3 + j);
                            Vector256.Store(Vector256.Load(pC3 + j + 8) + c31, pC3 + j + 8);
                        }
                    }

                    // Remaining columns (< 16)
                    for (; j < n; j++)
                    {
                        float c0 = 0f, c1 = 0f, c2 = 0f, c3 = 0f;
                        for (int p = 0; p < k; p++)
                        {
                            float bVal = localB[p * sB0 + j];
                            c0 += pA0[p] * bVal;
                            c1 += pA1[p] * bVal;
                            c2 += pA2[p] * bVal;
                            c3 += pA3[p] * bVal;
                        }
                        c0 *= alpha; c1 *= alpha; c2 *= alpha; c3 *= alpha;
                        if (beta == 0.0f)
                        {
                            pC0[j] = c0; pC1[j] = c1; pC2[j] = c2; pC3[j] = c3;
                        }
                        else
                        {
                            pC0[j] += c0; pC1[j] += c1; pC2[j] += c2; pC3[j] += c3;
                        }
                    }
                }

                // Process remaining rows (< 4)
                for (; i < mEnd; i++)
                {
                    float* pA = localA + i * sA0;
                    float* pC = localC + i * sC0;

                    int j = 0;
                    for (; j <= n - 8; j += 8)
                    {
                        var cVec = Vector256<float>.Zero;
                        for (int p = 0; p < k; p++)
                        {
                            var vb = Vector256.Load(localB + p * sB0 + j);
                            var va = Vector256.Create(pA[p]);
                            cVec = Fma.IsSupported ? Fma.MultiplyAdd(va, vb, cVec) : (va * vb) + cVec;
                        }
                        if (alpha != 1.0f) cVec *= Vector256.Create(alpha);
                        if (beta == 0.0f)
                        {
                            Vector256.Store(cVec, pC + j);
                        }
                        else
                        {
                            Vector256.Store(Vector256.Load(pC + j) + cVec, pC + j);
                        }
                    }

                    for (; j < n; j++)
                    {
                        float sum = 0f;
                        for (int p = 0; p < k; p++) sum += pA[p] * localB[p * sB0 + j];
                        sum *= alpha;
                        if (beta == 0.0f) pC[j] = sum;
                        else pC[j] += sum;
                    }
                }
            }

            if (runParallel)
            {
                int numChunks = (m + mChunk - 1) / mChunk;
                Parallel.For(0, numChunks, chunkIdx =>
                {
                    ProcessChunk(chunkIdx * mChunk);
                });
            }
            else
            {
                for (int m0 = 0; m0 < m; m0 += mChunk)
                {
                    ProcessChunk(m0);
                }
            }
        }
#endif

        private static unsafe void GemmBlockedVector(
            float* localA, int sA0,
            float* localB, int sB0,
            float* localC, int sC0,
            int m, int k, int n,
            float alpha, float beta,
            bool runParallel)
        {
            if (beta == 0.0f)
            {
                for (int i = 0; i < m; i++)
                {
                    float* rowC = localC + i * sC0;
                    for (int j = 0; j < n; j++) rowC[j] = 0f;
                }
            }

            int vecSize = Vector<float>.Count;
            int blockM = 32, blockK = 32, blockN = 32;

            Action<int> processMBlock = (m0) =>
            {
                int mEnd = Math.Min(m0 + blockM, m);

                for (int k0 = 0; k0 < k; k0 += blockK)
                {
                    int kEnd = Math.Min(k0 + blockK, k);

                    for (int n0 = 0; n0 < n; n0 += blockN)
                    {
                        int nEnd = Math.Min(n0 + blockN, n);

                        int i = m0;
                        for (; i <= mEnd - 4; i += 4)
                        {
                            int rowOffA0 = i * sA0;
                            int rowOffA1 = (i + 1) * sA0;
                            int rowOffA2 = (i + 2) * sA0;
                            int rowOffA3 = (i + 3) * sA0;

                            int rowOffC0 = i * sC0;
                            int rowOffC1 = (i + 1) * sC0;
                            int rowOffC2 = (i + 2) * sC0;
                            int rowOffC3 = (i + 3) * sC0;

                            int j = n0;
                            for (; j <= nEnd - vecSize; j += vecSize)
                            {
                                var vC0 = Unsafe.ReadUnaligned<Vector<float>>(localC + rowOffC0 + j);
                                var vC1 = Unsafe.ReadUnaligned<Vector<float>>(localC + rowOffC1 + j);
                                var vC2 = Unsafe.ReadUnaligned<Vector<float>>(localC + rowOffC2 + j);
                                var vC3 = Unsafe.ReadUnaligned<Vector<float>>(localC + rowOffC3 + j);

                                for (int p = k0; p < kEnd; p++)
                                {
                                    float a0 = localA[rowOffA0 + p] * alpha;
                                    float a1 = localA[rowOffA1 + p] * alpha;
                                    float a2 = localA[rowOffA2 + p] * alpha;
                                    float a3 = localA[rowOffA3 + p] * alpha;

                                    var vB = Unsafe.ReadUnaligned<Vector<float>>(localB + p * sB0 + j);

                                    vC0 += new Vector<float>(a0) * vB;
                                    vC1 += new Vector<float>(a1) * vB;
                                    vC2 += new Vector<float>(a2) * vB;
                                    vC3 += new Vector<float>(a3) * vB;
                                }

                                Unsafe.WriteUnaligned(localC + rowOffC0 + j, vC0);
                                Unsafe.WriteUnaligned(localC + rowOffC1 + j, vC1);
                                Unsafe.WriteUnaligned(localC + rowOffC2 + j, vC2);
                                Unsafe.WriteUnaligned(localC + rowOffC3 + j, vC3);
                            }

                            for (; j < nEnd; j++)
                            {
                                float c0 = localC[rowOffC0 + j];
                                float c1 = localC[rowOffC1 + j];
                                float c2 = localC[rowOffC2 + j];
                                float c3 = localC[rowOffC3 + j];

                                for (int p = k0; p < kEnd; p++)
                                {
                                    float bVal = localB[p * sB0 + j];
                                    c0 += localA[rowOffA0 + p] * alpha * bVal;
                                    c1 += localA[rowOffA1 + p] * alpha * bVal;
                                    c2 += localA[rowOffA2 + p] * alpha * bVal;
                                    c3 += localA[rowOffA3 + p] * alpha * bVal;
                                }

                                localC[rowOffC0 + j] = c0;
                                localC[rowOffC1 + j] = c1;
                                localC[rowOffC2 + j] = c2;
                                localC[rowOffC3 + j] = c3;
                            }
                        }

                        for (; i < mEnd; i++)
                        {
                            int rowOffA = i * sA0;
                            int rowOffC = i * sC0;

                            int j = n0;
                            for (; j <= nEnd - vecSize; j += vecSize)
                            {
                                var vC = Unsafe.ReadUnaligned<Vector<float>>(localC + rowOffC + j);
                                for (int p = k0; p < kEnd; p++)
                                {
                                    float aVal = localA[rowOffA + p] * alpha;
                                    var vB = Unsafe.ReadUnaligned<Vector<float>>(localB + p * sB0 + j);
                                    vC += new Vector<float>(aVal) * vB;
                                }
                                Unsafe.WriteUnaligned(localC + rowOffC + j, vC);
                            }

                            for (; j < nEnd; j++)
                            {
                                float cVal = localC[rowOffC + j];
                                for (int p = k0; p < kEnd; p++)
                                {
                                    cVal += localA[rowOffA + p] * alpha * localB[p * sB0 + j];
                                }
                                localC[rowOffC + j] = cVal;
                            }
                        }
                    }
                }
            };

            if (runParallel)
            {
                int numMBlocks = (m + blockM - 1) / blockM;
                Parallel.For(0, numMBlocks, blockIdx =>
                {
                    processMBlock(blockIdx * blockM);
                });
            }
            else
            {
                for (int m0 = 0; m0 < m; m0 += blockM)
                {
                    processMBlock(m0);
                }
            }
        }

        private static Tensor<float> BatchedMatMul(Tensor<float> a, Tensor<float> b)
        {
            // Extract batch dimensions and matrix dimensions
            int aRank = a.Rank;
            int bRank = b.Rank;

            int m = a.Shape[aRank - 2];
            int kA = a.Shape[aRank - 1];
            int kB = b.Shape[bRank - 2];
            int n = b.Shape[bRank - 1];

            if (kA != kB)
            {
                throw new InvalidOperationException($"Batched matrix dimensions mismatch: A inner is {kA}, B inner is {kB}.");
            }

            // Extract batch shapes
            var aBatchDims = new int[aRank - 2];
            for (int i = 0; i < aRank - 2; i++) aBatchDims[i] = a.Shape[i];

            var bBatchDims = new int[bRank - 2];
            for (int i = 0; i < bRank - 2; i++) bBatchDims[i] = b.Shape[i];

            var aBatchShape = new TensorShape(aBatchDims);
            var bBatchShape = new TensorShape(bBatchDims);

            if (!aBatchShape.IsCompatibleForBroadcasting(bBatchShape, out var outBatchShape))
            {
                throw new InvalidOperationException($"Batch dimensions {aBatchShape} and {bBatchShape} cannot be broadcast.");
            }

            var outDims = new int[outBatchShape.Rank + 2];
            for (int i = 0; i < outBatchShape.Rank; i++) outDims[i] = outBatchShape[i];
            outDims[outDims.Length - 2] = m;
            outDims[outDims.Length - 1] = n;

            var result = new Tensor<float>(new TensorShape(outDims));
            int batchCount = outBatchShape.TotalElements;

            // Broadcast to matching batch shapes without materializing non-contiguous buffers
            var aBroadcast = a.BroadcastTo(new TensorShape(CombineBatchAndMatrix(outBatchShape, m, kA)));
            var bBroadcast = b.BroadcastTo(new TensorShape(CombineBatchAndMatrix(outBatchShape, kB, n)));

            var matShapeA = new TensorShape(m, kA);
            var matStridesA = new[] { aBroadcast.Strides[aBroadcast.Rank - 2], aBroadcast.Strides[aBroadcast.Rank - 1] };

            var matShapeB = new TensorShape(kB, n);
            var matStridesB = new[] { bBroadcast.Strides[bBroadcast.Rank - 2], bBroadcast.Strides[bBroadcast.Rank - 1] };

            var matShapeC = new TensorShape(m, n);
            var matStridesC = new[] { result.Strides[result.Rank - 2], result.Strides[result.Rank - 1] };

            int batchRank = outBatchShape.Rank;

            void ComputeBatchSlice(int bIdx)
            {
                int rem = bIdx;
                int aSliceOffset = aBroadcast.Offset;
                int bSliceOffset = bBroadcast.Offset;
                int cSliceOffset = result.Offset;

                for (int dim = batchRank - 1; dim >= 0; dim--)
                {
                    int dimSize = outBatchShape[dim];
                    int coord = rem % dimSize;
                    rem /= dimSize;
                    aSliceOffset += coord * aBroadcast.Strides[dim];
                    bSliceOffset += coord * bBroadcast.Strides[dim];
                    cSliceOffset += coord * result.Strides[dim];
                }

                var aSlice = new Tensor<float>(aBroadcast.Storage, aSliceOffset, matShapeA, matStridesA);
                var bSlice = new Tensor<float>(bBroadcast.Storage, bSliceOffset, matShapeB, matStridesB);
                var cSlice = new Tensor<float>(result.Storage, cSliceOffset, matShapeC, matStridesC);

                Gemm(aSlice, bSlice, cSlice, parallel: batchCount <= 1);
            }

            if (batchCount > 1)
            {
                System.Threading.Tasks.Parallel.For(0, batchCount, ComputeBatchSlice);
            }
            else
            {
                ComputeBatchSlice(0);
            }

            return result;
        }

        private static int[] CombineBatchAndMatrix(TensorShape batchShape, int r, int c)
        {
            var res = new int[batchShape.Rank + 2];
            for (int i = 0; i < batchShape.Rank; i++) res[i] = batchShape[i];
            res[res.Length - 2] = r;
            res[res.Length - 1] = c;
            return res;
        }

        /// <summary>
        /// Computes the dot product of two 1D vectors: sum(A[i] * B[i]).
        /// </summary>
        public static float Dot(Tensor<float> a, Tensor<float> b)
        {
            if (a.Rank != 1 || b.Rank != 1)
            {
                throw new ArgumentException($"Dot product requires 1D vectors, got ranks {a.Rank} and {b.Rank}.");
            }

            if (a.Length != b.Length)
            {
                throw new ArgumentException($"Vectors must have equal length: {a.Length} != {b.Length}.");
            }

            float dot = 0f;
            if (a.IsContiguous && b.IsContiguous)
            {
                var bufA = a.Buffer;
                var bufB = b.Buffer;
                int offA = a.Offset;
                int offB = b.Offset;
                int length = a.Length;

                int vecSize = Vector<float>.Count;
                var acc = Vector<float>.Zero;
                int i = 0;
                for (; i <= length - vecSize; i += vecSize)
                {
                    var va = new Vector<float>(bufA, offA + i);
                    var vb = new Vector<float>(bufB, offB + i);
                    acc += va * vb;
                }
                for (int v = 0; v < vecSize; v++) dot += acc[v];
                for (; i < length; i++) dot += bufA[offA + i] * bufB[offB + i];
                return dot;
            }

            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
            }
            return dot;
        }

        /// <summary>
        /// Computes the Euclidean / Frobenius norm of a tensor: sqrt(sum(x^2)).
        /// </summary>
        public static float Norm(Tensor<float> a)
        {
            float sumSq = 0f;
            if (a.IsContiguous)
            {
                var buf = a.Buffer;
                int off = a.Offset;
                int length = a.Length;

                int vecSize = Vector<float>.Count;
                var acc = Vector<float>.Zero;
                int i = 0;
                for (; i <= length - vecSize; i += vecSize)
                {
                    var v = new Vector<float>(buf, off + i);
                    acc += v * v;
                }
                for (int v = 0; v < vecSize; v++) sumSq += acc[v];
                for (; i < length; i++) sumSq += buf[off + i] * buf[off + i];
                return (float)Math.Sqrt(sumSq);
            }

            a.ForEachElement(v => sumSq += v * v);
            return (float)Math.Sqrt(sumSq);
        }
    }
}
