using System;

namespace ZeroTensor.Core
{
    public static partial class TensorOps
    {
        #region TopK

        /// <summary>
        /// Finds the values and indices of the k largest (or smallest) elements along a given axis.
        /// </summary>
        public static (Tensor<T> Values, Tensor<int> Indices) TopK<T>(
            Tensor<T> t,
            int k,
            int axis = -1,
            bool largest = true) where T : unmanaged, IComparable<T>, IEquatable<T>
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));
            int dimSize = t.Shape[ax];
            if (k <= 0 || k > dimSize)
            {
                throw new ArgumentOutOfRangeException(nameof(k), $"k must be in range [1, {dimSize}], got {k}.");
            }

            var outDims = t.Shape.ToArray();
            outDims[ax] = k;
            var outShape = new TensorShape(outDims);

            var values = new Tensor<T>(outShape);
            var indices = new Tensor<int>(outShape);

            // Shape of coordinates excluding the target axis
            var outerShapeDims = new int[t.Rank - 1];
            for (int i = 0, j = 0; i < t.Rank; i++)
            {
                if (i != ax) outerShapeDims[j++] = t.Shape[i];
            }
            var outerShape = new TensorShape(outerShapeDims);

            outerShape.ForEachCoordinate(outerCoords =>
            {
                var srcCoords = ExpandCoordsForAxis(outerCoords, ax, 0);
                var pairList = new (T Value, int Index)[dimSize];

                for (int i = 0; i < dimSize; i++)
                {
                    srcCoords[ax] = i;
                    pairList[i] = (t[srcCoords], i);
                }

                if (largest)
                {
                    Array.Sort(pairList, (x, y) => y.Value.CompareTo(x.Value));
                }
                else
                {
                    Array.Sort(pairList, (x, y) => x.Value.CompareTo(y.Value));
                }

                var dstCoords = ExpandCoordsForAxis(outerCoords, ax, 0);
                for (int i = 0; i < k; i++)
                {
                    dstCoords[ax] = i;
                    values[dstCoords] = pairList[i].Value;
                    indices[dstCoords] = pairList[i].Index;
                }
            });

            return (values, indices);
        }

        #endregion

        #region Gather

        /// <summary>
        /// Gathers values along an axis specified by an index tensor (equivalent to PyTorch torch.gather / ONNX GatherElements).
        /// Output shape matches indices shape.
        /// </summary>
        public static Tensor<T> Gather<T>(Tensor<T> input, int axis, Tensor<int> indices) where T : unmanaged, IEquatable<T>
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (input.Rank != indices.Rank)
            {
                throw new ArgumentException($"Input rank ({input.Rank}) must match indices rank ({indices.Rank}).");
            }

            int ax = axis < 0 ? axis + input.Rank : axis;
            if (ax < 0 || ax >= input.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int maxDim = input.Shape[ax];
            var output = new Tensor<T>(indices.Shape);

            indices.ForEachCoordinate(coords =>
            {
                int idx = indices[coords];
                if (idx < 0) idx += maxDim;
                if ((uint)idx >= (uint)maxDim)
                {
                    throw new ArgumentOutOfRangeException(nameof(indices), $"Index {idx} out of bounds for axis {ax} with size {maxDim}.");
                }

                var srcCoords = (int[])coords.Clone();
                srcCoords[ax] = idx;
                output[coords] = input[srcCoords];
            });

            return output;
        }

        #endregion

        #region CumSum

        /// <summary>
        /// Computes the cumulative sum of elements along a given axis (float).
        /// </summary>
        public static Tensor<float> CumSum(Tensor<float> t, int axis = 0)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int dimSize = t.Shape[ax];
            var result = new Tensor<float>(t.Shape);

            var outerDims = new int[t.Rank - 1];
            for (int i = 0, j = 0; i < t.Rank; i++)
            {
                if (i != ax) outerDims[j++] = t.Shape[i];
            }
            var outerShape = new TensorShape(outerDims);

            outerShape.ForEachCoordinate(outerCoords =>
            {
                var coords = ExpandCoordsForAxis(outerCoords, ax, 0);
                float sum = 0f;
                for (int i = 0; i < dimSize; i++)
                {
                    coords[ax] = i;
                    sum += t[coords];
                    result[coords] = sum;
                }
            });

            return result;
        }

        /// <summary>
        /// Computes the cumulative sum of elements along a given axis (double).
        /// </summary>
        public static Tensor<double> CumSum(Tensor<double> t, int axis = 0)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int dimSize = t.Shape[ax];
            var result = new Tensor<double>(t.Shape);

            var outerDims = new int[t.Rank - 1];
            for (int i = 0, j = 0; i < t.Rank; i++)
            {
                if (i != ax) outerDims[j++] = t.Shape[i];
            }
            var outerShape = new TensorShape(outerDims);

            outerShape.ForEachCoordinate(outerCoords =>
            {
                var coords = ExpandCoordsForAxis(outerCoords, ax, 0);
                double sum = 0.0;
                for (int i = 0; i < dimSize; i++)
                {
                    coords[ax] = i;
                    sum += t[coords];
                    result[coords] = sum;
                }
            });

            return result;
        }

        #endregion

        #region OneHot

        /// <summary>
        /// Generates a one-hot tensor from class indices.
        /// Appends an axis of size depth at the end of the tensor.
        /// </summary>
        public static Tensor<T> OneHot<T>(
            Tensor<int> indices,
            int depth,
            T onValue,
            T offValue) where T : unmanaged, IEquatable<T>
        {
            if (indices == null) throw new ArgumentNullException(nameof(indices));
            if (depth <= 0) throw new ArgumentException("Depth must be positive.", nameof(depth));

            var outDims = new int[indices.Rank + 1];
            for (int i = 0; i < indices.Rank; i++) outDims[i] = indices.Shape[i];
            outDims[indices.Rank] = depth;

            var result = new Tensor<T>(new TensorShape(outDims));
            result.Fill(offValue);

            indices.ForEachCoordinate(coords =>
            {
                int classIdx = indices[coords];
                if (classIdx >= 0 && classIdx < depth)
                {
                    var targetCoords = new int[coords.Length + 1];
                    for (int i = 0; i < coords.Length; i++) targetCoords[i] = coords[i];
                    targetCoords[coords.Length] = classIdx;
                    result[targetCoords] = onValue;
                }
            });

            return result;
        }

        /// <summary>
        /// Generates a float one-hot tensor with onValue=1.0f and offValue=0.0f.
        /// </summary>
        public static Tensor<float> OneHot(Tensor<int> indices, int depth) =>
            OneHot(indices, depth, 1.0f, 0.0f);

        #endregion

        #region Double ArgMax and ArgMin

        /// <summary>
        /// Returns the indices of the maximum values along an axis (double precision).
        /// </summary>
        public static Tensor<int> ArgMax(Tensor<double> t, int axis = -1)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            var outDims = GetReducedDimensions(t.Shape, ax, false);
            var result = new Tensor<int>(outDims);
            int reduceCount = t.Shape[ax];

            result.ForEachCoordinate(coords =>
            {
                var srcCoords = ExpandCoordsForAxis(coords, ax, 0);
                double maxVal = double.NegativeInfinity;
                int bestIdx = 0;

                for (int i = 0; i < reduceCount; i++)
                {
                    srcCoords[ax] = i;
                    double val = t[srcCoords];
                    if (val > maxVal)
                    {
                        maxVal = val;
                        bestIdx = i;
                    }
                }

                result[coords] = bestIdx;
            });

            return result;
        }

        /// <summary>
        /// Returns the indices of the minimum values along an axis (double precision).
        /// </summary>
        public static Tensor<int> ArgMin(Tensor<double> t, int axis = -1)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            int ax = axis < 0 ? axis + t.Rank : axis;
            if (ax < 0 || ax >= t.Rank) throw new ArgumentOutOfRangeException(nameof(axis));

            var outDims = GetReducedDimensions(t.Shape, ax, false);
            var result = new Tensor<int>(outDims);
            int reduceCount = t.Shape[ax];

            result.ForEachCoordinate(coords =>
            {
                var srcCoords = ExpandCoordsForAxis(coords, ax, 0);
                double minVal = double.PositiveInfinity;
                int bestIdx = 0;

                for (int i = 0; i < reduceCount; i++)
                {
                    srcCoords[ax] = i;
                    double val = t[srcCoords];
                    if (val < minVal)
                    {
                        minVal = val;
                        bestIdx = i;
                    }
                }

                result[coords] = bestIdx;
            });

            return result;
        }

        #endregion
    }

    public static partial class Tensor
    {
        /// <summary>
        /// Generates a one-hot tensor from class indices.
        /// </summary>
        public static Tensor<float> OneHot(Tensor<int> indices, int depth) =>
            TensorOps.OneHot(indices, depth);

        /// <summary>
        /// Generates a one-hot tensor from class indices with specified values.
        /// </summary>
        public static Tensor<T> OneHot<T>(Tensor<int> indices, int depth, T onValue, T offValue) where T : unmanaged, IEquatable<T> =>
            TensorOps.OneHot(indices, depth, onValue, offValue);
    }
}
