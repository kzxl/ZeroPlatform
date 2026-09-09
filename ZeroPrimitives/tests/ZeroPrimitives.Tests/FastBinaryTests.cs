using System;
using Xunit;
using ZeroPrimitives.Buffers;

namespace ZeroPrimitives.Tests
{
    public class FastBinaryTests
    {
        [Fact]
        public void LittleEndian_ReadWrite_RoundtripsAccurately()
        {
            Span<byte> buffer = stackalloc byte[32];

            FastBinary.WriteInt16LittleEndian(buffer, -1234);
            Assert.Equal((short)-1234, FastBinary.ReadInt16LittleEndian(buffer));

            FastBinary.WriteInt32LittleEndian(buffer, 123456789);
            Assert.Equal(123456789, FastBinary.ReadInt32LittleEndian(buffer));

            FastBinary.WriteInt64LittleEndian(buffer, 987654321012345L);
            Assert.Equal(987654321012345L, FastBinary.ReadInt64LittleEndian(buffer));

            FastBinary.WriteSingleLittleEndian(buffer, 123.456f);
            Assert.Equal(123.456f, FastBinary.ReadSingleLittleEndian(buffer));

            FastBinary.WriteDoubleLittleEndian(buffer, 9876.54321);
            Assert.Equal(9876.54321, FastBinary.ReadDoubleLittleEndian(buffer));
        }

        [Fact]
        public void BigEndian_ReadWrite_RoundtripsAccurately()
        {
            Span<byte> buffer = stackalloc byte[32];

            FastBinary.WriteInt16BigEndian(buffer, -1234);
            Assert.Equal((short)-1234, FastBinary.ReadInt16BigEndian(buffer));

            FastBinary.WriteInt32BigEndian(buffer, 0x12345678);
            Assert.Equal(0x12, buffer[0]);
            Assert.Equal(0x34, buffer[1]);
            Assert.Equal(0x56, buffer[2]);
            Assert.Equal(0x78, buffer[3]);
            Assert.Equal(0x12345678, FastBinary.ReadInt32BigEndian(buffer));

            FastBinary.WriteInt64BigEndian(buffer, 987654321012345L);
            Assert.Equal(987654321012345L, FastBinary.ReadInt64BigEndian(buffer));

            FastBinary.WriteSingleBigEndian(buffer, 123.456f);
            Assert.Equal(123.456f, FastBinary.ReadSingleBigEndian(buffer));

            FastBinary.WriteDoubleBigEndian(buffer, 9876.54321);
            Assert.Equal(9876.54321, FastBinary.ReadDoubleBigEndian(buffer));
        }
    }
}
