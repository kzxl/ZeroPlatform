using System;

namespace ZeroGraphics.Imaging.Core
{
    /// <summary>
    /// Execution backend preference.
    /// </summary>
    public enum ExecutionBackend
    {
        /// <summary>
        /// Automatically picks the most optimal backend based on image dimensions, operation complexity, and data residency.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Force execution on CPU using AVX2/AVX-512 SIMD vectorization.
        /// </summary>
        CpuSimd = 1,

        /// <summary>
        /// Force execution on GPU using DirectCompute (CS 5.0) or Pixel Shaders.
        /// </summary>
        GpuCompute = 2
    }

    /// <summary>
    /// Intelligent Heuristic Dispatcher and Auto-Backend Selector.
    /// Solves the Data Residency Paradox: avoids offloading small workloads to GPU where PCIe transfer latency exceeds compute gain.
    /// </summary>
    public static class AutoBackendSelector
    {
        /// <summary>
        /// Pixel count threshold below which single-pass CPU SIMD typically outperforms GPU due to PCIe DMA latency (~786K pixels / ~1024x768).
        /// </summary>
        public const int DefaultSinglePassCpuCutoffPixels = 1024 * 768;

        /// <summary>
        /// Pixel count threshold for multi-pass chains where GPU wins even on medium images (~300K pixels / ~640x480).
        /// </summary>
        public const int DefaultMultiPassGpuThresholdPixels = 640 * 480;

        /// <summary>
        /// Determines the optimal execution backend for an image processing task.
        /// </summary>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="isAlreadyResidentOnGpu">True if source data is already resident in GPU VRAM.</param>
        /// <param name="passCount">Number of chained operations/passes.</param>
        /// <param name="preference">Explicit user backend preference.</param>
        /// <returns>Resolved execution backend (CpuSimd or GpuCompute).</returns>
        public static ExecutionBackend ResolveBackend(
            int width,
            int height,
            bool isAlreadyResidentOnGpu = false,
            int passCount = 1,
            ExecutionBackend preference = ExecutionBackend.Auto)
        {
            if (preference == ExecutionBackend.CpuSimd) return ExecutionBackend.CpuSimd;
            if (preference == ExecutionBackend.GpuCompute) return ExecutionBackend.GpuCompute;

            // 1. If data already resides in VRAM, avoid reading back to CPU
            if (isAlreadyResidentOnGpu)
            {
                return ExecutionBackend.GpuCompute;
            }

            int pixelCount = width * height;

            // 2. Chained operations amortize PCIe transfer overhead across multiple passes
            if (passCount >= 3 && pixelCount >= DefaultMultiPassGpuThresholdPixels)
            {
                return ExecutionBackend.GpuCompute;
            }

            // 3. For single or dual pass operations on small images, CPU SIMD avoids ~3ms DMA latency
            if (pixelCount < DefaultSinglePassCpuCutoffPixels)
            {
                return ExecutionBackend.CpuSimd;
            }

            // 4. Large images saturate GPU compute units and memory bus bandwidth
            return ExecutionBackend.GpuCompute;
        }
    }
}
