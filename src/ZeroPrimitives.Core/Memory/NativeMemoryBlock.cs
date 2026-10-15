using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Represents an off-heap unmanaged memory block that is completely invisible to the Garbage Collector.
    /// Supports zero-allocation span wrapping, pointer access, and automatic pool recycling.
    /// </summary>
    public sealed unsafe class NativeMemoryBlock : IDisposable
    {
        private byte* _pointer;
        private readonly int _capacity;
        private int _length;
        private readonly Action<NativeMemoryBlock>? _recycleCallback;
        private readonly bool _ownsMemory;
        private int _disposed;

        /// <summary>
        /// Gets the raw unmanaged memory pointer.
        /// </summary>
        public byte* Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return _pointer;
            }
        }

        /// <summary>
        /// Gets the total byte capacity of the block.
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// Gets or sets the active payload length of the block.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
            set
            {
                if (value < 0 || value > _capacity)
                    throw new ArgumentOutOfRangeException(nameof(value), "Length cannot exceed block capacity.");
                _length = value;
            }
        }

        /// <summary>
        /// Gets a writable span view over the block's active payload.
        /// </summary>
        public Span<byte> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return new Span<byte>(_pointer, _length);
            }
        }

        /// <summary>
        /// Gets a read-only span view over the block's active payload.
        /// </summary>
        public ReadOnlySpan<byte> ReadOnlySpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return new ReadOnlySpan<byte>(_pointer, _length);
            }
        }

        /// <summary>
        /// Gets a writable span view over the entire physical capacity of the block.
        /// </summary>
        public Span<byte> CapacitySpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                ThrowIfDisposed();
                return new Span<byte>(_pointer, _capacity);
            }
        }

        /// <summary>
        /// Gets whether this memory block has been disposed.
        /// </summary>
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

        /// <summary>
        /// Creates a new unmanaged memory block wrapping an existing pointer.
        /// </summary>
        public NativeMemoryBlock(byte* pointer, int capacity, int length = 0, Action<NativeMemoryBlock>? recycleCallback = null, bool ownsMemory = false)
        {
            if (pointer == null)
                throw new ArgumentNullException(nameof(pointer));
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

            _pointer = pointer;
            _capacity = capacity;
            _length = length > 0 ? length : capacity;
            _recycleCallback = recycleCallback;
            _ownsMemory = ownsMemory;
        }

        /// <summary>
        /// Allocates a standalone off-heap unmanaged memory block using native heap allocation.
        /// </summary>
        public static NativeMemoryBlock Allocate(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be positive.");

#if NET8_0_OR_GREATER
            byte* ptr = (byte*)NativeMemory.Alloc((nuint)capacity);
#else
            byte* ptr = (byte*)Marshal.AllocHGlobal(capacity).ToPointer();
#endif
            return new NativeMemoryBlock(ptr, capacity, capacity, ownsMemory: true);
        }

        /// <summary>
        /// Clears the block payload by zeroing out the active span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            ThrowIfDisposed();
#if NET8_0_OR_GREATER
            NativeMemory.Clear(_pointer, (nuint)_capacity);
#else
            Span.Clear();
#endif
        }

        /// <summary>
        /// Resets the disposal state when the block is re-acquired from a pool.
        /// </summary>
        internal void Reset(int activeLength)
        {
            Volatile.Write(ref _disposed, 0);
            Length = activeLength;
        }

        internal void Reset(byte* pointer, int activeLength)
        {
            _pointer = pointer;
            Volatile.Write(ref _disposed, 0);
            Length = activeLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte* DetachPointer()
        {
            byte* ptr = _pointer;
            _pointer = null;
            return ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte* DangerousGetPointer() => _pointer;

        /// <summary>
        /// Explicitly frees the underlying native unmanaged memory regardless of disposal state.
        /// Used by pool allocators during pool shutdown.
        /// </summary>
        internal void DestroyMemory()
        {
            if (_pointer != null)
            {
#if NET8_0_OR_GREATER
                NativeMemory.Free(_pointer);
#else
                Marshal.FreeHGlobal((IntPtr)_pointer);
#endif
                _pointer = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ThrowIfDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(nameof(NativeMemoryBlock), "The unmanaged memory block has already been disposed or recycled.");
        }

        /// <summary>
        /// Recycles this block back to its owning allocator or frees the unmanaged memory.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            if (_recycleCallback != null)
            {
                _recycleCallback(this);
            }
            else if (_ownsMemory && _pointer != null)
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

        /// <summary>
        /// Finalizer ensures unmanaged memory is reclaimed if not disposed manually.
        /// </summary>
        ~NativeMemoryBlock()
        {
            if (_ownsMemory && _pointer != null && Volatile.Read(ref _disposed) == 0)
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
