using System;
using Xunit;
using ZeroPrimitives.Buffers;

namespace ZeroPrimitives.Tests
{
    public class VarIntCodecTests
    {
        [Theory]
        [InlineData(0u, 1)]
        [InlineData(1u, 1)]
        [InlineData(127u, 1)]
        [InlineData(128u, 2)]
        [InlineData(16383u, 2)]
        [InlineData(16384u, 3)]
        [InlineData(uint.MaxValue, 5)]
        public void VarUInt32_EncodesAndDecodesCorrectly(uint value, int expectedBytes)
        {
            Span<byte> buffer = stackalloc byte[8];
            VarIntCodec.WriteVarUInt32(buffer, value, out int written);

            Assert.Equal(expectedBytes, written);
            Assert.True(VarIntCodec.TryReadVarUInt32(buffer.Slice(0, written), out uint decoded, out int read));
            Assert.Equal(written, read);
            Assert.Equal(value, decoded);
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(-1, 1)]
        [InlineData(1, 1)]
        [InlineData(-64, 1)]
        [InlineData(64, 2)]
        [InlineData(-65, 2)]
        [InlineData(int.MaxValue, 5)]
        [InlineData(int.MinValue, 5)]
        public void ZigZag_VarInt32_EncodesAndDecodesCorrectly(int value, int expectedBytes)
        {
            Span<byte> buffer = stackalloc byte[8];
            VarIntCodec.WriteVarInt32(buffer, value, out int written);

            Assert.Equal(expectedBytes, written);
            Assert.True(VarIntCodec.TryReadVarInt32(buffer.Slice(0, written), out int decoded, out int read));
            Assert.Equal(written, read);
            Assert.Equal(value, decoded);
        }

        [Theory]
        [InlineData(0L, 1)]
        [InlineData(-1L, 1)]
        [InlineData(1000000000L, 5)]
        [InlineData(long.MaxValue, 10)]
        [InlineData(long.MinValue, 10)]
        public void ZigZag_VarInt64_EncodesAndDecodesCorrectly(long value, int expectedBytes)
        {
            Span<byte> buffer = stackalloc byte[16];
            VarIntCodec.WriteVarInt64(buffer, value, out int written);

            Assert.Equal(expectedBytes, written);
            Assert.True(VarIntCodec.TryReadVarInt64(buffer.Slice(0, written), out long decoded, out int read));
            Assert.Equal(written, read);
            Assert.Equal(value, decoded);
        }
    }
}
