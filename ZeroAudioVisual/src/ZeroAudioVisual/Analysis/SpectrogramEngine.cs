using System;

namespace ZeroAudioVisual.Analysis
{
    /// <summary>
    /// Window functions used for Fast Fourier Transform spectral leakage reduction.
    /// </summary>
    [Obsolete("WindowType has been moved to ZeroAudio.Analysis.WindowType in ZeroAudio.Core.")]
    public enum WindowType
    {
        Rectangular = 0,
        Hann = 1,
        Hamming = 2,
        Blackman = 3
    }

    /// <summary>
    /// High-performance Short-Time Fourier Transform (STFT) engine.
    /// Produces time-frequency spectrogram matrices for vibration and acoustic analysis.
    /// </summary>
    [Obsolete("SpectrogramEngine has been moved to ZeroAudio.Analysis.SpectrogramEngine in ZeroAudio.Core.")]
    public static class SpectrogramEngine
    {
        public static SpectrogramResult ComputeStft(
            ReadOnlySpan<float> signal,
            int sampleRate,
            int windowSize = 512,
            int hopSize = 256,
            WindowType windowType = WindowType.Hann)
        {
            var res = ZeroAudio.Analysis.SpectrogramEngine.ComputeStft(
                signal,
                sampleRate,
                windowSize,
                hopSize,
                (ZeroAudio.Analysis.WindowType)windowType);

            return new SpectrogramResult(res);
        }

        /// <summary>
        /// In-place Cooley-Tukey Radix-2 Fast Fourier Transform delegating to ZeroAudio.Analysis.FastFourierTransform.
        /// </summary>
        public static void FftRadix2(float[] real, float[] imag)
        {
            if (real == null) throw new ArgumentNullException(nameof(real));
            if (imag == null) throw new ArgumentNullException(nameof(imag));

            ZeroAudio.Analysis.FastFourierTransform.Forward(real, imag);
        }
    }

    [Obsolete("SpectrogramResult has been moved to ZeroAudio.Analysis.SpectrogramResult in ZeroAudio.Core.")]
    public class SpectrogramResult
    {
        private readonly ZeroAudio.Analysis.SpectrogramResult _inner;

        public float[,] Magnitudes => _inner.Magnitudes;
        public float[] Frequencies => _inner.Frequencies;
        public float[] Times => _inner.Times;
        public int SampleRate => _inner.SampleRate;
        public int WindowSize => _inner.WindowSize;

        public int FrameCount => _inner.FrameCount;
        public int BinCount => _inner.BinCount;

        public SpectrogramResult(float[,] magnitudes, float[] frequencies, float[] times, int sampleRate, int windowSize)
        {
            _inner = new ZeroAudio.Analysis.SpectrogramResult(magnitudes, frequencies, times, sampleRate, windowSize);
        }

        public SpectrogramResult(ZeroAudio.Analysis.SpectrogramResult inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        /// <summary>
        /// Computes time-averaged spectrum (Mean PSD in dB across all time frames).
        /// </summary>
        public float[] ComputeAverageSpectrum() => _inner.ComputeAverageSpectrum();
    }
}
