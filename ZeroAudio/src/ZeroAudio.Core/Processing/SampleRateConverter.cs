using System;

namespace ZeroAudio.Processing
{
    /// <summary>
    /// Provides sample rate conversion algorithms including fast linear interpolation and high-fidelity cubic Hermite resampling.
    /// Works with interleaved multi-channel audio data.
    /// </summary>
    public static class SampleRateConverter
    {
        /// <summary>
        /// Calculates the number of output audio frames resulting from resampling.
        /// </summary>
        public static int CalculateOutputFrames(int inputFrames, double sourceRate, double targetRate)
        {
            if (inputFrames <= 0 || sourceRate <= 0 || targetRate <= 0) return 0;
            return (int)Math.Round(inputFrames * (targetRate / sourceRate));
        }

        /// <summary>
        /// Resamples audio samples using fast linear interpolation.
        /// Ideal for real-time streaming and resource-constrained environments.
        /// </summary>
        /// <param name="input">Interleaved source audio samples.</param>
        /// <param name="output">Destination buffer for interleaved resampled samples.</param>
        /// <param name="channels">Number of interleaved audio channels.</param>
        /// <param name="sourceRate">Source sampling rate in Hz (e.g. 44100).</param>
        /// <param name="targetRate">Target sampling rate in Hz (e.g. 48000).</param>
        /// <returns>The number of output frames written.</returns>
        public static int ResampleLinear(ReadOnlySpan<float> input, Span<float> output, int channels, double sourceRate, double targetRate)
        {
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sourceRate <= 0 || targetRate <= 0) throw new ArgumentOutOfRangeException("Sample rate must be positive.");

            int inputFrames = input.Length / channels;
            if (inputFrames <= 0) return 0;

            int maxOutputFrames = output.Length / channels;
            int targetFrames = Math.Min(maxOutputFrames, CalculateOutputFrames(inputFrames, sourceRate, targetRate));
            if (targetFrames <= 0) return 0;

            double ratio = sourceRate / targetRate;

            for (int outFrame = 0; outFrame < targetFrames; outFrame++)
            {
                double srcPos = outFrame * ratio;
                int srcIndex = (int)srcPos;
                float frac = (float)(srcPos - srcIndex);

                int nextIndex = Math.Min(srcIndex + 1, inputFrames - 1);
                int outOffset = outFrame * channels;
                int inOffset1 = srcIndex * channels;
                int inOffset2 = nextIndex * channels;

                for (int ch = 0; ch < channels; ch++)
                {
                    float s1 = input[inOffset1 + ch];
                    float s2 = input[inOffset2 + ch];
                    output[outOffset + ch] = s1 + frac * (s2 - s1);
                }
            }

            return targetFrames;
        }

        /// <summary>
        /// Resamples audio samples using 4-point, 3rd-order Catmull-Rom / Cubic Hermite interpolation.
        /// Delivers high audio fidelity with suppressed aliasing artifacts.
        /// </summary>
        public static int ResampleCubic(ReadOnlySpan<float> input, Span<float> output, int channels, double sourceRate, double targetRate)
        {
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            if (sourceRate <= 0 || targetRate <= 0) throw new ArgumentOutOfRangeException("Sample rate must be positive.");

            int inputFrames = input.Length / channels;
            if (inputFrames <= 0) return 0;

            int maxOutputFrames = output.Length / channels;
            int targetFrames = Math.Min(maxOutputFrames, CalculateOutputFrames(inputFrames, sourceRate, targetRate));
            if (targetFrames <= 0) return 0;

            double ratio = sourceRate / targetRate;

            for (int outFrame = 0; outFrame < targetFrames; outFrame++)
            {
                double srcPos = outFrame * ratio;
                int i1 = (int)srcPos;
                float t = (float)(srcPos - i1);

                int i0 = Math.Max(0, i1 - 1);
                int i2 = Math.Min(inputFrames - 1, i1 + 1);
                int i3 = Math.Min(inputFrames - 1, i1 + 2);

                int outOffset = outFrame * channels;
                int off0 = i0 * channels;
                int off1 = i1 * channels;
                int off2 = i2 * channels;
                int off3 = i3 * channels;

                for (int ch = 0; ch < channels; ch++)
                {
                    float y0 = input[off0 + ch];
                    float y1 = input[off1 + ch];
                    float y2 = input[off2 + ch];
                    float y3 = input[off3 + ch];

                    // Catmull-Rom cubic spline evaluation
                    float a0 = -0.5f * y0 + 1.5f * y1 - 1.5f * y2 + 0.5f * y3;
                    float a1 = y0 - 2.5f * y1 + 2.0f * y2 - 0.5f * y3;
                    float a2 = -0.5f * y0 + 0.5f * y2;
                    float a3 = y1;

                    output[outOffset + ch] = ((a0 * t + a1) * t + a2) * t + a3;
                }
            }

            return targetFrames;
        }
    }
}
