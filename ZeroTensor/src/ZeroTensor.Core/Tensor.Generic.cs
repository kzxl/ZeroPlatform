using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using ZeroTensor.Core.Storage;

namespace ZeroTensor.Core
{
    /// <summary>
    /// Represents an N-dimensional tensor backed by an abstract memory storage.
    /// Supports zero-copy slicing, transposing, reshaping, broadcasting views,
    /// and multiple storage backends (Managed array, Native unmanaged memory, Memory-Mapped file, GPU buffer).
    /// </summary>
    /// <typeparam name="T">Unmanaged scalar type (e.g. float, double, int, byte).</typeparam>
    public class Tensor<T> : IEquatable<Tensor<T>>, IDisposable where T : unmanaged, IEquatable<T>
    {
        private readonly ITensorStorage<T> _storage;
        private readonly int _offset;
        private readonly TensorShape _shape;
        private readonly int[] _strides;

        /// <summary>
        /// Gets the shape of the tensor.
        /// </summary>
        public TensorShape Shape => _shape;

        /// <summary>
        /// Gets the strides for each dimension.
        /// </summary>
        public IReadOnlyList<int> Strides => _strides;

        /// <summary>
        /// Gets the memory buffer offset for the first element.
        /// </summary>
        public int Offset => _offset;

        /// <summary>
        /// Gets the rank (number of dimensions) of the tensor.
        /// </summary>
        public int Rank => _shape.Rank;

        /// <summary>
        /// Gets the total number of elements.
        /// </summary>
        public int Length => _shape.TotalElements;

        /// <summary>
        /// Gets whether the underlying buffer memory is stored contiguously in row-major order without holes or non-standard strides.
        /// </summary>
        public bool IsContiguous => TensorStrides.IsContiguous(_shape, _strides);

        /// <summary>
        /// Gets the storage backend holding this tensor's memory.
        /// </summary>
        public ITensorStorage<T> Storage => _storage;

        /// <summary>
        /// Gets the device domain where this tensor resides.
        /// </summary>
        public DeviceType Device => _storage.Device;

        /// <summary>
        /// Internal accessor to the underlying flat buffer if backed by a managed array, or materializes one.
        /// </summary>
        internal T[] Buffer => _storage.TryGetArray(out _) ?? ToArray();

        /// <summary>
        /// Initializes a new contiguous tensor of the specified shape.
        /// </summary>
        public Tensor(TensorShape shape)
        {
            _shape = shape ?? throw new ArgumentNullException(nameof(shape));
            _storage = new ManagedArrayStorage<T>(_shape.TotalElements);
            _offset = 0;
            _strides = TensorStrides.ComputeContiguousStrides(_shape);
        }

        /// <summary>
        /// Initializes a new contiguous tensor with specified dimensions.
        /// </summary>
        public Tensor(params int[] dimensions) : this(new TensorShape(dimensions))
        {
        }

        /// <summary>
        /// Initializes a tensor backed by custom storage with specified offset, shape, and strides.
        /// </summary>
        public Tensor(ITensorStorage<T> storage, int offset, TensorShape shape, int[] strides)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _offset = offset;
            _shape = shape ?? throw new ArgumentNullException(nameof(shape));
            _strides = strides ?? throw new ArgumentNullException(nameof(strides));
        }

        /// <summary>
        /// Internal constructor for creating zero-copy views over existing buffer memory.
        /// </summary>
        internal Tensor(T[] buffer, int offset, TensorShape shape, int[] strides)
            : this(new ManagedArrayStorage<T>(buffer), offset, shape, strides)
        {
        }

        #region Indexers

