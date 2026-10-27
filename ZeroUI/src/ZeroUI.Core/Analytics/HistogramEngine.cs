using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Represents an individual frequency bin in a statistical histogram.
    /// </summary>
    public class HistogramBin
    {
        public double LowerBound { get; }
        public double UpperBound { get; }
        public int Count { get; }
        public double RelativeFrequency { get; }
        public double Density { get; }
        public double CenterX => (LowerBound + UpperBound) / 2.0;

        public HistogramBin(double lowerBound, double upperBound, int count, double relativeFrequency, double density)
        {
            LowerBound = lowerBound;
            UpperBound = upperBound;
            Count = count;
            RelativeFrequency = relativeFrequency;
            Density = density;
        }

        public override string ToString() => $"[{LowerBound:F2} - {UpperBound:F2}): {Count} ({RelativeFrequency:P1})";
    }

    /// <summary>
    /// Point representing the evaluated Gaussian normal distribution probability density function (PDF).
    /// </summary>
    public struct NormalCurvePoint
    {
        public double X { get; }
        public double Density { get; }
        public double ScaledCount { get; }

        public NormalCurvePoint(double x, double density, double scaledCount)
        {
            X = x;
            Density = density;
            ScaledCount = scaledCount;
        }
    }

    /// <summary>
    /// Comprehensive outcome of statistical histogram computation, including distribution moments and PDF curve points.
    /// </summary>
    public class HistogramResult
    {
        public IReadOnlyList<HistogramBin> Bins { get; }
        public IReadOnlyList<NormalCurvePoint> GaussianCurve { get; }
        public double Min { get; }
        public double Max { get; }
        public double Mean { get; }
        public double StandardDeviation { get; }
        public int TotalCount { get; }
        public double BinWidth { get; }
        public int MaxBinCount { get; }

        public HistogramResult(
            IReadOnlyList<HistogramBin> bins,
            IReadOnlyList<NormalCurvePoint> gaussianCurve,
            double min,
            double max,
            double mean,
            double standardDeviation,
            int totalCount,
            double binWidth,
            int maxBinCount)
        {
            Bins = bins ?? Array.Empty<HistogramBin>();
            GaussianCurve = gaussianCurve ?? Array.Empty<NormalCurvePoint>();
            Min = min;
            Max = max;
            Mean = mean;
            StandardDeviation = standardDeviation;
            TotalCount = totalCount;
            BinWidth = binWidth;
            MaxBinCount = maxBinCount;
        }
    }

    /// <summary>
    /// Core calculation engine implementing continuous data binning, statistical moments,
    /// and Gaussian normal distribution curve generation without any UI dependency.
    /// </summary>
    public static class HistogramEngine
    {
        /// <summary>
        /// Computes frequency bins and normal distribution curve for a continuous numerical series.
        /// </summary>
        /// <param name="data">The raw numeric values.</param>
        /// <param name="binCount">Specified bin count; if 0 or negative, automatically determined using Sturges' formula.</param>
        /// <param name="curveSamplePoints">Number of interpolation points for the Gaussian normal distribution curve.</param>
        public static HistogramResult Compute(
            IEnumerable<double> data,
            int binCount = 0,
            int curveSamplePoints = 100)
        {
            if (data == null)
            {
                return new HistogramResult(Array.Empty<HistogramBin>(), Array.Empty<NormalCurvePoint>(), 0, 0, 0, 0, 0, 0, 0);
            }

            var validValues = data.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
            int n = validValues.Count;
            if (n == 0)
            {
                return new HistogramResult(Array.Empty<HistogramBin>(), Array.Empty<NormalCurvePoint>(), 0, 0, 0, 0, 0, 0, 0);
            }

            double min = validValues.Min();
            double max = validValues.Max();
            double sum = validValues.Sum();
            double mean = sum / n;

            double varianceSum = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = validValues[i] - mean;
                varianceSum += diff * diff;
            }
            double variance = n > 1 ? varianceSum / (n - 1) : 0.0;
            double stdDev = Math.Sqrt(variance);

            // Handle edge case where all values are identical
            if (Math.Abs(max - min) < 1e-12)
            {
                min -= 1.0;
                max += 1.0;
            }

            // Determine bin count
            int k = binCount;
            if (k <= 0)
            {
                // Sturges' rule: k = ceil(log2(n) + 1)
                k = (int)Math.Ceiling(Math.Log(n, 2.0) + 1.0);
                if (k < 5) k = 5;
                if (k > 50) k = 50;
            }

            double range = max - min;
            double binWidth = range / k;

            int[] counts = new int[k];
            for (int i = 0; i < n; i++)
            {
                double v = validValues[i];
                int binIndex = (int)((v - min) / binWidth);
                if (binIndex >= k) binIndex = k - 1;
                if (binIndex < 0) binIndex = 0;
                counts[binIndex]++;
            }

            int maxCount = 0;
            var bins = new List<HistogramBin>(k);
            for (int i = 0; i < k; i++)
            {
                int c = counts[i];
                if (c > maxCount) maxCount = c;
                double lower = min + i * binWidth;
                double upper = (i == k - 1) ? max : lower + binWidth;
                double relFreq = (double)c / n;
                double density = binWidth > 0 ? relFreq / binWidth : 0;
                bins.Add(new HistogramBin(lower, upper, c, relFreq, density));
            }

            // Generate Gaussian Normal Distribution PDF points if stdDev > 0
            var curve = new List<NormalCurvePoint>();
            if (stdDev > 1e-9 && curveSamplePoints > 1)
            {
                double curveMin = min - binWidth * 0.5;
                double curveMax = max + binWidth * 0.5;
                double step = (curveMax - curveMin) / (curveSamplePoints - 1);
                double invStdSqrt2Pi = 1.0 / (stdDev * Math.Sqrt(2.0 * Math.PI));
                double twoVar = 2.0 * variance;

                for (int i = 0; i < curveSamplePoints; i++)
                {
                    double x = curveMin + i * step;
                    double diff = x - mean;
                    double pdf = invStdSqrt2Pi * Math.Exp(-(diff * diff) / twoVar);
                    double scaledCount = pdf * n * binWidth;
                    curve.Add(new NormalCurvePoint(x, pdf, scaledCount));
                }
            }

            return new HistogramResult(bins, curve, min, max, mean, stdDev, n, binWidth, maxCount);
        }
    }
}
