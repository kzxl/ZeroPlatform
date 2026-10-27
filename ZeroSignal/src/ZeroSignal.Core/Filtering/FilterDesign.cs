using System;
using System.Collections.Generic;

namespace ZeroSignal.Core.Filtering
{
    public enum FilterType
    {
        Lowpass,
        Highpass,
        Bandpass,
        Bandstop
    }

    /// <summary>
    /// Synthesis of analog prototypes and digital IIR filter transformations via Bilinear Transform.
    /// Provides Butterworth, Chebyshev Type I, and Biquad peaking/notch designs.
    /// </summary>
    public static class FilterDesign
    {
        /// <summary>
        /// Designs an N-th order digital Butterworth IIR filter.
        /// </summary>
        /// <param name="order">Filter order (typically 1 to 10).</param>
        /// <param name="cutoffHz">Cutoff (-3dB) frequency in Hz.</param>
        /// <param name="sampleRateHz">Sampling rate in Hz.</param>
        /// <param name="type">Filter type (Lowpass or Highpass).</param>
        public static DigitalFilter Butterworth(int order, double cutoffHz, double sampleRateHz, FilterType type = FilterType.Lowpass)
        {
            if (order < 1) throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be at least 1.");
            if (sampleRateHz <= 0.0) throw new ArgumentOutOfRangeException(nameof(sampleRateHz), "Sample rate must be positive.");
            if (cutoffHz <= 0.0 || cutoffHz >= sampleRateHz / 2.0)
                throw new ArgumentOutOfRangeException(nameof(cutoffHz), $"Cutoff frequency must be between 0 and Nyquist ({sampleRateHz / 2.0} Hz).");

            double fcNorm = cutoffHz / sampleRateHz;
            double k = Math.Tan(Math.PI * fcNorm);

            var sections = new List<BiquadSection>();

            // Conjugate pole pairs (2 poles per biquad)
            int pairs = order / 2;
            for (int i = 1; i <= pairs; i++)
            {
                double theta = Math.PI * (2 * i - 1) / (2.0 * order);
                double q = 1.0 / (2.0 * Math.Sin(theta));

                if (type == FilterType.Lowpass)
                {
                    double norm = 1.0 + k / q + k * k;
                    double b0 = (k * k) / norm;
                    double b1 = 2.0 * b0;
                    double b2 = b0;
                    double a1 = 2.0 * (k * k - 1.0) / norm;
                    double a2 = (1.0 - k / q + k * k) / norm;
                    sections.Add(new BiquadSection(b0, b1, b2, a1, a2));
                }
                else if (type == FilterType.Highpass)
                {
                    double norm = 1.0 + k / q + k * k;
                    double b0 = 1.0 / norm;
                    double b1 = -2.0 * b0;
                    double b2 = b0;
                    double a1 = 2.0 * (k * k - 1.0) / norm;
                    double a2 = (1.0 - k / q + k * k) / norm;
                    sections.Add(new BiquadSection(b0, b1, b2, a1, a2));
                }
                else
                {
                    throw new NotSupportedException($"Filter type {type} is not supported directly in Butterworth order synthesis.");
                }
            }

            // Odd order single real pole (1st order section represented as biquad with b2=0, a2=0)
            if (order % 2 != 0)
            {
                if (type == FilterType.Lowpass)
                {
                    double norm = 1.0 + k;
                    double b0 = k / norm;
                    double b1 = b0;
                    double a1 = (k - 1.0) / norm;
                    sections.Add(new BiquadSection(b0, b1, 0.0, a1, 0.0));
                }
                else if (type == FilterType.Highpass)
                {
                    double norm = 1.0 + k;
                    double b0 = 1.0 / norm;
                    double b1 = -b0;
                    double a1 = (k - 1.0) / norm;
                    sections.Add(new BiquadSection(b0, b1, 0.0, a1, 0.0));
                }
            }

            return new DigitalFilter(sections);
        }

