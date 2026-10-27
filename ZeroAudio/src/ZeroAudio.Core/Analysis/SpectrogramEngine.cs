using System;
using System.Buffers;
using ZeroAudio.Buffers;

namespace ZeroAudio.Analysis
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
    /// Minimal-allocation Short-Time Fourier Transform (STFT) engine.
    /// Produces time-frequency spectrogram matrices for audio and acoustic signal analysis.
    /// </summary>
    public static class SpectrogramEngine
    {
        /// <summary>
        /// Computes Short-Time Fourier Transform spectrogram from an audio signal.
        /// </summary>
        public static SpectrogramResult ComputeStft(
            ReadOnlySpan<float> signal,
            int sampleRate,
            int windowSize = 512,
            int hopSize = 256,
            WindowType windowType = WindowType.Hann)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (windowSize <= 0 || (windowSize & (windowSize - 1)) != 0)
                throw new ArgumentException("Window size must be a positive power of two.", nameof(windowSize));
            if (hopSize <= 0) throw new ArgumentOutOfRangeException(nameof(hopSize));

            int totalSamples = signal.Length;
            if (totalSamples < windowSize)
            {
                return new SpectrogramResult(new float[0, 0], new float[0], new float[0], sampleRate, windowSize);
            }

            int numFrames = (totalSamples - windowSize) / hopSize + 1;
            int numBins = windowSize / 2 + 1;

            float[,] spectrogram = new float[numFrames, numBins];
            float[] times = new float[numFrames];
            float[] window = CreateWindow(windowSize, windowType);

            // Rent temporary working buffers from ArrayPool
            float[] realPool = ArrayPool<float>.Shared.Rent(windowSize);
            float[] imagPool = ArrayPool<float>.Shared.Rent(windowSize);

            try
            {
                Span<float> real = realPool.AsSpan(0, windowSize);
                Span<float> imag = imagPool.AsSpan(0, windowSize);

                for (int f = 0; f < numFrames; f++)
                {
                    times[f] = (f * hopSize) / (float)sampleRate;
                    int frameOffset = f * hopSize;

                    // Apply windowing
                    for (int i = 0; i < windowSize; i++)
                    {
                        real[i] = signal[frameOffset + i] * window[i];
                        imag[i] = 0.0f;
                    }

                    // In-place Radix-2 FFT
                    FastFourierTransform.Forward(real, imag);

                    // Compute Magnitude in decibels (dB)
                    float normFactor = windowSize / 2.0f;
                    for (int k = 0; k < numBins; k++)
                    {
                        float r = real[k];
                        float im = imag[k];
                        float mag = (float)Math.Sqrt(r * r + im * im) / normFactor;
                        float db = (mag > 1e-7f) ? 20.0f * (float)Math.Log10(mag) : -140.0f;
                        spectrogram[f, k] = db;
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(realPool);
                ArrayPool<float>.Shared.Return(imagPool);
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

        /// <summary>
        /// Computes Short-Time Fourier Transform spectrogram from a channel in an AudioBuffer.
        /// </summary>
        public static SpectrogramResult ComputeStft(
            AudioBuffer buffer,
            int channel = 0,
            int windowSize = 512,
            int hopSize = 256,
            WindowType windowType = WindowType.Hann)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (channel < 0 || channel >= buffer.Channels)
                throw new ArgumentOutOfRangeException(nameof(channel));

            if (buffer.Channels == 1)
            {
                return ComputeStft(buffer.Samples, buffer.SampleRate, windowSize, hopSize, windowType);
            }

            float[] rented = ArrayPool<float>.Shared.Rent(buffer.FrameCount);
            try
            {
                var span = rented.AsSpan(0, buffer.FrameCount);
                buffer.DeinterleaveChannel(channel, span);
                return ComputeStft(span, buffer.SampleRate, windowSize, hopSize, windowType);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(rented);
            }
        }

        private static float[] CreateWindow(int size, WindowType type)
        {
            float[] w = new float[size];
            for (int i = 0; i < size; i++)
            {
                switch (type)
                {
                    case WindowType.Hann:
                        w[i] = 0.5f * (1.0f - (float)Math.Cos(2.0 * Math.PI * i / (size - 1)));
                        break;
                    case WindowType.Hamming:
                        w[i] = 0.54f - 0.46f * (float)Math.Cos(2.0 * Math.PI * i / (size - 1));
                        break;
                    case WindowType.Blackman:
                        w[i] = 0.42f - 0.5f * (float)Math.Cos(2.0 * Math.PI * i / (size - 1)) + 0.08f * (float)Math.Cos(4.0 * Math.PI * i / (size - 1));
                        break;
                    default:
                        w[i] = 1.0f;
                        break;
                }
            }
            return w;
        }
    }

    /// <summary>
    /// Represents the computed STFT spectrogram matrix and time/frequency axes.
    /// </summary>
    public sealed class SpectrogramResult
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
            Magnitudes = magnitudes ?? new float[0, 0];
            Frequencies = frequencies ?? Array.Empty<float>();
            Times = times ?? Array.Empty<float>();
            SampleRate = sampleRate;
            WindowSize = windowSize;
        }

        /// <summary>
        /// Computes time-averaged spectrum (Mean PSD in dB across all time frames).
        /// </summary>
        public float[] ComputeAverageSpectrum()
        {
            if (FrameCount == 0) return Array.Empty<float>();
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
