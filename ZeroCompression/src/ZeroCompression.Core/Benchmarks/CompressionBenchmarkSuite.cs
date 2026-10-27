using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using ZeroCompression.Core.Codecs;

namespace ZeroCompression.Core.Benchmarks
{
    public sealed class BenchmarkResult
    {
        public required string CodecName { get; init; }
        public required CompressionMethod Method { get; init; }
        public long OriginalBytes { get; init; }
        public long CompressedBytes { get; init; }
        public double RatioPercent => OriginalBytes > 0 ? (1.0 - ((double)CompressedBytes / OriginalBytes)) * 100.0 : 0.0;
        public double CompressionFactor => CompressedBytes > 0 ? (double)OriginalBytes / CompressedBytes : 0.0;
        public double CompressTimeMs { get; init; }
        public double CompressThroughputMBs => CompressTimeMs > 0 ? ((OriginalBytes / (1024.0 * 1024.0)) / (CompressTimeMs / 1000.0)) : 0.0;
        public double DecompressTimeMs { get; init; }
        public double DecompressThroughputMBs => DecompressTimeMs > 0 ? ((OriginalBytes / (1024.0 * 1024.0)) / (DecompressTimeMs / 1000.0)) : 0.0;
        public bool RoundtripVerified { get; init; }
        public string? Notes { get; init; }
    }

    public sealed class DatasetBenchmarkReport
    {
        public required string DatasetName { get; init; }
        public required string Description { get; init; }
        public long TotalBytes { get; init; }
        public List<BenchmarkResult> Results { get; } = new();

        public string ToMarkdownTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"### Benchmark: {DatasetName}");
            sb.AppendLine($"*{Description} (Size: {TotalBytes:N0} bytes / {TotalBytes / 1024.0:F1} KB)*\n");
            sb.AppendLine("| Codec | Original | Compressed | Ratio (%) | Factor | Comp Speed | Decomp Speed | Roundtrip |");
            sb.AppendLine("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |");

            foreach (var r in Results)
            {
                string verifiedStr = r.RoundtripVerified ? "✅ PASS" : "❌ FAIL";
                sb.AppendLine($"| **{r.CodecName}** | {r.OriginalBytes / 1024.0:F1} KB | {r.CompressedBytes / 1024.0:F1} KB | {r.RatioPercent:F1}% | {r.CompressionFactor:F2}x | {r.CompressThroughputMBs:F1} MB/s | {r.DecompressThroughputMBs:F1} MB/s | {verifiedStr} |");
            }

