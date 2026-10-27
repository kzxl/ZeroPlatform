namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Fidelity quality tier dynamically modulated by <see cref="ZeroAdaptiveRenderMonitor"/>.
    /// Adjusts shader complexity and shadow sampling passes according to real-time frame time budget.
    /// </summary>
    public enum RenderFidelityTier : byte
    {
        /// <summary>
        /// Maximum visual fidelity (120–144 FPS target). Full analytical continuous SDF, unconstrained blur radius,
        /// real-time neon bloom, and full-resolution background convolution.
        /// </summary>
        Ultra = 0,

        /// <summary>
        /// Balanced profile (60 FPS target). Leverages 9-slice texture atlas for recurring card elevations,
        /// clamping high blur radii to retain steady frame delivery.
        /// </summary>
        Balanced = 1,

        /// <summary>
        /// Power saver / Throttled fallback. Triggered when frame time budget exceeds 16.6ms or GPU is under load.
        /// Replaces live multi-pass convolutions with 9-slice cache or lightweight CPU borders.
        /// </summary>
        PowerSaver = 2
    }
}
