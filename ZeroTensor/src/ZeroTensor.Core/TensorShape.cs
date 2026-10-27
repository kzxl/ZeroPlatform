using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Represents the immutable multidimensional shape of a tensor.
    /// Supports arbitrary ranks, broadcasting compatibility checks, and view dimension transformations.
    /// </summary>
    public sealed class TensorShape : IEquatable<TensorShape>
    {
        private readonly int[] _dimensions;
        private readonly int _totalElements;

        /// <summary>
        /// Gets the dimensions array as a read-only list.
        /// </summary>
        public IReadOnlyList<int> Dimensions => _dimensions;

        /// <summary>
        /// Gets the rank (number of axes/dimensions) of the tensor.
        /// </summary>
        public int Rank => _dimensions.Length;

        /// <summary>
        /// Gets the total number of scalar elements in this shape.
        /// </summary>
        public int TotalElements => _totalElements;

        /// <summary>
        /// Gets the size of the specified dimension index.
        /// </summary>
        public int this[int index]
        {
            get
            {
                if (index < 0)
                {
                    index += _dimensions.Length;
                }

                if (index < 0 || index >= _dimensions.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index), $"Axis {index} is out of bounds for rank {Rank}.");
                }

                return _dimensions[index];
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TensorShape"/> class with specified dimension sizes.
        /// </summary>
        public TensorShape(params int[] dimensions)
        {
            if (dimensions == null)
            {
                throw new ArgumentNullException(nameof(dimensions));
            }

            _dimensions = new int[dimensions.Length];
            long count = dimensions.Length == 0 ? 1 : 1;

            for (int i = 0; i < dimensions.Length; i++)
            {
                if (dimensions[i] < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(dimensions), $"Dimension sizes must be non-negative: dim[{i}] = {dimensions[i]}.");
                }

                _dimensions[i] = dimensions[i];
                count *= dimensions[i];
            }

            if (count > int.MaxValue)
            {
                throw new OverflowException($"Total tensor element count {count} exceeds Int32.MaxValue.");
            }

            _totalElements = (int)count;
        }

        /// <summary>
        /// Initializes a scalar shape (Rank 0, 1 element).
        /// </summary>
        public static TensorShape Scalar { get; } = new TensorShape();

        /// <summary>
        /// Creates a 1D vector shape.
        /// </summary>
        public static TensorShape Vector(int length) => new TensorShape(length);

        /// <summary>
        /// Creates a 2D matrix shape (rows x cols).
        /// </summary>
        public static TensorShape Matrix(int rows, int cols) => new TensorShape(rows, cols);

        /// <summary>
        /// Creates a 3D volume shape (depth x rows x cols).
        /// </summary>
        public static TensorShape Volume(int depth, int rows, int cols) => new TensorShape(depth, rows, cols);

        /// <summary>
        /// Copies the dimensions into an array.
        /// </summary>
        public int[] ToArray()
        {
            var result = new int[_dimensions.Length];
            Array.Copy(_dimensions, result, _dimensions.Length);
            return result;
        }

        /// <summary>
        /// Determines if two tensor shapes can be broadcast together according to standard NumPy broadcasting rules.
        /// If compatible, outputs the resulting common broadcast shape.
        /// </summary>
        public bool IsCompatibleForBroadcasting(TensorShape other, out TensorShape broadcastShape)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            int maxRank = Math.Max(Rank, other.Rank);
            int[] resultDims = new int[maxRank];

            for (int i = 0; i < maxRank; i++)
            {
                int dimA = i < Rank ? _dimensions[Rank - 1 - i] : 1;
                int dimB = i < other.Rank ? other._dimensions[other.Rank - 1 - i] : 1;

                if (dimA == dimB)
                {
                    resultDims[maxRank - 1 - i] = dimA;
                }
                else if (dimA == 1)
                {
                    resultDims[maxRank - 1 - i] = dimB;
                }
                else if (dimB == 1)
                {
                    resultDims[maxRank - 1 - i] = dimA;
                }
                else
                {
                    broadcastShape = Scalar;
                    return false;
                }
            }

            broadcastShape = new TensorShape(resultDims);
            return true;
        }

        /// <summary>
        /// Returns the broadcast shape resulting from broadcasting shape A and shape B.
        /// Throws <see cref="InvalidOperationException"/> if shapes cannot be broadcast.
        /// </summary>
        public static TensorShape Broadcast(TensorShape a, TensorShape b)
        {
            if (!a.IsCompatibleForBroadcasting(b, out var result))
            {
                throw new InvalidOperationException($"Operands could not be broadcast together with shapes {a} and {b}.");
            }

            return result;
        }

        /// <summary>
        /// Removes axes of length 1. If an axis is specified, only removes that axis if its size is 1.
        /// </summary>
        public TensorShape Squeeze(int? axis = null)
        {
            if (axis.HasValue)
            {
                int targetAxis = axis.Value < 0 ? axis.Value + Rank : axis.Value;
                if (targetAxis < 0 || targetAxis >= Rank)
                {
                    throw new ArgumentOutOfRangeException(nameof(axis), $"Axis {axis} is out of bounds for shape {this}.");
                }

                if (_dimensions[targetAxis] != 1)
                {
                    return this;
                }

                var newDims = new List<int>(Rank - 1);
                for (int i = 0; i < Rank; i++)
                {
                    if (i != targetAxis)
                    {
                        newDims.Add(_dimensions[i]);
                    }
                }

                return new TensorShape(newDims.ToArray());
            }

            var squeezed = _dimensions.Where(d => d != 1).ToArray();
            return squeezed.Length == 0 ? Scalar : new TensorShape(squeezed);
        }

        /// <summary>
        /// Inserts a new axis of size 1 at the specified axis position.
        /// </summary>
        public TensorShape Unsqueeze(int axis)
        {
            int targetAxis = axis < 0 ? axis + Rank + 1 : axis;
            if (targetAxis < 0 || targetAxis > Rank)
            {
                throw new ArgumentOutOfRangeException(nameof(axis), $"Axis {axis} is out of bounds for unsqueezing shape {this}.");
            }

            var newDims = new int[Rank + 1];
            for (int i = 0, j = 0; i <= Rank; i++)
            {
                if (i == targetAxis)
                {
                    newDims[i] = 1;
                }
                else
                {
                    newDims[i] = _dimensions[j++];
                }
            }

            return new TensorShape(newDims);
        }

        /// <summary>
        /// Reorders dimensions according to the specified permutation of axis indices.
        /// </summary>
        public TensorShape Permute(params int[] axes)
        {
            if (axes == null || axes.Length != Rank)
            {
                throw new ArgumentException($"Permutation must specify exactly {Rank} axes.", nameof(axes));
            }

            var seen = new bool[Rank];
            var newDims = new int[Rank];

            for (int i = 0; i < Rank; i++)
            {
                int ax = axes[i] < 0 ? axes[i] + Rank : axes[i];
                if (ax < 0 || ax >= Rank || seen[ax])
                {
                    throw new ArgumentException($"Invalid permutation axis {axes[i]} at position {i}.", nameof(axes));
                }

                seen[ax] = true;
                newDims[i] = _dimensions[ax];
            }

            return new TensorShape(newDims);
        }

        /// <inheritdoc />
        public bool Equals(TensorShape? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Rank != other.Rank || TotalElements != other.TotalElements) return false;

            for (int i = 0; i < Rank; i++)
            {
                if (_dimensions[i] != other._dimensions[i]) return false;
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is TensorShape other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = 17;
            for (int i = 0; i < _dimensions.Length; i++)
            {
                hash = hash * 31 + _dimensions[i];
            }

            return hash;
        }

        /// <inheritdoc />
        public override string ToString() => $"({string.Join(", ", _dimensions)})";

        /// <summary>
        /// Executes an action for every multidimensional coordinate within this shape.
        /// </summary>
        public void ForEachCoordinate(Action<int[]> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (Rank == 0)
            {
                action(Array.Empty<int>());
                return;
            }

            int[] coords = new int[Rank];
            TraverseCoordinates(coords, 0, action);
        }

        private void TraverseCoordinates(int[] coords, int currentDim, Action<int[]> action)
        {
            if (currentDim == Rank)
            {
                action(coords);
                return;
            }

            for (int i = 0; i < _dimensions[currentDim]; i++)
            {
                coords[currentDim] = i;
                TraverseCoordinates(coords, currentDim + 1, action);
            }
        }

        public static bool operator ==(TensorShape? a, TensorShape? b) => a?.Equals(b) ?? b is null;
        public static bool operator !=(TensorShape? a, TensorShape? b) => !(a == b);
    }
}
