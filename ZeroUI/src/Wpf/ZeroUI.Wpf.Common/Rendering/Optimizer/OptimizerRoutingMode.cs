namespace ZeroUI.Wpf.Rendering.Optimizer
{
    /// <summary>
    /// Routing mode for <see cref="ZeroOptimizedCard"/>.
    /// </summary>
    public enum OptimizerRoutingMode
    {
        /// <summary>
        /// Automatically analyzes operation cost, surface area, and frame budget via ZeroRenderAnalyzer.
        /// </summary>
        Auto = 0,

        /// <summary>
        /// Forces native CPU rasterization (DrawingContext).
        /// </summary>
        ForceCpu = 1,

        /// <summary>
        /// Forces Direct3D 11 live GPU shader pipeline (Continuous SDF).
        /// </summary>
        ForceGpuShader = 2,

        /// <summary>
        /// Forces 9-slice cached GPU texture atlas.
        /// </summary>
        ForceGpuAtlas = 3
    }
}
