using System;
using System.Text;
using Xunit;
using ZeroComm.Core.Buffers;

namespace ZeroComm.Tests
{
    public class RingBufferTests
    {
        [Fact]
        public void CircularRingBuffer_BasicWriteAndRead_WorksCorrectly()
        {
            var ring = new CircularRingBuffer(16);
            byte[] input = new byte[] { 1, 2, 3, 4, 5 };

            ring.Write(input);
            Assert.Equal(5, ring.Count);
            Assert.Equal(11, ring.FreeSpace);

            byte[] output = new byte[5];
            int read = ring.Read(output, 0, 5);

            Assert.Equal(5, read);
            Assert.Equal(0, ring.Count);
            Assert.Equal(input, output);
        }

        [Fact]
        public void CircularRingBuffer_WrapsAround_MaintainsIntegrity()
        {
            var ring = new CircularRingBuffer(8);

            // Write 6 bytes
            ring.Write(new byte[] { 1, 2, 3, 4, 5, 6 });
            // Read 4 bytes
            byte[] out1 = new byte[4];
            ring.Read(out1, 0, 4);

            Assert.Equal(2, ring.Count);

            // Now tail is at index 6. Write 5 bytes -> wraps across buffer end (8)
            byte[] in2 = new byte[] { 7, 8, 9, 10, 11 };
            ring.Write(in2);

            Assert.Equal(7, ring.Count);

            byte[] out2 = new byte[7];
            ring.Read(out2, 0, 7);

            Assert.Equal(new byte[] { 5, 6, 7, 8, 9, 10, 11 }, out2);
        }

        [Fact]
        public void CircularRingBuffer_SearchSequences_FindsCorrectOffset()
        {
            var ring = new CircularRingBuffer(32);
            byte[] payload = Encoding.ASCII.GetBytes("HEADER:DATA_PAYLOAD\r\nNEXT");
            ring.Write(payload);

            byte[] delimiter = Encoding.ASCII.GetBytes("\r\n");
            int idx = ring.IndexOfSequence(delimiter);

            Assert.Equal(19, idx);
        }
    }
}
