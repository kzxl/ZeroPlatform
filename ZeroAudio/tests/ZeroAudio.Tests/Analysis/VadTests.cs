using System;
using Xunit;
using ZeroAudio.Analysis;
using ZeroAudio.Buffers;

namespace ZeroAudio.Tests.Analysis
{
    public class VadTests
    {
        [Fact]
        public void VadDetector_Silence_ClassifiedAsNotVoice()
        {
            var vad = new VadDetector();

            // 160 samples (20 ms at 8kHz) of silence with near-zero thermal noise
            short[] silence = new short[160];
            var rnd = new Random(42);
            for (int i = 0; i < silence.Length; i++)
            {
                silence[i] = (short)rnd.Next(-10, 10);
            }

            var decision = vad.ProcessFrame(silence);
            Assert.False(decision.IsVoice);
            Assert.True(decision.RmsEnergy < vad.MinEnergyThreshold);
        }

        [Fact]
        public void VadDetector_SpeechTone_ClassifiedAsVoice()
        {
            var vad = new VadDetector();

            // 160 samples of 500 Hz tone (within human vowel range) at decent amplitude
            short[] speech = new short[160];
            for (int i = 0; i < speech.Length; i++)
            {
                speech[i] = (short)(Math.Sin(2.0 * Math.PI * 500.0 * i / 8000.0) * 12000.0);
            }

            var decision = vad.ProcessFrame(speech);
            Assert.True(decision.IsVoice);
            Assert.True(decision.RmsEnergy > vad.MinEnergyThreshold);
            Assert.InRange(decision.ZeroCrossingRate, vad.MinZcr, vad.MaxZcr);
        }

        [Fact]
        public void VadDetector_HighFrequencyHiss_RejectedByMaxZcr()
        {
            var vad = new VadDetector();

            // High frequency oscillation alternating every sample (+-10000) -> ZCR = 1.0
            short[] hiss = new short[160];
            for (int i = 0; i < hiss.Length; i++)
            {
                hiss[i] = (short)((i % 2 == 0) ? 10000 : -10000);
            }

            var decision = vad.ProcessFrame(hiss);
            // High energy, but ZCR exceeds MaxZcr -> Classified as noise, not voice
            Assert.False(decision.IsVoice);
            Assert.True(decision.ZeroCrossingRate > vad.MaxZcr);
        }

        [Fact]
        public void VadDetector_AudioBuffer_OverloadAnalyzesVoice()
        {
            var vad = new VadDetector();
            using var buffer = AudioBuffer.Create(ZeroAudio.Formats.AudioFormat.Pcm16(8000, 1), 160);

            var span = buffer.Samples;
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = (float)Math.Sin(2.0 * Math.PI * 400.0 * i / 8000.0) * 0.5f;
            }

            var decision = vad.ProcessFrame(buffer);
            Assert.True(decision.IsVoice);
            Assert.InRange(decision.ZeroCrossingRate, 0.05f, 0.15f);
        }
    }
}
