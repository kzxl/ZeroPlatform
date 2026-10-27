using System;
using System.Runtime.CompilerServices;

namespace ZeroAudio.Equalization
{
    /// <summary>
    /// Types of standard biquad audio filters.
    /// </summary>
    public enum BiquadFilterType
    {
        LowPass,
        HighPass,
        BandPass,
        Notch,
        PeakingEq,
        LowShelf,
        HighShelf
    }

    /// <summary>
    /// Second-order IIR biquad filter implemented using Direct Form II Transposed architecture for maximum numerical stability.
    /// Derived from Robert Bristow-Johnson's Audio EQ Cookbook formulas.
    /// </summary>
    public sealed class BiquadFilter
    {
        // Normalized filter coefficients
        private float _b0, _b1, _b2;
        private float _a1, _a2;

        // Direct Form II Transposed delay state variables
        private float _z1, _z2;

        public BiquadFilterType FilterType { get; }
        public float Frequency { get; }
        public float Q { get; }
        public float GainDb { get; }

        public BiquadFilter(BiquadFilterType type, int sampleRate, float frequency, float q = 0.7071f, float gainDb = 0.0f)
        {
            FilterType = type;
            Frequency = frequency;
            Q = q;
            GainDb = gainDb;

            ComputeCoefficients(type, sampleRate, frequency, q, gainDb);
        }

        public static BiquadFilter CreateLowPass(int sampleRate, float cutoffHz, float q = 0.7071f) =>
            new BiquadFilter(BiquadFilterType.LowPass, sampleRate, cutoffHz, q);

        public static BiquadFilter CreateHighPass(int sampleRate, float cutoffHz, float q = 0.7071f) =>
            new BiquadFilter(BiquadFilterType.HighPass, sampleRate, cutoffHz, q);

        public static BiquadFilter CreatePeakingEq(int sampleRate, float centerHz, float q, float gainDb) =>
            new BiquadFilter(BiquadFilterType.PeakingEq, sampleRate, centerHz, q, gainDb);

        public static BiquadFilter CreateNotch(int sampleRate, float centerHz, float q = 10.0f) =>
            new BiquadFilter(BiquadFilterType.Notch, sampleRate, centerHz, q);

        public static BiquadFilter CreateLowShelf(int sampleRate, float cutoffHz, float gainDb) =>
            new BiquadFilter(BiquadFilterType.LowShelf, sampleRate, cutoffHz, 0.7071f, gainDb);

        public static BiquadFilter CreateHighShelf(int sampleRate, float cutoffHz, float gainDb) =>
            new BiquadFilter(BiquadFilterType.HighShelf, sampleRate, cutoffHz, 0.7071f, gainDb);

        private void ComputeCoefficients(BiquadFilterType type, int sampleRate, float f0, float q, float gainDb)
        {
            double w0 = 2.0 * Math.PI * f0 / sampleRate;
            double cosW0 = Math.Cos(w0);
            double sinW0 = Math.Sin(w0);
            double alpha = sinW0 / (2.0 * Math.Max(0.001, q));
            double a = Math.Pow(10.0, gainDb / 40.0); // For peaking & shelving EQ

            double b0 = 0, b1 = 0, b2 = 0;
            double a0 = 1, a1 = 0, a2 = 0;

            switch (type)
            {
                case BiquadFilterType.LowPass:
                    b0 = (1.0 - cosW0) / 2.0;
                    b1 = 1.0 - cosW0;
                    b2 = (1.0 - cosW0) / 2.0;
                    a0 = 1.0 + alpha;
                    a1 = -2.0 * cosW0;
                    a2 = 1.0 - alpha;
                    break;

                case BiquadFilterType.HighPass:
                    b0 = (1.0 + cosW0) / 2.0;
                    b1 = -(1.0 + cosW0);
                    b2 = (1.0 + cosW0) / 2.0;
                    a0 = 1.0 + alpha;
                    a1 = -2.0 * cosW0;
                    a2 = 1.0 - alpha;
                    break;

                case BiquadFilterType.BandPass:
                    b0 = alpha;
                    b1 = 0.0;
                    b2 = -alpha;
                    a0 = 1.0 + alpha;
                    a1 = -2.0 * cosW0;
                    a2 = 1.0 - alpha;
                    break;

                case BiquadFilterType.Notch:
                    b0 = 1.0;
                    b1 = -2.0 * cosW0;
                    b2 = 1.0;
                    a0 = 1.0 + alpha;
                    a1 = -2.0 * cosW0;
                    a2 = 1.0 - alpha;
                    break;

                case BiquadFilterType.PeakingEq:
                    b0 = 1.0 + alpha * a;
                    b1 = -2.0 * cosW0;
                    b2 = 1.0 - alpha * a;
                    a0 = 1.0 + alpha / a;
                    a1 = -2.0 * cosW0;
                    a2 = 1.0 - alpha / a;
                    break;

                case BiquadFilterType.LowShelf:
                {
                    double sqrtA = Math.Sqrt(a);
                    b0 = a * ((a + 1.0) - (a - 1.0) * cosW0 + 2.0 * sqrtA * alpha);
                    b1 = 2.0 * a * ((a - 1.0) - (a + 1.0) * cosW0);
                    b2 = a * ((a + 1.0) - (a - 1.0) * cosW0 - 2.0 * sqrtA * alpha);
                    a0 = (a + 1.0) + (a - 1.0) * cosW0 + 2.0 * sqrtA * alpha;
                    a1 = -2.0 * ((a - 1.0) + (a + 1.0) * cosW0);
                    a2 = (a + 1.0) + (a - 1.0) * cosW0 - 2.0 * sqrtA * alpha;
                    break;
                }

                case BiquadFilterType.HighShelf:
                {
                    double sqrtA = Math.Sqrt(a);
                    b0 = a * ((a + 1.0) + (a - 1.0) * cosW0 + 2.0 * sqrtA * alpha);
                    b1 = -2.0 * a * ((a - 1.0) + (a + 1.0) * cosW0);
                    b2 = a * ((a + 1.0) + (a - 1.0) * cosW0 - 2.0 * sqrtA * alpha);
                    a0 = (a + 1.0) - (a - 1.0) * cosW0 + 2.0 * sqrtA * alpha;
                    a1 = 2.0 * ((a - 1.0) - (a + 1.0) * cosW0);
                    a2 = (a + 1.0) - (a - 1.0) * cosW0 - 2.0 * sqrtA * alpha;
                    break;
                }
            }

            // Normalize by a0
            _b0 = (float)(b0 / a0);
            _b1 = (float)(b1 / a0);
            _b2 = (float)(b2 / a0);
            _a1 = (float)(a1 / a0);
            _a2 = (float)(a2 / a0);
        }

        /// <summary>
        /// Processes a single audio sample through the biquad filter.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float ProcessSample(float x)
        {
            // Direct Form II Transposed difference equation
            float y = _b0 * x + _z1;
            _z1 = _b1 * x - _a1 * y + _z2;
            _z2 = _b2 * x - _a2 * y;
            return y;
        }

        /// <summary>
        /// Filters a span of audio samples in-place.
        /// </summary>
        public void Process(Span<float> samples)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = ProcessSample(samples[i]);
            }
        }

        /// <summary>
        /// Resets the internal filter delay states to zero.
        /// </summary>
        public void Reset()
        {
            _z1 = 0f;
            _z2 = 0f;
        }
    }
}
