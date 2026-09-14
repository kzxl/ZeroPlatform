using System;
using System.Collections.Generic;

namespace ZeroAudioVisual.Analysis
{
    /// <summary>
    /// Window functions used for Fast Fourier Transform spectral leakage reduction.
    /// </summary>
    public enum WindowType
    {
        Rectangular,
        Hann,
        Hamming,
        Blackman
    }

    /// <summary>
    /// Zero-allocation, high-performance Short-Time Fourier Transform (STFT) engine.
    /// Produces time-frequency spectrogram matrices for vibration and acoustic analysis.
    /// </summary>
    public class SpectrogramEngine
    {
        public static SpectrogramResult ComputeStft(
            ReadOnlySpan<float> signal,
            int sampleRate,
            int windowSize = 512,
            int hopSize = 256,
            WindowType windowType = WindowType.Hann)
        {
            if ((windowSize & (windowSize - 1)) != 0)
                throw new ArgumentException("Window size must be a power of two.", nameof(windowSize));

            int totalSamples = signal.Length;
            if (totalSamples < windowSize)
            {
                return new SpectrogramResult(new float[0, 0], new float[0], new float[0], sampleRate, windowSize);
            }

            int numFrames = (totalSamples - windowSize) / hopSize + 1;
            int numBins = windowSize / 2 + 1;

            float[,] spectrogram = new float[numFrames, numBins];
            float[] real = new float[windowSize];
            float[] imag = new float[windowSize];
            float[] window = CreateWindow(windowSize, windowType);

            float[] times = new float[numFrames];
            for (int f = 0; f < numFrames; f++)
            {
                times[f] = (f * hopSize) / (float)sampleRate;

                // Windowing
                int frameOffset = f * hopSize;
                for (int i = 0; i < windowSize; i++)
                {
                    real[i] = signal[frameOffset + i] * window[i];
                    imag[i] = 0f;
                }

                // In-place Radix-2 FFT
                FftRadix2(real, imag);

                // Compute Magnitude in decibels (dB)
                for (int k = 0; k < numBins; k++)
                {
                    float r = real[k];
                    float im = imag[k];
                    float mag = (float)Math.Sqrt(r * r + im * im) / (windowSize / 2f);
                    float db = (mag > 1e-7f) ? 20f * (float)Math.Log10(mag) : -140f;
                    spectrogram[f, k] = db;
                }
            }

            // Frequency axis
            float[] freqs = new float[numBins];
            float freqResolution = sampleRate / (float)windowSize;
            for (int k = 0; k < numBins; k++)
            {
                freqs[k] = k * freqResolution;
            }

            return new SpectrogramResult(spectrogram, freqs, times, sampleRate, windowSize);
        }

        private static float[] CreateWindow(int size, WindowType type)
        {
            float[] w = new float[size];
            for (int i = 0; i < size; i++)
            {
                switch (type)
                {
                    case WindowType.Hann:
                        w[i] = 0.5f * (1f - (float)Math.Cos(2 * Math.PI * i / (size - 1)));
                        break;
                    case WindowType.Hamming:
                        w[i] = 0.54f - 0.46f * (float)Math.Cos(2 * Math.PI * i / (size - 1));
                        break;
                    case WindowType.Blackman:
                        w[i] = 0.42f - 0.5f * (float)Math.Cos(2 * Math.PI * i / (size - 1)) + 0.08f * (float)Math.Cos(4 * Math.PI * i / (size - 1));
                        break;
                    default:
                        w[i] = 1.0f;
                        break;
                }
            }
            return w;
        }

        /// <summary>
        /// In-place Cooley-Tukey Radix-2 Fast Fourier Transform.
        /// </summary>
        public static void FftRadix2(float[] real, float[] imag)
        {
            int n = real.Length;
            // Bit reversal
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    float tr = real[i]; real[i] = real[j]; real[j] = tr;
                    float ti = imag[i]; imag[i] = imag[j]; imag[j] = ti;
                }
                int k = n >> 1;
                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            // Danielson-Lanczos butterfly
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2.0 * Math.PI / len;
                float wlen_r = (float)Math.Cos(angle);
                float wlen_i = (float)Math.Sin(angle);

                for (int i = 0; i < n; i += len)
                {
                    float wr = 1.0f;
                    float wi = 0.0f;
                    int half = len >> 1;
                    for (int step = 0; step < half; step++)
                    {
                        int u = i + step;
                        int v = i + step + half;

                        float vr = real[v] * wr - imag[v] * wi;
                        float vi = real[v] * wi + imag[v] * wr;

                        real[v] = real[u] - vr;
                        imag[v] = imag[u] - vi;
                        real[u] += vr;
                        imag[u] += vi;

                        float next_wr = wr * wlen_r - wi * wlen_i;
                        float next_wi = wr * wlen_i + wi * wlen_r;
                        wr = next_wr;
                        wi = next_wi;
                    }
                }
            }
        }
    }

    public class SpectrogramResult
    {
        public float[,] Magnitudes { get; }
        public float[] Frequencies { get; }
        public float[] Times { get; }
        public int SampleRate { get; }
        public int WindowSize { get; }

        public int FrameCount => Magnitudes.GetLength(0);
        public int BinCount => Magnitudes.GetLength(1);

        public SpectrogramResult(float[,] magnitudes, float[] frequencies, float[] times, int sampleRate, int windowSize)
        {
            Magnitudes = magnitudes;
            Frequencies = frequencies;
            Times = times;
            SampleRate = sampleRate;
            WindowSize = windowSize;
        }

        /// <summary>
        /// Computes time-averaged spectrum (Mean PSD in dB across all time frames).
        /// </summary>
        public float[] ComputeAverageSpectrum()
        {
            if (FrameCount == 0) return new float[0];
            float[] avg = new float[BinCount];
            for (int k = 0; k < BinCount; k++)
            {
                float sum = 0f;
                for (int f = 0; f < FrameCount; f++)
                {
                    sum += Magnitudes[f, k];
                }
                avg[k] = sum / FrameCount;
            }
            return avg;
        }
    }
}
