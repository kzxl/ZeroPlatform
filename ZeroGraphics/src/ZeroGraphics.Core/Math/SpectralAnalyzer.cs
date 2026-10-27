using System;
using System.Collections.Generic;

namespace ZeroGraphics.Core.Math
{
    /// <summary>
    /// Represents a detected dominant frequency peak in a magnitude spectrum.
    /// </summary>
    public readonly struct SpectralPeak
    {
        /// <summary>
        /// Peak frequency in Hertz (sub-bin precision interpolated).
        /// </summary>
        public float Frequency { get; }

        /// <summary>
        /// Peak magnitude amplitude.
        /// </summary>
        public float Magnitude { get; }

        /// <summary>
        /// Nearest integer discrete frequency bin index.
        /// </summary>
        public int BinIndex { get; }

        public SpectralPeak(float frequency, float magnitude, int binIndex)
        {
            Frequency = frequency;
            Magnitude = magnitude;
            BinIndex = binIndex;
        }

        public override string ToString() => $"Peak: {Frequency:F2} Hz (Amp: {Magnitude:F4}, Bin: {BinIndex})";
    }

    /// <summary>
    /// Advanced Spectral Analysis and Industrial Predictive Maintenance Engine.
    /// Provides sub-bin peak detection, Total Harmonic Distortion (THD), and Signal-to-Noise Ratio (SNR) metrics.
    /// </summary>
    public static class SpectralAnalyzer
    {
        /// <summary>
        /// Identifies dominant local frequency peaks in a single-sided magnitude spectrum using 3-point parabolic interpolation.
        /// </summary>
        /// <param name="magnitudeSpectrum">Single-sided magnitude spectrum (length = fftSize / 2 + 1).</param>
        /// <param name="sampleRate">Sampling frequency in Hz.</param>
        /// <param name="maxPeaks">Maximum number of dominant peaks to return.</param>
        /// <param name="minThresholdRatio">Minimum peak height relative to the global spectrum maximum.</param>
        public static List<SpectralPeak> FindDominantPeaks(
            ReadOnlySpan<float> magnitudeSpectrum,
            float sampleRate,
            int maxPeaks = 5,
            float minThresholdRatio = 0.05f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
            int count = magnitudeSpectrum.Length;
            if (count < 3) return new List<SpectralPeak>();

            int fftSize = (count - 1) * 2;
            float freqResolution = sampleRate / fftSize;

            // Find global maximum to establish relative threshold
            float globalMax = 0f;
            for (int i = 1; i < count - 1; i++)
            {
                if (magnitudeSpectrum[i] > globalMax) globalMax = magnitudeSpectrum[i];
            }

            float threshold = globalMax * minThresholdRatio;
            var candidates = new List<SpectralPeak>();

            for (int k = 1; k < count - 1; k++)
            {
                float yCurr = magnitudeSpectrum[k];
                if (yCurr < threshold) continue;

                float yPrev = magnitudeSpectrum[k - 1];
                float yNext = magnitudeSpectrum[k + 1];

                // Local maximum check
                if (yCurr > yPrev && yCurr > yNext)
                {
                    // Parabolic interpolation for sub-bin frequency refinement
                    // delta = 0.5 * (yPrev - yNext) / (yPrev - 2*yCurr + yNext)
                    float denom = yPrev - 2f * yCurr + yNext;
                    float delta = System.Math.Abs(denom) > 1e-9f ? 0.5f * (yPrev - yNext) / denom : 0f;

                    float refinedFreq = (k + delta) * freqResolution;
                    float refinedMag = yCurr - 0.25f * (yPrev - yNext) * delta;

                    candidates.Add(new SpectralPeak(refinedFreq, refinedMag, k));
                }
            }

            // Sort peaks by magnitude descending
            candidates.Sort((a, b) => b.Magnitude.CompareTo(a.Magnitude));

            if (candidates.Count > maxPeaks)
            {
                candidates.RemoveRange(maxPeaks, candidates.Count - maxPeaks);
            }

            return candidates;
        }

