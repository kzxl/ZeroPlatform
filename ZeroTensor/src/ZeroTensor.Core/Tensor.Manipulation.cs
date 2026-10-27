using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Specifies the padding mode used by tensor padding operations.
    /// </summary>
    public enum PadMode
    {
        /// <summary>
        /// Pads with a constant value.
        /// </summary>
        Constant = 0,

        /// <summary>
        /// Pads with the edge values replicated.
        /// </summary>
        Edge = 1,

        /// <summary>
        /// Pads with the reflection of the vector mirrored on the first and last values.
        /// </summary>
        Reflect = 2,

        /// <summary>
        /// Pads with the reflection of the vector mirrored along the edge of the array.
        /// </summary>
        Symmetric = 3
    }

    public static partial class Tensor
    {
        /// <summary>
        /// Concatenates a sequence of tensors along an existing axis.
        /// All dimensions except for the specified axis must match exactly.
        /// </summary>
        public static Tensor<T> Concat<T>(IReadOnlyList<Tensor<T>> tensors, int axis = 0) where T : unmanaged, IEquatable<T>
        {
            if (tensors == null || tensors.Count == 0)
            {
                throw new ArgumentException("Must provide at least one tensor to concatenate.", nameof(tensors));
            }

            if (tensors.Count == 1)
            {
                return tensors[0].Clone();
            }

            var first = tensors[0];
            int rank = first.Rank;
            int ax = axis < 0 ? axis + rank : axis;

            if (ax < 0 || ax >= rank)
            {
                throw new ArgumentOutOfRangeException(nameof(axis), $"Axis {axis} is out of bounds for rank {rank}.");
            }

            int totalConcatDim = 0;
            for (int i = 0; i < tensors.Count; i++)
            {
                var t = tensors[i];
                if (t.Rank != rank)
                {
                    throw new ArgumentException($"All tensors must have the same rank. Tensor at index {i} has rank {t.Rank}, expected {rank}.");
                }

                for (int d = 0; d < rank; d++)
                {
                    if (d != ax && t.Shape[d] != first.Shape[d])
                    {
                        throw new ArgumentException($"Dimension {d} mismatch at tensor index {i}: {t.Shape[d]} != {first.Shape[d]}.");
                    }
                }

                totalConcatDim += t.Shape[ax];
            }

            var newDims = first.Shape.ToArray();
            newDims[ax] = totalConcatDim;

            var result = new Tensor<T>(new TensorShape(newDims));
            int currentOffset = 0;

            for (int i = 0; i < tensors.Count; i++)
            {
                var t = tensors[i];
                int sliceLen = t.Shape[ax];
                if (sliceLen > 0)
                {
                    var destSlice = result.Slice(ax, currentOffset, sliceLen);
                    t.CopyTo(destSlice);
                    currentOffset += sliceLen;
                }
            }

            return result;
        }

        /// <summary>
        /// Concatenates a sequence of tensors along an existing axis (params overload).
        /// </summary>
        public static Tensor<T> Concat<T>(int axis, params Tensor<T>[] tensors) where T : unmanaged, IEquatable<T>
        {
            return Concat(tensors, axis);
        }

        /// <summary>
        /// Stacks a sequence of tensors along a new axis. All tensors must have identical shapes.
        /// </summary>
        public static Tensor<T> Stack<T>(IReadOnlyList<Tensor<T>> tensors, int axis = 0) where T : unmanaged, IEquatable<T>
        {
            if (tensors == null || tensors.Count == 0)
            {
                throw new ArgumentException("Must provide at least one tensor to stack.", nameof(tensors));
            }

            var first = tensors[0];
            int newRank = first.Rank + 1;
            int ax = axis < 0 ? axis + newRank : axis;

            if (ax < 0 || ax >= newRank)
            {
                throw new ArgumentOutOfRangeException(nameof(axis), $"Axis {axis} is out of bounds for new rank {newRank}.");
            }

            var unsqueezed = new List<Tensor<T>>(tensors.Count);
            for (int i = 0; i < tensors.Count; i++)
            {
                if (tensors[i].Shape != first.Shape)
                {
                    throw new ArgumentException($"Tensor at index {i} shape {tensors[i].Shape} does not match first tensor shape {first.Shape}.");
                }
                unsqueezed.Add(tensors[i].Unsqueeze(ax));
            }

            return Concat(unsqueezed, ax);
        }

        /// <summary>
        /// Splits a tensor into a specified number of equal parts along the specified axis.
        /// </summary>
        public static Tensor<T>[] Split<T>(Tensor<T> tensor, int parts, int axis = 0) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (parts <= 0) throw new ArgumentOutOfRangeException(nameof(parts), "Parts count must be positive.");

            int rank = tensor.Rank;
            int ax = axis < 0 ? axis + rank : axis;
            if (ax < 0 || ax >= rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int dimSize = tensor.Shape[ax];
            if (dimSize % parts != 0)
            {
                throw new ArgumentException($"Dimension {ax} size {dimSize} is not evenly divisible by {parts}.");
            }

            int partSize = dimSize / parts;
            var result = new Tensor<T>[parts];

            for (int i = 0; i < parts; i++)
            {
                result[i] = tensor.Slice(ax, i * partSize, partSize).Clone();
            }

            return result;
        }

        /// <summary>
        /// Splits a tensor into multiple tensors along the specified axis according to given section sizes.
        /// </summary>
        public static Tensor<T>[] Split<T>(Tensor<T> tensor, int[] splitSizes, int axis = 0) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (splitSizes == null || splitSizes.Length == 0) throw new ArgumentException("Split sizes cannot be null or empty.", nameof(splitSizes));

            int rank = tensor.Rank;
            int ax = axis < 0 ? axis + rank : axis;
            if (ax < 0 || ax >= rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int totalSize = splitSizes.Sum();
            if (totalSize != tensor.Shape[ax])
            {
                throw new ArgumentException($"Sum of split sizes ({totalSize}) does not equal dimension {ax} size ({tensor.Shape[ax]}).");
            }

            var result = new Tensor<T>[splitSizes.Length];
            int offset = 0;

            for (int i = 0; i < splitSizes.Length; i++)
            {
                result[i] = tensor.Slice(ax, offset, splitSizes[i]).Clone();
                offset += splitSizes[i];
            }

            return result;
        }

        /// <summary>
        /// Splits a tensor into chunks of the specified size along an axis.
        /// The last chunk may be smaller if the dimension size is not divisible by chunkSize.
        /// </summary>
        public static Tensor<T>[] Chunk<T>(Tensor<T> tensor, int chunkSize, int axis = 0) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (chunkSize <= 0) throw new ArgumentOutOfRangeException(nameof(chunkSize), "Chunk size must be positive.");

            int rank = tensor.Rank;
            int ax = axis < 0 ? axis + rank : axis;
            if (ax < 0 || ax >= rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int dimSize = tensor.Shape[ax];
            int numChunks = (dimSize + chunkSize - 1) / chunkSize;
            var result = new Tensor<T>[numChunks];

            for (int i = 0; i < numChunks; i++)
            {
                int start = i * chunkSize;
                int len = Math.Min(chunkSize, dimSize - start);
                result[i] = tensor.Slice(ax, start, len).Clone();
            }

            return result;
        }

        /// <summary>
        /// Pads a tensor with specified amounts before and after each dimension.
        /// </summary>
        public static Tensor<T> Pad<T>(
            Tensor<T> tensor,
            int[] padBefore,
            int[] padAfter,
            PadMode mode = PadMode.Constant,
            T constantValue = default) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (padBefore == null) throw new ArgumentNullException(nameof(padBefore));
            if (padAfter == null) throw new ArgumentNullException(nameof(padAfter));

            int rank = tensor.Rank;
            if (padBefore.Length != rank || padAfter.Length != rank)
            {
                throw new ArgumentException($"Pad arrays must match tensor rank {rank}.");
            }

            var newDims = new int[rank];
            for (int d = 0; d < rank; d++)
            {
                if (padBefore[d] < 0 || padAfter[d] < 0)
                {
                    throw new ArgumentOutOfRangeException($"Pad values must be non-negative at dim {d}.");
                }
                newDims[d] = tensor.Shape[d] + padBefore[d] + padAfter[d];
            }

            var result = new Tensor<T>(new TensorShape(newDims));

            if (mode == PadMode.Constant)
            {
                result.Fill(constantValue);
                // Copy source tensor into inner target view
                var targetSlice = result;
                for (int d = 0; d < rank; d++)
                {
                    targetSlice = targetSlice.Slice(d, padBefore[d], tensor.Shape[d]);
                }
                tensor.CopyTo(targetSlice);
                return result;
            }

            // Edge, Reflect, and Symmetric padding coordinate mapping
            result.ForEachCoordinate(targetCoords =>
            {
                var srcCoords = new int[rank];
                for (int d = 0; d < rank; d++)
                {
                    int dimSize = tensor.Shape[d];
                    int y = targetCoords[d];
                    int before = padBefore[d];

                    if (y >= before && y < before + dimSize)
                    {
                        srcCoords[d] = y - before;
                    }
                    else if (y < before)
                    {
                        int diff = before - y;
                        if (mode == PadMode.Edge)
                        {
                            srcCoords[d] = 0;
                        }
                        else if (mode == PadMode.Reflect)
                        {
                            srcCoords[d] = Math.Min(dimSize - 1, diff);
                        }
                        else // Symmetric
                        {
                            srcCoords[d] = Math.Min(dimSize - 1, diff - 1);
                        }
                    }
                    else // y >= before + dimSize
                    {
                        int diff = y - (before + dimSize);
                        if (mode == PadMode.Edge)
                        {
                            srcCoords[d] = dimSize - 1;
                        }
                        else if (mode == PadMode.Reflect)
                        {
                            srcCoords[d] = Math.Max(0, dimSize - 2 - diff);
                        }
                        else // Symmetric
                        {
                            srcCoords[d] = Math.Max(0, dimSize - 1 - diff);
                        }
                    }
                }

                result[targetCoords] = tensor[srcCoords];
            });

            return result;
        }

        /// <summary>
        /// Constructs a new tensor by repeating tensor dimensions the number of times given by reps.
        /// </summary>
        public static Tensor<T> Tile<T>(Tensor<T> tensor, params int[] reps) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (reps == null || reps.Length == 0) throw new ArgumentException("Reps must be specified.", nameof(reps));

            int maxRank = Math.Max(tensor.Rank, reps.Length);
            var workTensor = tensor;
            while (workTensor.Rank < maxRank)
            {
                workTensor = workTensor.Unsqueeze(0);
            }

            var normReps = new int[maxRank];
            int diff = maxRank - reps.Length;
            for (int i = 0; i < maxRank; i++)
            {
                normReps[i] = i < diff ? 1 : reps[i - diff];
                if (normReps[i] <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(reps), "Repetition counts must be positive.");
                }
            }

            var newDims = new int[maxRank];
            for (int i = 0; i < maxRank; i++)
            {
                newDims[i] = workTensor.Shape[i] * normReps[i];
            }

            var result = new Tensor<T>(new TensorShape(newDims));
            var srcCoords = new int[maxRank];

            result.ForEachCoordinate(coords =>
            {
                for (int d = 0; d < maxRank; d++)
                {
                    srcCoords[d] = coords[d] % workTensor.Shape[d];
                }
                result[coords] = workTensor[srcCoords];
            });

            return result;
        }

        /// <summary>
        /// Repeats elements of a tensor along an axis.
        /// </summary>
        public static Tensor<T> Repeat<T>(Tensor<T> tensor, int repeats, int axis) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (repeats <= 0) throw new ArgumentOutOfRangeException(nameof(repeats), "Repeats must be positive.");

            int rank = tensor.Rank;
            int ax = axis < 0 ? axis + rank : axis;
            if (ax < 0 || ax >= rank) throw new ArgumentOutOfRangeException(nameof(axis));

            var newDims = tensor.Shape.ToArray();
            newDims[ax] = tensor.Shape[ax] * repeats;

            var result = new Tensor<T>(new TensorShape(newDims));
            var srcCoords = new int[rank];

            result.ForEachCoordinate(coords =>
            {
                for (int d = 0; d < rank; d++)
                {
                    srcCoords[d] = d == ax ? coords[d] / repeats : coords[d];
                }
                result[coords] = tensor[srcCoords];
            });

            return result;
        }

        /// <summary>
        /// Rolls tensor elements along the given axis circularly by the specified shift amount.
        /// </summary>
        public static Tensor<T> Roll<T>(Tensor<T> tensor, int shift, int axis = 0) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            int rank = tensor.Rank;
            int ax = axis < 0 ? axis + rank : axis;
            if (ax < 0 || ax >= rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int dim = tensor.Shape[ax];
            if (dim <= 1) return tensor.Clone();

            int effectiveShift = ((shift % dim) + dim) % dim;
            if (effectiveShift == 0) return tensor.Clone();

            var part1 = tensor.Slice(ax, dim - effectiveShift, effectiveShift);
            var part2 = tensor.Slice(ax, 0, dim - effectiveShift);

            return Concat(new[] { part1, part2 }, ax);
        }

        /// <summary>
        /// Reverses the order of elements in a tensor along the given axis.
        /// </summary>
        public static Tensor<T> Flip<T>(Tensor<T> tensor, int axis = 0) where T : unmanaged, IEquatable<T>
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            int rank = tensor.Rank;
            int ax = axis < 0 ? axis + rank : axis;
            if (ax < 0 || ax >= rank) throw new ArgumentOutOfRangeException(nameof(axis));

            int dim = tensor.Shape[ax];
            var result = new Tensor<T>(tensor.Shape);
            var srcCoords = new int[rank];

            result.ForEachCoordinate(coords =>
            {
                for (int d = 0; d < rank; d++)
                {
                    srcCoords[d] = d == ax ? dim - 1 - coords[d] : coords[d];
                }
                result[coords] = tensor[srcCoords];
            });

            return result;
        }
    }
}
