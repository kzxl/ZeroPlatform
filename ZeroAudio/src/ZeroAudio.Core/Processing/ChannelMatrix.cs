using System;
using System.Runtime.CompilerServices;

namespace ZeroAudio.Processing
{
    /// <summary>
    /// Provides channel format routing, splitting, merging, and surround-sound downmixing.
    /// </summary>
    public static class ChannelMatrix
    {
        private const float SqrtHalf = 0.70710678f; // -3dB power balance factor

        /// <summary>
        /// Duplicates a mono audio signal into left and right channels of a stereo destination.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int MonoToStereo(ReadOnlySpan<float> mono, Span<float> stereo)
        {
            int frames = Math.Min(mono.Length, stereo.Length / 2);
            for (int i = 0; i < frames; i++)
            {
                float s = mono[i];
                stereo[i * 2] = s;
                stereo[i * 2 + 1] = s;
            }
            return frames;
        }

        /// <summary>
        /// Combines left and right stereo channels into a single mono destination.
        /// </summary>
        /// <param name="stereo">Interleaved stereo samples [L, R, L, R...].</param>
        /// <param name="mono">Destination mono samples.</param>
        /// <param name="preservePower">If true, scales each channel by 1/sqrt(2) (-3dB) to prevent volume peaking. If false, divides by 2.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int StereoToMono(ReadOnlySpan<float> stereo, Span<float> mono, bool preservePower = true)
        {
            int frames = Math.Min(stereo.Length / 2, mono.Length);
            float scale = preservePower ? SqrtHalf : 0.5f;

            for (int i = 0; i < frames; i++)
            {
                float left = stereo[i * 2];
                float right = stereo[i * 2 + 1];
                mono[i] = (left + right) * scale;
            }
            return frames;
        }

        /// <summary>
        /// Downmixes a 5.1 surround sound audio stream into stereo according to ITU-R BS.775 standards.
        /// Standard channel ordering: [FL, FR, Center, LFE/Sub, SL, SR].
        /// </summary>
        public static int DownmixSurroundToStereo(ReadOnlySpan<float> surround51, Span<float> stereo)
        {
            int frames = Math.Min(surround51.Length / 6, stereo.Length / 2);
            const float centerScale = 0.70710678f;  // -3dB for center channel
            const float surroundScale = 0.70710678f;// -3dB for surround channels
            const float normFactor = 1.0f / (1.0f + centerScale + surroundScale); // Normalize to avoid hard clipping

            for (int i = 0; i < frames; i++)
            {
                int inIdx = i * 6;
                float fl = surround51[inIdx];
                float fr = surround51[inIdx + 1];
                float fc = surround51[inIdx + 2];
                // LFE at inIdx + 3 is omitted in standard ITU stereo downmix
                float sl = surround51[inIdx + 4];
                float sr = surround51[inIdx + 5];

                float left = (fl + centerScale * fc + surroundScale * sl) * normFactor;
                float right = (fr + centerScale * fc + surroundScale * sr) * normFactor;

                stereo[i * 2] = left;
                stereo[i * 2 + 1] = right;
            }
            return frames;
        }

        /// <summary>
        /// Interleaves separate left and right channel arrays into a stereo output.
        /// </summary>
        public static int InterleaveStereo(ReadOnlySpan<float> left, ReadOnlySpan<float> right, Span<float> stereo)
        {
            int frames = Math.Min(Math.Min(left.Length, right.Length), stereo.Length / 2);
            for (int i = 0; i < frames; i++)
            {
                stereo[i * 2] = left[i];
                stereo[i * 2 + 1] = right[i];
            }
            return frames;
        }

        /// <summary>
        /// Deinterleaves a stereo stream into separate left and right channel destinations.
        /// </summary>
        public static int DeinterleaveStereo(ReadOnlySpan<float> stereo, Span<float> left, Span<float> right)
        {
            int frames = Math.Min(stereo.Length / 2, Math.Min(left.Length, right.Length));
            for (int i = 0; i < frames; i++)
            {
                left[i] = stereo[i * 2];
                right[i] = stereo[i * 2 + 1];
            }
            return frames;
        }
    }
}
