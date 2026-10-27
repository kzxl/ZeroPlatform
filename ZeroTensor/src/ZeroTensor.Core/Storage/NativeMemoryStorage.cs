using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using ZeroPrimitives.Memory;

namespace ZeroTensor.Core.Storage
{
    /// <summary>
    /// Tensor storage backed by off-heap unmanaged memory (completely invisible to the Garbage Collector).
    /// Eliminates LOH fragmentation and GC pauses during high-throughput AI workloads.
    /// </summary>
    public sealed unsafe class NativeMemoryStorage<T> : ITensorStorage<T> where T : unmanaged, IEquatable<T>
    {
        private T* _pointer;
        private readonly int _length;
        private readonly NativeMemoryBlock? _block;
        private readonly bool _ownsPointer;
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

        public NativeMemoryBlock? UnderlyingBlock => _block;

        public NativeMemoryStorage(NativeMemoryBlock block, int elementOffset = 0, int elementCount = -1)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            int elementSize = sizeof(T);
            int maxElements = block.Length / elementSize;

            if (elementOffset < 0 || elementOffset > maxElements)
                throw new ArgumentOutOfRangeException(nameof(elementOffset));

            int actualCount = elementCount < 0 ? maxElements - elementOffset : elementCount;
            if (elementOffset + actualCount > maxElements)
                throw new ArgumentOutOfRangeException(nameof(elementCount));

            _block = block;
            _pointer = (T*)block.Pointer + elementOffset;
            _length = actualCount;
            _ownsPointer = false;
        }

        public NativeMemoryStorage(T* pointer, int length, bool ownsPointer = false)
        {
            if (pointer == null) throw new ArgumentNullException(nameof(pointer));
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));

            _pointer = pointer;
            _length = length;
            _ownsPointer = ownsPointer;
            _block = null;
        }

        public static NativeMemoryStorage<T> Allocate(int length)
        {
            if (length < 0) throw new ArgumentOutOfRangeException(nameof(length));
            int bytes = length * sizeof(T);
            var block = NativeMemoryBlock.Allocate(Math.Max(bytes, 1));
            return new NativeMemoryStorage<T>(block, 0, length);
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
                throw new ObjectDisposedException(nameof(NativeMemoryStorage<T>), "The native memory storage has already been disposed.");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_block != null)
            {
                _block.Dispose();
                _pointer = null;
            }
            else if (_ownsPointer && _pointer != null)
            {
#if NET8_0_OR_GREATER
                NativeMemory.Free(_pointer);
#else
                Marshal.FreeHGlobal((IntPtr)_pointer);
#endif
                _pointer = null;
            }

            GC.SuppressFinalize(this);
        }

        ~NativeMemoryStorage()
        {
            if (_ownsPointer && _pointer != null && Volatile.Read(ref _disposed) == 0)
            {
#if NET8_0_OR_GREATER
                NativeMemory.Free(_pointer);
#else
                Marshal.FreeHGlobal((IntPtr)_pointer);
#endif
                _pointer = null;
            }
        }
    }
}
