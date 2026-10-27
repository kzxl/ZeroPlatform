using System;
using System.Collections.Generic;

namespace ZeroSignal.Core.Filtering
{
    /// <summary>
    /// Represents a digital IIR filter composed of cascaded Second-Order Sections (biquads).
    /// Provides forward filtering, in-place filtering, and zero-phase forward-backward filtering (FiltFilt).
    /// </summary>
    public sealed class DigitalFilter
    {
        private readonly List<BiquadSection> _sections;

        public IReadOnlyList<BiquadSection> Sections => _sections;

        public int SectionCount => _sections.Count;

        public int Order => _sections.Count * 2;

        public DigitalFilter(IEnumerable<BiquadSection> sections)
        {
            if (sections == null) throw new ArgumentNullException(nameof(sections));
            _sections = new List<BiquadSection>(sections);
            if (_sections.Count == 0)
                throw new ArgumentException("Filter must contain at least one biquad section.", nameof(sections));
        }

        public DigitalFilter(params BiquadSection[] sections)
            : this((IEnumerable<BiquadSection>)sections)
        {
        }

        /// <summary>
        /// Filters an input signal through the cascaded biquads. Thread-safe (does not mutate instance state).
        /// </summary>
        public double[] Filter(ReadOnlySpan<double> input)
        {
            if (input.Length == 0) return Array.Empty<double>();

            var output = input.ToArray();
            FilterInPlace(output.AsSpan());
            return output;
        }

        /// <summary>
        /// Filters the given buffer in-place through all cascaded biquad sections.
        /// </summary>
        public void FilterInPlace(Span<double> data)
        {
            if (data.Length == 0) return;

            for (int s = 0; s < _sections.Count; s++)
            {
                var runner = _sections[s].Clone();
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = runner.ProcessSample(data[i]);
                }
            }
        }

        /// <summary>
        /// Performs zero-phase forward-backward filtering (FiltFilt) with boundary reflection.
        /// Phase distortion is identically zero with doubled filter order attenuation.
        /// </summary>
        public double[] FiltFilt(ReadOnlySpan<double> input)
        {
            int n = input.Length;
            if (n == 0) return Array.Empty<double>();
            if (n < 4)
            {
                // Very short signal: forward filter fallback
                return Filter(input);
            }

            // Determine pad length (standard: 3 * order, capped at n - 1)
            int padLen = Math.Min(Math.Max(Order * 3, 6), n - 1);
            int totalLen = n + 2 * padLen;
            double[] padded = new double[totalLen];

            // Left pad: 2 * x[0] - x[padLen..1]
            double x0 = input[0];
            for (int i = 0; i < padLen; i++)
            {
                padded[i] = 2.0 * x0 - input[padLen - i];
            }

            // Center: copy input
            for (int i = 0; i < n; i++)
            {
                padded[padLen + i] = input[i];
            }

            // Right pad: 2 * x[n-1] - x[n-2..n-1-padLen]
            double xEnd = input[n - 1];
            for (int i = 0; i < padLen; i++)
            {
                padded[padLen + n + i] = 2.0 * xEnd - input[n - 2 - i];
            }

            // Forward filtering pass with DC steady-state initialization
            FilterForwardWithSteadyState(padded, padded[0]);

            // Reverse buffer for backward pass
            Array.Reverse(padded);

            // Backward filtering pass with DC steady-state initialization
            FilterForwardWithSteadyState(padded, padded[0]);

            // Reverse back to restore correct chronological order
            Array.Reverse(padded);

            // Extract unpadded center
            double[] result = new double[n];
            Array.Copy(padded, padLen, result, 0, n);
            return result;
        }

        private void FilterForwardWithSteadyState(double[] data, double initialVal)
        {
            double currentVal = initialVal;
            for (int s = 0; s < _sections.Count; s++)
            {
                var runner = _sections[s].Clone();

                // Compute DC steady state for direct form II transposed:
                // H(1) = (b0 + b1 + b2) / (1 + a1 + a2)
                double den = 1.0 + runner.A1 + runner.A2;
                double gain = Math.Abs(den) > 1e-12 ? (runner.B0 + runner.B1 + runner.B2) / den : 1.0;
                double yss = gain * currentVal;

                double s2 = (runner.B2 - runner.A2 * gain) * currentVal;
                double s1 = (runner.B1 - runner.A1 * gain) * currentVal + s2;
                runner.SetInitialState(s1, s2);

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = runner.ProcessSample(data[i]);
                }
                currentVal = yss;
            }
        }
    }
}
