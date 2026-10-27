using System;
using Xunit;
using ZeroAudio.Dynamics;

namespace ZeroAudio.Tests.Dynamics
{
    public class DynamicsTests
    {
        [Fact]
        public void AudioLimiter_ClampsPeaksExceedingCeiling()
        {
            var limiter = new AudioLimiter(44100, ceilingDb: -0.5f, attackMs: 0.1f, releaseMs: 10.0f);

            // Audio with extreme peak
            float[] audio = new float[1000];
            for (int i = 0; i < audio.Length; i++) audio[i] = 0.5f;
            audio[500] = 5.0f; // extreme spike

            limiter.Process(audio);

            // Ceiling in linear for -0.5 dB is ~0.944f
            for (int i = 0; i < audio.Length; i++)
            {
                Assert.True(Math.Abs(audio[i]) <= 1.05f, $"Sample at {i} was {audio[i]}");
            }
        }

        [Fact]
        public void AudioCompressor_ReducesDynamicRange()
        {
            var compressor = new AudioCompressor(44100, thresholdDb: -12.0f, ratio: 4.0f, attackMs: 1.0f, releaseMs: 50.0f);

            // Loud signal (-0dBFS = 1.0)
            float[] loud = new float[2000];
            for (int i = 0; i < loud.Length; i++) loud[i] = 1.0f;

            compressor.Process(loud);

            // Tail of loud signal should be compressed significantly below 1.0
            float tailSample = loud[1999];
            Assert.True(tailSample < 0.6f, $"Compressed sample {tailSample} should be < 0.6f");
        }

        [Fact]
        public void NoiseGate_AttenuatesQuietNoiseFloor()
        {
            var gate = new NoiseGate(44100, thresholdDb: -30.0f, attackMs: 1.0f, holdMs: 5.0f, releaseMs: 10.0f);

            // Very quiet noise at -50dBFS (linear ≈ 0.0031)
            float[] quiet = new float[1000];
            for (int i = 0; i < quiet.Length; i++) quiet[i] = 0.002f;

            gate.Process(quiet);

            // Should be gated down near zero
            Assert.True(quiet[999] < 0.0005f);
        }

        [Fact]
        public void AgcEngine_AmplifiesLowLevelAudio()
        {
            // Target -18 dBFS (approx 0.126 linear)
            var agc = new AgcEngine(44100, targetRmsDb: -18.0f, maxGainDb: 20.0f, minGainDb: -10.0f, responseTimeMs: 10.0f);

            float[] quietAudio = new float[4000];
            for (int i = 0; i < quietAudio.Length; i++) quietAudio[i] = 0.02f; // very quiet

            agc.Process(quietAudio);

            // Later frames should be boosted towards target
            Assert.True(quietAudio[3999] > 0.05f, $"Sample {quietAudio[3999]} should be boosted");
        }
    }
}
