using System;
using System.Runtime.CompilerServices;
using ZeroAudio.Buffers;
using ZeroAudio.Processing;

namespace ZeroAudio.Synthesis
{
    /// <summary>
    /// Multi-track audio summing and mixing engine.
    /// Supports weighted summation, master bus soft-clipping, and zero heap allocations during mixing loops.
    /// </summary>
    public static class AudioMixer
    {
        /// <summary>
        /// Mixes (sums) source samples into the destination buffer with a specific track gain.
        /// Destination samples are updated in-place: dest[i] += source[i] * gain.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Mix(ReadOnlySpan<float> source, Span<float> destination, float gain = 1.0f)
        {
            if (gain == 0.0f) return;
            int count = Math.Min(source.Length, destination.Length);

            if (gain == 1.0f)
            {
                for (int i = 0; i < count; i++)
                {
                    destination[i] += source[i];
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    destination[i] += source[i] * gain;
                }
            }
        }

        /// <summary>
        /// Clears destination and mixes multiple memory tracks into the master output buffer, optionally applying soft saturation.
        /// </summary>
        public static void MixTracks(
            ReadOnlyMemory<float>[] tracks,
            float[]? trackGains,
            Span<float> masterOutput,
            bool applySoftSaturation = true)
        {
            if (tracks == null || tracks.Length == 0)
            {
                masterOutput.Clear();
                return;
            }

            masterOutput.Clear();

            for (int t = 0; t < tracks.Length; t++)
            {
                float gain = (trackGains != null && t < trackGains.Length) ? trackGains[t] : 1.0f;
                Mix(tracks[t].Span, masterOutput, gain);
            }

            if (applySoftSaturation)
            {
                GainController.ApplySoftClip(masterOutput, 0.90f);
            }
        }

        /// <summary>
        /// Clears master buffer and mixes multiple AudioBuffer tracks, optionally applying soft saturation.
        /// </summary>
        public static void MixBuffers(
            AudioBuffer[] tracks,
            float[]? trackGains,
            AudioBuffer masterOutput,
            bool applySoftSaturation = true)
        {
            if (tracks == null || tracks.Length == 0)
            {
                masterOutput.Clear();
                return;
            }

            masterOutput.Clear();

            for (int t = 0; t < tracks.Length; t++)
            {
                float gain = (trackGains != null && t < trackGains.Length) ? trackGains[t] : 1.0f;
                Mix(tracks[t].ReadOnlySamples, masterOutput.Samples, gain);
            }

            if (applySoftSaturation)
            {
                GainController.ApplySoftClip(masterOutput.Samples, 0.90f);
            }
        }
    }
}
