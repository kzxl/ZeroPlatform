using System;
using System.Text;
using Xunit;
using ZeroPrimitives.Cryptography;

namespace ZeroPrimitives.Tests
{
    public class FastCrcTests
    {
        [Fact]
        public void Crc16Modbus_StandardVector_ReturnsExpected()
        {
            byte[] data = Encoding.ASCII.GetBytes("123456789");
            ushort crc = FastCrc.Crc16Modbus(data);

            Assert.Equal(0x4B37, crc);
        }

        [Fact]
        public void Crc32_StandardVector_ReturnsExpected()
        {
            byte[] data = Encoding.ASCII.GetBytes("123456789");
            uint crc = FastCrc.Crc32(data);

            Assert.Equal(0xCBF43926u, crc);
        }

        [Fact]
        public void Crc16Ccitt_StandardVector_ReturnsExpected()
        {
            byte[] data = Encoding.ASCII.GetBytes("123456789");
            ushort crc = FastCrc.Crc16Ccitt(data, 0xFFFF);

            Assert.Equal(0x29B1, crc);
        }

        [Fact]
        public void EmptyData_ReturnsInitialCrc()
        {
            Assert.Equal(0xFFFF, FastCrc.Crc16Modbus(ReadOnlySpan<byte>.Empty));
            Assert.Equal(0u, FastCrc.Crc32(ReadOnlySpan<byte>.Empty));
        }
    }
}