        /// <summary>
        /// Designs an N-th order digital Chebyshev Type I IIR filter with specified passband ripple.
        /// </summary>
        /// <param name="order">Filter order.</param>
        /// <param name="rippleDb">Passband ripple in dB (e.g. 0.5, 1.0 dB).</param>
        /// <param name="cutoffHz">Passband edge frequency in Hz.</param>
        /// <param name="sampleRateHz">Sampling rate in Hz.</param>
        /// <param name="type">Filter type (Lowpass or Highpass).</param>
        public static DigitalFilter Chebyshev1(int order, double rippleDb, double cutoffHz, double sampleRateHz, FilterType type = FilterType.Lowpass)
        {
            if (order < 1) throw new ArgumentOutOfRangeException(nameof(order), "Filter order must be at least 1.");
            if (rippleDb <= 0.0) throw new ArgumentOutOfRangeException(nameof(rippleDb), "Passband ripple must be positive.");
            if (cutoffHz <= 0.0 || cutoffHz >= sampleRateHz / 2.0)
                throw new ArgumentOutOfRangeException(nameof(cutoffHz), $"Cutoff frequency must be between 0 and Nyquist ({sampleRateHz / 2.0} Hz).");

            double eps = Math.Sqrt(Math.Pow(10.0, rippleDb / 10.0) - 1.0);
            double asinhVal = Math.Log(1.0 / eps + Math.Sqrt((1.0 / (eps * eps)) + 1.0));
            double mu = asinhVal / order;
            double sinhMu = Math.Sinh(mu);
            double coshMu = Math.Cosh(mu);

            double fcNorm = cutoffHz / sampleRateHz;
            double k = Math.Tan(Math.PI * fcNorm);

            var sections = new List<BiquadSection>();
            int pairs = order / 2;

            for (int i = 1; i <= pairs; i++)
            {
                double theta = Math.PI * (2 * i - 1) / (2.0 * order);
                double sigma = -sinhMu * Math.Sin(theta);
                double omega = coshMu * Math.Cos(theta);
                double w0 = Math.Sqrt(sigma * sigma + omega * omega);
                double q = w0 / (-2.0 * sigma);

                double w = k * w0;

                if (type == FilterType.Lowpass)
                {
                    double norm = 1.0 + w / q + w * w;
                    double b0 = (w * w) / norm;
                    double b1 = 2.0 * b0;
                    double b2 = b0;
                    double a1 = 2.0 * (w * w - 1.0) / norm;
                    double a2 = (1.0 - w / q + w * w) / norm;
                    sections.Add(new BiquadSection(b0, b1, b2, a1, a2));
                }
                else if (type == FilterType.Highpass)
                {
                    double norm = 1.0 + w / q + w * w;
                    double b0 = 1.0 / norm;
                    double b1 = -2.0 * b0;
                    double b2 = b0;
                    double a1 = 2.0 * (w * w - 1.0) / norm;
                    double a2 = (1.0 - w / q + w * w) / norm;
                    sections.Add(new BiquadSection(b0, b1, b2, a1, a2));
                }
            }

            if (order % 2 != 0)
            {
                double w = k * sinhMu;
                if (type == FilterType.Lowpass)
                {
                    double norm = 1.0 + w;
                    double b0 = w / norm;
                    double b1 = b0;
                    double a1 = (w - 1.0) / norm;
                    sections.Add(new BiquadSection(b0, b1, 0.0, a1, 0.0));
                }
                else if (type == FilterType.Highpass)
                {
                    double norm = 1.0 + w;
                    double b0 = 1.0 / norm;
                    double b1 = -b0;
                    double a1 = (w - 1.0) / norm;
                    sections.Add(new BiquadSection(b0, b1, 0.0, a1, 0.0));
                }
            }

            return new DigitalFilter(sections);
        }

        /// <summary>
        /// Creates a 2nd-order Bandpass filter centered at centerHz with specified Q factor.
        /// </summary>
        public static DigitalFilter Bandpass(double centerHz, double q, double sampleRateHz)
        {
            if (q <= 0.0) throw new ArgumentOutOfRangeException(nameof(q), "Quality factor Q must be positive.");
            double k = Math.Tan(Math.PI * centerHz / sampleRateHz);
            double norm = 1.0 + k / q + k * k;

            double b0 = (k / q) / norm;
            double b1 = 0.0;
            double b2 = -b0;
            double a1 = 2.0 * (k * k - 1.0) / norm;
            double a2 = (1.0 - k / q + k * k) / norm;

            return new DigitalFilter(new BiquadSection(b0, b1, b2, a1, a2));
        }

        /// <summary>
        /// Creates a 2nd-order Notch (Bandstop) filter to eliminate a specific interference frequency (e.g. 50/60Hz hum).
        /// </summary>
        public static DigitalFilter Notch(double notchHz, double q, double sampleRateHz)
        {
            if (q <= 0.0) throw new ArgumentOutOfRangeException(nameof(q), "Quality factor Q must be positive.");
            double k = Math.Tan(Math.PI * notchHz / sampleRateHz);
            double norm = 1.0 + k / q + k * k;

            double b0 = (1.0 + k * k) / norm;
            double b1 = 2.0 * (k * k - 1.0) / norm;
            double b2 = b0;
            double a1 = b1;
            double a2 = (1.0 - k / q + k * k) / norm;

            return new DigitalFilter(new BiquadSection(b0, b1, b2, a1, a2));
        }
    }
}