            return sb.ToString();
        }

        public string ToConsoleTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"========================================================================================================");
            sb.AppendLine($"BENCHMARK DATASET: {DatasetName.ToUpperInvariant()} ({TotalBytes:N0} bytes / {TotalBytes / 1024.0:F1} KB)");
            sb.AppendLine($"Description: {Description}");
            sb.AppendLine($"========================================================================================================");
            sb.AppendLine(string.Format("{0,-24} | {1,10} | {2,10} | {3,8} | {4,7} | {5,12} | {6,12} | {7,8}",
                "Codec", "Orig (KB)", "Comp (KB)", "Ratio", "Factor", "Comp (MB/s)", "Decomp(MB/s)", "Verified"));
            sb.AppendLine(new string('-', 104));

            foreach (var r in Results)
            {
                sb.AppendLine(string.Format("{0,-24} | {1,10:F1} | {2,10:F1} | {3,7:F1}% | {4,6:F2}x | {5,10:F1} MB/s | {6,10:F1} MB/s | {7,8}",
                    r.CodecName,
                    r.OriginalBytes / 1024.0,
                    r.CompressedBytes / 1024.0,
                    r.RatioPercent,
                    r.CompressionFactor,
                    r.CompressThroughputMBs,
                    r.DecompressThroughputMBs,
                    r.RoundtripVerified ? "PASS" : "FAIL"));
            }
            sb.AppendLine(new string('-', 104));
            return sb.ToString();
        }
    }

    /// <summary>
    /// Side-by-side automated compression benchmark suite comparing ZeroTelemetry against
    /// general-purpose industry standards (Zstd, LZMA, Brotli, Deflate).
    /// </summary>
    public static class CompressionBenchmarkSuite
    {
        public static (long[] Timestamps, double[] Values, byte[] RawBytes) GenerateTelemetryDataset(int sampleCount = 50000)
        {
            var timestamps = new long[sampleCount];
            var values = new double[sampleCount];
            var rawBytes = new byte[sampleCount * 16];

            long baseTime = 1_700_000_000_000L; // ms epoch
            var rng = new Random(42);

            for (int i = 0; i < sampleCount; i++)
            {
                // Realistic ~1000ms polling interval with small millisecond jitter
                baseTime += 1000 + rng.Next(-2, 3);
                timestamps[i] = baseTime;

                // Industrial sensor signal: smooth sinusoid + minor noise + occasional steady state
                double signal = 25.0 + 5.0 * Math.Sin(i * 0.005) + 0.2 * Math.Cos(i * 0.05);
                if (i % 50 < 10)
                {
                    // Plateau / steady-state reading
                    signal = 25.0 + 5.0 * Math.Sin((i - (i % 50)) * 0.005);
                }
                values[i] = signal;

                int offset = i * 16;
                System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(rawBytes.AsSpan(offset, 8), timestamps[i]);
                ulong valBits = BitConverter.DoubleToUInt64Bits(values[i]);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(rawBytes.AsSpan(offset + 8, 8), valBits);
            }

            return (timestamps, values, rawBytes);
        }

        public static byte[] GenerateStructuredJsonDataset(int recordCount = 4000)
        {
            var sb = new StringBuilder();
            var rng = new Random(100);
            string[] levels = { "INFO", "WARN", "DEBUG", "ERROR" };
            string[] components = { "AuthService", "OrderProcessor", "InventorySync", "DatabasePool", "Gateway" };

            long timestamp = 1_700_000_000_000L;
            for (int i = 0; i < recordCount; i++)
            {
                timestamp += rng.Next(50, 500);
                string level = levels[rng.Next(levels.Length)];
                string comp = components[rng.Next(components.Length)];
                int code = rng.Next(100, 999);
                sb.AppendLine($"{{\"timestamp\":{timestamp},\"level\":\"{level}\",\"component\":\"{comp}\",\"code\":{code},\"message\":\"Transaction processed successfully for order ref {i:D8}\"}}");
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public static byte[] GenerateMixedBinaryDataset(int totalBytes = 800 * 1024)
        {
            var data = new byte[totalBytes];
            var rng = new Random(2026);
            int written = 0;

            while (written < totalBytes)
            {
                int patternType = rng.Next(4);
                int chunkLen = Math.Min(rng.Next(256, 4096), totalBytes - written);

                switch (patternType)
                {
                    case 0: // repeating zero / padding
                        Array.Clear(data, written, chunkLen);
                        break;
                    case 1: // incremental counter
                        for (int k = 0; k < chunkLen; k++) data[written + k] = (byte)(k & 0xFF);
                        break;
                    case 2: // text strings
                        byte[] txt = Encoding.ASCII.GetBytes("ZeroUniverse Sovereign Architecture Benchmark Data ");
                        for (int k = 0; k < chunkLen; k++) data[written + k] = txt[k % txt.Length];
                        break;
                    default: // pseudorandom entropy
                        rng.NextBytes(data.AsSpan(written, chunkLen));
                        break;
                }
                written += chunkLen;
            }

            return data;
        }

        public static DatasetBenchmarkReport BenchmarkDataset(string name, string description, byte[] data, (long[] Timestamps, double[] Values)? telemetryData = null)
        {
            var report = new DatasetBenchmarkReport
            {
                DatasetName = name,
                Description = description,
                TotalBytes = data.Length
            };

            var configurations = new (string Name, CompressionMethod Method, int Level)[]
            {
                ("ZeroTelemetry (Gorilla)", CompressionMethod.ZeroTelemetry, 0),
                ("Zstandard (Level 1 - Fast)", CompressionMethod.Zstd, 1),
                ("Zstandard (Level 3 - Default)", CompressionMethod.Zstd, 3),
                ("Zstandard (Level 19 - Ultra)", CompressionMethod.Zstd, 19),
                ("LZMA (7-Zip / LZip)", CompressionMethod.Lzma, 0),
                ("Brotli (Level 4 - Normal)", CompressionMethod.Brotli, 4),
                ("Brotli (Level 11 - Ultra)", CompressionMethod.Brotli, 11),
                ("Deflate (Optimal)", CompressionMethod.Deflate, 6),
            };

            foreach (var config in configurations)
            {
                var result = BenchmarkSingleCodec(config.Name, config.Method, config.Level, data);
                report.Results.Add(result);
            }

            return report;
        }

        private static BenchmarkResult BenchmarkSingleCodec(string codecName, CompressionMethod method, int level, byte[] input)
        {
            var options = new CompressionOptions
            {
                Method = method,
                Level = level,
                Workers = 1
            };

            // Warmup run
            try
            {
                using var warmupOut = new MemoryStream();
                using (var cmp = CodecRegistry.WrapCompress(warmupOut, options))
                {
                    cmp.Write(input, 0, Math.Min(input.Length, 1024));
                }
            }
            catch { /* warmup safeguard */ }

            // Measurement: Compression
            byte[] compressedBytes;
            var sw = Stopwatch.StartNew();
            using (var compMs = new MemoryStream())
            {
                using (var compressor = CodecRegistry.WrapCompress(compMs, options))
                {
                    compressor.Write(input, 0, input.Length);
                }
                compressedBytes = compMs.ToArray();
            }
            sw.Stop();
            double compTimeMs = sw.Elapsed.TotalMilliseconds;

            // Measurement: Decompression
            sw.Restart();
            byte[] decompressedBytes;
            using (var srcMs = new MemoryStream(compressedBytes))
            using (var decompressor = CodecRegistry.WrapDecompress(srcMs, method))
            using (var outMs = new MemoryStream(input.Length))
            {
                decompressor.CopyTo(outMs);
                decompressedBytes = outMs.ToArray();
            }
            sw.Stop();
            double decompTimeMs = sw.Elapsed.TotalMilliseconds;

            bool roundtripVerified = input.AsSpan().SequenceEqual(decompressedBytes);

            return new BenchmarkResult
            {
                CodecName = codecName,
                Method = method,
                OriginalBytes = input.Length,
                CompressedBytes = compressedBytes.Length,
                CompressTimeMs = Math.Max(compTimeMs, 0.001),
                DecompressTimeMs = Math.Max(decompTimeMs, 0.001),
                RoundtripVerified = roundtripVerified
            };
        }

        public static List<DatasetBenchmarkReport> RunAllBenchmarks()
        {
            var reports = new List<DatasetBenchmarkReport>();

            // 1. Industrial SCADA Telemetry Dataset
            var telemetry = GenerateTelemetryDataset(50000); // 800 KB
            reports.Add(BenchmarkDataset("SCADA Telemetry (50k samples)", "Timestamp delta-of-delta + IEEE 754 float64 sensor records", telemetry.RawBytes, (telemetry.Timestamps, telemetry.Values)));

            // 2. Structured JSON / Log Dataset
            var json = GenerateStructuredJsonDataset(4000); // ~800 KB
            reports.Add(BenchmarkDataset("Structured Logs (JSON)", "Repetitive JSON event schema with dynamic payloads", json));

            // 3. Mixed Binary Dataset
            var binary = GenerateMixedBinaryDataset(800 * 1024); // 800 KB
            reports.Add(BenchmarkDataset("Mixed Binary Payloads", "Heterogeneous blocks of zero-padding, sequential, text, and entropy", binary));

            return reports;
        }
    }
}
