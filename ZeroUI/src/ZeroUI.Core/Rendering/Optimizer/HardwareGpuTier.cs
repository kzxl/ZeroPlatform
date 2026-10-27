namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Classification tier of the active graphics processing hardware.
    /// Used by <see cref="ZeroRenderAnalyzer"/> to adjust shader dispatch cost curves.
    /// </summary>
    public enum HardwareGpuTier : byte
    {
        /// <summary>
        /// Software rasterizer (WARP) or basic display adapter.
        /// Higher context-switch cost; optimizer heavily prefers CPU or 9-slice atlas.
        /// </summary>
        Tier0_Software = 0,

        /// <summary>
        /// Integrated graphics processor (Intel UHD/Iris Xe, AMD Radeon Vega APU).
        /// Shared system memory; ideal for 60 FPS standard analytical SDF shaders and atlas batching.
        /// </summary>
        Tier1_Integrated = 1,

        /// <summary>
        /// Dedicated discrete GPU (NVIDIA GeForce/RTX, AMD Radeon RX, Intel Arc) with dedicated VRAM.
        /// Massive parallel ALU capacity; optimizer promotes unconstrained live shaders and 144 FPS delivery.
        /// </summary>
        Tier2_Discrete = 2
    }
}
