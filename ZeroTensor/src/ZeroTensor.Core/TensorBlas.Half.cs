using System;
using System.Threading.Tasks;

namespace ZeroTensor.Core
{
    public static partial class TensorBlas
    {
        /// <summary>
        /// Computes the matrix multiplication of two half-precision (FP16) tensors: C = A @ B.
        /// Accumulates in single-precision (FP32) to prevent numerical underflow/overflow, matching industry-standard mixed precision.
        /// </summary>
        public static Tensor<Half> MatMul(Tensor<Half> a, Tensor<Half> b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));

            if (a.Rank == 1 && b.Rank == 2)
            {
                var a2D = a.Unsqueeze(0);
                var res2D = MatMul2D(a2D, b);
                return res2D.Squeeze(0);
            }

            if (a.Rank == 2 && b.Rank == 1)
            {
                var b2D = b.Unsqueeze(1);
                var res2D = MatMul2D(a, b2D);
                return res2D.Squeeze(1);
            }

            if (a.Rank == 2 && b.Rank == 2)
            {
                return MatMul2D(a, b);
            }

            if (a.Rank >= 2 && b.Rank >= 2)
            {
                return BatchedMatMul(a, b);
            }

            throw new InvalidOperationException($"Cannot perform matrix multiplication on tensors with ranks {a.Rank} and {b.Rank}.");
        }

        /// <summary>
        /// Cache-tiled General Matrix Multiplication for half-precision: C = A (M x K) @ B (K x N).
        /// Accumulates in FP32 registers for numerical stability.
        /// </summary>
        public static Tensor<Half> MatMul2D(Tensor<Half> a, Tensor<Half> b)
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

            int k = kA;
            var c = new Tensor<Half>(m, n);

            var aContig = a.IsContiguous ? a : a.ToContiguous();
            var bContig = b.IsContiguous ? b : b.ToContiguous();

            int offA = aContig.Offset;
            int offB = bContig.Offset;
            int offC = c.Offset;

            int aStride0 = aContig.Strides[0], aStride1 = aContig.Strides[1];
            int bStride0 = bContig.Strides[0], bStride1 = bContig.Strides[1];
            int cStride0 = c.Strides[0], cStride1 = c.Strides[1];

            // Cache blocking parameters
            const int bm = 32;
            const int bn = 32;
            const int bk = 32;

            int numBlocksM = (m + bm - 1) / bm;

            unsafe
            {
                fixed (Half* pA = &aContig.Storage.GetPinnableReference(offA))
                fixed (Half* pB = &bContig.Storage.GetPinnableReference(offB))
                fixed (Half* pC = &c.Storage.GetPinnableReference(offC))
                {
                    IntPtr ptrA = (IntPtr)pA;
                    IntPtr ptrB = (IntPtr)pB;
                    IntPtr ptrC = (IntPtr)pC;

                    Action<int> processMBlock = (m0) =>
                    {
                        Half* localA = (Half*)ptrA;
                        Half* localB = (Half*)ptrB;
                        Half* localC = (Half*)ptrC;

                        int iEnd = Math.Min(m0 + bm, m);

                        for (int lStart = 0; lStart < k; lStart += bk)
                        {
                            int lEnd = Math.Min(lStart + bk, k);

                            for (int jStart = 0; jStart < n; jStart += bn)
                            {
                                int jEnd = Math.Min(jStart + bn, n);

                                for (int i = m0; i < iEnd; i++)
                                {
                                    int aRowBase = i * aStride0;
                                    int cRowBase = i * cStride0;

                                    for (int l = lStart; l < lEnd; l++)
                                    {
                                        float aVal = (float)localA[aRowBase + l * aStride1];
                                        if (aVal == 0f) continue;

                                        int bRowBase = l * bStride0;

                                        for (int j = jStart; j < jEnd; j++)
                                        {
                                            int cIdx = cRowBase + j * cStride1;
                                            float current = (float)localC[cIdx];
                                            localC[cIdx] = (Half)(current + aVal * (float)localB[bRowBase + j * bStride1]);
                                        }
                                    }
                                }
                            }
                        }
                    };

                    if (m * n >= 4096)
                    {
                        Parallel.For(0, numBlocksM, blockMIdx =>
                        {
                            processMBlock(blockMIdx * bm);
                        });
                    }
                    else
                    {
                        for (int m0 = 0; m0 < m; m0 += bm)
                        {
                            processMBlock(m0);
                        }
                    }
                }
            }

            return c;
        }

        private static Tensor<Half> BatchedMatMul(Tensor<Half> a, Tensor<Half> b)
        {
            int rank = Math.Max(a.Rank, b.Rank);
            var aBatchDims = new int[a.Rank - 2];
            for (int i = 0; i < a.Rank - 2; i++) aBatchDims[i] = a.Shape[i];

            var bBatchDims = new int[b.Rank - 2];
            for (int i = 0; i < b.Rank - 2; i++) bBatchDims[i] = b.Shape[i];

            var commonBatchShape = TensorShape.Broadcast(new TensorShape(aBatchDims), new TensorShape(bBatchDims));

            int m = a.Shape[a.Rank - 2];
            int kA = a.Shape[a.Rank - 1];
            int kB = b.Shape[b.Rank - 2];
            int n = b.Shape[b.Rank - 1];

            if (kA != kB)
            {
                throw new InvalidOperationException($"Inner matrix dimensions must agree: {kA} != {kB}.");
            }

            var outDims = new int[commonBatchShape.Rank + 2];
            for (int i = 0; i < commonBatchShape.Rank; i++) outDims[i] = commonBatchShape[i];
            outDims[outDims.Length - 2] = m;
            outDims[outDims.Length - 1] = n;

            var result = new Tensor<Half>(outDims);

            commonBatchShape.ForEachCoordinate(batchCoords =>
            {
                var aSlice = SliceBatch2D(a, batchCoords, m, kA);
                var bSlice = SliceBatch2D(b, batchCoords, kB, n);

                var c2D = MatMul2D(aSlice, bSlice);

                var outSliceCoords = new int[outDims.Length];
                for (int i = 0; i < batchCoords.Length; i++) outSliceCoords[i] = batchCoords[i];

                for (int i = 0; i < m; i++)
                {
                    outSliceCoords[outDims.Length - 2] = i;
                    for (int j = 0; j < n; j++)
                    {
                        outSliceCoords[outDims.Length - 1] = j;
                        result[outSliceCoords] = c2D[i, j];
                    }
                }
            });

            return result;
        }

        private static Tensor<Half> SliceBatch2D(Tensor<Half> t, int[] batchCoords, int rows, int cols)
        {
            var tBatchCoords = new int[t.Rank - 2];
            int batchOffset = commonBatchOffset(batchCoords, tBatchCoords, t.Rank - 2);

            int baseOffset = t.Offset;
            for (int i = 0; i < t.Rank - 2; i++)
            {
                int coord = tBatchCoords[i];
                baseOffset += coord * t.Strides[i];
            }

            int[] subStrides = new int[] { t.Strides[t.Rank - 2], t.Strides[t.Rank - 1] };
            return new Tensor<Half>(t.Storage, baseOffset, new TensorShape(rows, cols), subStrides);
        }

        private static int commonBatchOffset(int[] commonCoords, int[] targetCoords, int targetRank)
        {
            int diff = commonCoords.Length - targetRank;
            for (int i = 0; i < targetRank; i++)
            {
                targetCoords[i] = commonCoords[i + diff];
            }
            return 0;
        }
    }
}
