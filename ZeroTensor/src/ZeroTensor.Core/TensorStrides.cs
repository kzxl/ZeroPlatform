using System;
using System.Runtime.CompilerServices;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Utility methods for computing, validating, and applying multidimensional tensor strides.
    /// </summary>
    public static class TensorStrides
    {
        /// <summary>
        /// Computes standard C-contiguous (row-major) strides for a given shape.
        /// </summary>
        public static int[] ComputeContiguousStrides(TensorShape shape)
        {
            if (shape == null)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            int rank = shape.Rank;
            if (rank == 0)
            {
                return Array.Empty<int>();
            }

            var strides = new int[rank];
            int currentStride = 1;

            for (int i = rank - 1; i >= 0; i--)
            {
                strides[i] = currentStride;
                currentStride *= Math.Max(1, shape[i]);
            }

            return strides;
        }

        /// <summary>
        /// Validates whether the given strides represent a contiguous row-major memory block.
        /// </summary>
        public static bool IsContiguous(TensorShape shape, int[] strides)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            if (strides == null) throw new ArgumentNullException(nameof(strides));

            int rank = shape.Rank;
            if (rank == 0 || shape.TotalElements <= 1)
            {
                return true;
            }

            if (strides.Length != rank)
            {
                return false;
            }

            int expectedStride = 1;
            for (int i = rank - 1; i >= 0; i--)
            {
                if (shape[i] > 1 && strides[i] != expectedStride)
                {
                    return false;
                }

                expectedStride *= Math.Max(1, shape[i]);
            }

            return true;
        }

        /// <summary>
        /// Computes the strides for a broadcasted tensor shape.
        /// Dimensions that are expanded from size 1 have their stride set to 0.
        /// </summary>
        public static int[] ComputeBroadcastStrides(TensorShape originalShape, int[] originalStrides, TensorShape targetShape)
        {
            if (originalShape == null) throw new ArgumentNullException(nameof(originalShape));
            if (originalStrides == null) throw new ArgumentNullException(nameof(originalStrides));
            if (targetShape == null) throw new ArgumentNullException(nameof(targetShape));

            int targetRank = targetShape.Rank;
            int origRank = originalShape.Rank;
            var resultStrides = new int[targetRank];

            int diff = targetRank - origRank;

            for (int i = 0; i < targetRank; i++)
            {
                if (i < diff)
                {
                    // Prepended broadcast dimension
                    resultStrides[i] = 0;
                }
                else
                {
                    int origDimIndex = i - diff;
                    int origDim = originalShape[origDimIndex];
                    int targetDim = targetShape[i];

                    if (origDim == targetDim)
                    {
                        resultStrides[i] = originalStrides[origDimIndex];
                    }
                    else if (origDim == 1)
                    {
                        // Expanded dimension: step size is 0
                        resultStrides[i] = 0;
                    }
                    else
                    {
                        throw new InvalidOperationException($"Cannot broadcast shape {originalShape} to {targetShape}.");
                    }
                }
            }

            return resultStrides;
        }

        /// <summary>
        /// Computes flat 1D memory offset from multidimensional coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ComputeFlatIndex(int[] strides, int offset, int[] indices)
        {
            int flat = offset;
            for (int i = 0; i < indices.Length; i++)
            {
                flat += indices[i] * strides[i];
            }
            return flat;
        }

        /// <summary>
        /// Computes flat 1D memory offset for 1D coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index1D(int stride0, int offset, int i0) => offset + i0 * stride0;

        /// <summary>
        /// Computes flat 1D memory offset for 2D coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index2D(int stride0, int stride1, int offset, int i0, int i1) =>
            offset + i0 * stride0 + i1 * stride1;

        /// <summary>
        /// Computes flat 1D memory offset for 3D coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index3D(int stride0, int stride1, int stride2, int offset, int i0, int i1, int i2) =>
            offset + i0 * stride0 + i1 * stride1 + i2 * stride2;

        /// <summary>
        /// Computes flat 1D memory offset for 4D coordinates.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Index4D(int stride0, int stride1, int stride2, int stride3, int offset, int i0, int i1, int i2, int i3) =>
            offset + i0 * stride0 + i1 * stride1 + i2 * stride2 + i3 * stride3;
    }
}
