using System;

namespace ZeroGraphics.Core.Math
{
    /// <summary>
    /// Represents a single-precision 32-bit complex number (real and imaginary components).
    /// Used for high-throughput zero-allocation digital signal processing.
    /// </summary>
    public struct ComplexF
    {
        public float Real;
        public float Imaginary;

        public ComplexF(float real, float imaginary)
        {
            Real = real;
            Imaginary = imaginary;
        }

        public float Magnitude => (float)System.Math.Sqrt(Real * Real + Imaginary * Imaginary);

        public float MagnitudeSquared => Real * Real + Imaginary * Imaginary;

        public static ComplexF operator +(ComplexF a, ComplexF b)
            => new ComplexF(a.Real + b.Real, a.Imaginary + b.Imaginary);

        public static ComplexF operator -(ComplexF a, ComplexF b)
            => new ComplexF(a.Real - b.Real, a.Imaginary - b.Imaginary);

        public static ComplexF operator *(ComplexF a, ComplexF b)
            => new ComplexF(a.Real * b.Real - a.Imaginary * b.Imaginary, a.Real * b.Imaginary + a.Imaginary * b.Real);

        public static ComplexF operator *(ComplexF a, float scalar)
            => new ComplexF(a.Real * scalar, a.Imaginary * scalar);

        public override string ToString() => $"({Real:F4}, {Imaginary:F4}i)";
    }

    /// <summary>
    /// Spectral analysis window functions to suppress side-lobe leakage.
    /// </summary>
    public enum WindowFunction
    {
        Rectangular,
        Hann,
        Hamming,
        Blackman
    }

    /// <summary>
    /// Ultra-fast, zero-allocation 1D Radix-2 Decimation-in-Time Fast Fourier Transform (FFT).
    /// Essential for frequency domain telemetry, vibration analysis, and audio/waveform spectral rendering.
    /// </summary>
    public static class FastFourierTransform
    {
        public static bool IsPowerOfTwo(int n) => n > 0 && (n & (n - 1)) == 0;

        public static int NextPowerOfTwo(int n)
        {
            if (n <= 1) return 1;
            int power = 1;
            while (power < n) power <<= 1;
            return power;
        }

        /// <summary>
        /// In-place Radix-2 Decimation-in-Time FFT / IFFT algorithm.
        /// Requires buffer length to be an exact power of 2.
        /// </summary>
        /// <param name="buffer">Complex buffer transformed in-place.</param>
        /// <param name="inverse">True for Inverse FFT (IFFT), false for forward FFT.</param>
        public static void Fft(Span<ComplexF> buffer, bool inverse = false)
        {
            int n = buffer.Length;
            if (!IsPowerOfTwo(n))
                throw new ArgumentException($"FFT buffer length must be a power of two. Got {n}.", nameof(buffer));

            if (n <= 1) return;

            // 1. Bit-reversal permutation
            int j = 0;
            for (int i = 0; i < n - 1; i++)
            {
                if (i < j)
                {
                    ComplexF temp = buffer[i];
                    buffer[i] = buffer[j];
                    buffer[j] = temp;
                }

                int k = n >> 1;
                while (k <= j)
                {
                    j -= k;
                    k >>= 1;
                }
                j += k;
            }

            // 2. Cooley-Tukey butterfly computation stages
            double angleSign = inverse ? 1.0 : -1.0;

            for (int len = 2; len <= n; len <<= 1)
            {
                int halfLen = len >> 1;
                double angleStep = angleSign * (2.0 * System.Math.PI / len);

                // Incremental twiddle factor calculation
                double wStepReal = System.Math.Cos(angleStep);
                double wStepImag = System.Math.Sin(angleStep);

                for (int i = 0; i < n; i += len)
                {
                    double wReal = 1.0;
                    double wImag = 0.0;

                    for (int m = 0; m < halfLen; m++)
                    {
                        int uIdx = i + m;
                        int vIdx = i + m + halfLen;

                        ComplexF u = buffer[uIdx];
                        ComplexF v = buffer[vIdx];

                        // t = v * W
                        float tReal = (float)(v.Real * wReal - v.Imaginary * wImag);
                        float tImag = (float)(v.Real * wImag + v.Imaginary * wReal);

                        buffer[uIdx] = new ComplexF(u.Real + tReal, u.Imaginary + tImag);
                        buffer[vIdx] = new ComplexF(u.Real - tReal, u.Imaginary - tImag);

                        // Advance twiddle
                        double nextWReal = wReal * wStepReal - wImag * wStepImag;
                        double nextWImag = wReal * wStepImag + wImag * wStepReal;
                        wReal = nextWReal;
                        wImag = nextWImag;
                    }
                }
            }

            // 3. Normalization for IFFT
            if (inverse)
            {
                float invN = 1.0f / n;
                for (int i = 0; i < n; i++)
                {
                    buffer[i].Real *= invN;
                    buffer[i].Imaginary *= invN;
                }
            }
        }

