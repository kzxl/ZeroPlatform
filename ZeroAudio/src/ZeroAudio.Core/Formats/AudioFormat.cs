using System;
using ZeroAudio.Common;

namespace ZeroAudio.Formats
{
    /// <summary>
    /// Represents an immutable audio format specification including sample rate, channel count, bit depth, and layout.
    /// </summary>
    public readonly struct AudioFormat : IEquatable<AudioFormat>
    {
        public int SampleRate { get; }
        public int Channels { get; }
        public int BitsPerSample { get; }
        public AudioSampleFormat SampleFormat { get; }

        public int BytesPerSample => (BitsPerSample + 7) / 8;
        public int BytesPerFrame => BytesPerSample * Channels;
        public int BytesPerSecond => BytesPerFrame * SampleRate;

        public AudioFormat(int sampleRate, int channels, int bitsPerSample, AudioSampleFormat sampleFormat)
        {
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate), "Sample rate must be positive.");
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be positive.");
            if (bitsPerSample <= 0) throw new ArgumentOutOfRangeException(nameof(bitsPerSample), "BitsPerSample must be positive.");

            SampleRate = sampleRate;
            Channels = channels;
            BitsPerSample = bitsPerSample;
            SampleFormat = sampleFormat;
        }

        public static AudioFormat Pcm16(int sampleRate, int channels) =>
            new AudioFormat(sampleRate, channels, 16, AudioSampleFormat.Pcm16);

        public static AudioFormat Float32(int sampleRate, int channels) =>
            new AudioFormat(sampleRate, channels, 32, AudioSampleFormat.IeeeFloat32);

        // Standard Industry Presets
        public static readonly AudioFormat Pcm16Stereo44kHz = new AudioFormat(44100, 2, 16, AudioSampleFormat.Pcm16);
        public static readonly AudioFormat Pcm16Stereo48kHz = new AudioFormat(48000, 2, 16, AudioSampleFormat.Pcm16);
        public static readonly AudioFormat Pcm16Mono16kHz = new AudioFormat(16000, 1, 16, AudioSampleFormat.Pcm16);
        public static readonly AudioFormat Pcm16Mono8kHz = new AudioFormat(8000, 1, 16, AudioSampleFormat.Pcm16);
        public static readonly AudioFormat Float32Stereo44kHz = new AudioFormat(44100, 2, 32, AudioSampleFormat.IeeeFloat32);
        public static readonly AudioFormat Float32Stereo48kHz = new AudioFormat(48000, 2, 32, AudioSampleFormat.IeeeFloat32);
        public static readonly AudioFormat Float32Mono48kHz = new AudioFormat(48000, 1, 32, AudioSampleFormat.IeeeFloat32);

        public TimeSpan CalculateDuration(long byteCount)
        {
            if (BytesPerSecond == 0) return TimeSpan.Zero;
            double seconds = (double)byteCount / BytesPerSecond;
            return TimeSpan.FromSeconds(seconds);
        }

        public long CalculateByteCount(TimeSpan duration) =>
            (long)(duration.TotalSeconds * BytesPerSecond);

        public long FramesToBytes(long frameCount) => frameCount * BytesPerFrame;
        public long BytesToFrames(long byteCount) => byteCount / BytesPerFrame;

        public bool Equals(AudioFormat other) =>
            SampleRate == other.SampleRate &&
            Channels == other.Channels &&
            BitsPerSample == other.BitsPerSample &&
            SampleFormat == other.SampleFormat;

        public override bool Equals(object? obj) => obj is AudioFormat other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SampleRate;
                hash = (hash * 397) ^ Channels;
                hash = (hash * 397) ^ BitsPerSample;
                hash = (hash * 397) ^ (int)SampleFormat;
                return hash;
            }
        }

        public static bool operator ==(AudioFormat left, AudioFormat right) => left.Equals(right);
        public static bool operator !=(AudioFormat left, AudioFormat right) => !left.Equals(right);

        public override string ToString() =>
            $"{SampleRate}Hz {Channels}ch {BitsPerSample}-bit {SampleFormat} ({BytesPerSecond / 1000} KB/s)";
    }
}
