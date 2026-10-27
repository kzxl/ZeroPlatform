using System;

namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Bitmask flags identifying the visual rendering operations requested by a UI control.
    /// Used by <see cref="ZeroRenderAnalyzer"/> to estimate computational cost and route to CPU or GPU.
    /// </summary>
    [Flags]
    public enum RenderEffectKind : ushort
    {
        /// <summary>No active visual effects.</summary>
        None = 0,

        /// <summary>Subpixel text glyph rendering (optimal on CPU with DirectWrite/ClearType font cache).</summary>
        Text = 1 << 0,

        /// <summary>Solid flat background fill.</summary>
        FlatFill = 1 << 1,

        /// <summary>Standard 1-pixel or uniform border outline.</summary>
        Border = 1 << 2,

        /// <summary>Linear color gradient fill.</summary>
        LinearGradient = 1 << 3,

        /// <summary>Analytical or convolution drop shadow (O(N*R^2) on CPU, O(1) parallel SDF on GPU).</summary>
        DropShadow = 1 << 4,

        /// <summary>Multi-pass separable Gaussian background or backdrop blur.</summary>
        GaussianBlur = 1 << 5,

        /// <summary>Neon radial glow or bloom with exponential falloff.</summary>
        NeonGlow = 1 << 6,

        /// <summary>Bitmap resampling, 2D matrix transformation, or UV texture sampling.</summary>
        ImageTransform = 1 << 7,

        /// <summary>Complex 3D mesh, tessellation, or waveform visualization.</summary>
        ComplexMesh = 1 << 8
    }
}
