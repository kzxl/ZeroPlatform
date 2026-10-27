using System;
using System.Text;
using Xunit;
using ZeroAudio.Streaming;

namespace ZeroAudio.Tests.Streaming
{
    public class StreamingAudioTests
    {
        [Fact]
        public void AudioJitterBuffer_ReordersOutSequencePackets()
        {
            var buffer = new AudioJitterBuffer(targetDelayMs: 40, packetDurationMs: 20);

            byte[] p1 = Encoding.UTF8.GetBytes("frame1");
            byte[] p2 = Encoding.UTF8.GetBytes("frame2");
            byte[] p3 = Encoding.UTF8.GetBytes("frame3");

            // Arrives out of order: frame 2, then frame 1, then frame 3
            buffer.Push(sequenceNumber: 2, timestampMs: 40, payload: p2);
            buffer.Push(sequenceNumber: 1, timestampMs: 20, payload: p1);
            buffer.Push(sequenceNumber: 3, timestampMs: 60, payload: p3);

            Assert.Equal(3, buffer.Count);

            // Playout should pop in strict sequence order: 1, 2, 3
            Assert.True(buffer.TryPop(out var out1));
            Assert.Equal(1u, out1.SequenceNumber);
            Assert.Equal("frame1", Encoding.UTF8.GetString(out1.Payload));

            Assert.True(buffer.TryPop(out var out2));
            Assert.Equal(2u, out2.SequenceNumber);
            Assert.Equal("frame2", Encoding.UTF8.GetString(out2.Payload));

            Assert.True(buffer.TryPop(out var out3));
            Assert.Equal(3u, out3.SequenceNumber);
            Assert.Equal("frame3", Encoding.UTF8.GetString(out3.Payload));

            Assert.Equal(0, buffer.Count);
        }

        [Fact]
        public void AudioJitterBuffer_DropsDuplicatesAndLatePackets()
        {
            var buffer = new AudioJitterBuffer(targetDelayMs: 20, packetDurationMs: 20);

            byte[] dummy = new byte[] { 0x01, 0x02 };

            Assert.True(buffer.Push(1, 20, dummy));
            Assert.False(buffer.Push(1, 20, dummy)); // Duplicate -> dropped
            Assert.Equal(1, buffer.DuplicatePacketsDropped);

            Assert.True(buffer.TryPop(out var popped));
            Assert.Equal(1u, popped.SequenceNumber);

            // Arrives after frame 1 was already played
            Assert.False(buffer.Push(1, 20, dummy)); // Late packet -> dropped
            Assert.Equal(1, buffer.LatePacketsDropped);
        }

        [Fact]
        public void AudioJitterBuffer_BufferingUnderrunAndRecovery()
        {
            var buffer = new AudioJitterBuffer(targetDelayMs: 60, packetDurationMs: 20); // Requires 3 packets to start playout

            byte[] dummy = new byte[] { 0xAA };

            buffer.Push(1, 20, dummy);
            buffer.Push(2, 40, dummy);

            // Only 2 packets pushed, target requires 3 (60ms / 20ms) -> Still buffering
            Assert.False(buffer.TryPop(out _));

            buffer.Push(3, 60, dummy);
            // Now 3 packets reached -> Buffering complete, should pop
            Assert.True(buffer.TryPop(out var p1));
            Assert.Equal(1u, p1.SequenceNumber);
            Assert.True(buffer.TryPop(out var p2));
            Assert.Equal(2u, p2.SequenceNumber);
            Assert.True(buffer.TryPop(out var p3));
            Assert.Equal(3u, p3.SequenceNumber);

            // Buffer is empty now -> Underrun occurs, re-enters buffering mode
            Assert.False(buffer.TryPop(out _));
        }
    }
}
