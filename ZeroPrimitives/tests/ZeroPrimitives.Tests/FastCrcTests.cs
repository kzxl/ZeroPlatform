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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(1024)]
        public void Crc32_VariousLengths_DeterministicallyComputes(int length)
        {
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++) data[i] = (byte)((i * 19 + 7) & 0xFF);

            uint crc1 = FastCrc.Crc32(data);
            uint crc2 = FastCrc.Crc32(data);

            Assert.Equal(crc1, crc2);
            Assert.NotEqual(0u, crc1);
        }
    }
}
