using Xunit;
using ZeroComm.Core.Omron;

namespace ZeroComm.Tests
{
    public class FinsTests
    {
        [Fact]
        public void Fins_BuildMemoryAreaReadRequest_ConstructsValidFrame()
        {
            byte[] frame = FinsFrame.BuildMemoryAreaReadRequest(
                memoryArea: FinsMemoryArea.DM_Word,
                address: 200,
                numberOfItems: 4,
                destNode: 10,
                srcNode: 20,
                serviceId: 0x5A);

            Assert.Equal(18, frame.Length);
            Assert.Equal(0x80, frame[0]); // ICF
            Assert.Equal(10, frame[4]);   // DA1
            Assert.Equal(20, frame[7]);   // SA1
            Assert.Equal(0x5A, frame[9]); // SID
            Assert.Equal(0x01, frame[10]); // MRC
            Assert.Equal(0x01, frame[11]); // SRC
            Assert.Equal((byte)FinsMemoryArea.DM_Word, frame[12]);
            Assert.Equal(0x00, frame[13]); // Addr High
            Assert.Equal(200, frame[14]);  // Addr Low
            Assert.Equal(0, frame[15]);    // Sub-addr
            Assert.Equal(0x00, frame[16]); // Count High
            Assert.Equal(4, frame[17]);    // Count Low
        }

        [Fact]
        public void Fins_ParseResponse_ValidWords()
        {
            // FINS Header (10B) + Command (2B) + EndCode (2B: 0x0000) + Data (4B: 0x1122, 0x3344)
            byte[] response = new byte[]
            {
                0xC0, 0x00, 0x02, 0x00, 0x20, 0x00, 0x00, 0x10, 0x00, 0x5A, // Header
                0x01, 0x01,                                                 // Command: 0x0101
                0x00, 0x00,                                                 // EndCode: Normal
                0x11, 0x22,                                                 // Word 0: 0x1122
                0x33, 0x44                                                  // Word 1: 0x3344
            };

            bool ok = FinsFrame.ParseResponse(response, 0, response.Length, out byte sid, out ushort endCode, out byte[] data);

            Assert.True(ok);
            Assert.Equal(0x5A, sid);
            Assert.Equal(0x0000, endCode);
            ushort[] words = FinsFrame.ToWords(data);
            Assert.Equal(2, words.Length);
            Assert.Equal(0x1122, words[0]);
            Assert.Equal(0x3344, words[1]);
        }
    }
}
