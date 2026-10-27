namespace ZeroGraphics.Core.Telemetry
{
    /// <summary>
    /// Classification tier of the active graphics processing hardware.
    /// </summary>
    public enum HardwareGpuTier : byte
    {
        /// <summary>
        /// Software rasterizer (WARP) or basic display adapter.
        /// </summary>
        Tier0_Software = 0,

        /// <summary>
        /// Integrated graphics processor (Intel UHD/Iris Xe, AMD Vega APU).
        /// </summary>
        Tier1_Integrated = 1,

        /// <summary>
        /// Dedicated discrete GPU (NVIDIA GeForce/RTX, AMD Radeon RX, Intel Arc).
        /// </summary>
        Tier2_Discrete = 2
    }

    /// <summary>
    /// Global registry and hardware telemetry for active GPU capabilities.
    /// </summary>
    public static class GpuCapabilities
    {
        public static string AdapterName { get; private set; } = "Direct3D 11 Hardware Accelerator";
        public static HardwareGpuTier CurrentTier { get; private set; } = HardwareGpuTier.Tier2_Discrete;
        public static double DedicatedVramMb { get; private set; } = 4096.0;
        public static double SharedSystemMemoryMb { get; private set; } = 8192.0;
        public static uint VendorId { get; private set; } = 0x10DE;
        public static bool IsHardwareAccelerated => CurrentTier != HardwareGpuTier.Tier0_Software;

        public static void Configure(
            string adapterName,
            HardwareGpuTier tier,
            double dedicatedVramMb,
            double sharedMemoryMb,
            uint vendorId = 0)
        {
            AdapterName = !string.IsNullOrWhiteSpace(adapterName) ? adapterName.Trim() : "Direct3D 11 Accelerator";
            CurrentTier = tier;
            DedicatedVramMb = System.Math.Max(0.0, dedicatedVramMb);
            SharedSystemMemoryMb = System.Math.Max(0.0, sharedMemoryMb);
            VendorId = vendorId;
        }

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
