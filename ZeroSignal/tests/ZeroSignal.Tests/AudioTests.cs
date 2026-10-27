using System;
using Xunit;
using ZeroSignal.Core.Audio;

namespace ZeroSignal.Tests
{
    public class AudioTests
    {
        [Fact]
        public void VadDetector_Silence_ClassifiedAsNotVoice()
        {
            var vad = new VadDetector();
            var silence = new short[480]; // 10ms at 48kHz
            Array.Clear(silence, 0, silence.Length);

            var decision = vad.ProcessFrame(silence);

            Assert.False(decision.IsVoice);
            Assert.Equal(0f, decision.RmsEnergy);
            Assert.Equal(0f, decision.ZeroCrossingRate);
        }

        [Fact]
        public void VadDetector_SpeechTone_ClassifiedAsVoice()
        {
            var vad = new VadDetector();
            var frame = new short[480]; // 10ms of 440 Hz tone at 48kHz
            for (int i = 0; i < frame.Length; i++)
            {
                double t = i / 48000.0;
                // Sine wave with amplitude ~0.3 (9830 in PCM16)
                frame[i] = (short)(9830 * Math.Sin(2.0 * Math.PI * 440.0 * t));
            }

            var decision = vad.ProcessFrame(frame);

            Assert.True(decision.IsVoice);
            Assert.True(decision.RmsEnergy > 0.15f);
            Assert.True(decision.ZeroCrossingRate > 0.015f && decision.ZeroCrossingRate < 0.65f);
        }

        [Fact]
        public void VadDetector_HighFrequencyHiss_RejectedByMaxZcr()
        {
            var vad = new VadDetector();
            var frame = new short[480];
            // Alternating sign every single sample: ZCR = 1.0
            for (int i = 0; i < frame.Length; i++)
            {
                frame[i] = (short)(i % 2 == 0 ? 10000 : -10000);
            }

            var decision = vad.ProcessFrame(frame);

            // Even though energy is high, ZCR = 1.0 exceeds MaxZcr (0.65), so not speech
            Assert.False(decision.IsVoice);
            Assert.True(decision.ZeroCrossingRate > 0.9f);
        }

        [Fact]
        public void AudioJitterBuffer_ReordersOutSequencePackets()
        {
            var buffer = new AudioJitterBuffer(targetDelayMs: 40, packetDurationMs: 20);

            // Buffer requires at least 2 packets (40ms / 20ms) before releasing
            Assert.False(buffer.TryPop(out _));

            // Push packets out of order: 2, then 1
            var payload2 = new byte[] { 2, 2, 2 };
            var payload1 = new byte[] { 1, 1, 1 };

            Assert.True(buffer.Push(sequenceNumber: 2, timestampMs: 20, payload: payload2));
            Assert.True(buffer.Push(sequenceNumber: 1, timestampMs: 0, payload: payload1));

            // Now buffer has 2 packets, playout should release packet 1 first, then packet 2
            Assert.True(buffer.TryPop(out var packetA));
            Assert.Equal(1u, packetA.SequenceNumber);
            Assert.Equal(payload1, packetA.Payload);

            Assert.True(buffer.TryPop(out var packetB));
            Assert.Equal(2u, packetB.SequenceNumber);
            Assert.Equal(payload2, packetB.Payload);

            // Now buffer is empty
            Assert.False(buffer.TryPop(out _));
        }

        [Fact]
        public void AudioJitterBuffer_DropsDuplicatesAndLatePackets()
        {
            var buffer = new AudioJitterBuffer(targetDelayMs: 20, packetDurationMs: 20);

            Assert.True(buffer.Push(1, 0, new byte[] { 1 }));
            // Duplicate
            Assert.False(buffer.Push(1, 0, new byte[] { 1 }));
            Assert.Equal(1, buffer.DuplicatePacketsDropped);

            // Pop packet 1
            Assert.True(buffer.TryPop(out var popped));
            Assert.Equal(1u, popped.SequenceNumber);

            // Push packet 1 again after it was already played -> should be marked late
            Assert.False(buffer.Push(1, 0, new byte[] { 1 }));
            Assert.Equal(1, buffer.LatePacketsDropped);
        }
    }
}
