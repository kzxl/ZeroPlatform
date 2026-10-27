using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZeroTensor.Core.Storage
{
    /// <summary>
    /// Tensor storage backed by a standard managed garbage-collected array T[] or ArrayPool rented buffer.
    /// </summary>
    public sealed class ManagedArrayStorage<T> : ITensorStorage<T> where T : unmanaged, IEquatable<T>
    {
        private T[]? _array;
        private readonly int _offset;
        private readonly int _length;
        private readonly bool _returnToPool;
        private int _disposed;

        public DeviceType Device => DeviceType.Cpu;
        public int Length => _length;
        public bool IsCpuAccessible => true;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        public unsafe T* UnsafePointer => null;

        /// <summary>
        /// Gets the underlying managed array buffer.
        /// </summary>
        public T[] Array
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return _array!;
            }
        }

        public int BaseOffset => _offset;

        public ManagedArrayStorage(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            _array = new T[length];
            _offset = 0;
            _length = length;
            _returnToPool = false;
        }

        public ManagedArrayStorage(T[] array, int offset = 0, int length = -1, bool returnToPool = false)
        {
            _array = array ?? throw new ArgumentNullException(nameof(array));
            if (offset < 0 || offset > array.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            int actualLength = length < 0 ? array.Length - offset : length;
            if (offset + actualLength > array.Length) throw new ArgumentOutOfRangeException(nameof(length));

            _offset = offset;
            _length = actualLength;
            _returnToPool = returnToPool;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int offset, int length)
        {
            ThrowIfDisposed();
            if ((uint)offset > (uint)_length || (uint)(offset + length) > (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return new Span<T>(_array!, _offset + offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsReadOnlySpan(int offset, int length)
        {
            ThrowIfDisposed();
            if ((uint)offset > (uint)_length || (uint)(offset + length) > (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return new ReadOnlySpan<T>(_array!, _offset + offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetPinnableReference(int offset)
        {
            ThrowIfDisposed();
            if ((uint)offset >= (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return ref _array![_offset + offset];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T[]? TryGetArray(out int arrayOffset)
        {
            ThrowIfDisposed();
            arrayOffset = _offset;
            return _array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(ManagedArrayStorage<T>), "The managed array storage has already been disposed.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_returnToPool && _array != null)
            {
                ArrayPool<T>.Shared.Return(_array);
                _array = null;
            }
        }
    }
}
