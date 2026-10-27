using System;
using System.Buffers;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Represents a rented tensor backed by an ArrayPool buffer.
    /// Disposing this instance returns the underlying buffer to the pool.
    /// </summary>
    public sealed class RentedTensor<T> : IDisposable where T : unmanaged, IEquatable<T>
    {
        private T[]? _rentedArray;
        private readonly Tensor<T> _tensor;
        private int _disposed;

        /// <summary>
        /// Gets the underlying tensor view over the rented buffer.
        /// </summary>
        public Tensor<T> Tensor => _tensor;

        /// <summary>
        /// Gets the shape of the rented tensor.
        /// </summary>
        public TensorShape Shape => _tensor.Shape;

        /// <summary>
        /// Gets the total number of elements.
        /// </summary>
        public int Length => _tensor.Length;

        /// <summary>
        /// Gets the rank (number of dimensions) of the tensor.
        /// </summary>
        public int Rank => _tensor.Rank;

        /// <summary>
        /// Gets or sets a reference to an element in a 1D tensor (or scalar if index is 0).
        /// </summary>
        public ref T this[int i] => ref _tensor[i];

        /// <summary>
        /// Gets or sets a reference to an element in a 2D tensor (row, col).
        /// </summary>
        public ref T this[int r, int c] => ref _tensor[r, c];

        /// <summary>
        /// Gets or sets a reference to an element in a 3D tensor (d0, d1, d2).
        /// </summary>
        public ref T this[int d0, int d1, int d2] => ref _tensor[d0, d1, d2];

        /// <summary>
        /// Gets or sets a reference to an element in a 4D tensor (d0, d1, d2, d3).
        /// </summary>
        public ref T this[int d0, int d1, int d2, int d3] => ref _tensor[d0, d1, d2, d3];

        /// <summary>
        /// Gets or sets an element in an N-D tensor.
        /// </summary>
        public T this[params int[] indices]
        {
            get => _tensor[indices];
            set => _tensor[indices] = value;
        }

        /// <summary>
        /// Gets a Span over the rented tensor's contiguous memory buffer.
        /// </summary>
        public Span<T> AsSpan() => _tensor.AsSpan();

        /// <summary>
        /// Gets a ReadOnlySpan over the rented tensor's contiguous memory buffer.
        /// </summary>
        public ReadOnlySpan<T> AsReadOnlySpan() => _tensor.AsReadOnlySpan();

        /// <summary>
        /// Gets whether the rented tensor is contiguous in memory.
        /// </summary>
        public bool IsContiguous => _tensor.IsContiguous;

        internal RentedTensor(T[] rentedArray, TensorShape shape)
        {
            _rentedArray = rentedArray;
            int[] strides = TensorStrides.ComputeContiguousStrides(shape);
            _tensor = ZeroTensor.Core.Tensor.CreateView(rentedArray, 0, shape, strides);
        }

        /// <summary>
        /// Implicitly converts a RentedTensor to its underlying Tensor view.
        /// </summary>
        public static implicit operator Tensor<T>(RentedTensor<T> rented)
        {
            if (rented == null) throw new ArgumentNullException(nameof(rented));
            return rented._tensor;
        }

        /// <summary>
        /// Returns the rented buffer to the shared ArrayPool.
        /// </summary>
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                var arr = _rentedArray;
                if (arr != null)
                {
                    _rentedArray = null;
                    ArrayPool<T>.Shared.Return(arr);
                }
            }
        }
    }

    /// <summary>
    /// High-performance thread-safe tensor memory rental engine using ArrayPool.
    /// Eliminates GC allocations in repetitive compute and inference loops.
    /// </summary>
    public static class TensorPool
    {
        /// <summary>
        /// Rents a tensor with the specified dimensions from the shared ArrayPool.
        /// </summary>
        public static RentedTensor<T> Rent<T>(params int[] shape) where T : unmanaged, IEquatable<T>
        {
            var tensorShape = new TensorShape(shape);
            var buffer = ArrayPool<T>.Shared.Rent(tensorShape.TotalElements);
            return new RentedTensor<T>(buffer, tensorShape);
        }

        /// <summary>
        /// Rents a tensor with the specified TensorShape from the shared ArrayPool.
        /// </summary>
        public static RentedTensor<T> Rent<T>(TensorShape shape) where T : unmanaged, IEquatable<T>
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            var buffer = ArrayPool<T>.Shared.Rent(shape.TotalElements);
            return new RentedTensor<T>(buffer, shape);
        }
    }

    public static partial class Tensor
    {
        /// <summary>
        /// Rents a tensor with the specified dimensions from the shared pool.
        /// Must be disposed to return buffer to pool.
        /// </summary>
        public static RentedTensor<T> Rent<T>(params int[] shape) where T : unmanaged, IEquatable<T> =>
            TensorPool.Rent<T>(shape);
    }
}
