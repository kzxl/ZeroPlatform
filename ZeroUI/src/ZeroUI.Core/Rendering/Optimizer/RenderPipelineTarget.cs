namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Target execution pipeline determined by <see cref="ZeroRenderAnalyzer"/>.
    /// </summary>
    public enum RenderPipelineTarget : byte
    {
        /// <summary>
        /// Native CPU 2D graphics pipeline (DirectWrite / DrawingContext / GDI+).
        /// Zero GPU context-switch overhead; optimal for text glyphs, borders, and flat fills.
        /// </summary>
        Cpu = 0,

        /// <summary>
        /// Live Direct3D 11 GPU Shader pipeline.
        /// Evaluates continuous analytical Signed Distance Fields (SDF) or multi-pass convolutions per frame.
        /// </summary>
        GpuShader = 1,

        /// <summary>
        /// Cached 9-slice GPU texture atlas.
        /// Turns repetitive drop shadows and rounded corners into a single instanced texture quad lookup.
        /// </summary>
        GpuAtlas = 2,

        /// <summary>
        /// Split hybrid pipeline: Heavy visual effects (SDF shadow, glow, blur) execute on GPU,
        /// while interactive text and glyphs render on CPU with subpixel ClearType.
        /// </summary>
        Hybrid = 3
    }
}
