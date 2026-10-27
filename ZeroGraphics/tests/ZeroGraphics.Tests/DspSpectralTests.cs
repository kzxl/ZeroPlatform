using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.Core.Math;

namespace ZeroGraphics.Tests
{
    public class DspSpectralTests
    {
        [Fact]
        public void FastFourierTransform_FftAndIfft_RecoversOriginalSignal()
        {
            int n = 128;
            var buffer = new ComplexF[n];
            var original = new ComplexF[n];
            var rng = new Random(42);

            for (int i = 0; i < n; i++)
            {
                float r = (float)(rng.NextDouble() * 10.0 - 5.0);
                float im = (float)(rng.NextDouble() * 10.0 - 5.0);
                buffer[i] = new ComplexF(r, im);
                original[i] = buffer[i];
            }

            // Forward FFT
            FastFourierTransform.Fft(buffer.AsSpan(), inverse: false);

            // Inverse FFT
            FastFourierTransform.Fft(buffer.AsSpan(), inverse: true);

            // Verify round-trip identity
            for (int i = 0; i < n; i++)
            {
                Assert.InRange(buffer[i].Real, original[i].Real - 1e-4f, original[i].Real + 1e-4f);
                Assert.InRange(buffer[i].Imaginary, original[i].Imaginary - 1e-4f, original[i].Imaginary + 1e-4f);
            }
        }

        [Fact]
        public void ComputeMagnitudeSpectrum_DetectsPureSineFrequency()
        {
            float sampleRate = 1000.0f; // 1 kHz
            float targetFreq = 125.0f;  // 125 Hz
            int n = 512;

            float[] signal = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)sampleRate;
                signal[i] = (float)Math.Sin(2.0 * Math.PI * targetFreq * t);
            }

            int halfSize = n / 2;
            float[] spectrum = new float[halfSize + 1];

            FastFourierTransform.ComputeMagnitudeSpectrum(signal, spectrum, WindowFunction.Hann);

            // Frequency resolution = sampleRate / n = 1000 / 512 ~= 1.953 Hz
            // Expected peak bin index = round(125 / 1.953) = 64
            int expectedBin = 64;

            float maxAmp = 0f;
            int maxBin = -1;
            for (int k = 0; k <= halfSize; k++)
            {
                if (spectrum[k] > maxAmp)
                {
                    maxAmp = spectrum[k];
                    maxBin = k;
                }
            }

            Assert.Equal(expectedBin, maxBin);
            Assert.True(maxAmp > 0.4f, $"Peak amplitude should be prominent, got {maxAmp}");
        }

        [Fact]
        public void SpectralAnalyzer_FindDominantPeaks_WithSubBinPrecision()
        {
            float sampleRate = 1000.0f;
            float freq1 = 80.3f; // Non-integer bin
            float freq2 = 240.0f;
            int n = 1024;

            float[] signal = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)sampleRate;
                signal[i] = (float)(1.5 * Math.Sin(2.0 * Math.PI * freq1 * t) + 0.8 * Math.Sin(2.0 * Math.PI * freq2 * t));
            }

            float[] spectrum = new float[n / 2 + 1];
            FastFourierTransform.ComputeMagnitudeSpectrum(signal, spectrum, WindowFunction.Blackman);

            var peaks = SpectralAnalyzer.FindDominantPeaks(spectrum, sampleRate, maxPeaks: 2);

            Assert.Equal(2, peaks.Count);

            // Peak 1: ~80.3 Hz
            Assert.InRange(peaks[0].Frequency, 79.5f, 81.0f);
            Assert.InRange(peaks[0].Magnitude, 1.2f, 1.6f);

            // Peak 2: ~240 Hz
            Assert.InRange(peaks[1].Frequency, 239.0f, 241.0f);
            Assert.InRange(peaks[1].Magnitude, 0.6f, 0.9f);
        }

        [Fact]
        public void SpectralAnalyzer_CalculatesThdAccurately()
        {
            float sampleRate = 2000.0f;
            float fundamental = 100.0f; // 100 Hz fundamental
            int n = 1024;

            // Signal with fundamental (amp=1.0), 2nd harmonic (amp=0.1), 3rd harmonic (amp=0.05)
            // Theoretical THD = sqrt(0.1^2 + 0.05^2) / 1.0 * 100% ~= 11.18%
            float[] signal = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)sampleRate;
                signal[i] = (float)(1.0 * Math.Sin(2.0 * Math.PI * fundamental * t)
                                  + 0.1 * Math.Sin(2.0 * Math.PI * 2.0 * fundamental * t)
                                  + 0.05 * Math.Sin(2.0 * Math.PI * 3.0 * fundamental * t));
            }

            float[] spectrum = new float[n / 2 + 1];
            FastFourierTransform.ComputeMagnitudeSpectrum(signal, spectrum, WindowFunction.Hamming);

            double thd = SpectralAnalyzer.CalculateThd(spectrum, sampleRate, fundamental, maxHarmonics: 4);

            // Verify THD within 1.5% tolerance of theoretical 11.18%
            Assert.InRange(thd, 9.5, 13.0);
        }
    }
}
