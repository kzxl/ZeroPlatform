using System;
using Xunit;
using ZeroAudio.Buffers;
using ZeroAudio.Formats;
using ZeroAudio.Synthesis;

namespace ZeroAudio.Tests.Synthesis
{
    public class SynthesisTests
    {
        [Fact]
        public void SignalGenerator_SineWave_RmsMatchesTheoreticalValue()
        {
            float[] buffer = new float[44100]; // 1 second
            double phase = 0.0;
            SignalGenerator.GenerateSine(buffer, 440f, 44100, ref phase, amplitude: 1.0f);

            // Theoretical RMS of pure sine with amplitude 1.0 is 1/sqrt(2) ≈ 0.707106
            double sumSquares = 0.0;
            for (int i = 0; i < buffer.Length; i++) sumSquares += (double)buffer[i] * buffer[i];
            float rms = (float)Math.Sqrt(sumSquares / buffer.Length);

            Assert.InRange(rms, 0.705f, 0.709f);
        }

        [Fact]
        public void SignalGenerator_SquareAndSawtooth_GenerateWithinBounds()
        {
            float[] buffer = new float[1000];
            double phase = 0.0;

            SignalGenerator.GenerateSquare(buffer, 100f, 44100, ref phase, amplitude: 0.8f);
            for (int i = 0; i < buffer.Length; i++)
            {
                Assert.True(Math.Abs(buffer[i]) == 0.8f);
            }

            phase = 0.0;
            SignalGenerator.GenerateSawtooth(buffer, 100f, 44100, ref phase, amplitude: 1.0f);
            for (int i = 0; i < buffer.Length; i++)
            {
                Assert.InRange(buffer[i], -1.01f, 1.01f);
            }
        }

        [Fact]
        public void SignalGenerator_NoiseAndDtmf_GenerateCorrectly()
        {
            float[] noise = new float[2000];
            SignalGenerator.GenerateWhiteNoise(noise, amplitude: 0.5f, seed: 42);
            for (int i = 0; i < noise.Length; i++)
            {
                Assert.InRange(noise[i], -0.5f, 0.5f);
            }

            float[] dtmf = new float[1000];
            double phaseL = 0, phaseH = 0;
            bool ok = SignalGenerator.GenerateDtmf(dtmf, '5', 8000, ref phaseL, ref phaseH, amplitude: 1.0f);
            Assert.True(ok);
            Assert.InRange(dtmf[100], -1.0f, 1.0f);

            // Invalid DTMF key
            bool invalid = SignalGenerator.GenerateDtmf(dtmf, 'Z', 8000, ref phaseL, ref phaseH);
            Assert.False(invalid);
        }

        [Fact]
        public void AudioMixer_MixesTracksWithGains()
        {
            float[] track1 = new float[] { 0.2f, 0.2f };
            float[] track2 = new float[] { 0.4f, 0.4f };
            float[] master = new float[2];

            AudioMixer.Mix(track1, master, 1.0f);
            Assert.Equal(0.2f, master[0], 0.0001f);

            AudioMixer.Mix(track2, master, 0.5f); // 0.2 + 0.4 * 0.5 = 0.4
            Assert.Equal(0.4f, master[0], 0.0001f);

            // MixBuffers
            var format = AudioFormat.Pcm16Stereo44kHz;
            using var b1 = AudioBuffer.Create(format, 10);
            using var b2 = AudioBuffer.Create(format, 10);
            using var masterBuf = AudioBuffer.Create(format, 10);

            for (int i = 0; i < b1.Samples.Length; i++) b1.Samples[i] = 0.3f;
            for (int i = 0; i < b2.Samples.Length; i++) b2.Samples[i] = 0.2f;

            AudioMixer.MixBuffers(new[] { b1, b2 }, new[] { 1.0f, 1.0f }, masterBuf);

            Assert.Equal(0.5f, masterBuf.Samples[0], 0.01f);
        }
    }
}
