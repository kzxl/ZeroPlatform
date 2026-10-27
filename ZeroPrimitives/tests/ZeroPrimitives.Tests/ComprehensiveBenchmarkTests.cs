using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ZeroPrimitives;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Concurrency;
using ZeroPrimitives.Cryptography;
using ZeroPrimitives.Extensions;
using ZeroPrimitives.Parsing;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class ComprehensiveBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public ComprehensiveBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static long GetAllocatedBytes()
        {
#if NET8_0_OR_GREATER
            return GC.GetAllocatedBytesForCurrentThread();
#else
            return GC.GetTotalMemory(false);
#endif
        }


        [Fact]
        public async Task Benchmark_AllComparativeScenarios_OutputsDetailedMetrics()
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("========================================================================================================================");
            sb.AppendLine("                   ZEROPRIMITIVES vs C# BCL / TRADITIONAL METHODS BENCHMARK REPORT");
            sb.AppendLine("========================================================================================================================");
            sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                "Scenario", "C# Standard (Time / Heap)", "ZeroPrimitives (Time / Heap)", "Speedup", "Alloc Saved"));
            sb.AppendLine("------------------------------------------------------------------------------------------------------------------------");

            // 1. Unboxing & Primitive Conversion (100,000 ops)
            {
                const int iterations = 100_000;
                object boxed = 123456;

                // Warm up
                Convert.ToInt32(boxed);
                FastConvert.AsInt(boxed);

                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                int sumBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    sumBcl += Convert.ToInt32(boxed);
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                int sumZp = 0;
                for (int i = 0; i < iterations; i++)
                {
                    sumZp += FastConvert.AsInt(boxed);
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                Assert.Equal(sumBcl, sumZp);
                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "1. Unboxing Convert (100k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 2. Formatted Currency/Number Parsing (100,000 ops)
            {
                const int iterations = 100_000;
                string price = " 1,500,000.50 ";

                // Warm up
                decimal.TryParse(price, NumberStyles.Currency, CultureInfo.InvariantCulture, out _);
                FastConvert.AsDecimal(price);

                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                decimal sumBcl = 0m;
                for (int i = 0; i < iterations; i++)
                {
                    if (decimal.TryParse(price, NumberStyles.Currency, CultureInfo.InvariantCulture, out decimal val))
                        sumBcl += val;
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                decimal sumZp = 0m;
                for (int i = 0; i < iterations; i++)
                {
                    sumZp += FastConvert.AsDecimal(price);
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                Assert.Equal(sumBcl, sumZp);
                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "2. Currency Parse (100k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 3. Delimited Text / CSV Parsing (50,000 lines)
            {
                const int iterations = 50_000;
                string csvLine = "1001,\"MDS-SP-ITEM\",150.5,2026-09-15,\"ACTIVE\"";

                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                int countBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    string[] parts = csvLine.Split(',');
                    countBcl += parts.Length;
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                int countZp = 0;
                for (int i = 0; i < iterations; i++)
                {
                    foreach (var cell in FastCsvParser.EnumerateCells(csvLine.AsSpan(), ','))
                    {
                        countZp++;
                    }
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "3. CSV Line Parse (50k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 4. Binary Packet Deserialization (50,000 packets)
            {
                const int iterations = 50_000;
                byte[] packet = new byte[32];
                var w = new SpanWriter(packet);
                w.WriteByte(0xFA);
                w.WriteInt32LittleEndian(1001);
                w.WriteInt64LittleEndian(999999999L);
                w.WriteSingleLittleEndian(45.5f);
                w.WriteInt16LittleEndian(20);

                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                long sumBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    using var ms = new MemoryStream(packet);
                    using var br = new BinaryReader(ms);
                    sumBcl += br.ReadByte();
                    sumBcl += br.ReadInt32();
                    sumBcl += br.ReadInt64();
                    sumBcl += (long)br.ReadSingle();
                    sumBcl += br.ReadInt16();
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                long sumZp = 0;
                for (int i = 0; i < iterations; i++)
                {
                    var reader = new SpanReader(packet);
                    sumZp += reader.ReadByte();
                    sumZp += reader.ReadInt32LittleEndian();
                    sumZp += reader.ReadInt64LittleEndian();
                    sumZp += (long)reader.ReadSingleLittleEndian();
                    sumZp += reader.ReadInt16LittleEndian();
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                Assert.Equal(sumBcl, sumZp);
                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "4. Binary Packet Read (50k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 5. Alpha Sequence Generation (50,000 iterations)
            {
                const int iterations = 50_000;
                string col = "AZ";

                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                int lenBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    string s = col + i.ToString();
                    lenBcl += s.Length;
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                int lenZp = 0;
                var span = col.AsSpan();
                Span<char> buf = stackalloc char[8];
                for (int i = 0; i < iterations; i++)
                {
                    if (AlphaSequence.TryIncrement(span, buf, out int written))
                    {
                        lenZp += written;
                    }
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "5. Alpha Sequence (50k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }


            // 6. Concurrent Producer-Consumer Queue (100,000 items)
            {
                const int totalItems = 100_000;

                // ConcurrentQueue
                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                var cq = new ConcurrentQueue<int>();
                var p1 = Task.Run(() => { for (int i = 0; i < totalItems; i++) cq.Enqueue(i); });
                var c1 = Task.Run(() =>
                {
                    int r = 0;
                    while (r < totalItems) { if (cq.TryDequeue(out _)) r++; }
                });
                await Task.WhenAll(p1, c1);
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                // ZeroPrimitives SpscQueue
                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                var spsc = new SpscQueue<int>(1024);
                var p2 = Task.Run(() =>
                {
                    for (int i = 0; i < totalItems; i++)
                    {
                        while (!spsc.TryEnqueue(i)) System.Threading.Thread.SpinWait(5);
                    }
                });
                var c2 = Task.Run(() =>
                {
                    int r = 0;
                    while (r < totalItems)
                    {
                        if (spsc.TryDequeue(out _)) r++;
                        else System.Threading.Thread.SpinWait(5);
                    }
                });
                await Task.WhenAll(p2, c2);
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "6. SPSC Queue (100k items)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 7. Checksum (Castagnoli CRC32C, 50,000 payload calculations)
            {
                const int iterations = 50_000;
                byte[] payload = new byte[256];
                for (int i = 0; i < payload.Length; i++) payload[i] = (byte)i;

                // Traditional Software Bitwise CRC
                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                uint sumBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    uint crc = 0xFFFFFFFFu;
                    for (int b = 0; b < payload.Length; b++)
                    {
                        crc ^= payload[b];
                        for (int k = 0; k < 8; k++)
                            crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0x82F63B78u : 0);
                    }
                    sumBcl += crc;
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                // ZeroPrimitives Hardware-Accelerated CRC32C
                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                uint sumZp = 0;
                for (int i = 0; i < iterations; i++)
                {
                    sumZp += FastCrc.Crc32C(payload);
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "7. CRC32C HW (50k x 256B)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 8. Micro JSON Tokenizer / Reader (50,000 reads)
            {
                const int iterations = 50_000;
                string json = @"{ ""id"": 12345, ""code"": ""MDS-PART"", ""price"": 99.50, ""active"": true }";
                byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

                // System.Text.Json JsonDocument
                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                int sumIdBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(jsonBytes);
                    sumIdBcl += doc.RootElement.GetProperty("id").GetInt32();
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                // ZeroPrimitives FastJsonReader
                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                int sumIdZp = 0;
                for (int i = 0; i < iterations; i++)
                {
                    var reader = new FastJsonReader(jsonBytes);
                    while (reader.Read())
                    {
                        if (reader.TokenType == FastJsonTokenType.PropertyName && reader.ValueSpan.SequenceEqual("id"u8))
                        {
                            if (reader.Read() && reader.TokenType == FastJsonTokenType.Number)
                            {
                                sumIdZp += reader.GetInt32();
                                break;
                            }
                        }
                    }
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                Assert.Equal(sumIdBcl, sumIdZp);
                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "8. JSON Stream Parse (50k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 9. FastHex vs BitConverter (50,000 RFID 12-byte EPC encodings)
            {
                const int iterations = 50_000;
                byte[] epcBytes = new byte[] { 0xE2, 0x80, 0x11, 0x70, 0x00, 0x00, 0x02, 0x0B, 0x12, 0x34, 0x56, 0x78 };

                // Warm up
                _ = BitConverter.ToString(epcBytes).Replace("-", "");
                Span<char> warmBuf = stackalloc char[24];
                FastHex.Encode(epcBytes, warmBuf);

                // BitConverter + Replace
                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                int hashBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    string s = BitConverter.ToString(epcBytes).Replace("-", "");
                    hashBcl += s.Length;
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                // ZeroPrimitives FastHex
                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                int hashZp = 0;
                Span<char> hexChars = stackalloc char[24];
                for (int i = 0; i < iterations; i++)
                {
                    hashZp += FastHex.Encode(epcBytes, hexChars);
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                Assert.Equal(hashBcl, hashZp);
                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "9. Hex Encode RFID EPC (50k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            // 10. SpanSplitter vs string.Split (50,000 multi-token ERP strings)
            {
                const int iterations = 50_000;
                string config = "ORDER_2026_001;CUSTOMER_ABC;WAREHOUSE_NORTH;SKU_ITEM_999;QTY_100;APPROVED";

                // Warm up
                _ = config.Split(';');
                foreach (var _ in config.AsSpan().SplitFast(';')) { }

                // string.Split
                long memBclBefore = GetAllocatedBytes();
                var swBcl = Stopwatch.StartNew();
                int totalTokensBcl = 0;
                for (int i = 0; i < iterations; i++)
                {
                    string[] parts = config.Split(';');
                    totalTokensBcl += parts.Length;
                }
                swBcl.Stop();
                long memBcl = GetAllocatedBytes() - memBclBefore;

                // ZeroPrimitives SpanSplitter
                long memZpBefore = GetAllocatedBytes();
                var swZp = Stopwatch.StartNew();
                int totalTokensZp = 0;
                for (int i = 0; i < iterations; i++)
                {
                    foreach (var _ in config.AsSpan().SplitFast(';'))
                    {
                        totalTokensZp++;
                    }
                }
                swZp.Stop();
                long memZp = GetAllocatedBytes() - memZpBefore;

                Assert.Equal(totalTokensBcl, totalTokensZp);
                double speedup = (double)Math.Max(swBcl.ElapsedTicks, 1) / Math.Max(swZp.ElapsedTicks, 1);
                sb.AppendLine(string.Format("{0,-32} | {1,-22} | {2,-22} | {3,-12} | {4,-10}",
                    "10. SpanSplitter Tokenize (50k)",
                    $"{swBcl.ElapsedMilliseconds} ms ({FormatBytes(memBcl)})",
                    $"{swZp.ElapsedMilliseconds} ms ({FormatBytes(memZp)})",
                    $"{speedup:F1}x Faster",
                    $"{FormatBytes(Math.Max(0, memBcl - memZp))}"));
            }

            sb.AppendLine("========================================================================================================================");
            _output.WriteLine(sb.ToString());
            Console.WriteLine(sb.ToString());
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes == 0) return "0 B";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{(bytes / 1024.0):F1} KB";
            return $"{(bytes / (1024.0 * 1024.0)):F1} MB";
        }
    }
}
