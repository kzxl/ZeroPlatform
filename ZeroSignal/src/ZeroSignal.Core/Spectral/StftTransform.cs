using System;

namespace ZeroSignal.Core.Spectral
{
    public enum WindowFunction
    {
        Hann,
        Hamming,
        Blackman,
        Rectangular
    }

    /// <summary>
    /// Short-Time Fourier Transform (STFT) and Spectrogram calculator.
    /// Pure C# with zero external dependencies.
    /// </summary>
    public static class StftTransform
    {
        public static double[] CreatePeriodicWindow(int size, WindowFunction type)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var window = new double[size];

            switch (type)
            {
                case WindowFunction.Hann:
                    for (int i = 0; i < size; i++)
                        window[i] = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * i / size));
                    break;

                case WindowFunction.Hamming:
                    for (int i = 0; i < size; i++)
                        window[i] = 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * i / size);
                    break;

                case WindowFunction.Blackman:
                    for (int i = 0; i < size; i++)
                        window[i] = 0.42 - 0.5 * Math.Cos(2.0 * Math.PI * i / size) + 0.08 * Math.Cos(4.0 * Math.PI * i / size);
                    break;

                case WindowFunction.Rectangular:
                default:
                    for (int i = 0; i < size; i++) window[i] = 1.0;
                    break;
            }

            return window;
        }

        /// <summary>
        /// Computes 2D complex STFT representation: [FrequencyBins, TimeFrames].
        /// FrequencyBins = fftSize / 2 + 1 (one-sided spectrum).
        /// </summary>
        public static Complex64[,] Stft(
            double[] signal,
            int windowSize,
            int hopSize,
            WindowFunction window = WindowFunction.Hann,
            int? fftSize = null)
        {
            if (signal == null) throw new ArgumentNullException(nameof(signal));
            if (windowSize <= 0) throw new ArgumentOutOfRangeException(nameof(windowSize));
            if (hopSize <= 0) throw new ArgumentOutOfRangeException(nameof(hopSize));

            int N = fftSize.HasValue ? FastFourierTransform.NextPowerOfTwo(Math.Max(fftSize.Value, windowSize)) : FastFourierTransform.NextPowerOfTwo(windowSize);
            int numFrames = Math.Max(0, (signal.Length - windowSize) / hopSize + 1);
            int freqBins = N / 2 + 1;

            var result = new Complex64[freqBins, numFrames];
            var win = CreatePeriodicWindow(windowSize, window);
            var fftBuf = new Complex64[N];

            for (int frame = 0; frame < numFrames; frame++)
            {
                int start = frame * hopSize;

                // Windowing & zero padding
                for (int i = 0; i < N; i++)
                {
                    if (i < windowSize && start + i < signal.Length)
                        fftBuf[i] = new Complex64(signal[start + i] * win[i], 0.0);
                    else
                        fftBuf[i] = Complex64.Zero;
                }

                // Compute FFT
                FastFourierTransform.FFT(fftBuf);

                // Store one-sided spectrum
                for (int bin = 0; bin < freqBins; bin++)
                {
                    result[bin, frame] = fftBuf[bin];
                }
            }

            return result;
        }

        /// <summary>
        /// Computes 2D power or magnitude Spectrogram matrix: [FrequencyBins, TimeFrames].
        /// </summary>
        public static double[,] Spectrogram(
            double[] signal,
            int windowSize,
            int hopSize,
            WindowFunction window = WindowFunction.Hann,
            bool toDecibels = false)
        {
            var stft = Stft(signal, windowSize, hopSize, window);
            int freqBins = stft.GetLength(0);
            int numFrames = stft.GetLength(1);

            var spec = new double[freqBins, numFrames];

            for (int bin = 0; bin < freqBins; bin++)
            {
                for (int frame = 0; frame < numFrames; frame++)
                {
                    double mag = stft[bin, frame].Magnitude;
                    if (toDecibels)
                    {
                        spec[bin, frame] = 20.0 * Math.Log10(Math.Max(mag, 1e-12));
                    }
                    else
                    {
                        spec[bin, frame] = mag;
                    }
                }
            }

            return spec;
        }
    }
}
