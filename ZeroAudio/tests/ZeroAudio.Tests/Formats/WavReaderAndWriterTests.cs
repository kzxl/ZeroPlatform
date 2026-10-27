using System;
using System.IO;
using Xunit;
using ZeroAudio.Common;
using ZeroAudio.Formats;

namespace ZeroAudio.Tests.Formats
{
    public class WavReaderAndWriterTests
    {
        [Fact]
        public void WavWriterAndReader_Pcm16_RoundTripSucceeds()
        {
            var format = AudioFormat.Pcm16Stereo44kHz;
            int frames = 1000;
            float[] original = new float[frames * format.Channels];

            // Generate simple test pattern
            for (int i = 0; i < original.Length; i++)
            {
                original[i] = (float)Math.Sin(i * 0.05) * 0.8f;
            }

            using var ms = new MemoryStream();
            using (var writer = new WavWriter(ms, format, leaveOpen: true))
            {
                writer.WriteSamples(original);
            }

            ms.Position = 0;

            using (var reader = new WavReader(ms, leaveOpen: true))
            {
                Assert.Equal(format.SampleRate, reader.Format.SampleRate);
                Assert.Equal(format.Channels, reader.Format.Channels);
                Assert.Equal(format.BitsPerSample, reader.Format.BitsPerSample);
                Assert.Equal(frames, reader.TotalFrames);

                float[] readBack = new float[original.Length];
                int readCount = reader.ReadSamples(readBack);
                Assert.Equal(original.Length, readCount);

                // Verify values with 16-bit quantization tolerance (~1/32768 = 0.00003)
                for (int i = 0; i < original.Length; i++)
                {
                    Assert.InRange(Math.Abs(original[i] - readBack[i]), 0.0f, 0.0002f);
                }
            }
        }

        [Fact]
        public void WavWriterAndReader_Float32_LosslessRoundTripSucceeds()
        {
            var format = AudioFormat.Float32Stereo48kHz;
            int frames = 500;
            float[] original = new float[frames * format.Channels];

            for (int i = 0; i < original.Length; i++)
            {
                original[i] = (i % 2 == 0) ? 0.75f : -0.75f;
            }

            using var ms = new MemoryStream();
            using (var writer = new WavWriter(ms, format, leaveOpen: true))
            {
                writer.WriteSamples(original);
            }

            ms.Position = 0;

            using (var reader = new WavReader(ms, leaveOpen: true))
            {
                Assert.Equal(AudioSampleFormat.IeeeFloat32, reader.Format.SampleFormat);
                Assert.Equal(48000, reader.Format.SampleRate);
                Assert.Equal(frames, reader.TotalFrames);

                float[] readBack = new float[original.Length];
                int read = reader.ReadSamples(readBack);
                Assert.Equal(original.Length, read);

                // IEEE Float is 100% bit-exact lossless
                for (int i = 0; i < original.Length; i++)
                {
                    Assert.Equal(original[i], readBack[i]);
                }
            }
        }

        [Theory]
        [InlineData(AudioSampleFormat.Pcm8, 8, 0.01f)]
        [InlineData(AudioSampleFormat.Pcm24, 24, 0.0001f)]
        [InlineData(AudioSampleFormat.Pcm32, 32, 0.00001f)]
        public void Wav_EncodeAndDecode_MultiFormats_Succeeds(AudioSampleFormat sampleFormat, int bits, float tolerance)
        {
            float[] original = new float[] { -0.8f, -0.4f, 0.0f, 0.4f, 0.8f };
            int bytesPerSample = (bits + 7) / 8;
            byte[] encoded = new byte[original.Length * bytesPerSample];

            WavWriter.EncodeFloatToPcm(original, encoded, sampleFormat, bits);

            float[] decoded = new float[original.Length];
            WavReader.DecodePcmToFloat(encoded, decoded, sampleFormat, bits);

            for (int i = 0; i < original.Length; i++)
            {
                Assert.InRange(Math.Abs(original[i] - decoded[i]), 0.0f, tolerance);
            }
        }

        [Fact]
        public void WavWriter_StaticWriteAndReadAllSamples_Succeeds()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"zeroaudio_test_{Guid.NewGuid():N}.wav");
            try
            {
                var format = AudioFormat.Pcm16Mono16kHz;
                float[] original = new float[800];
                for (int i = 0; i < original.Length; i++) original[i] = 0.5f;

                WavWriter.WriteAllSamples(tempFile, original, format);
                Assert.True(File.Exists(tempFile));

                float[] readBack = WavReader.ReadAllSamples(tempFile, out var readFormat);
                Assert.Equal(format.SampleRate, readFormat.SampleRate);
                Assert.Equal(format.Channels, readFormat.Channels);
                Assert.Equal(original.Length, readBack.Length);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
