using System;
using Xunit;
using ZeroAudio.Processing;

namespace ZeroAudio.Tests.Processing
{
    public class ProcessingTests
    {
        [Fact]
        public void SampleRateConverter_LinearAndCubic_ResamplesCorrectly()
        {
            int inputFrames = 44100;
            float[] input = new float[inputFrames]; // 1 second mono
            for (int i = 0; i < input.Length; i++) input[i] = (float)Math.Sin(2.0 * Math.PI * 440.0 * i / 44100.0);

            int expectedOutputFrames = 48000;
            float[] outputLinear = new float[expectedOutputFrames];
            int writtenLinear = SampleRateConverter.ResampleLinear(input, outputLinear, 1, 44100, 48000);

            Assert.Equal(expectedOutputFrames, writtenLinear);
            Assert.True(Math.Abs(outputLinear[0]) < 0.1f); // starts near 0

            float[] outputCubic = new float[expectedOutputFrames];
            int writtenCubic = SampleRateConverter.ResampleCubic(input, outputCubic, 1, 44100, 48000);

            Assert.Equal(expectedOutputFrames, writtenCubic);
            Assert.True(Math.Abs(outputCubic[0]) < 0.1f);
        }

        [Fact]
        public void ChannelMatrix_MonoAndStereo_ConversionsWork()
        {
            float[] mono = new float[] { 0.5f, 0.8f, -0.3f };
            float[] stereo = new float[6];

            int frames = ChannelMatrix.MonoToStereo(mono, stereo);
            Assert.Equal(3, frames);
            Assert.Equal(0.5f, stereo[0]);
            Assert.Equal(0.5f, stereo[1]);
            Assert.Equal(0.8f, stereo[2]);
            Assert.Equal(0.8f, stereo[3]);

            float[] monoConverted = new float[3];
            ChannelMatrix.StereoToMono(stereo, monoConverted, preservePower: false);
            Assert.Equal(0.5f, monoConverted[0], 0.0001f);
            Assert.Equal(0.8f, monoConverted[1], 0.0001f);
        }

        [Fact]
        public void ChannelMatrix_DownmixSurroundToStereo_BS775_PreservesBalance()
        {
            // 5.1 surround frame: [FL, FR, Center, LFE, SL, SR]
            float[] surround = new float[]
            {
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f // Left only
            };
            float[] stereo = new float[2];

            ChannelMatrix.DownmixSurroundToStereo(surround, stereo);

            // Left should have energy, right should be 0
            Assert.True(stereo[0] > 0.3f);
            Assert.Equal(0.0f, stereo[1]);
        }

        [Fact]
        public void GainController_PanAndSoftClip_BehaveAsExpected()
        {
            float[] stereo = new float[] { 1.0f, 1.0f };

            // Full left pan
            GainController.ApplyPan(stereo, -1.0f, PanLaw.EqualPower);
            Assert.Equal(1.0f, stereo[0], 0.001f);
            Assert.Equal(0.0f, stereo[1], 0.001f);

            // Soft-clip saturation
            float[] loudSamples = new float[] { 2.0f, -3.0f, 0.5f };
            GainController.ApplySoftClip(loudSamples, 0.85f);

            // Signal above 0.85f should be compressed toward 1.0f, but not exceeded 1.0f
            Assert.InRange(loudSamples[0], 0.85f, 1.0f);
            Assert.InRange(loudSamples[1], -1.0f, -0.85f);
            Assert.Equal(0.5f, loudSamples[2]); // Untouched below threshold
        }

        [Fact]
        public void GainController_FadeInAndOut_OperatesSmoothly()
        {
            float[] samples = new float[] { 1f, 1f, 1f, 1f };
            GainController.ApplyFadeIn(samples, 1);

            Assert.Equal(0.0f, samples[0]);
            Assert.True(samples[3] > samples[1]);

            GainController.ApplyFadeOut(samples, 1);
            Assert.True(samples[3] < 0.3f);
        }
    }
}
