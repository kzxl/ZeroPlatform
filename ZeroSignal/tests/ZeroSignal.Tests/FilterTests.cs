using System;
using Xunit;
using ZeroSignal.Core.Filtering;

namespace ZeroSignal.Tests
{
    public class FilterTests
    {
        [Fact]
        public void Butterworth_Lowpass_AttenuatesHighFrequency()
        {
            double fs = 1000.0;
            int n = 1000;
            double fLow = 5.0;   // In passband
            double fHigh = 100.0; // In stopband

            double[] signal = new double[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / fs;
                signal[i] = Math.Sin(2.0 * Math.PI * fLow * t) + Math.Sin(2.0 * Math.PI * fHigh * t);
            }

            // 4th order lowpass at 20 Hz
            var filter = FilterDesign.Butterworth(4, 20.0, fs, FilterType.Lowpass);
            double[] filtered = filter.FiltFilt(signal);

            // In steady-state (samples 200..800), high-frequency is removed and 5 Hz tone is preserved with 0 phase delay
            double maxLowDiff = 0.0;
            for (int i = 200; i < 800; i++)
            {
                double t = i / fs;
                double expectedLow = Math.Sin(2.0 * Math.PI * fLow * t);
                double diff = Math.Abs(filtered[i] - expectedLow);
                if (diff > maxLowDiff) maxLowDiff = diff;
            }

            Assert.True(maxLowDiff < 0.05, $"High frequency tone was not properly attenuated or passband distorted. Max diff: {maxLowDiff}");

            // Also test causal Filter on pure stopband signal: amplitude should drop from 1.0 to < 0.05
            double[] stopbandSignal = new double[n];
            for (int i = 0; i < n; i++) stopbandSignal[i] = Math.Sin(2.0 * Math.PI * fHigh * (i / fs));
            double[] causalFiltered = filter.Filter(stopbandSignal);
            double maxStopbandAmp = 0.0;
            for (int i = 500; i < 900; i++)
            {
                if (Math.Abs(causalFiltered[i]) > maxStopbandAmp) maxStopbandAmp = Math.Abs(causalFiltered[i]);
            }
            Assert.True(maxStopbandAmp < 0.05, $"Causal lowpass filter did not attenuate 100 Hz tone. Max amplitude: {maxStopbandAmp}");
        }

        [Fact]
        public void FiltFilt_PreservesZeroPhaseAndPeakLocation()
        {
            double fs = 500.0;
            int n = 500;
            double[] signal = new double[n];

            // Single symmetric Gaussian pulse centered at sample 250
            int peakIndex = 250;
            for (int i = 0; i < n; i++)
            {
                double diff = i - peakIndex;
                signal[i] = Math.Exp(-0.5 * (diff * diff) / (15.0 * 15.0));
            }

            var filter = FilterDesign.Butterworth(4, 30.0, fs, FilterType.Lowpass);

            // Forward filter causes phase delay
            double[] forwardFiltered = filter.Filter(signal);
            int forwardPeak = ArgMax(forwardFiltered);
            Assert.True(forwardPeak > peakIndex, "Forward filter should have a positive delay (phase lag).");

            // FiltFilt has EXACT zero phase delay
            double[] zeroPhaseFiltered = filter.FiltFilt(signal);
            int zeroPhasePeak = ArgMax(zeroPhaseFiltered);
            Assert.Equal(peakIndex, zeroPhasePeak);
        }

        [Fact]
        public void Chebyshev1_Lowpass_FiltersCorrectly()
        {
            double fs = 800.0;
            var filter = FilterDesign.Chebyshev1(4, 1.0, 40.0, fs, FilterType.Lowpass);

            double[] input = new double[400];
            for (int i = 0; i < input.Length; i++)
            {
                double t = i / fs;
                input[i] = Math.Cos(2.0 * Math.PI * 10.0 * t); // 10 Hz in passband
            }

            double[] output = filter.Filter(input);
            Assert.Equal(input.Length, output.Length);

            // Check steady state amplitude remains near 1.0
            double maxAmp = 0.0;
            for (int i = 200; i < 350; i++)
            {
                if (Math.Abs(output[i]) > maxAmp) maxAmp = Math.Abs(output[i]);
            }
            Assert.True(maxAmp > 0.85 && maxAmp < 1.15, $"Chebyshev passband amplitude {maxAmp} outside expected envelope.");
        }

        [Fact]
        public void Bandpass_PassesCenterFrequency()
        {
            double fs = 1000.0;
            double center = 50.0;
            var bp = FilterDesign.Bandpass(center, 5.0, fs);

            double[] testSignal = new double[500];
            for (int i = 0; i < testSignal.Length; i++)
            {
                testSignal[i] = Math.Sin(2.0 * Math.PI * center * (i / fs));
            }

            double[] output = bp.Filter(testSignal);
            Assert.Equal(testSignal.Length, output.Length);

            double maxVal = 0.0;
            for (int i = 250; i < 450; i++)
            {
                if (Math.Abs(output[i]) > maxVal) maxVal = Math.Abs(output[i]);
            }
            Assert.True(maxVal > 0.7, $"Bandpass should pass center frequency signal strongly. Measured: {maxVal}");
        }

        private static int ArgMax(double[] array)
        {
            int maxIdx = 0;
            double maxVal = array[0];
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] > maxVal)
                {
                    maxVal = array[i];
                    maxIdx = i;
                }
            }
            return maxIdx;
        }
    }
}
