using System;
using Xunit;
using ZeroAudio.Equalization;

namespace ZeroAudio.Tests.Equalization
{
    public class EqualizationTests
    {
        [Fact]
        public void BiquadFilter_LowPass_AttenuatesHighFrequency()
        {
            int sampleRate = 48000;
            var lpf = BiquadFilter.CreateLowPass(sampleRate, cutoffHz: 1000f, q: 0.7071f);

            // 10 kHz high-frequency sine
            float[] highFreq = new float[1000];
            for (int i = 0; i < highFreq.Length; i++)
            {
                highFreq[i] = (float)Math.Sin(2.0 * Math.PI * 10000.0 * i / sampleRate);
            }

            lpf.Process(highFreq);

            // In steady-state, 10 kHz should be heavily attenuated by 1 kHz low-pass filter
            float tailAmplitude = Math.Abs(highFreq[900]);
            Assert.True(tailAmplitude < 0.15f, $"10kHz amplitude was {tailAmplitude}, expected < 0.15f");
        }

        [Fact]
        public void BiquadFilter_HighPass_AttenuatesLowFrequency()
        {
            int sampleRate = 48000;
            var hpf = BiquadFilter.CreateHighPass(sampleRate, cutoffHz: 5000f, q: 0.7071f);

            // 100 Hz low-frequency sine
            float[] lowFreq = new float[1000];
            for (int i = 0; i < lowFreq.Length; i++)
            {
                lowFreq[i] = (float)Math.Sin(2.0 * Math.PI * 100.0 * i / sampleRate);
            }

            hpf.Process(lowFreq);

            float tailAmplitude = Math.Abs(lowFreq[900]);
            Assert.True(tailAmplitude < 0.10f, $"100Hz amplitude was {tailAmplitude}, expected < 0.10f");
        }

        [Fact]
        public void GraphicEqualizer_AdjustsMultiChannelBands()
        {
            int sampleRate = 44100;
            var eq = new GraphicEqualizer(sampleRate, channels: 2);

            Assert.Equal(10, eq.BandCount);
            Assert.Equal(2, eq.Channels);

            // Boost 1 kHz band (band index 5) by +6 dB
            eq.SetBandGain(5, 6.0f);
            Assert.Equal(6.0f, eq.GetBandGain(5));

            float[] stereoAudio = new float[2000]; // 1000 stereo frames
            for (int i = 0; i < stereoAudio.Length; i++)
            {
                stereoAudio[i] = (float)Math.Sin(2.0 * Math.PI * 1000.0 * (i / 2) / sampleRate);
            }

            eq.Process(stereoAudio);

            // Should run stably without NaN or Infinity
            for (int i = 0; i < stereoAudio.Length; i++)
            {
                Assert.False(float.IsNaN(stereoAudio[i]));
                Assert.False(float.IsInfinity(stereoAudio[i]));
            }
        }
    }
}
