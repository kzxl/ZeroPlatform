using System;
using System.Collections.Generic;

namespace ZeroSignal.Core.Wavelets
{
    /// <summary>
    /// Represents the multi-level decomposition of a 1D signal into approximation
    /// and detail coefficients across dyadic frequency scales.
    /// </summary>
    public sealed class WaveletDecomposition
    {
        public WaveletType WaveletType { get; }
        public int OriginalLength { get; }
        public double[] Approximation { get; }
        public IReadOnlyList<double[]> Details { get; }

        public int Levels => Details.Count;

        public WaveletDecomposition(
            WaveletType waveletType,
            int originalLength,
            double[] approximation,
            IReadOnlyList<double[]> details)
        {
            WaveletType = waveletType;
            OriginalLength = originalLength;
            Approximation = approximation ?? throw new ArgumentNullException(nameof(approximation));
            Details = details ?? throw new ArgumentNullException(nameof(details));
        }
    }
}
