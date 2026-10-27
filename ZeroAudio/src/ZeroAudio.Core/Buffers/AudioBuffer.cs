using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using ZeroAudio.Formats;

namespace ZeroAudio.Buffers
{
    /// <summary>
    /// Represents an audio buffer holding multi-channel float samples.
    /// Supports pooling via ArrayPool for minimal memory allocation in high-throughput audio loops.
    /// </summary>
    public sealed class AudioBuffer : IDisposable
    {
        private float[]? _rentedArray;
        private readonly float[] _array;
        private bool _isDisposed;

        public AudioFormat Format { get; }
        public int FrameCount { get; }
        public int Channels => Format.Channels;
        public int SampleRate => Format.SampleRate;
        public int SampleCount => FrameCount * Channels;

        public Span<float> Samples => _array.AsSpan(0, SampleCount);
        public ReadOnlySpan<float> ReadOnlySamples => _array.AsSpan(0, SampleCount);

        private AudioBuffer(AudioFormat format, int frameCount, float[] array, float[]? rentedArray)
        {
            Format = format;
            FrameCount = frameCount;
            _array = array;
            _rentedArray = rentedArray;
        }

        /// <summary>
        /// Rents an audio buffer from the shared ArrayPool. Must be disposed after use.
        /// </summary>
        public static AudioBuffer Rent(AudioFormat format, int frameCount)
        {
            if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be positive.");
            int sampleCount = frameCount * format.Channels;
            float[] rented = ArrayPool<float>.Shared.Rent(sampleCount);
            return new AudioBuffer(format, frameCount, rented, rented);
        }

        /// <summary>
        /// Allocates a dedicated (unpooled) audio buffer.
        /// </summary>
        public static AudioBuffer Create(AudioFormat format, int frameCount)
        {
            if (frameCount <= 0) throw new ArgumentOutOfRangeException(nameof(frameCount), "Frame count must be positive.");
            int sampleCount = frameCount * format.Channels;
            float[] array = new float[sampleCount];
            return new AudioBuffer(format, frameCount, array, null);
        }

        /// <summary>
        /// Clears all sample values in the buffer to 0.0f (silence).
        /// </summary>
        public void Clear() => Samples.Clear();

        /// <summary>
        /// Multiplies all samples in the buffer by a linear gain factor.
        /// </summary>
        public void ApplyGain(float gain)
        {
            if (gain == 1.0f) return;
            var span = Samples;
            for (int i = 0; i < span.Length; i++)
            {
                span[i] *= gain;
            }
        }

        /// <summary>
        /// Computes the Root Mean Square (RMS) energy level across all samples in the buffer.
        /// </summary>
        public float CalculateRms()
        {
            var span = Samples;
            if (span.IsEmpty) return 0f;

            double sumSquares = 0.0;
            for (int i = 0; i < span.Length; i++)
            {
                float s = span[i];
                sumSquares += (double)s * s;
            }
            return (float)Math.Sqrt(sumSquares / span.Length);
        }

        /// <summary>
        /// Computes the absolute peak sample value in the buffer.
        /// </summary>
        public float CalculatePeak()
        {
            var span = Samples;
            float peak = 0f;
            for (int i = 0; i < span.Length; i++)
            {
                float abs = Math.Abs(span[i]);
                if (abs > peak) peak = abs;
            }
            return peak;
        }

        /// <summary>
        /// Computes the peak level in decibels relative to full scale (dBFS).
        /// Returns -120 dBFS for silence.
        /// </summary>
        public float CalculatePeakDb()
        {
            float peak = CalculatePeak();
            if (peak <= 0.000001f) return -120.0f;
            return (float)(20.0 * Math.Log10(peak));
        }

        /// <summary>
        /// Computes the Crest Factor (Peak to RMS ratio) of the buffer.
        /// Useful for acoustic dynamics and mechanical shock detection.
        /// </summary>
        public float CalculateCrestFactor()
        {
            float rms = CalculateRms();
            if (rms <= 0.000001f) return 0f;
            return CalculatePeak() / rms;
        }

        /// <summary>
        /// Computes the standardized 4th moment (Kurtosis) of the buffer.
        /// Standard Gaussian noise yields approximately 3.0.
        /// Values > 3.0 indicate impulsive spike or acoustic defect characteristics.
        /// </summary>
        public float CalculateKurtosis()
        {
            var span = Samples;
            if (span.IsEmpty) return 3.0f;

            double sum = 0.0;
            double sumSq = 0.0;
            for (int i = 0; i < span.Length; i++)
            {
                float val = span[i];
                sum += val;
                sumSq += (double)val * val;
            }

            int n = span.Length;
            double mean = sum / n;
            double variance = (sumSq / n) - (mean * mean);
            if (variance <= 1e-7) return 3.0f;

            double sumFourth = 0.0;
            for (int i = 0; i < span.Length; i++)
            {
                double diff = span[i] - mean;
                sumFourth += diff * diff * diff * diff;
            }

            return (float)(sumFourth / (n * variance * variance));
        }

        /// <summary>
        /// Extracts a specific channel's samples from the interleaved buffer into destination.
        /// </summary>
        public void DeinterleaveChannel(int channelIndex, Span<float> destination)
        {
            if (channelIndex < 0 || channelIndex >= Channels)
                throw new ArgumentOutOfRangeException(nameof(channelIndex));

            int framesToCopy = Math.Min(FrameCount, destination.Length);
            var src = Samples;
            int channels = Channels;

            for (int f = 0; f < framesToCopy; f++)
            {
                destination[f] = src[f * channels + channelIndex];
            }
        }

        /// <summary>
        /// Copies samples into another AudioBuffer.
        /// </summary>
        public void CopyTo(AudioBuffer destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            ReadOnlySamples.Slice(0, Math.Min(SampleCount, destination.SampleCount)).CopyTo(destination.Samples);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_rentedArray != null)
            {
                ArrayPool<float>.Shared.Return(_rentedArray);
                _rentedArray = null;
            }
        }
    }
}
