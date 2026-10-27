using System;
using Xunit;
using ZeroSignal.Core.Estimation;
using ZeroSignal.Core.Spectral;

namespace ZeroSignal.Tests
{
    public class SpectralAndEstimationTests
    {
        [Fact]
        public void TestFFT_RoundtripAndPeakFrequency()
        {
            int n = 128;
            double samplingRate = 128.0; // 1 Hz per bin
            double targetFreq = 10.0; // Peak at bin 10

            var original = new Complex64[n];
            var buffer = new Complex64[n];

            for (int i = 0; i < n; i++)
            {
                double t = i / samplingRate;
                double val = Math.Sin(2.0 * Math.PI * targetFreq * t);
                original[i] = new Complex64(val, 0.0);
                buffer[i] = original[i];
            }

            // Forward FFT
            FastFourierTransform.FFT(buffer);

            // Verify peak is at bin 10
            int maxBin = 0;
            double maxMag = 0;
            for (int i = 0; i < n / 2; i++)
            {
                if (buffer[i].Magnitude > maxMag)
                {
                    maxMag = buffer[i].Magnitude;
                    maxBin = i;
                }
            }
            Assert.Equal(10, maxBin);

            // Inverse FFT
            FastFourierTransform.IFFT(buffer);

            // Verify bit-exact/numerical roundtrip
            for (int i = 0; i < n; i++)
            {
                Assert.True(Math.Abs(buffer[i].Real - original[i].Real) < 1e-10);
                Assert.True(Math.Abs(buffer[i].Imaginary) < 1e-10);
            }
        }

        [Fact]
        public void TestStftAndSpectrogram_TimeFrequencyLocalization()
        {
            int n = 1024;
            double fs = 1000.0;
            double[] signal = new double[n];

            // First half: 50 Hz tone
            for (int i = 0; i < n / 2; i++)
            {
                signal[i] = Math.Sin(2.0 * Math.PI * 50.0 * (i / fs));
            }
            // Second half: 250 Hz tone
            for (int i = n / 2; i < n; i++)
            {
                signal[i] = Math.Sin(2.0 * Math.PI * 250.0 * (i / fs));
            }

            int winSize = 128;
            int hopSize = 32;
            var spec = StftTransform.Spectrogram(signal, winSize, hopSize, WindowFunction.Hann);

            int freqBins = spec.GetLength(0);
            int frames = spec.GetLength(1);
            Assert.True(frames > 10);

            // Find peak frequency bin in an early frame (e.g. frame 2)
            int earlyPeakBin = 0;
            double earlyMax = 0;
            for (int b = 0; b < freqBins; b++)
            {
                if (spec[b, 2] > earlyMax)
                {
                    earlyMax = spec[b, 2];
                    earlyPeakBin = b;
                }
            }

            // Find peak frequency bin in a late frame (e.g. frame 25)
            int latePeakBin = 0;
            double lateMax = 0;
            for (int b = 0; b < freqBins; b++)
            {
                if (spec[b, frames - 3] > lateMax)
                {
                    lateMax = spec[b, frames - 3];
                    latePeakBin = b;
                }
            }

            // 250 Hz tone bin must be distinctly higher than 50 Hz tone bin
            Assert.True(latePeakBin > earlyPeakBin);
        }

        [Fact]
        public void TestExtendedKalmanFilter_NonlinearTracking()
        {
            // Non-linear tracking:
            // State: [position x, velocity v]
            // State transition: x_{k+1} = x_k + v_k * dt, v_{k+1} = v_k
            // Measurement: distance squared z = x^2
            double dt = 0.1;

            Func<double[], double[]?, double[]> f = (x, u) =>
            {
                return new double[]
                {
                    x[0] + x[1] * dt,
                    x[1]
                };
            };

            Func<double[], double[]> h = (x) =>
            {
                return new double[] { x[0] * x[0] }; // Non-linear measurement
            };

            double[] x0 = new double[] { 1.5, 0.8 }; // Initial guess
            double[,] P0 = new double[2, 2] { { 1.0, 0.0 }, { 0.0, 1.0 } };
            double[,] Q = new double[2, 2] { { 0.01, 0.0 }, { 0.0, 0.01 } };
            double[,] R = new double[1, 1] { { 0.05 } };

            var ekf = new ExtendedKalmanFilter(2, 1, f, h, x0, P0, Q, R);

            // True trajectory starting at x = 2.0, v = 1.0
            double trueX = 2.0;
            double trueV = 1.0;

            for (int step = 0; step < 30; step++)
            {
                // True motion
                trueX += trueV * dt;
                double trueZ = trueX * trueX;

                // EKF steps
                ekf.Predict();
                ekf.Update(new double[] { trueZ });
            }

            // Estimate should track true state accurately
            Assert.True(Math.Abs(ekf.State[0] - trueX) < 0.2, $"State x ({ekf.State[0]}) should be close to trueX ({trueX})");
            Assert.True(Math.Abs(ekf.State[1] - trueV) < 0.2, $"State v ({ekf.State[1]}) should be close to trueV ({trueV})");
        }
    }
}
