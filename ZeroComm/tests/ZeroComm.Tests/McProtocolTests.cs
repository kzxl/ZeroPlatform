using Xunit;
using ZeroComm.Core.Mitsubishi;

namespace ZeroComm.Tests
{
    public class McProtocolTests
    {
        [Fact]
        public void McProtocol_BuildBatchReadWords_ConstructsValid3EFrame()
        {
            byte[] frame = McProtocolFrame.BuildBatchReadWordsRequest(
                deviceCode: McDeviceCode.D,
                headDeviceNumber: 100,
                devicePoints: 5);

            Assert.Equal(21, frame.Length);
            Assert.Equal(0x50, frame[0]);
            Assert.Equal(0x00, frame[1]);
            Assert.Equal(0x00, frame[2]); // Network
            Assert.Equal(0xFF, frame[3]); // PC
            Assert.Equal(0x01, frame[11]); // Command low (0x0401)
            Assert.Equal(0x04, frame[12]); // Command high
            Assert.Equal(0x00, frame[13]); // Subcommand: Word
            Assert.Equal(100, frame[15]); // Head device low
            Assert.Equal((byte)McDeviceCode.D, frame[18]);
            Assert.Equal(5, frame[19]); // Points low
        }

        [Fact]
        public void McProtocol_ParseResponse_ValidData()
        {
            // Simulate 3E response: Subheader(0xD0, 0x00), Route(5B), Length(6B), EndCode(0x0000), Data(4B = 2 words)
            byte[] response = new byte[]
            {
                0xD0, 0x00,             // Subheader
                0x00, 0xFF, 0xFF, 0x03, 0x00, // Route
                0x06, 0x00,             // Length = 6 (EndCode 2B + Data 4B)
                0x00, 0x00,             // EndCode = 0 (Success)
                0x34, 0x12,             // Word 0 = 0x1234
                0x78, 0x56              // Word 1 = 0x5678
            };

            bool ok = McProtocolFrame.ParseResponse(response, 0, response.Length, out ushort endCode, out byte[] data);

            Assert.True(ok);
            Assert.Equal(0x0000, endCode);
            ushort[] words = McProtocolFrame.ToWords(data);
            Assert.Equal(2, words.Length);
            Assert.Equal(0x1234, words[0]);
            Assert.Equal(0x5678, words[1]);
        }
    }
}
