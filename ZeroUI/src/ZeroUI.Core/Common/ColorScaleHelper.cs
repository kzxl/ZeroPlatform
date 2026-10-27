using System;

namespace ZeroUI.Core.Common
{
    /// <summary>
    /// Pure arithmetic RGB gradient and color-scale interpolation utilities for heatmaps,
    /// sensor telemetry ribbons, and optical density scales.
    /// </summary>
    public static class ColorScaleHelper
    {
        /// <summary>
        /// Linear interpolation between two RGB color triplets.
        /// </summary>
        public static void LerpRgb(
            byte r1, byte g1, byte b1,
            byte r2, byte g2, byte b2,
            float t,
            out byte r, out byte g, out byte b)
        {
            t = Math.Max(0.0f, Math.Min(1.0f, t));
            r = (byte)(r1 + (r2 - r1) * t);
            g = (byte)(g1 + (g2 - g1) * t);
            b = (byte)(b1 + (b2 - b1) * t);
        }

        /// <summary>
        /// Interpolates a standardized 4-stop industrial heatmap color (Navy -> Emerald -> Amber -> Crimson).
        /// Value must be normalized between 0.0 and 1.0.
        /// </summary>
        public static void InterpolateHeatmapRgb(float norm, out byte r, out byte g, out byte b)
        {
            norm = Math.Max(0.0f, Math.Min(1.0f, norm));

            // Stop 0 (0.0): Deep Navy (30, 58, 138)
            // Stop 1 (0.35): Emerald (16, 185, 129)
            // Stop 2 (0.70): Amber (245, 158, 11)
            // Stop 3 (1.00): Crimson (239, 68, 68)
            if (norm < 0.35f)
            {
                float t = norm / 0.35f;
                LerpRgb(30, 58, 138, 16, 185, 129, t, out r, out g, out b);
            }
            else if (norm < 0.70f)
            {
                float t = (norm - 0.35f) / 0.35f;
                LerpRgb(16, 185, 129, 245, 158, 11, t, out r, out g, out b);
            }
            else
            {
                float t = (norm - 0.70f) / 0.30f;
                LerpRgb(245, 158, 11, 239, 68, 68, t, out r, out g, out b);
            }
        }
        /// <summary>
        /// Interpolates a standardized 4-stop industrial heatmap color (Navy -> Emerald -> Amber -> Crimson).
        /// Returns (r, g, b) tuple for convenience.
        /// </summary>
        public static (byte r, byte g, byte b) InterpolateHeatmapRgb(float norm)
        {
            InterpolateHeatmapRgb(norm, out byte r, out byte g, out byte b);
            return (r, g, b);
        }
    }
}
