using System;
using System.Buffers;
using ZeroSignal.Core.Filtering;
using ZeroSignal.Core.Spectral;
using ZeroUI.Core.Charts;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// High-performance digital signal processing (DSP) and spectral analysis adapter for ZeroUI charts.
    /// Harnesses sovereign Tier-3 <see cref="ZeroSignal.Core"/> for zero-phase Butterworth filtering and FFT spectrum transforms.
    /// </summary>
    public static class SignalProcessingAdapter
    {
        /// <summary>
        /// Applies a zero-phase forward-backward Butterworth low-pass filter (FiltFilt) to remove high-frequency noise without phase distortion.
        /// </summary>
        public static double[] SmoothButterworth(ReadOnlySpan<double> rawSignal, double sampleRateHz, double cutoffHz, int order = 2)
        {
            if (rawSignal.Length == 0) return Array.Empty<double>();
            if (cutoffHz <= 0 || cutoffHz >= sampleRateHz / 2.0)
            {
                // Cutoff must be less than Nyquist frequency (sampleRate / 2)
                return rawSignal.ToArray();
            }

            var filter = FilterDesign.Butterworth(order, cutoffHz, sampleRateHz, FilterType.Lowpass);
            return filter.FiltFilt(rawSignal);
        }

        /// <summary>
        /// Applies a notch filter to eliminate 50Hz or 60Hz AC electrical hum or resonance noise.
        /// </summary>
        public static double[] RemoveHum(ReadOnlySpan<double> rawSignal, double sampleRateHz, double humFreqHz = 50.0, double q = 30.0)
        {
            if (rawSignal.Length == 0) return Array.Empty<double>();
            if (humFreqHz <= 0 || humFreqHz >= sampleRateHz / 2.0) return rawSignal.ToArray();

            var filter = FilterDesign.Notch(humFreqHz, q, sampleRateHz);
            return filter.FiltFilt(rawSignal);
        }

        /// <summary>
        /// Computes the single-sided Fast Fourier Transform (FFT) amplitude spectrum for frequency domain visualization.
        /// </summary>
        /// <param name="signal">Time-domain signal values.</param>
        /// <param name="sampleRateHz">Sampling rate in Hertz.</param>
        /// <param name="applyHanning">Whether to apply a Hanning window to suppress spectral leakage.</param>
        /// <returns>A tuple of frequencies (Hz) and linear magnitudes.</returns>
        public static (double[] Frequencies, double[] Magnitudes) ComputeSpectrum(
            ReadOnlySpan<double> signal,
            double sampleRateHz,
            bool applyHanning = true)
        {
            if (signal.Length < 4)
            {
                return (Array.Empty<double>(), Array.Empty<double>());
            }

            int n = signal.Length;
            int fftSize = FastFourierTransform.NextPowerOfTwo(n);

            var complexBuffer = new Complex64[fftSize];
            for (int i = 0; i < n; i++)
            {
                double windowWeight = 1.0;
                if (applyHanning)
                {
                    // Hanning window: 0.5 * (1 - cos(2*pi*i / (N-1)))
                    windowWeight = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / (n - 1)));
                }

                complexBuffer[i] = new Complex64(signal[i] * windowWeight, 0.0);
            }

            // Zero-padding for remaining slots
            for (int i = n; i < fftSize; i++)
            {
                complexBuffer[i] = new Complex64(0.0, 0.0);
            }

            FastFourierTransform.FFT(complexBuffer);

            // Single-sided spectrum has (fftSize / 2) bins
            int halfBins = fftSize / 2;
            var freqs = new double[halfBins];
            var mags = new double[halfBins];

            double freqResolution = sampleRateHz / fftSize;

            for (int i = 0; i < halfBins; i++)
            {
                freqs[i] = i * freqResolution;
                var c = complexBuffer[i];
                double mag = c.Magnitude / n;
                if (i > 0 && i < halfBins - 1)
                {
                    mag *= 2.0; // Conserve energy for single-sided spectrum
                }
                mags[i] = mag;
            }

            return (freqs, mags);
        }

        /// <summary>
        /// Converts the frequency spectrum of a telemetry signal into a ready-to-render <see cref="ChartSeries"/>.
        /// </summary>
        public static ChartSeries CreateSpectrumSeries(
            ReadOnlySpan<double> signal,
            double sampleRateHz,
            string seriesName = "FFT Spectrum",
            uint? colorRgba = null)
        {
            var (freqs, mags) = ComputeSpectrum(signal, sampleRateHz, applyHanning: true);
            var series = new ChartSeries(seriesName)
            {
                Type = ChartType.Area,
                ColorRgba = colorRgba ?? 0xFF00B4D8 // Cyan-Blue default
            };

            for (int i = 0; i < freqs.Length; i++)
            {
                series.Add(new ChartPoint(freqs[i], mags[i], $"{freqs[i]:F1} Hz"));
            }

            return series;
        }

        /// <summary>
        /// Smooths an entire <see cref="ChartSeries"/> in-place or returns a smoothed clone.
        /// </summary>
        public static ChartSeries CreateSmoothedSeries(
            ChartSeries source,
            double sampleRateHz,
            double cutoffHz,
            int order = 2,
            string? newSeriesName = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            int count = source.Points.Count;
            if (count == 0) return new ChartSeries(newSeriesName ?? source.Name);

            var rawValues = new double[count];
            for (int i = 0; i < count; i++)
            {
                rawValues[i] = source.Points[i].Value;
            }

            var smoothed = SmoothButterworth(rawValues, sampleRateHz, cutoffHz, order);

            var result = new ChartSeries(newSeriesName ?? $"{source.Name} (Smoothed)")
            {
                Type = source.Type,
                ColorRgba = source.ColorRgba,
                LineWidth = source.LineWidth,
                ShowMarkers = source.ShowMarkers
            };

            for (int i = 0; i < count; i++)
            {
                var pt = source.Points[i];
                result.Add(new ChartPoint(pt.Label, smoothed[i], pt.ColorRgba)
                {
                    X = pt.X,
                    Y = smoothed[i],
                    Tag = pt.Tag
                });
            }

            return result;
        }
    }
}
