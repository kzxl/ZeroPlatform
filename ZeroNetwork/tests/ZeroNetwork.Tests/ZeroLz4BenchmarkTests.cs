using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;
using Xunit.Abstractions;
using ZeroNetwork.Common;

namespace ZeroNetwork.Tests
{
    public class ZeroLz4BenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public ZeroLz4BenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ZeroLz4_EmptyAndSmallPayload_RoundTripsCorrectly()
        {
            // Empty
            byte[] empty = Array.Empty<byte>();
            byte[] compressedEmpty = ZeroLz4.CompressFramed(empty);
            Assert.Empty(compressedEmpty);
            byte[] decompressedEmpty = ZeroLz4.DecompressFramed(compressedEmpty);
            Assert.Empty(decompressedEmpty);

            // Small string (< 13 bytes)
            byte[] small = Encoding.UTF8.GetBytes("ZeroPlatform");
            byte[] compressedSmall = ZeroLz4.CompressFramed(small);
            byte[] decompressedSmall = ZeroLz4.DecompressFramed(compressedSmall);
            Assert.Equal(small, decompressedSmall);
        }

        [Fact]
        public void ZeroLz4_RepetitiveData_HighCompressionRatio()
        {
            byte[] repetitive = new byte[65536];
            for (int i = 0; i < repetitive.Length; i++)
            {
                repetitive[i] = (byte)(i % 16);
            }

            byte[] compressed = ZeroLz4.CompressFramed(repetitive);
            byte[] decompressed = ZeroLz4.DecompressFramed(compressed);

            Assert.Equal(repetitive, decompressed);
            // Repetitive data should compress significantly (> 90% reduction)
            Assert.True(compressed.Length < repetitive.Length / 10, $"Compressed size was {compressed.Length}, expected < {repetitive.Length / 10}");
        }

