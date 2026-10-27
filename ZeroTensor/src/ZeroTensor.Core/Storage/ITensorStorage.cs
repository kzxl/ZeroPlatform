using System;

namespace ZeroTensor.Core.Storage
{
    /// <summary>
    /// Represents the foundational memory storage abstraction for a tensor.
    /// Decouples tensor multidimensional geometry from physical memory location (managed heap, native unmanaged, memory-mapped file, or GPU buffer).
    /// </summary>
    /// <typeparam name="T">Unmanaged scalar type.</typeparam>
    public interface ITensorStorage<T> : IDisposable where T : unmanaged, IEquatable<T>
    {
        /// <summary>
        /// Gets the device domain where this storage resides.
        /// </summary>
        DeviceType Device { get; }

        /// <summary>
        /// Gets the total element capacity of this storage.
        /// </summary>
        int Length { get; }

        /// <summary>
        /// Gets whether this storage is directly accessible from CPU host memory.
        /// </summary>
        bool IsCpuAccessible { get; }

        /// <summary>
        /// Gets whether this storage has been disposed.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets a direct unmanaged pointer to the first element in memory, or null if not unmanaged or not CPU-accessible.
        /// </summary>
        unsafe T* UnsafePointer { get; }

        /// <summary>
        /// Returns a writable Span over the specified range.
        /// </summary>
        Span<T> AsSpan(int offset, int length);

        /// <summary>
        /// Returns a ReadOnlySpan over the specified range.
        /// </summary>
        ReadOnlySpan<T> AsReadOnlySpan(int offset, int length);

        /// <summary>
        /// Gets a reference to the element at the specified offset.
        /// </summary>
        ref T GetPinnableReference(int offset);

        /// <summary>
        /// Attempts to get the underlying managed array if backed by one.
        /// </summary>
        /// <param name="arrayOffset">Base offset inside the returned array.</param>
        /// <returns>The managed array, or null if off-heap/native/device memory.</returns>
        T[]? TryGetArray(out int arrayOffset);
    }
}
