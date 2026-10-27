using System;
using System.Collections.Generic;

namespace ZeroSignal.Core.Wavelets
{
    public enum WaveletType
    {
        Haar,
        Db2,
        Db4
    }

    /// <summary>
    /// Discrete Wavelet Transform (DWT), Inverse DWT (IDWT), and multi-scale Wavelet Denoising
    /// using orthonormal Mallat pyramid decomposition with periodic boundary extension.
    /// </summary>
    public static class Wavelet
    {
        private static readonly double Sqrt2 = Math.Sqrt(2.0);

        // Haar coefficients
        private static readonly double[] Haar_H = { 1.0 / Sqrt2, 1.0 / Sqrt2 };
        private static readonly double[] Haar_G = { 1.0 / Sqrt2, -1.0 / Sqrt2 };

        // Daubechies 2 (db2 - 4 taps)
        private static readonly double[] Db2_H = {
            (1.0 + Math.Sqrt(3.0)) / (4.0 * Sqrt2),
            (3.0 + Math.Sqrt(3.0)) / (4.0 * Sqrt2),
            (3.0 - Math.Sqrt(3.0)) / (4.0 * Sqrt2),
            (1.0 - Math.Sqrt(3.0)) / (4.0 * Sqrt2)
        };
        private static readonly double[] Db2_G = ComputeHighPass(Db2_H);

        // Daubechies 4 (db4 - 8 taps)
        private static readonly double[] Db4_H = {
             0.230377813308896,
             0.714846570552915,
             0.630880767929859,
            -0.027983769416860,
            -0.187034811718881,
             0.030841381835561,
             0.032883011666885,
            -0.010597401785069
        };
        private static readonly double[] Db4_G = ComputeHighPass(Db4_H);

        private static double[] ComputeHighPass(double[] h)
        {
            int n = h.Length;
            double[] g = new double[n];
            for (int k = 0; k < n; k++)
            {
                // g[k] = (-1)^k * h[n - 1 - k]
                double sign = (k % 2 == 0) ? 1.0 : -1.0;
                g[k] = sign * h[n - 1 - k];
            }
            return g;
        }

        public static (double[] h, double[] g) GetFilters(WaveletType type)
        {
            return type switch
            {
                WaveletType.Haar => (Haar_H, Haar_G),
                WaveletType.Db2 => (Db2_H, Db2_G),
                WaveletType.Db4 => (Db4_H, Db4_G),
                _ => throw new ArgumentOutOfRangeException(nameof(type))
            };
        }

        /// <summary>
        /// Single-level 1D Discrete Wavelet Transform (DWT).
        /// Signal length should preferably be even.
        /// </summary>
        public static void Dwt(ReadOnlySpan<double> signal, WaveletType type, out double[] cA, out double[] cD)
        {
            int n = signal.Length;
            if (n < 2) throw new ArgumentException("Signal length must be at least 2 for DWT.", nameof(signal));

            // Pad odd length by 1 sample (mirror last)
            int effectiveLength = (n % 2 == 0) ? n : n + 1;
            double[] input = new double[effectiveLength];
            for (int i = 0; i < n; i++) input[i] = signal[i];
            if (effectiveLength > n) input[n] = signal[n - 1];

            var (h, g) = GetFilters(type);
            int filterLen = h.Length;
            int half = effectiveLength / 2;

            cA = new double[half];
            cD = new double[half];

            for (int j = 0; j < half; j++)
            {
                double aSum = 0.0;
                double dSum = 0.0;
                for (int k = 0; k < filterLen; k++)
                {
                    int idx = (2 * j + k) % effectiveLength;
                    double x = input[idx];
                    aSum += h[k] * x;
                    dSum += g[k] * x;
                }
                cA[j] = aSum;
                cD[j] = dSum;
            }
        }

