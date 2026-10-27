using System;
using Xunit;
using ZeroPrimitives.Buffers;

namespace ZeroPrimitives.Tests
{
    public class SpanReaderAndWriterTests
    {
        [Fact]
        public void SpanWriterAndReader_RoundtripAllPrimitives()
        {
            Span<byte> buffer = stackalloc byte[128];
            var writer = new SpanWriter(buffer);

            writer.WriteByte(0xAB);
            writer.WriteInt16LittleEndian(1234);
            writer.WriteUInt16BigEndian(5678);
            writer.WriteInt32LittleEndian(100_000);
            writer.WriteUInt32BigEndian(200_000);
            writer.WriteInt64LittleEndian(9876543210123L);
            writer.WriteSingleLittleEndian(3.14159f);
            writer.WriteDoubleBigEndian(2.718281828);
            writer.WriteVarInt32(-42);
            writer.WriteVarUInt32(300);
            writer.WriteStringUtf8("ZeroPrimitives".AsSpan());

            Assert.True(writer.BytesWritten > 0);

            var reader = new SpanReader(writer.WrittenSpan);

            Assert.Equal(0xAB, reader.ReadByte());
            Assert.Equal(1234, reader.ReadInt16LittleEndian());
            Assert.Equal(5678, reader.ReadUInt16BigEndian());
            Assert.Equal(100_000, reader.ReadInt32LittleEndian());
            Assert.Equal(200_000u, reader.ReadUInt32BigEndian());
            Assert.Equal(9876543210123L, reader.ReadInt64LittleEndian());
            Assert.Equal(3.14159f, reader.ReadSingleLittleEndian(), 4);
            Assert.Equal(2.718281828, reader.ReadDoubleBigEndian(), 8);
            Assert.Equal(-42, reader.ReadVarInt32());
            Assert.Equal(300u, reader.ReadVarUInt32());
            Assert.Equal("ZeroPrimitives", reader.ReadStringUtf8("ZeroPrimitives".Length));

            Assert.Equal(0, reader.Remaining);
            Assert.False(reader.HasRemaining);
        }

        [Fact]
        public void SpanReader_Utf8String_HandlesUnicodeAndZeroLengths()
        {
            string original = "Xin chào ZeroPlatform! Công nghệ cao ⚡ \u00A9 2026";
            byte[] encoded = System.Text.Encoding.UTF8.GetBytes(original);

            var reader = new SpanReader(encoded);
            Assert.Equal(original, reader.ReadStringUtf8(encoded.Length));
            Assert.Equal(0, reader.Remaining);

            var emptyReader = new SpanReader(ReadOnlySpan<byte>.Empty);
            Assert.Equal(string.Empty, emptyReader.ReadStringUtf8(0));
            Assert.True(emptyReader.TryReadStringUtf8(0, out string emptyVal));
            Assert.Equal(string.Empty, emptyVal);
        }

        [Fact]
        public void ArrayPoolRentScope_ReturnsToPoolOnDispose()
        {
            using (var scope = ArrayPoolRentScope<byte>.Rent(256))
            {
                Assert.True(scope.Length >= 256);
                Assert.False(scope.Span.IsEmpty);

                scope.Span[0] = 0xAA;
                scope.Span[255] = 0xBB;
                Assert.Equal(0xAA, scope.ReadOnlySpan[0]);
                Assert.Equal(0xBB, scope.ReadOnlySpan[255]);
            }
            // Disposed without exception
        }

        [Fact]
        public void ArrayPoolRentScope_DoubleDispose_IsIdempotentAndDoesNotThrow()
        {
            var scope = ArrayPoolRentScope<byte>.Rent(128);
            Assert.NotNull(scope.RawArray);
            Assert.False(scope.Span.IsEmpty);

            // First dispose
            scope.Dispose();
            Assert.Null(scope.RawArray);
            Assert.True(scope.Span.IsEmpty);

            // Second dispose (must be completely idempotent and not corrupt ArrayPool)
            scope.Dispose();
            Assert.Null(scope.RawArray);

            // Third dispose
            scope.Dispose();

            // Explicit dispose inside using block
            using (var scopeInUsing = ArrayPoolRentScope<byte>.Rent(64))
            {
                scopeInUsing.Dispose();
                Assert.Null(scopeInUsing.RawArray);
                // Exit of using block calls Dispose() a second time
            }

            // Zero-length scope dispose
            var zeroScope = ArrayPoolRentScope<byte>.Rent(0);
            zeroScope.Dispose();
            zeroScope.Dispose();
        }
    }
}
