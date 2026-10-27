using System.Text;
using Xunit;
using ZeroComm.Core.Buffers;
using ZeroComm.Core.Modbus;

namespace ZeroComm.Tests
{
    public class StreamingParserTests
    {
        [Fact]
        public void StreamingParser_ModbusTcp_HandlesPacketFragmentation()
        {
            var ring = new CircularRingBuffer(128);
            byte[] modbusReq = ModbusTcpFrame.CreateReadHoldingRegistersRequest(1, 1, 0, 10);
            Assert.Equal(12, modbusReq.Length);

            // Send chunk 1: only 5 bytes (incomplete header)
            byte[] chunk1 = new byte[5];
            System.Array.Copy(modbusReq, 0, chunk1, 0, 5);
            ring.Write(chunk1);

            // Attempt extract: should return false (incomplete)
            bool extracted = StreamingFrameParser.TryExtractModbusTcpFrame(ring, out byte[] frame1);
            Assert.False(extracted);
            Assert.Equal(5, ring.Count);

            // Send chunk 2: remaining 7 bytes
            byte[] chunk2 = new byte[7];
            System.Array.Copy(modbusReq, 5, chunk2, 0, 7);
            ring.Write(chunk2);

            // Attempt extract: should succeed
            extracted = StreamingFrameParser.TryExtractModbusTcpFrame(ring, out byte[] frame2);
            Assert.True(extracted);
            Assert.Equal(12, frame2.Length);
            Assert.Equal(modbusReq, frame2);
            Assert.Equal(0, ring.Count);
        }

        [Fact]
        public void StreamingParser_DelimitedFrames_ExtractsCorrectly()
        {
            var ring = new CircularRingBuffer(128);
            byte[] stream = Encoding.ASCII.GetBytes("CMD1:VAL=10\r\nCMD2:VAL=20\r\n");
            ring.Write(stream);

            byte[] crlf = Encoding.ASCII.GetBytes("\r\n");

            // Extract Frame 1
            Assert.True(StreamingFrameParser.TryExtractDelimitedFrame(ring, crlf, out byte[] f1));
            Assert.Equal("CMD1:VAL=10", Encoding.ASCII.GetString(f1));

            // Extract Frame 2
            Assert.True(StreamingFrameParser.TryExtractDelimitedFrame(ring, crlf, out byte[] f2));
            Assert.Equal("CMD2:VAL=20", Encoding.ASCII.GetString(f2));

            // No more complete frames
            Assert.False(StreamingFrameParser.TryExtractDelimitedFrame(ring, crlf, out _));
            Assert.Equal(0, ring.Count);
        }
    }
}
