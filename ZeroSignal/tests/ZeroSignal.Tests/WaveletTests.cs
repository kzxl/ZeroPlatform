using System;
using Xunit;
using ZeroSignal.Core.Wavelets;

namespace ZeroSignal.Tests
{
    public class WaveletTests
    {
        [Theory]
        [InlineData(WaveletType.Haar)]
        [InlineData(WaveletType.Db2)]
        [InlineData(WaveletType.Db4)]
        public void Wavelet_SingleLevel_ReconstructsSignalPerfectly(WaveletType type)
        {
            int n = 128;
            double[] signal = new double[n];
            var rng = new Random(42);
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Sin(i * 0.1) + (rng.NextDouble() - 0.5) * 0.2;
            }

            Wavelet.Dwt(signal, type, out double[] cA, out double[] cD);
            Assert.Equal(n / 2, cA.Length);
            Assert.Equal(n / 2, cD.Length);

            double[] reconstructed = Wavelet.Idwt(cA, cD, type, n);
            Assert.Equal(n, reconstructed.Length);

            double maxDiff = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = Math.Abs(signal[i] - reconstructed[i]);
                if (diff > maxDiff) maxDiff = diff;
            }

            Assert.True(maxDiff < 1e-9, $"Reconstruction error too high for {type}: {maxDiff}");
        }

        [Fact]
        public void Wavelet_MultiLevelDecomposeAndReconstruct_MatchesOriginal()
        {
            int n = 256;
            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Sin(2.0 * Math.PI * 5.0 * (i / 256.0)) +
                            0.5 * Math.Cos(2.0 * Math.PI * 25.0 * (i / 256.0));
            }

            var decomp = Wavelet.Decompose(signal, levels: 4, WaveletType.Db2);
            Assert.Equal(4, decomp.Levels);

            double[] reconstructed = Wavelet.Reconstruct(decomp);
            Assert.Equal(n, reconstructed.Length);

            double maxDiff = 0.0;
            for (int i = 0; i < n; i++)
            {
                double diff = Math.Abs(signal[i] - reconstructed[i]);
                if (diff > maxDiff) maxDiff = diff;
            }

            Assert.True(maxDiff < 1e-8, $"Multi-level reconstruction error too high: {maxDiff}");
        }

        [Fact]
        public void Wavelet_Denoise_ReducesNoiseLevel()
        {
            int n = 256;
            double[] clean = new double[n];
            double[] noisy = new double[n];
            var rng = new Random(123);

            for (int i = 0; i < n; i++)
            {
                clean[i] = Math.Sin(2.0 * Math.PI * 3.0 * (i / 256.0));
                // Add significant Gaussian-like noise
                double noise = (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * 0.4;
                noisy[i] = clean[i] + noise;
            }

            double noisyMse = ComputeMse(clean, noisy);

            double[] denoised = Wavelet.Denoise(noisy, levels: 3, WaveletType.Db2, softThreshold: true);
            double denoisedMse = ComputeMse(clean, denoised);

            Assert.True(denoisedMse < noisyMse * 0.7,
                $"Wavelet denoising did not sufficiently reduce MSE: noisy={noisyMse}, denoised={denoisedMse}");
        }

        private static double ComputeMse(double[] a, double[] b)
        {
            double sum = 0.0;
            for (int i = 0; i < a.Length; i++)
            {
                double diff = a[i] - b[i];
                sum += diff * diff;
            }
            return sum / a.Length;
        }
    }
}
