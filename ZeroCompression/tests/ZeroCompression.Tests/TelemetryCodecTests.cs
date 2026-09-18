using System;
using System.IO;
using System.Text;
using Xunit;
using ZeroCompression.Core;
using ZeroCompression.Core.Codecs;

namespace ZeroCompression.Tests
{
    public class TelemetryCodecTests
    {
        [Fact]
        public void BitWriter_And_BitReader_FullRoundtrip()
        {
            using var ms = new MemoryStream();
            var writer = new ZeroTelemetryCodec.BitWriter(ms);

            writer.WriteBit(1);
            writer.WriteBit(0);
            writer.WriteBits(42, 6);
            writer.WriteBits(0xDEADBEEFCAFEBABEUL, 64);
            writer.WriteBits(7, 3);
            writer.Flush();

            ms.Position = 0;
            var reader = new ZeroTelemetryCodec.BitReader(ms);

            Assert.Equal(1, reader.ReadBit());
            Assert.Equal(0, reader.ReadBit());
            Assert.Equal(42UL, reader.ReadBits(6));
            Assert.Equal(0xDEADBEEFCAFEBABEUL, reader.ReadBits(64));
            Assert.Equal(7UL, reader.ReadBits(3));
        }

        [Fact]
        public void DirectTelemetryBatch_Roundtrip_ExactMatch()
        {
            const int count = 2000;
            var timestamps = new long[count];
            var values = new double[count];

            long baseTime = 1_700_000_000_000L;
            for (int i = 0; i < count; i++)
            {
                baseTime += 1000; // 1 second intervals (1-bit DoD)
                timestamps[i] = baseTime;
                values[i] = 25.0 + 5.0 * Math.Sin(i * 0.005);
            }

            using var ms = new MemoryStream();
            ZeroTelemetryCodec.CompressTelemetry(timestamps, values, ms);

            byte[] compressed = ms.ToArray();
            Assert.NotEmpty(compressed);
            Assert.True(compressed.Length < (count * 16) / 2, "Telemetry compression should reduce size by >50%");

            ms.Position = 0;
            var decTimestamps = new long[count];
            var decValues = new double[count];
            ZeroTelemetryCodec.DecompressTelemetry(ms, decTimestamps, decValues);

            for (int i = 0; i < count; i++)
            {
                Assert.Equal(timestamps[i], decTimestamps[i]);
                Assert.Equal(values[i], decValues[i]);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(100)]
        [InlineData(16384)]
        [InlineData(50003)] // unaligned across multiple blocks
        public void StreamCodec_ArbitraryPayload_Roundtrip_Exact(int byteCount)
        {
            var original = new byte[byteCount];
            var rng = new Random(42);
            rng.NextBytes(original);

            var options = new CompressionOptions { Method = CompressionMethod.ZeroTelemetry };

            using var compMs = new MemoryStream();
            using (var cmp = CodecRegistry.WrapCompress(compMs, options))
            {
                cmp.Write(original, 0, original.Length);
            }

            byte[] compressed = compMs.ToArray();

            using var srcMs = new MemoryStream(compressed);
            using var dec = CodecRegistry.WrapDecompress(srcMs, CompressionMethod.ZeroTelemetry);
            using var outMs = new MemoryStream();
            dec.CopyTo(outMs);

            byte[] roundtripped = outMs.ToArray();
            Assert.Equal(original, roundtripped);
        }

        [Fact]
        public void CodecRegistry_ResolvesZeroTelemetry()
        {
            var codec = CodecRegistry.Get(CompressionMethod.ZeroTelemetry);
            Assert.NotNull(codec);
            Assert.Equal(CompressionMethod.ZeroTelemetry, codec.Method);
            Assert.IsType<ZeroTelemetryCodec>(codec);
        }
    }
}
