using System;
using System.Text;
using Xunit;
using ZeroPrimitives.Cryptography;

namespace ZeroPrimitives.Tests
{
    public class FastCrc32CTests
    {
        [Fact]
        public void Crc32C_ComputesKnownTestVectors()
        {
            // Standard Castagnoli test vector: "123456789" -> 0xE3069283
            byte[] bytes = Encoding.ASCII.GetBytes("123456789");
            uint crc = FastCrc.Crc32C(bytes);

            Assert.Equal(0xE3069283u, crc);
        }

        [Fact]
        public void Crc32C_EmptyData_ReturnsZero()
        {
            Assert.Equal(0u, FastCrc.Crc32C(ReadOnlySpan<byte>.Empty));
        }

        [Fact]
        public void Crc32C_HardwareOrSoftware_ProducesConsistentResult()
        {
            byte[] buffer = new byte[1024];
            for (int i = 0; i < buffer.Length; i++) buffer[i] = (byte)(i & 0xFF);

            uint crc1 = FastCrc.Crc32C(buffer);
            uint crc2 = FastCrc.Crc32C(buffer);

            Assert.Equal(crc1, crc2);
            Assert.NotEqual(0u, crc1);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(31)]
        [InlineData(32)]
        [InlineData(63)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(127)]
        [InlineData(128)]
        [InlineData(255)]
        [InlineData(256)]
        [InlineData(1024)]
        [InlineData(4096)]
        public void Crc32C_VariousLengths_DeterministicallyComputes(int length)
        {
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++) data[i] = (byte)((i * 31 + 17) & 0xFF);

            uint crc1 = FastCrc.Crc32C(data);
            uint crc2 = FastCrc.Crc32C(data);

            Assert.Equal(crc1, crc2);
            Assert.NotEqual(0u, crc1);
        }
    }
}
