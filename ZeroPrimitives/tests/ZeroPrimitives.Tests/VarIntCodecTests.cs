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

        [Theory]
        [InlineData(0u, 1)]
        [InlineData(127u, 1)]
        [InlineData(128u, 2)]
        [InlineData(16384u, 3)]
        [InlineData(uint.MaxValue, 5)]
        public void GetEncodedLength_And_TryWriteVarUInt32_BehaveSafely(uint value, int expectedLen)
        {
            Assert.Equal(expectedLen, VarIntCodec.GetEncodedLength(value));

            byte[] tooSmall = new byte[expectedLen - 1];
            tooSmall.AsSpan().Fill((byte)0xAA);
            Assert.False(VarIntCodec.TryWriteVarUInt32(tooSmall, value, out int smallWritten));
            Assert.Equal(0, smallWritten);
            foreach (byte b in tooSmall) Assert.Equal((byte)0xAA, b);

            Span<byte> exact = stackalloc byte[expectedLen];
            Assert.True(VarIntCodec.TryWriteVarUInt32(exact, value, out int written));
            Assert.Equal(expectedLen, written);
            Assert.True(VarIntCodec.TryReadVarUInt32(exact, out uint decoded, out _));
            Assert.Equal(value, decoded);
        }

        [Fact]
        public void GetEncodedLength_And_TryWriteVarUInt64_And_Signed_BehaveSafely()
        {
            Assert.Equal(10, VarIntCodec.GetEncodedLength(ulong.MaxValue));

            byte[] tooSmall = new byte[9];
            tooSmall.AsSpan().Fill((byte)0x55);
            Assert.False(VarIntCodec.TryWriteVarUInt64(tooSmall, ulong.MaxValue, out int smallWritten));
            Assert.Equal(0, smallWritten);
            foreach (byte b in tooSmall) Assert.Equal((byte)0x55, b);

            Span<byte> exact64 = stackalloc byte[10];
            Assert.True(VarIntCodec.TryWriteVarUInt64(exact64, ulong.MaxValue, out int w64));
            Assert.Equal(10, w64);
            Assert.True(VarIntCodec.TryReadVarUInt64(exact64, out ulong u64, out _));
            Assert.Equal(ulong.MaxValue, u64);

            Assert.False(VarIntCodec.TryWriteVarInt32(tooSmall.AsSpan(0, 4), int.MinValue, out int wSmall32));
            Assert.Equal(0, wSmall32);
            Span<byte> exact32 = stackalloc byte[5];
            Assert.True(VarIntCodec.TryWriteVarInt32(exact32, int.MinValue, out int w32));
            Assert.True(VarIntCodec.TryReadVarInt32(exact32.Slice(0, w32), out int s32, out _));
            Assert.Equal(int.MinValue, s32);

            Assert.False(VarIntCodec.TryWriteVarInt64(tooSmall, long.MinValue, out int wSmall64));
            Assert.Equal(0, wSmall64);
            Assert.True(VarIntCodec.TryWriteVarInt64(exact64, long.MinValue, out int ws64));
            Assert.True(VarIntCodec.TryReadVarInt64(exact64.Slice(0, ws64), out long s64, out _));
            Assert.Equal(long.MinValue, s64);
        }
    }
}
