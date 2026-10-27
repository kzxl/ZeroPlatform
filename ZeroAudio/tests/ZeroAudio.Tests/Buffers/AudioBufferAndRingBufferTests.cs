using System;
using Xunit;
using ZeroAudio.Buffers;
using ZeroAudio.Formats;

namespace ZeroAudio.Tests.Buffers
{
    public class AudioBufferAndRingBufferTests
    {
        [Fact]
        public void AudioBuffer_RentAndOperations_WorkCorrectly()
        {
            var format = AudioFormat.Pcm16Stereo44kHz;
            using var buffer = AudioBuffer.Rent(format, 100);

            Assert.Equal(100, buffer.FrameCount);
            Assert.Equal(2, buffer.Channels);
            Assert.Equal(200, buffer.SampleCount);

            // Populate samples
            var span = buffer.Samples;
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = (i % 2 == 0) ? 0.5f : -0.5f;
            }

            Assert.Equal(0.5f, buffer.CalculatePeak(), 0.0001f);
            Assert.Equal(0.5f, buffer.CalculateRms(), 0.0001f);
            Assert.InRange(buffer.CalculatePeakDb(), -6.05f, -5.95f); // 20*log10(0.5) ≈ -6.02 dB

            // Apply gain
            buffer.ApplyGain(2.0f);
            Assert.Equal(1.0f, buffer.CalculatePeak(), 0.0001f);

            // Deinterleave channel 0 (Left)
            float[] left = new float[100];
            buffer.DeinterleaveChannel(0, left);
            for (int i = 0; i < left.Length; i++)
            {
                Assert.Equal(1.0f, left[i], 0.0001f);
            }

            // Clear
            buffer.Clear();
            Assert.Equal(0.0f, buffer.CalculatePeak());
            Assert.Equal(-120.0f, buffer.CalculatePeakDb());
        }

        [Fact]
        public void AudioRingBuffer_WriteReadAndWrapAround_Succeeds()
        {
            var ring = new AudioRingBuffer(16); // Rounds to 16
            Assert.Equal(16, ring.Capacity);
            Assert.Equal(0, ring.AvailableRead);
            Assert.Equal(16, ring.AvailableWrite);

            float[] writeData1 = new float[] { 1f, 2f, 3f, 4f };
            int written1 = ring.Write(writeData1);
            Assert.Equal(4, written1);
            Assert.Equal(4, ring.AvailableRead);
            Assert.Equal(12, ring.AvailableWrite);

            // Read partial
            float[] readData1 = new float[2];
            int read1 = ring.Read(readData1);
            Assert.Equal(2, read1);
            Assert.Equal(1f, readData1[0]);
            Assert.Equal(2f, readData1[1]);
            Assert.Equal(2, ring.AvailableRead);

            // Peek next
            float[] peekData = new float[2];
            int peeked = ring.Peek(peekData);
            Assert.Equal(2, peeked);
            Assert.Equal(3f, peekData[0]);
            Assert.Equal(4f, peekData[1]);
            Assert.Equal(2, ring.AvailableRead); // Peek does not consume

            // Fill buffer past original boundary to test wrap-around
            float[] writeData2 = new float[14];
            for (int i = 0; i < writeData2.Length; i++) writeData2[i] = 10f + i;
            int written2 = ring.Write(writeData2);
            Assert.Equal(14, written2); // 2 remaining + 14 = 16 (Full)
            Assert.Equal(16, ring.AvailableRead);
            Assert.Equal(0, ring.AvailableWrite);

            // Read all 16 samples
            float[] allRead = new float[16];
            int totalRead = ring.Read(allRead);
            Assert.Equal(16, totalRead);
            Assert.Equal(3f, allRead[0]);
            Assert.Equal(4f, allRead[1]);
            Assert.Equal(10f, allRead[2]);
            Assert.Equal(23f, allRead[15]);

            Assert.Equal(0, ring.AvailableRead);
            Assert.Equal(16, ring.AvailableWrite);
        }
    }
}