        /// <summary>
        /// Gets or sets an element using multidimensional coordinates.
        /// </summary>
        public T this[params int[] indices]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ValidateIndices(indices);
                int flatIndex = TensorStrides.ComputeFlatIndex(_strides, _offset, indices);
                return _storage.GetPinnableReference(flatIndex);
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                ValidateIndices(indices);
                int flatIndex = TensorStrides.ComputeFlatIndex(_strides, _offset, indices);
                _storage.GetPinnableReference(flatIndex) = value;
            }
        }

        /// <summary>
        /// Gets the scalar value for a 0-rank or single-element tensor.
        /// </summary>
        public T Scalar => _storage.GetPinnableReference(_offset);

        /// <summary>
        /// Gets or sets a reference to an element in a 1D tensor (or scalar if index is 0).
        /// </summary>
        public ref T this[int i]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Rank == 0 && i == 0) return ref _storage.GetPinnableReference(_offset);
                if (Rank != 1) throw new InvalidOperationException($"Rank is {Rank}, expected 1.");
                if ((uint)i >= (uint)_shape[0]) throw new ArgumentOutOfRangeException(nameof(i));
                return ref _storage.GetPinnableReference(_offset + i * _strides[0]);
            }
        }

        /// <summary>
        /// Gets or sets a reference to an element in a 2D tensor (row, col).
        /// </summary>
        public ref T this[int r, int c]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Rank != 2) throw new InvalidOperationException($"Rank is {Rank}, expected 2.");
                if ((uint)r >= (uint)_shape[0]) throw new ArgumentOutOfRangeException(nameof(r));
                if ((uint)c >= (uint)_shape[1]) throw new ArgumentOutOfRangeException(nameof(c));
                return ref _storage.GetPinnableReference(_offset + r * _strides[0] + c * _strides[1]);
            }
        }

        /// <summary>
        /// Gets or sets a reference to an element in a 3D tensor (d0, d1, d2).
        /// </summary>
        public ref T this[int d0, int d1, int d2]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Rank != 3) throw new InvalidOperationException($"Rank is {Rank}, expected 3.");
                if ((uint)d0 >= (uint)_shape[0]) throw new ArgumentOutOfRangeException(nameof(d0));
                if ((uint)d1 >= (uint)_shape[1]) throw new ArgumentOutOfRangeException(nameof(d1));
                if ((uint)d2 >= (uint)_shape[2]) throw new ArgumentOutOfRangeException(nameof(d2));
                return ref _storage.GetPinnableReference(_offset + d0 * _strides[0] + d1 * _strides[1] + d2 * _strides[2]);
            }
        }

        /// <summary>
        /// Gets or sets a reference to an element in a 4D tensor (d0, d1, d2, d3).
        /// </summary>
        public ref T this[int d0, int d1, int d2, int d3]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (Rank != 4) throw new InvalidOperationException($"Rank is {Rank}, expected 4.");
                if ((uint)d0 >= (uint)_shape[0]) throw new ArgumentOutOfRangeException(nameof(d0));
                if ((uint)d1 >= (uint)_shape[1]) throw new ArgumentOutOfRangeException(nameof(d1));
                if ((uint)d2 >= (uint)_shape[2]) throw new ArgumentOutOfRangeException(nameof(d2));
                if ((uint)d3 >= (uint)_shape[3]) throw new ArgumentOutOfRangeException(nameof(d3));
                return ref _storage.GetPinnableReference(_offset + d0 * _strides[0] + d1 * _strides[1] + d2 * _strides[2] + d3 * _strides[3]);
            }
        }

        /// <summary>
        /// Slices a 1D tensor using a modern C# Range.
        /// </summary>
        public Tensor<T> this[Range r]
        {
            get
            {
                if (Rank != 1) throw new InvalidOperationException($"Rank is {Rank}, expected 1.");
                var (start, len) = ResolveRange(r, _shape[0]);
                return Slice(0, start, len);
            }
        }

        /// <summary>
        /// Slices a 2D tensor using row and column Ranges.
        /// </summary>
        public Tensor<T> this[Range r, Range c]
        {
            get
            {
                if (Rank != 2) throw new InvalidOperationException($"Rank is {Rank}, expected 2.");
                var (rStart, rLen) = ResolveRange(r, _shape[0]);
                var (cStart, cLen) = ResolveRange(c, _shape[1]);
                return Slice(0, rStart, rLen).Slice(1, cStart, cLen);
            }
        }

        /// <summary>
        /// Slices a row of a 2D tensor across column Range, returning a 1D vector view.
        /// </summary>
        public Tensor<T> this[int r, Range c]
        {
            get
            {
                if (Rank != 2) throw new InvalidOperationException($"Rank is {Rank}, expected 2.");
                var (cStart, cLen) = ResolveRange(c, _shape[1]);
                return SubTensor(r).Slice(0, cStart, cLen);
            }
        }

        /// <summary>
        /// Slices a column of a 2D tensor across row Range, returning a 1D vector view.
        /// </summary>
        public Tensor<T> this[Range r, int c]
        {
            get
            {
                if (Rank != 2) throw new InvalidOperationException($"Rank is {Rank}, expected 2.");
                var (rStart, rLen) = ResolveRange(r, _shape[0]);
                return Slice(0, rStart, rLen).Slice(1, c, 1).Squeeze(1);
            }
        }

        /// <summary>
        /// Slices a 3D tensor using dimension Ranges.
        /// </summary>
        public Tensor<T> this[Range d0, Range d1, Range d2]
        {
            get
            {
                if (Rank != 3) throw new InvalidOperationException($"Rank is {Rank}, expected 3.");
                var (s0, l0) = ResolveRange(d0, _shape[0]);
                var (s1, l1) = ResolveRange(d1, _shape[1]);
                var (s2, l2) = ResolveRange(d2, _shape[2]);
                return Slice(0, s0, l0).Slice(1, s1, l1).Slice(2, s2, l2);
            }
        }

        /// <summary>
        /// Slices a 4D tensor using dimension Ranges.
        /// </summary>
        public Tensor<T> this[Range d0, Range d1, Range d2, Range d3]
        {
            get
            {
                if (Rank != 4) throw new InvalidOperationException($"Rank is {Rank}, expected 4.");
                var (s0, l0) = ResolveRange(d0, _shape[0]);
                var (s1, l1) = ResolveRange(d1, _shape[1]);
                var (s2, l2) = ResolveRange(d2, _shape[2]);
                var (s3, l3) = ResolveRange(d3, _shape[3]);
                return Slice(0, s0, l0).Slice(1, s1, l1).Slice(2, s2, l2).Slice(3, s3, l3);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static (int Start, int Length) ResolveRange(Range range, int dimSize)
        {
            int start = range.Start.GetOffset(dimSize);
            int end = range.End.GetOffset(dimSize);
            if (start < 0 || start > dimSize) throw new ArgumentOutOfRangeException(nameof(range), $"Start index {start} out of range [0, {dimSize}].");
            if (end < start || end > dimSize) throw new ArgumentOutOfRangeException(nameof(range), $"End index {end} out of range [{start}, {dimSize}].");
            return (start, end - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateIndices(int[] indices)
        {
            if (indices == null || indices.Length != Rank)
            {
                throw new ArgumentException($"Expected {Rank} indices, got {indices?.Length ?? 0}.", nameof(indices));
            }

            for (int i = 0; i < indices.Length; i++)
            {
                if ((uint)indices[i] >= (uint)_shape[i])
                {
                    throw new ArgumentOutOfRangeException(nameof(indices), $"Index {indices[i]} at dimension {i} out of range [0, {_shape[i]}).");
                }
            }
        }

        #endregion

        #region Zero-Copy View Transformations

        /// <summary>
        /// Returns an O(1) zero-copy view sliced along the specified axis.
        /// </summary>
        public Tensor<T> Slice(int axis, int start, int length, int step = 1)
        {
            int ax = axis < 0 ? axis + Rank : axis;
            if (ax < 0 || ax >= Rank)
            {
                throw new ArgumentOutOfRangeException(nameof(axis), $"Axis {axis} is out of bounds for rank {Rank}.");
            }

            int dimSize = _shape[ax];
            if (start < 0 || start >= dimSize)
            {
                throw new ArgumentOutOfRangeException(nameof(start), $"Start {start} is out of range for dimension size {dimSize}.");
            }

            if (length < 0 || start + (length - 1) * step >= dimSize)
            {
                throw new ArgumentOutOfRangeException(nameof(length), $"Slice range [start={start}, length={length}, step={step}] exceeds dimension size {dimSize}.");
            }

            int newOffset = _offset + start * _strides[ax];
            var newDims = _shape.ToArray();
            newDims[ax] = length;

            var newStrides = new int[Rank];
            Array.Copy(_strides, newStrides, Rank);
            newStrides[ax] = _strides[ax] * step;

            return new Tensor<T>(_storage, newOffset, new TensorShape(newDims), newStrides);
        }

        /// <summary>
        /// Returns an O(1) zero-copy sub-tensor view at the specified index along axis 0, dropping axis 0 (Rank decreases by 1).
        /// For example, selecting row r of an M x N matrix yields an N-element 1D vector view.
        /// </summary>
        public Tensor<T> SubTensor(int index)
        {
            if (Rank == 0)
            {
                throw new InvalidOperationException("Cannot take a sub-tensor of a scalar (rank 0).");
            }

            if ((uint)index >= (uint)_shape[0])
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"Index {index} out of bounds for axis 0 of size {_shape[0]}.");
            }

            int newOffset = _offset + index * _strides[0];
            int newRank = Rank - 1;

            if (newRank == 0)
            {
                return new Tensor<T>(_storage, newOffset, TensorShape.Scalar, Array.Empty<int>());
            }

            var newDims = new int[newRank];
            var newStrides = new int[newRank];

            for (int i = 0; i < newRank; i++)
            {
                newDims[i] = _shape[i + 1];
                newStrides[i] = _strides[i + 1];
            }

            return new Tensor<T>(_storage, newOffset, new TensorShape(newDims), newStrides);
        }

        /// <summary>
        /// Returns an O(1) zero-copy view with the two specified axes swapped.
        /// </summary>
        public Tensor<T> Transpose(int axis1 = 0, int axis2 = 1)
        {
            int ax1 = axis1 < 0 ? axis1 + Rank : axis1;
            int ax2 = axis2 < 0 ? axis2 + Rank : axis2;

            if (ax1 < 0 || ax1 >= Rank) throw new ArgumentOutOfRangeException(nameof(axis1));
            if (ax2 < 0 || ax2 >= Rank) throw new ArgumentOutOfRangeException(nameof(axis2));

            if (ax1 == ax2) return this;

            var newDims = _shape.ToArray();
            var newStrides = new int[Rank];
            Array.Copy(_strides, newStrides, Rank);

            newDims[ax1] = _shape[ax2];
            newDims[ax2] = _shape[ax1];

            newStrides[ax1] = _strides[ax2];
            newStrides[ax2] = _strides[ax1];

            return new Tensor<T>(_storage, _offset, new TensorShape(newDims), newStrides);
        }

        /// <summary>
        /// Returns an O(1) zero-copy view with axes permuted in the specified order.
        /// </summary>
        public Tensor<T> Permute(params int[] axes)
        {
            var newShape = _shape.Permute(axes);
            var newStrides = new int[Rank];

            for (int i = 0; i < Rank; i++)
            {
                int ax = axes[i] < 0 ? axes[i] + Rank : axes[i];
                newStrides[i] = _strides[ax];
            }

            return new Tensor<T>(_storage, _offset, newShape, newStrides);
        }

        /// <summary>
        /// Reshapes the tensor to the specified dimensions.
        /// If the tensor is contiguous, this is an O(1) zero-copy operation.
        /// If non-contiguous, a contiguous clone is created first.
        /// </summary>
        public Tensor<T> Reshape(params int[] newDimensions)
        {
            var targetShape = new TensorShape(newDimensions);
            if (targetShape.TotalElements != Length)
            {
                throw new InvalidOperationException($"Cannot reshape tensor of {Length} elements into {targetShape} ({targetShape.TotalElements} elements).");
            }

            if (IsContiguous)
            {
                var newStrides = TensorStrides.ComputeContiguousStrides(targetShape);
                return new Tensor<T>(_storage, _offset, targetShape, newStrides);
            }

            // Non-contiguous memory requires packing into contiguous buffer first
            return ToContiguous().Reshape(newDimensions);
        }

        /// <summary>
        /// Returns an O(1) zero-copy view with all dimensions of size 1 removed (or a specific axis).
        /// </summary>
        public Tensor<T> Squeeze(int? axis = null)
        {
            var newShape = _shape.Squeeze(axis);
            if (newShape == _shape) return this;

            var newStrides = new List<int>(newShape.Rank);
            for (int i = 0; i < Rank; i++)
            {
                if (axis.HasValue)
                {
                    int targetAxis = axis.Value < 0 ? axis.Value + Rank : axis.Value;
                    if (i != targetAxis || _shape[i] != 1)
                    {
                        newStrides.Add(_strides[i]);
                    }
                }
                else if (_shape[i] != 1)
                {
                    newStrides.Add(_strides[i]);
                }
            }

            return new Tensor<T>(_storage, _offset, newShape, newStrides.ToArray());
        }

        /// <summary>
        /// Returns an O(1) zero-copy view with a new dimension of size 1 inserted at the specified axis.
        /// </summary>
        public Tensor<T> Unsqueeze(int axis)
        {
            var newShape = _shape.Unsqueeze(axis);
            int targetAxis = axis < 0 ? axis + Rank + 1 : axis;

            var newStrides = new int[newShape.Rank];
            for (int i = 0, j = 0; i < newShape.Rank; i++)
            {
                if (i == targetAxis)
                {
                    // Stride for unsqueezed dimension: matches stride of next dimension or 1
                    newStrides[i] = j < Rank ? _strides[j] * _shape[j] : 1;
                }
                else
                {
                    newStrides[i] = _strides[j++];
                }
            }

            return new Tensor<T>(_storage, _offset, newShape, newStrides);
        }

        /// <summary>
        /// Flattens the tensor into a 1D tensor view (O(1) if contiguous, or packed copy).
        /// </summary>
        public Tensor<T> Flatten() => Reshape(Length);

        /// <summary>
        /// Returns an O(1) zero-copy view broadcast to the specified target shape.
        /// Expanded dimensions will have their stride set to 0.
        /// </summary>
        public Tensor<T> BroadcastTo(TensorShape targetShape)
        {
            if (Shape == targetShape) return this;

            if (!_shape.IsCompatibleForBroadcasting(targetShape, out var commonShape) || commonShape != targetShape)
            {
                throw new InvalidOperationException($"Cannot broadcast tensor of shape {_shape} to {targetShape}.");
            }

            var broadcastStrides = TensorStrides.ComputeBroadcastStrides(_shape, _strides, targetShape);
            return new Tensor<T>(_storage, _offset, targetShape, broadcastStrides);
        }

        #endregion

        #region Structural & Array API Operations

        /// <summary>
        /// Concatenates this tensor with another tensor along the specified axis.
        /// </summary>
        public Tensor<T> Concat(Tensor<T> other, int axis = 0) => Tensor.Concat(new[] { this, other }, axis);

        /// <summary>
        /// Splits this tensor into equal parts along the specified axis.
        /// </summary>
        public Tensor<T>[] Split(int parts, int axis = 0) => Tensor.Split(this, parts, axis);

        /// <summary>
        /// Splits this tensor into multiple tensors along the specified axis according to given section sizes.
        /// </summary>
        public Tensor<T>[] Split(int[] splitSizes, int axis = 0) => Tensor.Split(this, splitSizes, axis);

        /// <summary>
        /// Splits this tensor into chunks of the specified size along an axis.
        /// </summary>
        public Tensor<T>[] Chunk(int chunkSize, int axis = 0) => Tensor.Chunk(this, chunkSize, axis);

        /// <summary>
        /// Pads this tensor with specified amounts before and after each dimension.
        /// </summary>
        public Tensor<T> Pad(int[] padBefore, int[] padAfter, PadMode mode = PadMode.Constant, T constantValue = default) =>
            Tensor.Pad(this, padBefore, padAfter, mode, constantValue);

        /// <summary>
        /// Constructs a new tensor by repeating tensor dimensions the number of times given by reps.
        /// </summary>
        public Tensor<T> Tile(params int[] reps) => Tensor.Tile(this, reps);

        /// <summary>
        /// Repeats elements of this tensor along an axis.
        /// </summary>
        public Tensor<T> Repeat(int repeats, int axis) => Tensor.Repeat(this, repeats, axis);

        /// <summary>
        /// Rolls tensor elements along the given axis circularly by the specified shift amount.
        /// </summary>
        public Tensor<T> Roll(int shift, int axis = 0) => Tensor.Roll(this, shift, axis);

        /// <summary>
        /// Reverses the order of elements in this tensor along the given axis.
        /// </summary>
        public Tensor<T> Flip(int axis = 0) => Tensor.Flip(this, axis);

        #endregion

        #region Memory Operations

        /// <summary>
        /// Creates an independent, contiguous deep copy of this tensor.
        /// </summary>
        public Tensor<T> Clone()
        {
            var clone = new Tensor<T>(_shape);
            CopyTo(clone);
            return clone;
        }

        /// <summary>
        /// Releases any underlying unmanaged or pooled resources held by this tensor's storage.
        /// </summary>
        public void Dispose()
        {
            _storage.Dispose();
        }

        /// <summary>
        /// Returns a contiguous tensor. If this tensor is already contiguous, returns this instance; otherwise returns a packed contiguous clone.
        /// </summary>
        public Tensor<T> ToContiguous()
        {
            if (IsContiguous && _offset == 0 && _storage.Length == Length)
            {
                return this;
            }

            return Clone();
        }

        /// <summary>
        /// Ensures this tensor resides in host CPU memory.
        /// If already on CPU, returns this instance; otherwise transfers data from hardware device (VRAM) to host CPU.
        /// </summary>
        public Tensor<T> ToCpu()
        {
            if (Device == DeviceType.Cpu) return this;

            if (_storage is IDeviceStorageTransfer<T> transfer)
            {
                var cpuTensor = new Tensor<T>(_shape);
                transfer.CopyToHost(_offset, Length, cpuTensor.AsSpan());
                return cpuTensor;
            }

            throw new NotSupportedException($"Storage of type {_storage.GetType().Name} on device {Device} does not support Host transfer.");
        }

        /// <summary>
        /// Transfers this tensor to a destination hardware device storage (Direct3D 11, Vulkan, CUDA).
        /// </summary>
        public Tensor<T> ToDevice(ITensorStorage<T> deviceStorage)
        {
            if (deviceStorage == null) throw new ArgumentNullException(nameof(deviceStorage));

            if (deviceStorage is IDeviceStorageTransfer<T> transfer)
            {
                var contig = IsContiguous ? this : ToContiguous();
                transfer.CopyFromHost(0, Length, contig.AsReadOnlySpan());
                var strides = TensorStrides.ComputeContiguousStrides(_shape);
                return new Tensor<T>(deviceStorage, 0, _shape, strides);
            }

            throw new NotSupportedException($"Target device storage {deviceStorage.GetType().Name} does not implement {nameof(IDeviceStorageTransfer<T>)}.");
        }

        /// <summary>
        /// Returns a Span view over the contiguous memory buffer.
        /// Throws <see cref="InvalidOperationException"/> if the tensor is non-contiguous or strided.
        /// </summary>
        public Span<T> AsSpan()
        {
            if (!IsContiguous)
            {
                throw new InvalidOperationException("Cannot obtain Span over a non-contiguous or strided tensor. Call ToContiguous() first.");
            }

            return _storage.AsSpan(_offset, Length);
        }

        /// <summary>
        /// Returns a ReadOnlySpan view over the contiguous memory buffer.
        /// Throws <see cref="InvalidOperationException"/> if the tensor is non-contiguous or strided.
        /// </summary>
        public ReadOnlySpan<T> AsReadOnlySpan()
        {
            if (!IsContiguous)
            {
                throw new InvalidOperationException("Cannot obtain ReadOnlySpan over a non-contiguous or strided tensor. Call ToContiguous() first.");
            }

            return _storage.AsReadOnlySpan(_offset, Length);
        }

        /// <summary>
        /// Copies all elements into a new contiguous 1D array in standard row-major order.
        /// </summary>
        public T[] ToArray()
        {
            var result = new T[Length];
            if (IsContiguous)
            {
                AsReadOnlySpan().CopyTo(result);
                return result;
            }

            int index = 0;
            ForEachElement((val) => result[index++] = val);
            return result;
        }

        /// <summary>
        /// Fills all elements in this tensor with the specified value.
        /// </summary>
        public void Fill(T value)
        {
            if (IsContiguous)
            {
                AsSpan().Fill(value);
                return;
            }

            ForEachCoordinate(indices => this[indices] = value);
        }

        /// <summary>
        /// Copies elements from this tensor to the destination tensor.
        /// </summary>
        public void CopyTo(Tensor<T> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (Length != destination.Length)
            {
                throw new ArgumentException($"Destination length {destination.Length} does not match source length {Length}.");
            }

            if (IsContiguous && destination.IsContiguous)
            {
                AsReadOnlySpan().CopyTo(destination.AsSpan());
                return;
            }

            ForEachCoordinate(indices => destination[indices] = this[indices]);
        }

        /// <summary>
        /// Iterates through every element in the tensor and invokes an action.
        /// </summary>
        public void ForEachElement(Action<T> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (IsContiguous)
            {
                var span = AsReadOnlySpan();
                for (int i = 0; i < span.Length; i++)
                {
                    action(span[i]);
                }
                return;
            }

            ForEachCoordinate(indices => action(this[indices]));
        }

        /// <summary>
        /// Iterates through all multidimensional coordinate tuples.
        /// </summary>
        public void ForEachCoordinate(Action<int[]> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            int rank = Rank;
            if (rank == 0)
            {
                action(Array.Empty<int>());
                return;
            }

            var coords = new int[rank];
            TraverseCoordinates(coords, 0, action);
        }

        private void TraverseCoordinates(int[] coords, int currentDim, Action<int[]> action)
        {
            int dimSize = _shape[currentDim];
            bool isLeaf = currentDim == Rank - 1;

            for (int i = 0; i < dimSize; i++)
            {
                coords[currentDim] = i;
                if (isLeaf)
                {
                    action(coords);
                }
                else
                {
                    TraverseCoordinates(coords, currentDim + 1, action);
                }
            }
        }

        #endregion

        #region Equality & Formatting

        /// <inheritdoc />
        public bool Equals(Tensor<T>? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            if (Shape != other.Shape) return false;

            if (IsContiguous && other.IsContiguous)
            {
                var spanA = AsReadOnlySpan();
                var spanB = other.AsReadOnlySpan();
                for (int i = 0; i < spanA.Length; i++)
                {
                    if (!spanA[i].Equals(spanB[i])) return false;
                }
                return true;
            }

            bool equal = true;
            ForEachCoordinate(coords =>
            {
                if (!equal) return;
                if (!this[coords].Equals(other[coords]))
                {
                    equal = false;
                }
            });

            return equal;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is Tensor<T> other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hash = Shape.GetHashCode();
            if (Length > 0)
            {
                hash = hash * 31 + this[new int[Rank]].GetHashCode();
            }
            return hash;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append($"Tensor<{typeof(T).Name}>(shape={Shape}, strides=[{string.Join(", ", _strides)}]");

            if (Length <= 16)
            {
                sb.Append(", data=[");
                bool first = true;
                ForEachElement(val =>
                {
                    if (!first) sb.Append(", ");
                    sb.Append(val);
                    first = false;
                });
                sb.Append("]");
            }
            else
            {
                sb.Append($", elements={Length}");
            }

            sb.Append(")");
            return sb.ToString();
        }

        #endregion

        #region Operator Overloads

        public static Tensor<T> operator +(Tensor<T> a, Tensor<T> b) => TensorOps.Add(a, b);
        public static Tensor<T> operator -(Tensor<T> a, Tensor<T> b) => TensorOps.Subtract(a, b);
        public static Tensor<T> operator *(Tensor<T> a, Tensor<T> b) => TensorOps.Multiply(a, b);
        public static Tensor<T> operator /(Tensor<T> a, Tensor<T> b) => TensorOps.Divide(a, b);
        public static Tensor<T> operator -(Tensor<T> a) => TensorOps.Negate(a);

        public static Tensor<T> operator +(Tensor<T> a, T scalar) => TensorOps.AddScalar(a, scalar);
        public static Tensor<T> operator -(Tensor<T> a, T scalar) => TensorOps.SubtractScalar(a, scalar);
        public static Tensor<T> operator *(Tensor<T> a, T scalar) => TensorOps.MultiplyScalar(a, scalar);
        public static Tensor<T> operator /(Tensor<T> a, T scalar) => TensorOps.DivideScalar(a, scalar);

        public static Tensor<T> operator +(T scalar, Tensor<T> a) => TensorOps.AddScalar(a, scalar);
        public static Tensor<T> operator *(T scalar, Tensor<T> a) => TensorOps.MultiplyScalar(a, scalar);

        #endregion
    }
}