        [Fact]
        public void ZeroLz4_RealisticTelemetryPayload_RoundTripsLosslessly()
        {
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < 500; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"id\":\"sensor_{i % 20}\",\"ts\":{1700000000 + i},\"temp\":{24.5 + (i % 10)},\"press\":{101.3 + (i % 5)}}}");
            }
            sb.Append("]");

            byte[] jsonBytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] compressed = ZeroLz4.CompressFramed(jsonBytes);
            byte[] decompressed = ZeroLz4.DecompressFramed(compressed);

            Assert.Equal(jsonBytes, decompressed);
            Assert.True(compressed.Length < jsonBytes.Length * 0.4, $"Expected at least 60% compression on JSON telemetry. Original: {jsonBytes.Length}, Compressed: {compressed.Length}");
        }

        [Fact]
        public void ZeroLz4_RandomBytes_HandlesIncompressibleDataWithoutCorruption()
        {
            byte[] randomData = new byte[8192];
            new Random(42).NextBytes(randomData);

            byte[] compressed = ZeroLz4.CompressFramed(randomData);
            byte[] decompressed = ZeroLz4.DecompressFramed(compressed);

            Assert.Equal(randomData, decompressed);
        }

        [Fact]
        public void Benchmark_ZeroLz4_Vs_BclDeflate_EmpiricalSpeedup()
        {
            // Create a realistic 64KB industrial sensor JSON batch
            var sb = new StringBuilder();
            sb.Append("[");
            int idx = 0;
            while (sb.Length < 64000)
            {
                if (idx > 0) sb.Append(",");
                sb.Append($"{{\"deviceId\":\"machine_plc_{idx % 15}\",\"timestamp\":{1727500000 + idx},\"voltage\":{380.2 + (idx % 8)},\"rpm\":{1450 + (idx % 100)},\"vibration\":{0.045 + ((idx % 20) * 0.005)}}}");
                idx++;
            }
            sb.Append("]");

            byte[] rawBytes = Encoding.UTF8.GetBytes(sb.ToString());
            const int iterations = 1000;

            // Warmup
            byte[] lz4Warm = ZeroLz4.CompressFramed(rawBytes);
            _ = ZeroLz4.DecompressFramed(lz4Warm);

            using (var ms = new MemoryStream())
            {
                using (var def = new DeflateStream(ms, CompressionLevel.Fastest, true))
                    def.Write(rawBytes, 0, rawBytes.Length);
            }

            // 1. Benchmark BCL DeflateStream (Fastest)
            byte[] bclCompressed = Array.Empty<byte>();
            var swBclCompress = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                using var ms = new MemoryStream();
                using (var def = new DeflateStream(ms, CompressionLevel.Fastest, true))
                {
                    def.Write(rawBytes, 0, rawBytes.Length);
                }
                if (i == 0) bclCompressed = ms.ToArray();
            }
            swBclCompress.Stop();

            var swBclDecompress = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                using var ms = new MemoryStream(bclCompressed);
                using var def = new DeflateStream(ms, CompressionMode.Decompress);
                using var outMs = new MemoryStream(rawBytes.Length);
                def.CopyTo(outMs);
            }
            swBclDecompress.Stop();

            // 2. Benchmark ZeroLz4
            byte[] lz4Compressed = Array.Empty<byte>();
            Span<byte> lz4Buffer = new byte[ZeroLz4.MaximumOutputLength(rawBytes.Length)];
            Span<byte> lz4DecompBuffer = new byte[rawBytes.Length];

            var swLz4Compress = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                int written = ZeroLz4.Compress(rawBytes, lz4Buffer);
                if (i == 0) lz4Compressed = lz4Buffer.Slice(0, written).ToArray();
            }
            swLz4Compress.Stop();

            var swLz4Decompress = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                _ = ZeroLz4.Decompress(lz4Compressed, lz4DecompBuffer);
            }
            swLz4Decompress.Stop();

            double compressSpeedup = (double)swBclCompress.ElapsedTicks / Math.Max(1, swLz4Compress.ElapsedTicks);
            double decompressSpeedup = (double)swBclDecompress.ElapsedTicks / Math.Max(1, swLz4Decompress.ElapsedTicks);

            double lz4CompressMBps = ((double)rawBytes.Length * iterations / (1024 * 1024)) / swLz4Compress.Elapsed.TotalSeconds;
            double lz4DecompressMBps = ((double)rawBytes.Length * iterations / (1024 * 1024)) / swLz4Decompress.Elapsed.TotalSeconds;

            _output.WriteLine("=== EMPIRICAL COMPRESSION BENCHMARK (64KB JSON x 1,000 runs) ===");
            _output.WriteLine($"Payload Size        : {rawBytes.Length:N0} bytes");
            _output.WriteLine($"BCL Deflate Size    : {bclCompressed.Length:N0} bytes (ratio: {(double)bclCompressed.Length / rawBytes.Length * 100:F1}%)");
            _output.WriteLine($"ZeroLz4 Size        : {lz4Compressed.Length:N0} bytes (ratio: {(double)lz4Compressed.Length / rawBytes.Length * 100:F1}%)");
            _output.WriteLine($"BCL Compress Time   : {swBclCompress.ElapsedMilliseconds} ms");
            _output.WriteLine($"ZeroLz4 Compress    : {swLz4Compress.ElapsedMilliseconds} ms ({compressSpeedup:F1}x faster, {lz4CompressMBps:F0} MB/s)");
            _output.WriteLine($"BCL Decompress Time : {swBclDecompress.ElapsedMilliseconds} ms");
            _output.WriteLine($"ZeroLz4 Decompress  : {swLz4Decompress.ElapsedMilliseconds} ms ({decompressSpeedup:F1}x faster, {lz4DecompressMBps:F0} MB/s)");

            Assert.True(lz4CompressMBps > 100, $"Expected ZeroLz4 compress to be fast. Throughput: {lz4CompressMBps:F0} MB/s");
            Assert.True(lz4DecompressMBps > 200, $"Expected ZeroLz4 decompress to be fast. Throughput: {lz4DecompressMBps:F0} MB/s");
        }
    }
}
