using System;

namespace ZeroTensor.Core.Storage
{
    /// <summary>
    /// Contract for non-CPU device storages (Direct3D 11, CUDA, Vulkan, NPU)
    /// to support bidirectional data transfer between Host (CPU) and Device (VRAM).
    /// </summary>
    public interface IDeviceStorageTransfer<T> where T : unmanaged, IEquatable<T>
    {
        /// <summary>
        /// Reads data from the device memory buffer into the host CPU span.
        /// </summary>
        void CopyToHost(int offset, int length, Span<T> destination);

        /// <summary>
        /// Writes data from the host CPU span into the device memory buffer.
        /// </summary>
        void CopyFromHost(int offset, int length, ReadOnlySpan<T> source);
    }
}
