using System;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Encapsulates calculated Statistical Process Control (SPC) limits and process capability metrics.
    /// Immutable, stack-friendly readonly struct (zero GC allocation).
    /// </summary>
    public readonly struct SpcControlLimits
    {
        public double Mean { get; }
        public double StandardDeviation { get; }
        public double Ucl { get; }
        public double Lcl { get; }
        public double? Usl { get; }
        public double? Lsl { get; }
        public double Target { get; }
        public double Cp { get; }
        public double Cpk { get; }
        public int TotalSamples { get; }
        public int OutOfControlCount { get; }

        public bool HasSpecLimits => Usl.HasValue || Lsl.HasValue;

        public SpcControlLimits(
            double mean,
            double standardDeviation,
            double ucl,
            double lcl,
            double? usl,
            double? lsl,
            double target,
            double cp,
            double cpk,
            int totalSamples,
            int outOfControlCount)
        {
            Mean = mean;
            StandardDeviation = standardDeviation;
            Ucl = ucl;
            Lcl = lcl;
            Usl = usl;
            Lsl = lsl;
            Target = target;
            Cp = cp;
            Cpk = cpk;
            TotalSamples = totalSamples;
            OutOfControlCount = outOfControlCount;
        }
    }

    /// <summary>
    /// High-performance, zero-allocation Statistical Process Control (SPC) calculation engine.
    /// Computes sample mean, standard deviation, 3-sigma control limits (UCL/LCL),
    /// and Six Sigma process capability metrics (Cp, Cpk) directly over ReadOnlySpan<double>.
    /// </summary>
    public static class SpcStatisticsCalculator
    {
        /// <summary>
        /// Computes SPC control limits and capability indices for the given sample dataset.
        /// Zero heap allocation on hot analysis paths.
        /// </summary>
        /// <param name="values">Sample values span.</param>
        /// <param name="usl">Optional Upper Specification Limit (customer spec).</param>
        /// <param name="lsl">Optional Lower Specification Limit (customer spec).</param>
        /// <param name="target">Optional nominal target value (defaults to sample mean if omitted).</param>
        /// <param name="sigmaMultiplier">Sigma multiplier for control limits (standard is 3.0 for 99.73% coverage).</param>
        /// <returns>Calculated SPC limits struct.</returns>
        public static SpcControlLimits Calculate(
            ReadOnlySpan<double> values,
            double? usl = null,
            double? lsl = null,
            double? target = null,
            double sigmaMultiplier = 3.0)
        {
            if (values.IsEmpty)
            {
                return new SpcControlLimits(0, 0, 0, 0, usl, lsl, target ?? 0, 0, 0, 0, 0);
            }

            int n = values.Length;
            if (n == 1)
            {
                double singleVal = values[0];
                double singleTarget = target ?? singleVal;
                return new SpcControlLimits(singleVal, 0, singleVal, singleVal, usl, lsl, singleTarget, 0, 0, 1, 0);
            }

            // 1. Calculate Sample Mean
            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                sum += values[i];
            }
            double mean = sum / n;

            // 2. Calculate Sample Standard Deviation (Bessel's correction n-1)
            double sumSquares = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = values[i] - mean;
                sumSquares += diff * diff;
            }
            double stdev = Math.Sqrt(sumSquares / (n - 1));

            // 3. Control Limits
            double spread = sigmaMultiplier * stdev;
            double ucl = mean + spread;
            double lcl = mean - spread;
            double actualTarget = target ?? mean;

            // 4. Out-of-control point counts (points beyond UCL or LCL)
            int outCount = 0;
            for (int i = 0; i < n; i++)
            {
                double v = values[i];
                if (v > ucl || v < lcl)
                {
                    outCount++;
                }
            }

            // 5. Capability Indices (Cp, Cpk)
            double cp = 0.0;
            double cpk = 0.0;
            if (stdev > 1e-12)
            {
                if (usl.HasValue && lsl.HasValue && usl.Value > lsl.Value)
                {
                    cp = (usl.Value - lsl.Value) / (6.0 * stdev);
                    double cpu = (usl.Value - mean) / (3.0 * stdev);
                    double cpl = (mean - lsl.Value) / (3.0 * stdev);
                    cpk = Math.Min(cpu, cpl);
                }
                else if (usl.HasValue)
                {
                    cpk = (usl.Value - mean) / (3.0 * stdev);
                    cp = cpk;
                }
                else if (lsl.HasValue)
                {
                    cpk = (mean - lsl.Value) / (3.0 * stdev);
                    cp = cpk;
                }
            }

            return new SpcControlLimits(
                mean: mean,
                standardDeviation: stdev,
                ucl: ucl,
                lcl: lcl,
                usl: usl,
                lsl: lsl,
                target: actualTarget,
                cp: cp,
                cpk: cpk,
                totalSamples: n,
                outOfControlCount: outCount);
        }
    }
}
