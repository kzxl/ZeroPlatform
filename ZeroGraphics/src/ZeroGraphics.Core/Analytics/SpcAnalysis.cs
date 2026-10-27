using System;
using System.Collections.Generic;

namespace ZeroGraphics.Core.Analytics
{
    /// <summary>
    /// Statistical Process Control (SPC) analysis summary containing standard control limits and capability metrics.
    /// </summary>
    public struct SpcSummary
    {
        public int Count;
        public float Mean;
        public float Variance;
        public float StdDev;
        public float Min;
        public float Max;
        public float Range;

        // Control Limits (3-Sigma)
        public float CenterLine => Mean;
        public float UCL => Mean + 3.0f * StdDev;
        public float LCL => Mean - 3.0f * StdDev;

        // 1-Sigma & 2-Sigma Zones
        public float ZoneA_Upper => Mean + 3.0f * StdDev;
        public float ZoneB_Upper => Mean + 2.0f * StdDev;
        public float ZoneC_Upper => Mean + 1.0f * StdDev;
        public float ZoneC_Lower => Mean - 1.0f * StdDev;
        public float ZoneB_Lower => Mean - 2.0f * StdDev;
        public float ZoneA_Lower => Mean - 3.0f * StdDev;

        // Process Capability Indices (Nullable if USL/LSL not specified)
        public float? Cp;
        public float? Cpk;
        public float? Cpu;
        public float? Cpl;

        public override string ToString() => $"Count: {Count}, Mean: {Mean:F3}, StdDev: {StdDev:F3}, UCL: {UCL:F3}, LCL: {LCL:F3}, Cpk: {(Cpk.HasValue ? Cpk.Value.ToString("F3") : "N/A")}";
    }

    /// <summary>
    /// Represents a point on a Gaussian Bell Curve for distribution histograms.
    /// </summary>
    public struct GaussianPoint
    {
        public float X;
        public float Density;

        public GaussianPoint(float x, float density)
        {
            X = x;
            Density = density;
        }
    }

    /// <summary>
    /// Enterprise Statistical Process Control (SPC) math engine.
    /// Computes process capability, control boundaries, and Gaussian bell curves with zero external dependencies.
    /// </summary>
    public static class SpcAnalysis
    {
        private static readonly float InvSqrt2Pi = (float)(1.0 / global::System.Math.Sqrt(2.0 * global::System.Math.PI));

        /// <summary>
        /// Calculates complete SPC metrics for an array or list of measurement samples.
        /// </summary>
        /// <param name="samples">Array of numerical measurements.</param>
        /// <param name="usl">Optional Upper Specification Limit defined by engineering drawings.</param>
        /// <param name="lsl">Optional Lower Specification Limit defined by engineering drawings.</param>
        public static SpcSummary Calculate(float[] samples, float? usl = null, float? lsl = null)
        {
            if (samples == null || samples.Length == 0)
                throw new ArgumentException("Samples collection cannot be null or empty.", nameof(samples));

            int n = samples.Length;
            float sum = 0.0f;
            float min = samples[0];
            float max = samples[0];

            for (int i = 0; i < n; i++)
            {
                float val = samples[i];
                sum += val;
                if (val < min) min = val;
                if (val > max) max = val;
            }

            float mean = sum / n;

            // Two-pass variance to minimize numerical cancellation
            float sumSqDiff = 0.0f;
            for (int i = 0; i < n; i++)
            {
                float diff = samples[i] - mean;
                sumSqDiff += diff * diff;
            }

            float variance = n > 1 ? sumSqDiff / (n - 1) : 0.0f;
            float stdDev = (float)global::System.Math.Sqrt(variance);

            SpcSummary summary = new SpcSummary
            {
                Count = n,
                Mean = mean,
                Variance = variance,
                StdDev = stdDev,
                Min = min,
                Max = max,
                Range = max - min
            };

            // Compute Capability Indices (Cp, Cpk) if specs are provided
            if (usl.HasValue && lsl.HasValue && stdDev > 0.000001f)
            {
                summary.Cp = (usl.Value - lsl.Value) / (6.0f * stdDev);
                summary.Cpu = (usl.Value - mean) / (3.0f * stdDev);
                summary.Cpl = (mean - lsl.Value) / (3.0f * stdDev);
                summary.Cpk = global::System.Math.Min(summary.Cpu.Value, summary.Cpl.Value);
            }

            return summary;
        }

        /// <summary>
        /// Generates discrete points for a Gaussian (Normal) Distribution curve over the range [minX, maxX].
        /// </summary>
        /// <param name="mean">Mean of the distribution.</param>
        /// <param name="stdDev">Standard deviation of the distribution.</param>
        /// <param name="pointCount">Number of interpolation points.</param>
        /// <param name="minX">Starting X value (defaults to mean - 4*sigma if not specified).</param>
        /// <param name="maxX">Ending X value (defaults to mean + 4*sigma if not specified).</param>
        public static GaussianPoint[] GenerateGaussianCurve(float mean, float stdDev, int pointCount = 100, float? minX = null, float? maxX = null)
        {
            if (pointCount < 2) pointCount = 2;
            if (stdDev <= 0.000001f) stdDev = 0.000001f;

            float x0 = minX ?? (mean - 4.0f * stdDev);
            float x1 = maxX ?? (mean + 4.0f * stdDev);
            float step = (x1 - x0) / (pointCount - 1);

            float invStdDev = 1.0f / stdDev;
            float factor = InvSqrt2Pi * invStdDev;

            GaussianPoint[] points = new GaussianPoint[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                float x = x0 + i * step;
                float z = (x - mean) * invStdDev;
                float density = factor * (float)global::System.Math.Exp(-0.5f * z * z);
                points[i] = new GaussianPoint(x, density);
            }

            return points;
        }
    }
}
