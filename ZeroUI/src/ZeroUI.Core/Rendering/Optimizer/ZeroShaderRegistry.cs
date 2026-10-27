using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace ZeroUI.Core.Rendering.Optimizer
{
    /// <summary>
    /// Metadata descriptor for a pre-compiled or analytical procedural shader.
    /// </summary>
    public sealed class ShaderDescriptor
    {
        public string ShaderId { get; }
        public string Name { get; }
        public string Category { get; }
        public int ComplexityRating { get; }
        public bool IsGpuAccelerated { get; }
        public string Description { get; }

        public ShaderDescriptor(
            string shaderId,
            string name,
            string category,
            int complexityRating,
            bool isGpuAccelerated,
            string description)
        {
            ShaderId = shaderId ?? throw new ArgumentNullException(nameof(shaderId));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            ComplexityRating = complexityRating;
            IsGpuAccelerated = isGpuAccelerated;
            Description = description ?? string.Empty;
        }
    }

    /// <summary>
    /// Central registry of pre-compiled and analytical shaders for ZeroUI.
    /// Eliminates runtime HLSL D3DCompile stalls and provides exact mathematical analytical evaluators
    /// for Signed Distance Fields (SDF), Gaussian kernels, and neon glow falloffs.
    /// </summary>
    public static class ZeroShaderRegistry
    {
        private static readonly ConcurrentDictionary<string, ShaderDescriptor> _shaders =
            new ConcurrentDictionary<string, ShaderDescriptor>(StringComparer.OrdinalIgnoreCase);

        static ZeroShaderRegistry()
        {
            // Register standard analytical and procedural shaders
            Register(new ShaderDescriptor(
                "AnalyticalSdfBoxShadow",
                "Analytical SDF Rounded Box Shadow",
                "Shadow",
                complexityRating: 2,
                isGpuAccelerated: true,
                "Analytical signed distance field rounded box shadow. Evaluates zero-allocation blur directly per-pixel."));

            Register(new ShaderDescriptor(
                "SdfBoxShadowAtlas",
                "9-Slice Cached SDF Shadow Atlas",
                "Shadow",
                complexityRating: 1,
                isGpuAccelerated: true,
                "9-slice instanced texture atlas lookup for recurring card elevations and corner radii."));

            Register(new ShaderDescriptor(
                "DualPassGaussianBlur",
                "Separable Dual-Pass Gaussian Blur",
                "Blur",
                complexityRating: 3,
                isGpuAccelerated: true,
                "Separable horizontal + vertical Gaussian convolution with downsampled ping-pong targets."));

            Register(new ShaderDescriptor(
                "NeonGlowSdf",
                "Neon Radial Bloom & Glow",
                "Glow",
                complexityRating: 2,
                isGpuAccelerated: true,
                "Exponential distance-decay radial bloom with HDR tone mapping."));

            Register(new ShaderDescriptor(
                "GradientMesh",
                "Bilinear Gradient Mesh",
                "Gradient",
                complexityRating: 1,
                isGpuAccelerated: true,
                "4-corner bilinear gradient interpolator for vibrant cards and surfaces."));
        }

        public static void Register(ShaderDescriptor descriptor)
        {
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            _shaders[descriptor.ShaderId] = descriptor;
        }

        public static bool TryGet(string shaderId, out ShaderDescriptor? descriptor)
        {
            return _shaders.TryGetValue(shaderId, out descriptor);
        }

        public static IReadOnlyCollection<ShaderDescriptor> GetAll() => (IReadOnlyCollection<ShaderDescriptor>)_shaders.Values;

        // =========================================================================
        // MATHEMATICAL ANALYTICAL EVALUATORS (CPU Fallback & Precision Math)
        // =========================================================================

        /// <summary>
        /// Evaluates the Signed Distance Field (SDF) of a 2D rounded rectangle at point (px, py).
        /// Center of the box is at (0, 0).
        /// Formula: d = length(max(abs(p) - (b - r), 0.0)) - r
        /// </summary>
        /// <param name="px">X coordinate relative to box center.</param>
        /// <param name="py">Y coordinate relative to box center.</param>
        /// <param name="halfWidth">Half the width of the box.</param>
        /// <param name="halfHeight">Half the height of the box.</param>
        /// <param name="cornerRadius">Corner radius.</param>
        /// <returns>Signed distance to the boundary (negative inside, positive outside).</returns>
        public static float EvaluateBoxSdf(float px, float py, float halfWidth, float halfHeight, float cornerRadius)
        {
            float ax = Math.Abs(px);
            float ay = Math.Abs(py);

            float bx = halfWidth - cornerRadius;
            float by = halfHeight - cornerRadius;

            float qx = Math.Max(0f, ax - bx);
            float qy = Math.Max(0f, ay - by);

            float dist = (float)Math.Sqrt(qx * qx + qy * qy) - cornerRadius;
            return dist;
        }

        /// <summary>
        /// Computes analytical drop shadow alpha (0.0 to 1.0) using continuous SDF.
        /// </summary>
        public static float EvaluateBoxShadowAlpha(
            float px, float py,
            float halfWidth, float halfHeight,
            float cornerRadius, float blurRadius)
        {
            float dist = EvaluateBoxSdf(px, py, halfWidth, halfHeight, cornerRadius);
            if (blurRadius <= 0.001f)
            {
                return dist <= 0f ? 1.0f : 0.0f;
            }

            // Smoothstep or linear falloff over blur radius
            float alpha = 0.5f - (dist / blurRadius);
            if (alpha < 0f) return 0f;
            if (alpha > 1f) return 1f;
            return alpha;
        }

        /// <summary>
        /// Computes normalized 1D Gaussian kernel weights for separable blur convolution.
        /// </summary>
        /// <param name="radius">Blur radius in pixels (kernel size = 2 * radius + 1).</param>
        /// <param name="weights">Output buffer to receive normalized weights.</param>
        public static void ComputeGaussianKernel(int radius, Span<float> weights)
        {
            if (radius < 0) radius = 0;
            int kernelSize = radius * 2 + 1;
            if (weights.Length < kernelSize)
            {
                throw new ArgumentException($"Weights buffer length must be at least {kernelSize}.", nameof(weights));
            }

            float sigma = Math.Max(0.5f, radius / 2.0f);
            float twoSigmaSq = 2.0f * sigma * sigma;
            float sum = 0.0f;

            for (int i = 0; i < kernelSize; i++)
            {
                int x = i - radius;
                float w = (float)Math.Exp(-(x * x) / twoSigmaSq);
                weights[i] = w;
                sum += w;
            }

            // Normalize weights
            if (sum > 0.0001f)
            {
                float invSum = 1.0f / sum;
                for (int i = 0; i < kernelSize; i++)
                {
                    weights[i] *= invSum;
                }
            }
        }

        /// <summary>
        /// Evaluates radial exponential neon glow intensity (0.0 to 1.0+) given distance from emitter.
        /// </summary>
        public static float EvaluateNeonGlowIntensity(float distance, float glowRadius, float intensity = 1.0f)
        {
            if (glowRadius <= 0.001f) return 0f;
            float normDist = Math.Max(0f, distance) / glowRadius;
            float falloff = (float)Math.Exp(-Math.Pow(normDist, 1.35));
            return falloff * intensity;
        }
    }
}
