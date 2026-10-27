namespace ZeroTensor.Core.Storage
{
    /// <summary>
    /// Identifies the physical device or storage domain of a tensor buffer.
    /// </summary>
    public enum DeviceType
    {
        /// <summary>
        /// Host CPU memory (managed array, unmanaged native heap, or memory-mapped file).
        /// </summary>
        Cpu = 0,

        /// <summary>
        /// NVIDIA CUDA hardware acceleration buffer.
        /// </summary>
        Cuda = 1,

        /// <summary>
        /// Microsoft Direct3D 11 Compute StructuredBuffer.
        /// </summary>
        Direct3D11 = 2,

        /// <summary>
        /// Khronos Vulkan Compute StorageBuffer.
        /// </summary>
        Vulkan = 3
    }
}
