using System.Text;
using Xunit;
using ZeroComm.Core.Checksums;

namespace ZeroComm.Tests
{
    public class ChecksumTests
    {
        [Fact]
        public void Crc16_ModbusStandardVector_MatchesExpected()
        {
            // Standard Modbus request: Read 10 holding registers starting at address 0 from slave 1
            // 01 03 00 00 00 0A
            // Expected CRC: 0xC5CD (Low byte 0xC5, High byte 0xCD)
            byte[] data = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
            ushort crc = Crc16.ComputeModbus(data);

            // Modbus RTU sends Low Byte first (0xC5), then High Byte (0xCD)
            // As a ushort word (High << 8 | Low), this is 0xCDC5 = 52677
            Assert.Equal(0xCDC5, crc);
            Assert.Equal(0xC5, (byte)(crc & 0xFF));        // CRC Low byte
            Assert.Equal(0xCD, (byte)((crc >> 8) & 0xFF)); // CRC High byte
        }

        [Fact]
        public void Crc32_StandardVector_MatchesExpected()
        {
            // Standard CRC32 of ASCII string "123456789" is 0xCBF43926
            byte[] data = Encoding.ASCII.GetBytes("123456789");
            uint crc = Crc32.Compute(data);

            Assert.Equal(0xCBF43926u, crc);
        }

        [Fact]
        public void Crc16_Ccitt_CalculatesDeterministicChecksum()
        {
            byte[] data = Encoding.ASCII.GetBytes("123456789");
            ushort crc = Crc16.ComputeCcitt(data);

            Assert.NotEqual(0, crc);
            // Verify stability
            Assert.Equal(crc, Crc16.ComputeCcitt(data));
        }
    }
}
