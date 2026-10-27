using System;
using System.Runtime.CompilerServices;

namespace ZeroAudio.Processing
{
    /// <summary>
    /// Pan laws defining how volume is balanced between left and right channels.
    /// </summary>
    public enum PanLaw
    {
        /// <summary>Center channel attenuated by 3dB to maintain equal acoustic power.</summary>
        EqualPower = 0,

        /// <summary>Linear attenuation between left and right.</summary>
        Linear = 1
    }

    /// <summary>
    /// High-performance audio gain, panning, fading, and soft-saturation processor.
    /// </summary>
    public static class GainController
    {
        private const double PiOverFour = Math.PI / 4.0;

        /// <summary>
        /// Converts a linear amplitude factor to decibels (dB).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float LinearToDb(float linear)
        {
            if (linear <= 0.000001f) return -120.0f;
            return (float)(20.0 * Math.Log10(linear));
        }

        /// <summary>
        /// Converts a decibel level (dB) to a linear amplitude factor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DbToLinear(float db)
        {
            return (float)Math.Pow(10.0, db / 20.0);
        }

        /// <summary>
        /// Scales the amplitude of all samples in the buffer by a linear gain factor.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyGain(Span<float> samples, float gain)
        {
            if (gain == 1.0f) return;
            if (gain == 0.0f)
            {
                samples.Clear();
                return;
            }

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] *= gain;
            }
        }

        /// <summary>
        /// Scales amplitude by a decibel adjustment value (e.g. -6.0f dB or +3.0f dB).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyGainDb(Span<float> samples, float gainDb)
        {
            if (gainDb == 0.0f) return;
            ApplyGain(samples, DbToLinear(gainDb));
        }

        /// <summary>
        /// Applies stereo panning to an interleaved stereo buffer.
        /// </summary>
        /// <param name="stereo">Interleaved stereo samples [L, R, L, R...].</param>
        /// <param name="pan">Pan position from -1.0f (Full Left) to 0.0f (Center) to +1.0f (Full Right).</param>
        /// <param name="panLaw">Pan law curve (EqualPower or Linear).</param>
        public static void ApplyPan(Span<float> stereo, float pan, PanLaw panLaw = PanLaw.EqualPower)
        {
            pan = Math.Max(-1.0f, Math.Min(1.0f, pan));
            if (pan == 0.0f && panLaw == PanLaw.Linear) return;

            float leftGain;
            float rightGain;

            if (panLaw == PanLaw.EqualPower)
            {
                // Standard constant-power cosine/sine panning
                double angle = (pan + 1.0) * PiOverFour; // 0 to Pi/2
                leftGain = (float)Math.Cos(angle);
                rightGain = (float)Math.Sin(angle);
            }
            else
            {
                // Linear panning
                leftGain = 0.5f * (1.0f - pan);
                rightGain = 0.5f * (1.0f + pan);
            }

            int frames = stereo.Length / 2;
            for (int i = 0; i < frames; i++)
            {
                stereo[i * 2] *= leftGain;
                stereo[i * 2 + 1] *= rightGain;
            }
        }

        /// <summary>
        /// Applies an analog-modeled polynomial soft saturation curve to prevent harsh digital clipping.
        /// Signals below the threshold remain perfectly linear; signals above are smoothly compressed toward 1.0f.
        /// </summary>
        public static void ApplySoftClip(Span<float> samples, float threshold = 0.85f)
        {
            for (int i = 0; i < samples.Length; i++)
            {
                float x = samples[i];
                float abs = Math.Abs(x);

                if (abs > threshold)
                {
                    // Smooth cubic saturation above threshold
                    float excess = abs - threshold;
                    float range = 1.0f - threshold;
                    float compressed = threshold + range * (float)Math.Tanh(excess / range);
                    samples[i] = (x >= 0 ? compressed : -compressed);
                }
            }
        }

        /// <summary>
        /// Applies a smooth linear fade-in ramp across the specified audio samples.
        /// </summary>
        public static void ApplyFadeIn(Span<float> samples, int channels)
        {
            if (channels <= 0) return;
            int frames = samples.Length / channels;
            if (frames <= 0) return;

            float step = 1.0f / frames;
            for (int f = 0; f < frames; f++)
            {
                float gain = f * step;
                int offset = f * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    samples[offset + ch] *= gain;
                }
            }
        }

        /// <summary>
        /// Applies a smooth linear fade-out ramp across the specified audio samples.
        /// </summary>
        public static void ApplyFadeOut(Span<float> samples, int channels)
        {
            if (channels <= 0) return;
            int frames = samples.Length / channels;
            if (frames <= 0) return;

            float step = 1.0f / frames;
            for (int f = 0; f < frames; f++)
            {
                float gain = 1.0f - (f * step);
                int offset = f * channels;
                for (int ch = 0; ch < channels; ch++)
                {
                    samples[offset + ch] *= gain;
                }
            }
        }
    }
}
