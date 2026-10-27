using System;

namespace ZeroGraphics.Core.Math
{
    /// <summary>
    /// Exact mathematical evaluators for Signed Distance Fields (SDF), Gaussian blur kernels, and neon glow.
    /// </summary>
    public static class SdfMath
    {
        public static float EvaluateBoxSdf(float px, float py, float halfWidth, float halfHeight, float cornerRadius)
        {
            float ax = System.Math.Abs(px);
            float ay = System.Math.Abs(py);

            float bx = halfWidth - cornerRadius;
            float by = halfHeight - cornerRadius;

            float qx = System.Math.Max(0f, ax - bx);
            float qy = System.Math.Max(0f, ay - by);

            float dist = (float)System.Math.Sqrt(qx * qx + qy * qy) - cornerRadius;
            return dist;
        }

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

            float alpha = 0.5f - (dist / blurRadius);
            if (alpha < 0f) return 0f;
            if (alpha > 1f) return 1f;
            return alpha;
        }

        public static void ComputeGaussianKernel(int radius, Span<float> weights)
        {
            if (radius < 0) radius = 0;
            int kernelSize = radius * 2 + 1;
            if (weights.Length < kernelSize)
            {
                throw new ArgumentException($"Weights buffer length must be at least {kernelSize}.", nameof(weights));
            }

            float sigma = System.Math.Max(0.5f, radius / 2.0f);
            float twoSigmaSq = 2.0f * sigma * sigma;
            float sum = 0.0f;

            for (int i = 0; i < kernelSize; i++)
            {
                int x = i - radius;
                float w = (float)System.Math.Exp(-(x * x) / twoSigmaSq);
                weights[i] = w;
                sum += w;
            }

            if (sum > 0.0001f)
            {
                float invSum = 1.0f / sum;
                for (int i = 0; i < kernelSize; i++)
                {
                    weights[i] *= invSum;
                }
            }
        }

        public static float EvaluateNeonGlowIntensity(float distance, float glowRadius, float intensity = 1.0f)
        {
            if (glowRadius <= 0.001f) return 0f;
            float normDist = System.Math.Max(0f, distance) / glowRadius;
            float falloff = (float)System.Math.Exp(-System.Math.Pow(normDist, 1.35));
            return falloff * intensity;
        }
    }
}