        /// <summary>
        /// Multiplies a time-series buffer with a specified spectral window function in-place.
        /// </summary>
        public static void ApplyWindow(Span<float> timeSeries, WindowFunction window)
        {
            int n = timeSeries.Length;
            if (n <= 1 || window == WindowFunction.Rectangular) return;

            double denom = n - 1.0;

            switch (window)
            {
                case WindowFunction.Hann:
                    for (int i = 0; i < n; i++)
                    {
                        float w = (float)(0.5 * (1.0 - System.Math.Cos(2.0 * System.Math.PI * i / denom)));
                        timeSeries[i] *= w;
                    }
                    break;

                case WindowFunction.Hamming:
                    for (int i = 0; i < n; i++)
                    {
                        float w = (float)(0.54 - 0.46 * System.Math.Cos(2.0 * System.Math.PI * i / denom));
                        timeSeries[i] *= w;
                    }
                    break;

                case WindowFunction.Blackman:
                    for (int i = 0; i < n; i++)
                    {
                        double a0 = 0.42;
                        double a1 = 0.5;
                        double a2 = 0.08;
                        float w = (float)(a0 - a1 * System.Math.Cos(2.0 * System.Math.PI * i / denom)
                                             + a2 * System.Math.Cos(4.0 * System.Math.PI * i / denom));
                        timeSeries[i] *= w;
                    }
                    break;
            }
        }

        /// <summary>
        /// Computes the single-sided magnitude spectrum of a real-valued time-series signal.
        /// Writes (fftSize / 2 + 1) bins to outputMagnitudes.
        /// </summary>
        /// <param name="timeSeries">Input samples (can be any length, will be zero-padded to next power of 2).</param>
        /// <param name="outputMagnitudes">Destination span for single-sided magnitude values (length must be at least fftSize / 2 + 1).</param>
        /// <param name="window">Windowing function applied to reduce spectral leakage.</param>
        public static void ComputeMagnitudeSpectrum(
            ReadOnlySpan<float> timeSeries,
            Span<float> outputMagnitudes,
            WindowFunction window = WindowFunction.Hann)
        {
            if (timeSeries.Length == 0) throw new ArgumentException("Time series cannot be empty.", nameof(timeSeries));

            int fftSize = NextPowerOfTwo(timeSeries.Length);
            int halfSize = fftSize / 2;

            if (outputMagnitudes.Length < halfSize + 1)
                throw new ArgumentException($"Output buffer too small. Required at least {halfSize + 1}, got {outputMagnitudes.Length}.", nameof(outputMagnitudes));

            // Allocate temporary working buffer
            ComplexF[] workBuf = new ComplexF[fftSize];
            int n = timeSeries.Length;

            for (int i = 0; i < n; i++)
            {
                workBuf[i] = new ComplexF(timeSeries[i], 0f);
            }

            // Apply window to real part
            if (window != WindowFunction.Rectangular)
            {
                double denom = n - 1.0;
                for (int i = 0; i < n; i++)
                {
                    float w = window switch
                    {
                        WindowFunction.Hann => (float)(0.5 * (1.0 - System.Math.Cos(2.0 * System.Math.PI * i / denom))),
                        WindowFunction.Hamming => (float)(0.54 - 0.46 * System.Math.Cos(2.0 * System.Math.PI * i / denom)),
                        WindowFunction.Blackman => (float)(0.42 - 0.5 * System.Math.Cos(2.0 * System.Math.PI * i / denom)
                                                                 + 0.08 * System.Math.Cos(4.0 * System.Math.PI * i / denom)),
                        _ => 1.0f
                    };
                    workBuf[i].Real *= w;
                }
            }

            // Run in-place FFT
            Fft(workBuf.AsSpan());

            // Compute single-sided calibrated magnitude spectrum with window coherent gain compensation
            float coherentGain = window switch
            {
                WindowFunction.Hann => 0.5f,
                WindowFunction.Hamming => 0.54f,
                WindowFunction.Blackman => 0.42f,
                _ => 1.0f
            };

            float scale = (2.0f / fftSize) / coherentGain;
            float dcScale = (1.0f / fftSize) / coherentGain;

            outputMagnitudes[0] = workBuf[0].Magnitude * dcScale;

            for (int k = 1; k < halfSize; k++)
            {
                outputMagnitudes[k] = workBuf[k].Magnitude * scale;
            }

            outputMagnitudes[halfSize] = workBuf[halfSize].Magnitude * dcScale;
        }
    }
}
