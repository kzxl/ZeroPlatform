using System;
using System.Linq;
using Xunit;
using ZeroUI.Core.Analytics;
using ZeroUI.Core.Charts;

namespace ZeroUI.Core.Tests
{
    public class SignalProcessingAdapterTests
    {
        [Fact]
        public void SmoothButterworth_ReducesHighFrequencyNoise()
        {
            // Create a 10 Hz sine wave sampled at 1000 Hz with high-frequency 200 Hz noise
            double sampleRate = 1000.0;
            int n = 500;
            var clean = new double[n];
            var noisy = new double[n];

            for (int i = 0; i < n; i++)
            {
                double t = i / sampleRate;
                clean[i] = 10.0 * Math.Sin(2.0 * Math.PI * 10.0 * t);
                double noise = 3.0 * Math.Sin(2.0 * Math.PI * 200.0 * t);
                noisy[i] = clean[i] + noise;
            }

            // Apply Butterworth lowpass with cutoff at 30 Hz
            var smoothed = SignalProcessingAdapter.SmoothButterworth(noisy, sampleRate, cutoffHz: 30.0, order: 2);

            Assert.Equal(n, smoothed.Length);

            // Compute mean squared error against clean signal
            double rawMse = 0.0;
            double smoothedMse = 0.0;
            for (int i = 50; i < n - 50; i++) // Avoid edge transient effects
            {
                rawMse += Math.Pow(noisy[i] - clean[i], 2);
                smoothedMse += Math.Pow(smoothed[i] - clean[i], 2);
            }

            // Smoothed MSE should be significantly lower than raw noisy MSE
            Assert.True(smoothedMse < rawMse * 0.3, $"Expected smoothed MSE ({smoothedMse}) to be < 30% of raw MSE ({rawMse})");
        }

        [Fact]
        public void ComputeSpectrum_DetectsProminentFrequencyPeak()
        {
            // Generate a 50 Hz sinusoidal test signal sampled at 1000 Hz
            double sampleRate = 1000.0;
            int n = 1024;
            var signal = new double[n];
            double targetFreq = 50.0;

            for (int i = 0; i < n; i++)
            {
                double t = i / sampleRate;
                signal[i] = 5.0 * Math.Sin(2.0 * Math.PI * targetFreq * t);
            }

            var (freqs, mags) = SignalProcessingAdapter.ComputeSpectrum(signal, sampleRate, applyHanning: true);

            Assert.NotEmpty(freqs);
            Assert.NotEmpty(mags);
            Assert.Equal(freqs.Length, mags.Length);

            // Find index of maximum magnitude
            int peakIdx = 0;
            double maxMag = 0.0;
            for (int i = 0; i < mags.Length; i++)
            {
                if (mags[i] > maxMag)
                {
                    maxMag = mags[i];
                    peakIdx = i;
                }
            }

            double detectedFreq = freqs[peakIdx];
            // Detected frequency should be within +/- 2 Hz of target 50 Hz
            Assert.InRange(detectedFreq, 48.0, 52.0);
        }

        [Fact]
        public void CreateSpectrumSeries_ProducesChartSeriesWithPoints()
        {
            double sampleRate = 500.0;
            int n = 256;
            var signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                signal[i] = Math.Sin(2.0 * Math.PI * 25.0 * (i / sampleRate));
            }

            var series = SignalProcessingAdapter.CreateSpectrumSeries(signal, sampleRate, "Motor Vibration");

            Assert.Equal("Motor Vibration", series.Name);
            Assert.Equal(ChartType.Area, series.Type);
            Assert.NotEmpty(series.Points);
        }

        [Fact]
        public void CreateSmoothedSeries_ReturnsSmoothedChartSeries()
        {
            var rawSeries = new ChartSeries("Raw Pressure");
            for (int i = 0; i < 100; i++)
            {
                double val = 50.0 + (i % 2 == 0 ? 5.0 : -5.0); // Noisy square-like jitter
                rawSeries.Add($"T{i}", val);
            }

            var smoothed = SignalProcessingAdapter.CreateSmoothedSeries(rawSeries, sampleRateHz: 100.0, cutoffHz: 10.0);

            Assert.Equal(100, smoothed.Points.Count);
            Assert.Contains("Smoothed", smoothed.Name);
        }
    }
}
