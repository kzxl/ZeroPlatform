using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Memory
{
    /// <summary>
    /// Central high-performance memory pooling manager for ZeroUI.
    /// Manages pooled managed buffers (<see cref="ArrayPool{T}"/>) and unmanaged native memory allocations
    /// with zero GC allocation overhead on hot paths.
    /// </summary>
    public static unsafe class ZeroBufferPool
    {
        private static long _activeManagedRentedCount;
        private static long _activeNativeAllocatedBytes;

        /// <summary>
        /// Gets the current number of actively rented managed arrays.
        /// </summary>
        public static long ActiveManagedRentedCount => Volatile.Read(ref _activeManagedRentedCount);

        /// <summary>
        /// Gets the total bytes of actively allocated native memory blocks.
        /// </summary>
        public static long ActiveNativeAllocatedBytes => Volatile.Read(ref _activeNativeAllocatedBytes);

        /// <summary>
        /// Rents a managed byte array of at least the requested minimum length from the shared pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte[] RentByteArray(int minCapacity)
        {
            if (minCapacity < 0) throw new ArgumentOutOfRangeException(nameof(minCapacity));
            Interlocked.Increment(ref _activeManagedRentedCount);
            return ArrayPool<byte>.Shared.Rent(minCapacity);
        }

        /// <summary>
        /// Returns a previously rented managed byte array to the shared pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ReturnByteArray(byte[] array, bool clearArray = false)
        {
            if (array == null) return;
            Interlocked.Decrement(ref _activeManagedRentedCount);
            ArrayPool<byte>.Shared.Return(array, clearArray);
        }

        /// <summary>
        /// Rents a managed array of type T of at least the requested minimum length from the shared pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T[] Rent<T>(int minCapacity)
        {
            if (minCapacity < 0) throw new ArgumentOutOfRangeException(nameof(minCapacity));
            Interlocked.Increment(ref _activeManagedRentedCount);
            return ArrayPool<T>.Shared.Rent(minCapacity);
        }

        /// <summary>
        /// Returns a previously rented managed array to the shared pool.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Return<T>(T[] array, bool clearArray = false)
        {
            if (array == null) return;
            Interlocked.Decrement(ref _activeManagedRentedCount);
            ArrayPool<T>.Shared.Return(array, clearArray);
        }

        /// <summary>
        /// Allocates a contiguous block of unmanaged native memory wrapped in an <see cref="IDisposable"/> lease.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static NativeMemoryLease RentNative(nuint byteCount, bool zeroMemory = false)
        {
            if (byteCount == 0) return default;
            void* ptr = zeroMemory ? ZeroMemory.AllocZeroed(byteCount) : ZeroMemory.Alloc(byteCount);
            Interlocked.Add(ref _activeNativeAllocatedBytes, (long)byteCount);
            return new NativeMemoryLease(ptr, byteCount);
        }

        /// <summary>
        /// Frees a native memory pointer allocated via RentNative or ZeroMemory.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void FreeNative(void* pointer, nuint byteCount)
        {
            if (pointer == null) return;
            ZeroMemory.Free(pointer);
            Interlocked.Add(ref _activeNativeAllocatedBytes, -(long)byteCount);
        }
    }

    /// <summary>
    /// Represents an unmanaged native memory lease with deterministic lifecycle disposal.
    /// </summary>
    public readonly unsafe struct NativeMemoryLease : IDisposable
    {
        private readonly void* _pointer;
        private readonly nuint _byteCount;

        public void* Pointer => _pointer;
        public nuint ByteCount => _byteCount;
        public bool IsValid => _pointer != null;

        public NativeMemoryLease(void* pointer, nuint byteCount)
        {
            _pointer = pointer;
            _byteCount = byteCount;
        }

        public Span<byte> AsSpan()
        {
            return _pointer == null ? Span<byte>.Empty : new Span<byte>(_pointer, (int)_byteCount);
        }

        public void Dispose()
        {
            if (_pointer != null)
            {
                ZeroBufferPool.FreeNative(_pointer, _byteCount);
            }
        }
    }
}
