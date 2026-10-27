using System;
using Xunit;
using ZeroAudio.Common;
using ZeroAudio.Formats;

namespace ZeroAudio.Tests.Formats
{
    public class AudioFormatTests
    {
        [Fact]
        public void AudioFormat_PropertiesAndCalculations_AreAccurate()
        {
            var format = new AudioFormat(48000, 2, 16, AudioSampleFormat.Pcm16);

            Assert.Equal(48000, format.SampleRate);
            Assert.Equal(2, format.Channels);
            Assert.Equal(16, format.BitsPerSample);
            Assert.Equal(AudioSampleFormat.Pcm16, format.SampleFormat);
            Assert.Equal(2, format.BytesPerSample);
            Assert.Equal(4, format.BytesPerFrame);
            Assert.Equal(192000, format.BytesPerSecond);

            // 1 second duration
            Assert.Equal(TimeSpan.FromSeconds(1), format.CalculateDuration(192000));
            Assert.Equal(192000, format.CalculateByteCount(TimeSpan.FromSeconds(1)));
            Assert.Equal(48000, format.BytesToFrames(192000));
            Assert.Equal(192000, format.FramesToBytes(48000));
        }

        [Fact]
        public void AudioFormat_Presets_AreValid()
        {
            Assert.Equal(44100, AudioFormat.Pcm16Stereo44kHz.SampleRate);
            Assert.Equal(2, AudioFormat.Pcm16Stereo44kHz.Channels);

            Assert.Equal(48000, AudioFormat.Float32Stereo48kHz.SampleRate);
            Assert.Equal(4, AudioFormat.Float32Stereo48kHz.BytesPerSample);

            Assert.Equal(16000, AudioFormat.Pcm16Mono16kHz.SampleRate);
            Assert.Equal(1, AudioFormat.Pcm16Mono16kHz.Channels);
        }

        [Fact]
        public void AudioFormat_EqualityAndHashCode_WorkCorrectly()
        {
            var f1 = new AudioFormat(44100, 2, 16, AudioSampleFormat.Pcm16);
            var f2 = new AudioFormat(44100, 2, 16, AudioSampleFormat.Pcm16);
            var f3 = new AudioFormat(48000, 2, 16, AudioSampleFormat.Pcm16);

            Assert.Equal(f1, f2);
            Assert.True(f1 == f2);
            Assert.NotEqual(f1, f3);
            Assert.True(f1 != f3);
            Assert.Equal(f1.GetHashCode(), f2.GetHashCode());
        }
    }
}
