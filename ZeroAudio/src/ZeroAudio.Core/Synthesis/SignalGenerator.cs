using System;
using System.Runtime.CompilerServices;

namespace ZeroAudio.Synthesis
{
    /// <summary>
    /// High-performance synthetic waveform, noise, and DTMF telephone tone generator.
    /// Operates directly into output Spans with zero heap allocation.
    /// </summary>
    public static class SignalGenerator
    {
        private const double TwoPi = 2.0 * Math.PI;

        // DTMF Standard frequencies (Hz)
        // Row frequencies: 697, 770, 852, 941
        // Col frequencies: 1209, 1336, 1477, 1633
        private static readonly (float low, float high)[] DtmfFrequencies = new (float, float)[128];

        static SignalGenerator()
        {
            // Initialize DTMF table
            DtmfFrequencies['1'] = (697f, 1209f);
            DtmfFrequencies['2'] = (697f, 1336f);
            DtmfFrequencies['3'] = (697f, 1477f);
            DtmfFrequencies['A'] = (697f, 1633f);

            DtmfFrequencies['4'] = (770f, 1209f);
            DtmfFrequencies['5'] = (770f, 1336f);
            DtmfFrequencies['6'] = (770f, 1477f);
            DtmfFrequencies['B'] = (770f, 1633f);

            DtmfFrequencies['7'] = (852f, 1209f);
            DtmfFrequencies['8'] = (852f, 1336f);
            DtmfFrequencies['9'] = (852f, 1477f);
            DtmfFrequencies['C'] = (852f, 1633f);

            DtmfFrequencies['*'] = (941f, 1209f);
            DtmfFrequencies['0'] = (941f, 1336f);
            DtmfFrequencies['#'] = (941f, 1477f);
            DtmfFrequencies['D'] = (941f, 1633f);
        }

        /// <summary>
        /// Generates a continuous pure sine wave into the destination buffer.
        /// </summary>
        public static void GenerateSine(Span<float> buffer, float frequency, int sampleRate, ref double phase, float amplitude = 1.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double phaseStep = (TwoPi * frequency) / sampleRate;

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (float)(Math.Sin(phase) * amplitude);
                phase += phaseStep;
                if (phase >= TwoPi) phase -= TwoPi;
            }
        }

        /// <summary>
        /// Generates a square wave into the destination buffer.
        /// </summary>
        public static void GenerateSquare(Span<float> buffer, float frequency, int sampleRate, ref double phase, float amplitude = 1.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double phaseStep = (TwoPi * frequency) / sampleRate;

            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (phase < Math.PI) ? amplitude : -amplitude;
                phase += phaseStep;
                if (phase >= TwoPi) phase -= TwoPi;
            }
        }

        /// <summary>
        /// Generates a triangle wave into the destination buffer.
        /// </summary>
        public static void GenerateTriangle(Span<float> buffer, float frequency, int sampleRate, ref double phase, float amplitude = 1.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double phaseStep = (TwoPi * frequency) / sampleRate;

            for (int i = 0; i < buffer.Length; i++)
            {
                double normalized = phase / TwoPi; // 0 to 1
                double tri = (normalized < 0.5) ? (4.0 * normalized - 1.0) : (3.0 - 4.0 * normalized);
                buffer[i] = (float)(tri * amplitude);

                phase += phaseStep;
                if (phase >= TwoPi) phase -= TwoPi;
            }
        }

        /// <summary>
        /// Generates a sawtooth wave into the destination buffer.
        /// </summary>
        public static void GenerateSawtooth(Span<float> buffer, float frequency, int sampleRate, ref double phase, float amplitude = 1.0f)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            double phaseStep = (TwoPi * frequency) / sampleRate;

            for (int i = 0; i < buffer.Length; i++)
            {
                double normalized = phase / TwoPi; // 0 to 1
                buffer[i] = (float)((2.0 * normalized - 1.0) * amplitude);

                phase += phaseStep;
                if (phase >= TwoPi) phase -= TwoPi;
            }
        }

#if NET6_0_OR_GREATER
        private static Random GetRng(int? seed) => seed.HasValue ? new Random(seed.Value) : Random.Shared;
#else
        [ThreadStatic]
        private static Random? s_threadRng;
        private static Random GetRng(int? seed) => seed.HasValue ? new Random(seed.Value) : (s_threadRng ??= new Random());
#endif

        /// <summary>
        /// Generates uncorrelated White Gaussian/Uniform Noise into the buffer.
        /// </summary>
        public static void GenerateWhiteNoise(Span<float> buffer, float amplitude = 1.0f, int? seed = null)
        {
            var rng = GetRng(seed);
            for (int i = 0; i < buffer.Length; i++)
            {
                // Uniform noise between -amplitude and +amplitude
                buffer[i] = (float)((rng.NextDouble() * 2.0 - 1.0) * amplitude);
            }
        }

        /// <summary>
        /// Generates 1/f Pink Noise using the Voss-McCartney algorithm with 3-pole IIR filter approximation.
        /// </summary>
        public static void GeneratePinkNoise(Span<float> buffer, float amplitude = 1.0f, int? seed = null)
        {
            var rng = GetRng(seed);
            float b0 = 0f, b1 = 0f, b2 = 0f;

            for (int i = 0; i < buffer.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);

                // Paul Kellet's refined 3-pole pink noise filter
                b0 = 0.99765f * b0 + white * 0.0990460f;
                b1 = 0.96300f * b1 + white * 0.2965164f;
                b2 = 0.57000f * b2 + white * 1.0526913f;

                float pink = b0 + b1 + b2 + white * 0.1848f;
                buffer[i] = pink * 0.1f * amplitude;
            }
        }

        /// <summary>
        /// Generates a Dual-Tone Multi-Frequency (DTMF) dial tone (e.g. '0'-'9', '*', '#', 'A'-'D') into the buffer.
        /// </summary>
        public static bool GenerateDtmf(Span<float> buffer, char key, int sampleRate, ref double phaseLow, ref double phaseHigh, float amplitude = 0.5f)
        {
            if (key >= 128) return false;
            var (fLow, fHigh) = DtmfFrequencies[key];
            if (fLow == 0f || fHigh == 0f) return false;

            double stepLow = (TwoPi * fLow) / sampleRate;
            double stepHigh = (TwoPi * fHigh) / sampleRate;

            for (int i = 0; i < buffer.Length; i++)
            {
                float sample = (float)((Math.Sin(phaseLow) + Math.Sin(phaseHigh)) * 0.5 * amplitude);
                buffer[i] = sample;

                phaseLow += stepLow;
                if (phaseLow >= TwoPi) phaseLow -= TwoPi;

                phaseHigh += stepHigh;
                if (phaseHigh >= TwoPi) phaseHigh -= TwoPi;
            }

            return true;
        }
    }
}
