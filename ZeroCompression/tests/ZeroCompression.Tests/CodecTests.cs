using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroCompression.Core;

namespace ZeroCompression.Tests
{
    public class CodecTests
    {
        [Theory]
        [InlineData(CompressionMethod.Zstd)]
        [InlineData(CompressionMethod.Brotli)]
        [InlineData(CompressionMethod.Deflate)]
        [InlineData(CompressionMethod.GZip)]
        [InlineData(CompressionMethod.Store)]
        public void Codec_Roundtrip_MatchesOriginal(CompressionMethod method)
        {
            var originalBytes = Encoding.UTF8.GetBytes("ZeroUniverse Sovereign Compression Test! " + new string('A', 5000) + new string('B', 3000));
            var options = new CompressionOptions
            {
                Method = method,
                Level = 3
            };

            using var compressedStream = new MemoryStream();
            using (var compressor = CodecRegistry.WrapCompress(compressedStream, options))
            {
                compressor.Write(originalBytes, 0, originalBytes.Length);
            }

            byte[] compressedBytes = compressedStream.ToArray();
            Assert.NotEmpty(compressedBytes);

            using var sourceStream = new MemoryStream(compressedBytes);
            using var decompressor = CodecRegistry.WrapDecompress(sourceStream, method);
            using var outputStream = new MemoryStream();
            decompressor.CopyTo(outputStream);

            byte[] roundtrippedBytes = outputStream.ToArray();
            Assert.Equal(originalBytes, roundtrippedBytes);
        }

        [Fact]
        public void Zstd_HighCompression_And_LongDistance_Works()
        {
            var originalBytes = Encoding.UTF8.GetBytes(new string('X', 50000) + new string('Y', 50000));
            var options = CompressionOptions.FromProfile(CompressionProfile.Ultra, CompressionMethod.Zstd);
            options.LongDistanceMatching = true;
            options.WindowLog = 27;

            using var compressedStream = new MemoryStream();
            using (var compressor = CodecRegistry.WrapCompress(compressedStream, options))
            {
                compressor.Write(originalBytes, 0, originalBytes.Length);
            }

            byte[] compressed = compressedStream.ToArray();
            Assert.True(compressed.Length < originalBytes.Length / 10, "Should achieve dramatic compression ratio");

            using var source = new MemoryStream(compressed);
            using var decompressor = CodecRegistry.WrapDecompress(source, CompressionMethod.Zstd, windowLog: 27);
            using var output = new MemoryStream();
            decompressor.CopyTo(output);

            Assert.Equal(originalBytes, output.ToArray());
        }
    }
}
