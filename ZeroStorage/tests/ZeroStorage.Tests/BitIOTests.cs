using Xunit;
using ZeroStorage.Core.BitIO;

namespace ZeroStorage.Tests
{
    public class BitIOTests
    {
        [Fact]
        public void BitStream_WriteAndRead_MatchesBitExact()
        {
            byte[] bytes;
            using (var writer = new BitStreamWriter())
            {
                writer.WriteBit(1);
                writer.WriteBit(0);
                writer.WriteBit(1);
                writer.WriteBits(0b1011, 4); // 1, 0, 1, 1
                writer.WriteBit(0);          // Total 8 bits in first byte: 10110110 = 0xB6
                writer.WriteBits(0xDEADBEEFCAFE, 48);
                bytes = writer.ToByteArray();
            }

            Assert.Equal(7, bytes.Length); // 8 bits + 48 bits = 56 bits = 7 bytes
            Assert.Equal(0xB6, bytes[0]);

            var reader = new BitStreamReader(bytes);
            Assert.Equal(1, reader.ReadBit());
            Assert.Equal(0, reader.ReadBit());
            Assert.Equal(1, reader.ReadBit());
            Assert.Equal(0b1011UL, reader.ReadBits(4));
            Assert.Equal(0, reader.ReadBit());
            Assert.Equal(0xDEADBEEFCAFEUL, reader.ReadBits(48));
        }

        [Fact]
        public void BitStream_64BitWordBoundary_RoundTripsAccurately()
        {
            byte[] bytes;
            ulong val64 = 0xFEDCBA9876543210UL;
            ulong val33 = 0x1FFFFFFFFUL;
            ulong val17 = 0x1A2B3UL & 0x1FFFF;

            using (var writer = new BitStreamWriter())
            {
                writer.WriteBits(val64, 64);
                writer.WriteBits(val33, 33);
                writer.WriteBits(val17, 17);
                writer.WriteBit(1);
                bytes = writer.ToByteArray();
            }

            var reader = new BitStreamReader(bytes);
            Assert.Equal(val64, reader.ReadBits(64));
            Assert.Equal(val33, reader.ReadBits(33));
            Assert.Equal(val17, reader.ReadBits(17));
            Assert.Equal(1, reader.ReadBit());
        }

        [Fact]
        public void BitStream_ArbitraryBitLengths_LoopMatches()
        {
            byte[] bytes;
            using (var writer = new BitStreamWriter())
            {
                for (int len = 1; len <= 60; len++)
                {
                    ulong val = (ulong)len & ((1UL << len) - 1UL);
                    writer.WriteBits(val, len);
                }
                bytes = writer.ToByteArray();
            }

            var reader = new BitStreamReader(bytes);
            for (int len = 1; len <= 60; len++)
            {
                ulong expected = (ulong)len & ((1UL << len) - 1UL);
                ulong actual = reader.ReadBits(len);
                Assert.Equal(expected, actual);
            }
        }
    }
}