        /// <summary>
        /// Single-level 1D Inverse Discrete Wavelet Transform (IDWT).
        /// </summary>
        public static double[] Idwt(ReadOnlySpan<double> cA, ReadOnlySpan<double> cD, WaveletType type, int? targetLength = null)
        {
            if (cA.Length != cD.Length)
                throw new ArgumentException("Approximation and Detail coefficients must have the same length.");

            int half = cA.Length;
            int n = half * 2;
            var (h, g) = GetFilters(type);
            int filterLen = h.Length;

            double[] reconstructed = new double[n];

            for (int j = 0; j < half; j++)
            {
                double a = cA[j];
                double d = cD[j];
                for (int k = 0; k < filterLen; k++)
                {
                    int idx = (2 * j + k) % n;
                    reconstructed[idx] += a * h[k] + d * g[k];
                }
            }

            if (targetLength.HasValue && targetLength.Value < n)
            {
                double[] trimmed = new double[targetLength.Value];
                Array.Copy(reconstructed, 0, trimmed, 0, targetLength.Value);
                return trimmed;
            }

            return reconstructed;
        }

        /// <summary>
        /// Multi-level wavelet decomposition using the Mallat tree algorithm.
        /// </summary>
        public static WaveletDecomposition Decompose(ReadOnlySpan<double> signal, int levels, WaveletType type = WaveletType.Db2)
        {
            if (levels < 1) throw new ArgumentOutOfRangeException(nameof(levels), "Levels must be at least 1.");

            var details = new List<double[]>();
            double[] currentApprox = signal.ToArray();

            for (int l = 0; l < levels; l++)
            {
                if (currentApprox.Length < 2) break;
                Dwt(currentApprox, type, out double[] cA, out double[] cD);
                details.Add(cD);
                currentApprox = cA;
            }

            return new WaveletDecomposition(type, signal.Length, currentApprox, details);
        }

        /// <summary>
        /// Reconstructs the original signal from a multi-level wavelet decomposition.
        /// </summary>
        public static double[] Reconstruct(WaveletDecomposition decomp)
        {
            if (decomp == null) throw new ArgumentNullException(nameof(decomp));

            double[] current = decomp.Approximation;
            int levels = decomp.Details.Count;

            // Reconstruct in reverse order (coarsest to finest)
            for (int l = levels - 1; l >= 0; l--)
            {
                var cD = decomp.Details[l];
                // For the last reconstruction step, target original length if specified
                int? targetLen = (l == 0) ? decomp.OriginalLength : (int?)null;
                current = Idwt(current, cD, decomp.WaveletType, targetLen);
            }

            return current;
        }

        /// <summary>
        /// Denoises a 1D industrial sensor or metrology signal using multi-scale Wavelet thresholding (VisuShrink).
        /// </summary>
        public static double[] Denoise(
            ReadOnlySpan<double> signal,
            int levels = 3,
            WaveletType type = WaveletType.Db2,
            bool softThreshold = true)
        {
            if (signal.Length < 4) return signal.ToArray();

            var decomp = Decompose(signal, levels, type);
            if (decomp.Details.Count == 0) return signal.ToArray();

            // Estimate noise level sigma from finest detail coefficients (D1) using Median Absolute Deviation (MAD):
            // sigma = median(|cD1|) / 0.6745
            var d1 = decomp.Details[0];
            double[] absD1 = new double[d1.Length];
            for (int i = 0; i < d1.Length; i++) absD1[i] = Math.Abs(d1[i]);
            Array.Sort(absD1);
            double median = absD1[absD1.Length / 2];
            double sigma = median / 0.6745;

            // Universal VisuShrink threshold: lambda = sigma * sqrt(2 * ln(N))
            double lambda = sigma * Math.Sqrt(2.0 * Math.Log(signal.Length));

            // Apply thresholding to detail coefficients
            var thresholdedDetails = new List<double[]>();
            for (int l = 0; l < decomp.Details.Count; l++)
            {
                var detail = decomp.Details[l];
                double[] newDetail = new double[detail.Length];
                for (int i = 0; i < detail.Length; i++)
                {
                    double val = detail[i];
                    if (softThreshold)
                    {
                        // Soft thresholding: sign(val) * max(0, |val| - lambda)
                        double absVal = Math.Abs(val);
                        newDetail[i] = absVal > lambda ? Math.Sign(val) * (absVal - lambda) : 0.0;
                    }
                    else
                    {
                        // Hard thresholding: val if |val| > lambda else 0
                        newDetail[i] = Math.Abs(val) > lambda ? val : 0.0;
                    }
                }
                thresholdedDetails.Add(newDetail);
            }

            var denoisedDecomp = new WaveletDecomposition(
                decomp.WaveletType,
                decomp.OriginalLength,
                decomp.Approximation,
                thresholdedDetails);

            return Reconstruct(denoisedDecomp);
        }
    }
}
