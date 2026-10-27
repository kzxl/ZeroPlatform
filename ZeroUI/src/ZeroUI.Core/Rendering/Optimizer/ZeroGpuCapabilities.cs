using System;

namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Global registry and hardware telemetry for active GPU capabilities.
    /// Provides hardware tier classification, adapter name, and dedicated VRAM metrics to <see cref="ZeroRenderAnalyzer"/>.
    /// </summary>
    public static class ZeroGpuCapabilities
    {
        public static string AdapterName { get; private set; } = "Direct3D 11 Hardware Accelerator";
        public static HardwareGpuTier CurrentTier { get; private set; } = HardwareGpuTier.Tier2_Discrete;
        public static double DedicatedVramMb { get; private set; } = 4096.0;
        public static double SharedSystemMemoryMb { get; private set; } = 8192.0;
        public static uint VendorId { get; private set; } = 0x10DE; // Default discrete profile
        public static bool IsHardwareAccelerated => CurrentTier != HardwareGpuTier.Tier0_Software;

        /// <summary>
        /// Updates the active GPU capabilities telemetry.
        /// </summary>
        public static void Configure(
            string adapterName,
            HardwareGpuTier tier,
            double dedicatedVramMb,
            double sharedMemoryMb,
            uint vendorId = 0)
        {
            AdapterName = !string.IsNullOrWhiteSpace(adapterName) ? adapterName.Trim() : "Direct3D 11 Accelerator";
            CurrentTier = tier;
            DedicatedVramMb = Math.Max(0.0, dedicatedVramMb);
            SharedSystemMemoryMb = Math.Max(0.0, sharedMemoryMb);
            VendorId = vendorId;
        }

        /// <summary>
        /// Resets GPU capabilities to default discrete profile (useful for testing).
        /// </summary>
        public static void Reset()
        {
            AdapterName = "Direct3D 11 Hardware Accelerator";
            CurrentTier = HardwareGpuTier.Tier2_Discrete;
            DedicatedVramMb = 4096.0;
            SharedSystemMemoryMb = 8192.0;
            VendorId = 0x10DE;
        }
    }
}
