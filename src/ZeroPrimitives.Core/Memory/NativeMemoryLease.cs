using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace ZeroPrimitives.Memory
{
    /// <summary>
    /// Zero-allocation value-type lease over an off-heap unmanaged memory block rented from <see cref="NativeMemoryPool"/>.
    /// Eliminates the <see cref="NativeMemoryBlock"/> class wrapper allocation while providing deterministic return on <see cref="Dispose"/>.
    /// </summary>
    public readonly unsafe struct NativeMemoryLease : IDisposable, IEquatable<NativeMemoryLease>
    {
        private readonly NativeMemoryPool? _pool;
        private readonly IntPtr _pointer;
        private readonly int _length;
        private readonly int _capacity;

        /// <summary>
        /// Gets the unmanaged memory pointer as an <see cref="IntPtr"/>.
        /// </summary>
        public IntPtr Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer;
        }

        /// <summary>
        /// Gets the raw unmanaged byte pointer.
        /// </summary>
        public byte* RawPointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => (byte*)_pointer;
        }

        /// <summary>
        /// Gets the requested active payload length in bytes.
        /// </summary>
        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        /// <summary>
        /// Gets the underlying bucket capacity in bytes.
        /// </summary>
        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _capacity;
        }

        /// <summary>
        /// Gets whether this lease wraps a valid non-zero pointer.
        /// </summary>
        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _pointer != IntPtr.Zero;
        }

        /// <summary>
        /// Gets a writable <see cref="Span{T}"/> over the active length of the leased memory.
        /// </summary>
        public Span<byte> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Span<byte>((void*)_pointer, _length);
        }

        /// <summary>
        /// Gets a read-only <see cref="ReadOnlySpan{T}"/> over the active length of the leased memory.
        /// </summary>
        public ReadOnlySpan<byte> ReadOnlySpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new ReadOnlySpan<byte>((void*)_pointer, _length);
        }

        /// <summary>
        /// Gets a writable <see cref="Span{T}"/> over the full physical bucket capacity of the leased memory.
        /// </summary>
        public Span<byte> CapacitySpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new Span<byte>((void*)_pointer, _capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal NativeMemoryLease(NativeMemoryPool pool, IntPtr pointer, int length, int capacity)
        {
            _pool = pool;
            _pointer = pointer;
            _length = length;
            _capacity = capacity;
        }

        /// <summary>
        /// Zeroes out the leased memory block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            if (_pointer == IntPtr.Zero)
                return;

#if NET8_0_OR_GREATER
            NativeMemory.Clear((void*)_pointer, (nuint)_capacity);
#else
            CapacitySpan.Clear();
#endif
        }

        /// <summary>
        /// Returns the unmanaged memory pointer to the owning <see cref="NativeMemoryPool"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            if (_pointer != IntPtr.Zero && _pool != null)
            {
                _pool.ReturnLease(_pointer, _capacity);
            }
        }

        public bool Equals(NativeMemoryLease other) => _pointer == other._pointer && _length == other._length;
        public override bool Equals(object? obj) => obj is NativeMemoryLease other && Equals(other);
        public override int GetHashCode() => _pointer.GetHashCode();
        public static bool operator ==(NativeMemoryLease left, NativeMemoryLease right) => left.Equals(right);
        public static bool operator !=(NativeMemoryLease left, NativeMemoryLease right) => !left.Equals(right);
    }
}
