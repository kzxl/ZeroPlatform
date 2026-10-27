using System;

namespace ZeroSignal.Core.Filtering
{
    /// <summary>
    /// Represents a Second-Order Section (SOS / Biquad) IIR filter:
    /// H(z) = (b0 + b1*z^-1 + b2*z^-2) / (1 + a1*z^-1 + a2*z^-2).
    /// Implemented using Direct Form II Transposed for optimal numerical precision and stability.
    /// </summary>
    public sealed class BiquadSection
    {
        public double B0 { get; }
        public double B1 { get; }
        public double B2 { get; }
        public double A1 { get; }
        public double A2 { get; }

        private double _s1;
        private double _s2;

        public BiquadSection(double b0, double b1, double b2, double a1, double a2)
        {
            B0 = b0;
            B1 = b1;
            B2 = b2;
            A1 = a1;
            A2 = a2;
            _s1 = 0.0;
            _s2 = 0.0;
        }

        public BiquadSection(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            if (Math.Abs(a0) < 1e-15)
                throw new ArgumentException("a0 coefficient cannot be zero.", nameof(a0));

            // Normalize so that a0 == 1
            B0 = b0 / a0;
            B1 = b1 / a0;
            B2 = b2 / a0;
            A1 = a1 / a0;
            A2 = a2 / a0;
            _s1 = 0.0;
            _s2 = 0.0;
        }

        /// <summary>
        /// Processes a single input sample through the biquad section in Direct Form II Transposed.
        /// </summary>
        public double ProcessSample(double x)
        {
            double y = B0 * x + _s1;
            _s1 = B1 * x - A1 * y + _s2;
            _s2 = B2 * x - A2 * y;
            return y;
        }

        /// <summary>
        /// Sets initial state variables to match steady-state response for a constant DC input.
        /// Essential for zero-phase filtfilt boundary reflection.
        /// </summary>
        public void SetInitialState(double s1, double s2)
        {
            _s1 = s1;
            _s2 = s2;
        }

        /// <summary>
        /// Resets filter state memory to zero.
        /// </summary>
        public void Reset()
        {
            _s1 = 0.0;
            _s2 = 0.0;
        }

        /// <summary>
        /// Creates an independent clone of this biquad section with clean state memory.
        /// </summary>
        public BiquadSection Clone() => new BiquadSection(B0, B1, B2, A1, A2);
    }
}
