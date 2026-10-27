using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using ZeroPrimitives.Internal;

namespace ZeroPrimitives.Buffers
{
    /// <summary>
    /// A high-performance, expandable buffer writer that rents backing memory from ArrayPool.
    /// Implements IBufferWriter and IDisposable to completely prevent Large Object Heap (LOH) fragmentation
    /// during serialization and large report generation.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    public sealed class ArrayPoolBufferWriter<T> : IBufferWriter<T>, IDisposable
    {
        private const int DefaultInitialCapacity = 256;
        private readonly ArrayPool<T> _pool;
        private T[]? _buffer;
        private int _index;

        /// <summary>
        /// Initializes a new instance of ArrayPoolBufferWriter with the specified initial capacity and pool.
        /// </summary>
        public ArrayPoolBufferWriter(int initialCapacity = DefaultInitialCapacity, ArrayPool<T>? pool = null)
        {
            if (initialCapacity <= 0)
            {
                initialCapacity = DefaultInitialCapacity;
            }

            _pool = pool ?? ArrayPool<T>.Shared;
            _buffer = _pool.Rent(initialCapacity);
            _index = 0;
        }

        /// <summary>
        /// Gets the data written to the underlying buffer so far as ReadOnlyMemory.
        /// </summary>
        public ReadOnlyMemory<T> WrittenMemory
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return _buffer!.AsMemory(0, _index);
            }
        }

        /// <summary>
        /// Gets the data written to the underlying buffer so far as ReadOnlySpan.
        /// </summary>
        public ReadOnlySpan<T> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return _buffer!.AsSpan(0, _index);
            }
        }

        /// <summary>
        /// Gets the amount of data written to the underlying buffer.
        /// </summary>
        public int WrittenCount
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _index;
        }

        /// <summary>
        /// Gets the total capacity of the underlying rented buffer.
        /// </summary>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer?.Length ?? 0;
        }

        /// <summary>
        /// Gets the remaining free capacity in the current rented buffer before resizing is required.
        /// </summary>
        public int FreeCapacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _buffer != null ? _buffer.Length - _index : 0;
        }

        /// <summary>
        /// Notifies the writer that count amount of data was written to the output Span or Memory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Advance(int count)
        {
            ThrowIfDisposed();

            if (count < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");
            }

            if (_index > _buffer!.Length - count)
            {
                ThrowHelper.ThrowCannotAdvanceBeyondBounds(nameof(count));
            }

            _index += count;
        }

        /// <summary>
        /// Returns a Memory to write to that is at least the requested length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Memory<T> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer!.AsMemory(_index);
        }

        /// <summary>
        /// Returns a Span to write to that is at least the requested length.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer!.AsSpan(_index);
        }

        /// <summary>
        /// Resets the written count back to zero without releasing the rented buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reset()
        {
            ThrowIfDisposed();
            _index = 0;
        }

        /// <summary>
        /// Releases the currently rented buffer back to the pool and resets state.
        /// </summary>
        public void Dispose()
        {
            if (_buffer != null)
            {
                T[] toReturn = _buffer;
                _buffer = null;
                _index = 0;
                _pool.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (_buffer == null)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(ArrayPoolBufferWriter<T>));
            }
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            ThrowIfDisposed();

            if (sizeHint < 0)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(sizeHint), "Size hint cannot be negative.");
            }

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (FreeCapacity >= sizeHint)
            {
                return;
            }

            int currentLength = _buffer!.Length;
            int growBy = Math.Max(sizeHint, currentLength);
            int newSize = unchecked(currentLength + growBy);

            if ((uint)newSize > int.MaxValue)
            {
                newSize = unchecked(_index + sizeHint);
                if ((uint)newSize > int.MaxValue)
                {
                    ThrowHelper.ThrowInvalidOperationException("Buffer cannot be expanded beyond maximum array size.");
                }
            }

            T[] newBuffer = _pool.Rent(newSize);
            Array.Copy(_buffer, 0, newBuffer, 0, _index);

            T[] oldBuffer = _buffer;
            _buffer = newBuffer;
            _pool.Return(oldBuffer);
        }
    }
}