        /// <summary>
        /// Computes Total Harmonic Distortion (THD) percentage of a periodic signal.
        /// THD = sqrt(V2^2 + V3^2 + ... + Vn^2) / V1 * 100%.
        /// Essential for motor vibration diagnosis, acoustic analysis, and power quality monitoring.
        /// </summary>
        /// <param name="magnitudeSpectrum">Single-sided magnitude spectrum.</param>
        /// <param name="sampleRate">Sampling frequency in Hz.</param>
        /// <param name="fundamentalFreq">Fundamental frequency f0 in Hz.</param>
        /// <param name="maxHarmonics">Number of harmonics to evaluate (default 5).</param>
        public static double CalculateThd(
            ReadOnlySpan<float> magnitudeSpectrum,
            float sampleRate,
            float fundamentalFreq,
            int maxHarmonics = 5)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (fundamentalFreq <= 0) throw new ArgumentOutOfRangeException(nameof(fundamentalFreq));

            int count = magnitudeSpectrum.Length;
            int fftSize = (count - 1) * 2;
            float freqResolution = sampleRate / fftSize;

            float v1 = GetPeakMagnitude(magnitudeSpectrum, fundamentalFreq, freqResolution);
            if (v1 <= 1e-9f) return 0.0;

            double harmonicSumSq = 0.0;
            for (int h = 2; h <= maxHarmonics; h++)
            {
                float harmonicFreq = fundamentalFreq * h;
                if (harmonicFreq >= sampleRate * 0.5f) break; // Exceeded Nyquist

                float vh = GetPeakMagnitude(magnitudeSpectrum, harmonicFreq, freqResolution);
                harmonicSumSq += vh * vh;
            }

            return (System.Math.Sqrt(harmonicSumSq) / v1) * 100.0;
        }

        private static float GetPeakMagnitude(ReadOnlySpan<float> magnitudeSpectrum, float targetFreq, float freqResolution)
        {
            int count = magnitudeSpectrum.Length;
            int centerBin = (int)System.Math.Round(targetFreq / freqResolution);
            if (centerBin < 0 || centerBin >= count) return 0f;

            // Search in a narrow 3-bin window around centerBin
            float maxAmp = magnitudeSpectrum[centerBin];
            if (centerBin > 0 && magnitudeSpectrum[centerBin - 1] > maxAmp) maxAmp = magnitudeSpectrum[centerBin - 1];
            if (centerBin < count - 1 && magnitudeSpectrum[centerBin + 1] > maxAmp) maxAmp = magnitudeSpectrum[centerBin + 1];

            return maxAmp;
        }

        /// <summary>
        /// Computes the Signal-to-Noise Ratio (SNR) in decibels (dB) relative to the fundamental signal.
        /// </summary>
        public static double CalculateSnr(
            ReadOnlySpan<float> magnitudeSpectrum,
            float sampleRate,
            float fundamentalFreq)
        {
            if (sampleRate <= 0 || fundamentalFreq <= 0) return 0.0;
            int count = magnitudeSpectrum.Length;
            int fftSize = (count - 1) * 2;
            float freqResolution = sampleRate / fftSize;

            int targetBin = (int)System.Math.Round(fundamentalFreq / freqResolution);
            if (targetBin <= 0 || targetBin >= count) return 0.0;

            double signalPower = magnitudeSpectrum[targetBin] * magnitudeSpectrum[targetBin];
            double noisePower = 0.0;
            int noiseBins = 0;

            for (int k = 1; k < count; k++)
            {
                // Exclude signal bin and immediate neighbors
                if (System.Math.Abs(k - targetBin) > 2)
                {
                    noisePower += magnitudeSpectrum[k] * magnitudeSpectrum[k];
                    noiseBins++;
                }
            }

            if (noiseBins == 0 || noisePower <= 1e-12) return 100.0; // High SNR
            return 10.0 * System.Math.Log10(signalPower / (noisePower / noiseBins));
        }
    }
}
