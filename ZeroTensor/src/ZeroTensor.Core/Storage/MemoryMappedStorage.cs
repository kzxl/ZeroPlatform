using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace ZeroTensor.Core.Storage
{
    /// <summary>
    /// Tensor storage backed directly by an OS Memory-Mapped File (MMF).
    /// Provides instant, true zero-copy access into model weight files (SafeTensors, GGUF, NPY)
    /// directly from the OS page cache without loading bytes into user-space heap.
    /// </summary>
    public sealed unsafe class MemoryMappedStorage<T> : ITensorStorage<T> where T : unmanaged, IEquatable<T>
    {
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly bool _ownsMmf;
        private readonly IDisposable? _sharedLifetime;
        private byte* _basePointer;
        private T* _pointer;
        private readonly int _length;
        private int _disposed;

        public DeviceType Device => DeviceType.Cpu;
        public int Length => _length;
        public bool IsCpuAccessible => true;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        public T* UnsafePointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return _pointer;
            }
        }

        public MemoryMappedStorage(MemoryMappedFile mmf, long byteOffset, int elementCount, bool ownsMmf = true, IDisposable? sharedLifetime = null)
        {
            _mmf = mmf ?? throw new ArgumentNullException(nameof(mmf));
            _ownsMmf = ownsMmf;
            _sharedLifetime = sharedLifetime;
            if (byteOffset < 0) throw new ArgumentOutOfRangeException(nameof(byteOffset));
            if (elementCount < 0) throw new ArgumentOutOfRangeException(nameof(elementCount));

            long totalBytes = (long)elementCount * sizeof(T);
            _accessor = mmf.CreateViewAccessor(byteOffset, totalBytes, MemoryMappedFileAccess.Read);
            _accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref _basePointer);
            _pointer = (T*)(_basePointer + _accessor.PointerOffset);
            _length = elementCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AsSpan(int offset, int length)
        {
            ThrowIfDisposed();
            if ((uint)offset > (uint)_length || (uint)(offset + length) > (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return new Span<T>(_pointer + offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<T> AsReadOnlySpan(int offset, int length)
        {
            ThrowIfDisposed();
            if ((uint)offset > (uint)_length || (uint)(offset + length) > (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return new ReadOnlySpan<T>(_pointer + offset, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetPinnableReference(int offset)
        {
            ThrowIfDisposed();
            if ((uint)offset >= (uint)_length)
                throw new ArgumentOutOfRangeException(nameof(offset));
            return ref *(_pointer + offset);
        }

        public T[]? TryGetArray(out int arrayOffset)
        {
            arrayOffset = 0;
            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(MemoryMappedStorage<T>), "The memory-mapped storage has already been disposed.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_basePointer != null)
            {
                _accessor?.SafeMemoryMappedViewHandle.ReleasePointer();
                _basePointer = null;
                _pointer = null;
            }

            _accessor?.Dispose();
            if (_ownsMmf)
            {
                _mmf?.Dispose();
            }
            _sharedLifetime?.Dispose();
            GC.SuppressFinalize(this);
        }

        ~MemoryMappedStorage()
        {
            Dispose();
        }
    }
}
