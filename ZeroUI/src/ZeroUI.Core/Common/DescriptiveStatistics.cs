using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Common
{
    /// <summary>
    /// High-performance, zero-allocation descriptive statistical calculations for industrial sensors,
    /// assays, and laboratory measurements.
    /// </summary>
    public static class DescriptiveStatistics
    {
        /// <summary>
        /// Computes Mean, Sample Standard Deviation (SD), and Coefficient of Variation (%CV) for a sequence of values.
        /// </summary>
        public static bool TryCalculate(IReadOnlyList<double> values, out double mean, out double sd, out double cvPct)
        {
            mean = 0.0;
            sd = 0.0;
            cvPct = 0.0;

            if (values == null || values.Count == 0)
                return false;

            int count = values.Count;
            double sum = 0.0;
            for (int i = 0; i < count; i++)
            {
                sum += values[i];
            }

            mean = sum / count;

            if (count > 1)
            {
                double sumSq = 0.0;
                for (int i = 0; i < count; i++)
                {
                    double diff = values[i] - mean;
                    sumSq += diff * diff;
                }
                sd = Math.Sqrt(sumSq / (count - 1));
                cvPct = Math.Abs(mean) > 1e-12 ? (sd / mean) * 100.0 : 0.0;
            }

            return true;
        }

        /// <summary>
        /// Finds the minimum and maximum values in a sequence in a single pass.
        /// </summary>
        public static bool TryGetMinMax(IReadOnlyList<double> values, out double min, out double max)
        {
            min = double.MaxValue;
            max = double.MinValue;

            if (values == null || values.Count == 0)
            {
                min = 0.0;
                max = 0.0;
                return false;
            }

            int count = values.Count;
            for (int i = 0; i < count; i++)
            {
                double v = values[i];
                if (v < min) min = v;
                if (v > max) max = v;
            }

            return true;
        }
    }
}
