using System;
using System.Text;
using Xunit;
using ZeroPrimitives.Buffers;

namespace ZeroPrimitives.Tests
{
    public class FastBufferTests
    {
        [Fact]
        public void GzipCompress_And_Decompress_RoundtripsSuccessfully()
        {
            string original = "The quick brown fox jumps over the lazy dog. 1234567890! ZeroPlatform sovereign industrial suite.";
            byte[] rawBytes = Encoding.UTF8.GetBytes(original);

            byte[] compressed = FastBuffer.GzipCompress(rawBytes);
            Assert.NotEmpty(compressed);

            string decompressed = FastBuffer.GzipDecompressToString(compressed);
            Assert.Equal(original, decompressed);

            byte[] decompressedBytes = FastBuffer.GzipDecompress(compressed);
            Assert.Equal(rawBytes, decompressedBytes);
        }

        [Fact]
        public void GzipDecompress_UncompressedPayload_FallsBackSafelyWithoutException()
        {
            string plainText = "Plain uncompressed text payload that should not crash!";
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);

            // GzipDecompressToString on plain UTF-8 text must NOT throw, must return plain text
            string resultText = FastBuffer.GzipDecompressToString(plainBytes);
            Assert.Equal(plainText, resultText);

            // GzipDecompress on uncompressed bytes must return original bytes
            byte[] resultBytes = FastBuffer.GzipDecompress(plainBytes);
            Assert.Equal(plainBytes, resultBytes);
        }

        [Fact]
        public void Gzip_NullAndEmptyInputs_HandledSafely()
        {
            Assert.Equal(string.Empty, FastBuffer.GzipDecompressToString(null));
            Assert.Equal(string.Empty, FastBuffer.GzipDecompressToString(Array.Empty<byte>()));

            Assert.Empty(FastBuffer.GzipDecompress(null));
            Assert.Empty(FastBuffer.GzipDecompress(Array.Empty<byte>()));

            Assert.Empty(FastBuffer.GzipCompress((byte[])null!));
            Assert.Empty(FastBuffer.GzipCompress(Array.Empty<byte>()));
        }

        [Fact]
        public void EndianSwap_ReversesByteOrderAccurately()
        {
            // 16-bit: 0x1234 -> 0x3412
            short s = 0x1234;
            Assert.Equal((short)0x3412, FastBuffer.SwapInt16(s));
            Assert.Equal(s, FastBuffer.SwapInt16(FastBuffer.SwapInt16(s)));

            // 32-bit: 0x12345678 -> 0x78563412
            int i = 0x12345678;
            Assert.Equal(0x78563412, FastBuffer.SwapInt32(i));
            Assert.Equal(i, FastBuffer.SwapInt32(FastBuffer.SwapInt32(i)));

            // 64-bit: 0x0102030405060708 -> 0x0807060504030201
            long l = 0x0102030405060708L;
            long swappedL = 0x0807060504030201L;
            Assert.Equal(swappedL, FastBuffer.SwapInt64(l));
            Assert.Equal(l, FastBuffer.SwapInt64(FastBuffer.SwapInt64(l)));
        }
    }
}
