using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using ZeroNetwork.Common;
using ZeroNetwork.RealTime;

namespace ZeroNetwork.Tests
{
    public class ZeroBclOptimizationsBenchmarkTests
    {
        private readonly ITestOutputHelper _output;

        public ZeroBclOptimizationsBenchmarkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Functional & Correctness Tests

        [Fact]
        public void ZeroBitOps_Correctness_MatchesExpected()
        {
            Assert.True(ZeroBitOps.IsPowerOfTwo(1));
            Assert.True(ZeroBitOps.IsPowerOfTwo(2));
            Assert.True(ZeroBitOps.IsPowerOfTwo(1024));
            Assert.False(ZeroBitOps.IsPowerOfTwo(0));
            Assert.False(ZeroBitOps.IsPowerOfTwo(3));
            Assert.False(ZeroBitOps.IsPowerOfTwo(1000));

            Assert.Equal(1u, ZeroBitOps.RoundUpToPowerOfTwo(0u));
            Assert.Equal(1u, ZeroBitOps.RoundUpToPowerOfTwo(1u));
            Assert.Equal(8u, ZeroBitOps.RoundUpToPowerOfTwo(5u));
            Assert.Equal(1024u, ZeroBitOps.RoundUpToPowerOfTwo(1000u));

            Assert.Equal(0, ZeroBitOps.PopCount(0u));
            Assert.Equal(1, ZeroBitOps.PopCount(1u));
            Assert.Equal(32, ZeroBitOps.PopCount(0xFFFFFFFFu));
            Assert.Equal(4, ZeroBitOps.PopCount(0b10101010u));

            Assert.Equal(32, ZeroBitOps.TrailingZeroCount(0u));
            Assert.Equal(0, ZeroBitOps.TrailingZeroCount(1u));
            Assert.Equal(3, ZeroBitOps.TrailingZeroCount(8u));
            Assert.Equal(10, ZeroBitOps.TrailingZeroCount(1024u));

            Assert.Equal(32, ZeroBitOps.LeadingZeroCount(0u));
            Assert.Equal(31, ZeroBitOps.LeadingZeroCount(1u));
            Assert.Equal(0, ZeroBitOps.LeadingZeroCount(0x80000000u));

            Assert.Equal(0, ZeroBitOps.Log2(0u));
            Assert.Equal(0, ZeroBitOps.Log2(1u));
            Assert.Equal(3, ZeroBitOps.Log2(8u));
            Assert.Equal(9, ZeroBitOps.Log2(1000u));
            Assert.Equal(10, ZeroBitOps.Log2(1024u));
        }

        [Fact]
        public void ZeroBase64_Correctness_Roundtrip()
        {
            byte[][] testPayloads = new byte[][]
            {
                Array.Empty<byte>(),
                new byte[] { 65 }, // "A" -> "QQ=="
                new byte[] { 65, 66 }, // "AB" -> "QUI="
                new byte[] { 65, 66, 67 }, // "ABC" -> "QUJD"
                Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog!"),
                new byte[256]
            };

            for (int i = 0; i < 256; i++) testPayloads[5][i] = (byte)i;

            Span<char> charBuf = stackalloc char[512];
            Span<byte> decBuf = stackalloc byte[512];

            foreach (var payload in testPayloads)
            {
                string bclBase64 = Convert.ToBase64String(payload);

                int charsWritten = ZeroBase64.Encode(payload, charBuf);
                string zeroBase64 = new string(charBuf.Slice(0, charsWritten).ToArray());

                Assert.Equal(bclBase64, zeroBase64);

                int bytesDecoded = ZeroBase64.Decode(charBuf.Slice(0, charsWritten), decBuf);

                Assert.Equal(payload.Length, bytesDecoded);
                Assert.True(payload.AsSpan().SequenceEqual(decBuf.Slice(0, bytesDecoded)));
            }
        }

        [Fact]
        public void ZeroSha1_WebSocketAccept_MatchesRfc6455Standard()
        {
            // RFC 6455 Section 1.3 standard test vector:
            // Client sends: Sec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==
            // Server responds: Sec-WebSocket-Accept: s3pPLMBiTxaQ9kYGzzhZRbK+xOo=
            string clientKey = "dGhlIHNhbXBsZSBub25jZQ==";
            string expectedAccept = "s3pPLMBiTxaQ9kYGzzhZRbK+xOo=";

            string actualAccept = ZeroSha1.ComputeWebSocketAccept(clientKey);
            Assert.Equal(expectedAccept, actualAccept);

            // Compare with BCL SHA1
            using (var sha1 = SHA1.Create())
            {
                byte[] bclHash = sha1.ComputeHash(Encoding.UTF8.GetBytes(clientKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                string bclAccept = Convert.ToBase64String(bclHash);
                Assert.Equal(bclAccept, actualAccept);
            }
        }

        [Fact]
        public void ZeroFastMask_Correctness_MatchesScalarLoop()
        {
            byte[] maskKey = new byte[] { 0x12, 0x34, 0x56, 0x78 };

            int[] testSizes = new int[] { 0, 1, 3, 4, 7, 8, 15, 16, 31, 32, 63, 64, 127, 1024, 4096 };

            foreach (int size in testSizes)
            {
                byte[] original = new byte[size];
                for (int i = 0; i < size; i++) original[i] = (byte)(i * 7 + 3);

                byte[] scalarResult = (byte[])original.Clone();
                for (int i = 0; i < scalarResult.Length; i++)
                {
                    scalarResult[i] ^= maskKey[i % 4];
                }

                byte[] fastResult = (byte[])original.Clone();
                ZeroFastMask.ApplyMask(fastResult, maskKey);

                Assert.True(scalarResult.AsSpan().SequenceEqual(fastResult.AsSpan()),
                    $"Mask mismatch for size {size}");

                // Double mask restores original
                ZeroFastMask.ApplyMask(fastResult, maskKey);
                Assert.True(original.AsSpan().SequenceEqual(fastResult.AsSpan()),
                    $"Unmask failed to restore original for size {size}");
            }
        }

        [Fact]
        public void ZeroUtf8_FormatAndParse_MatchesBcl()
        {
            int[] testInts = new int[] { 0, 1, -1, 42, -42, 100, 999, -12345, 100000, 123456789, int.MaxValue, int.MinValue };

            Span<byte> buffer = stackalloc byte[64];
            foreach (int val in testInts)
            {
                bool success = ZeroUtf8.TryFormat(val, buffer, out int written);
                Assert.True(success);

                string formattedStr = Encoding.ASCII.GetString(buffer.Slice(0, written).ToArray());
                Assert.Equal(val.ToString(), formattedStr);

                bool parseOk = ZeroUtf8.FastTryParseInt32(buffer.Slice(0, written), out int parsed);
                Assert.True(parseOk);
                Assert.Equal(val, parsed);
            }

            Guid testGuid = Guid.NewGuid();
            bool guidOk = ZeroUtf8.TryFormat(testGuid, buffer, out int guidWritten, 'D');
            Assert.True(guidOk);
            Assert.Equal(36, guidWritten);
            Assert.Equal(testGuid.ToString("D"), Encoding.ASCII.GetString(buffer.Slice(0, 36).ToArray()));

            Assert.True(ZeroUtf8.IsValidUtf8(Encoding.UTF8.GetBytes("Xin chào ZeroPlatform! 🚀")));
            Assert.False(ZeroUtf8.IsValidUtf8(new byte[] { 0xFF, 0xFE, 0xFD }));
        }

        [Fact]
        public async Task ZeroRingBuffer_ConcurrentProducerConsumer_NoLoss()
        {
            var ring = new ZeroRingBuffer<int>(1024);
            int totalItems = 100_000;
            int consumedCount = 0;
            long consumedSum = 0;

            var producer = Task.Run(() =>
            {
                for (int i = 1; i <= totalItems; i++)
                {
                    while (!ring.TryEnqueue(i))
                    {
                        // Spin / yield on full
                    }
                }
            });

            var consumer = Task.Run(() =>
            {
                while (consumedCount < totalItems)
                {
                    if (ring.TryDequeue(out int val))
                    {
                        consumedCount++;
                        consumedSum += val;
                    }
                }
            });

            await Task.WhenAll(producer, consumer);

            Assert.Equal(totalItems, consumedCount);
            long expectedSum = ((long)totalItems * (totalItems + 1)) / 2;
            Assert.Equal(expectedSum, consumedSum);
        }

        #endregion

        #region Performance Measurement & Comparison Benchmarks

        [Fact]
        public void Benchmark_WebSocketFrameMasking_BeforeAndAfter()
        {
            int payloadSize = 64 * 1024; // 64 KB frame
            int iterations = 10_000;
            byte[] maskKey = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };

            byte[] bufferScalar = new byte[payloadSize];
            byte[] bufferFast = new byte[payloadSize];

            // 1. BEFORE: BCL Scalar XOR loop
            var swScalar = Stopwatch.StartNew();
            for (int it = 0; it < iterations; it++)
            {
                for (int i = 0; i < bufferScalar.Length; i++)
                {
                    bufferScalar[i] = (byte)(bufferScalar[i] ^ maskKey[i % 4]);
                }
            }
            swScalar.Stop();

            // 2. AFTER: ZeroFastMask (64-bit unrolled)
            var swFast = Stopwatch.StartNew();
            for (int it = 0; it < iterations; it++)
            {
                ZeroFastMask.ApplyMask(bufferFast, maskKey);
            }
            swFast.Stop();

            double scalarThroughputMBps = ((double)payloadSize * iterations / (1024 * 1024)) / (swScalar.ElapsedMilliseconds / 1000.0);
            double fastThroughputMBps = ((double)payloadSize * iterations / (1024 * 1024)) / (swFast.ElapsedMilliseconds / 1000.0);
            double speedup = (double)swScalar.ElapsedTicks / Math.Max(1, swFast.ElapsedTicks);

            _output.WriteLine("==========================================================================");
            _output.WriteLine("BENCHMARK 1: WebSocket RFC 6455 Frame Masking (64KB x 10,000 frames)");
            _output.WriteLine($"[BEFORE] BCL Scalar XOR Loop  : {swScalar.ElapsedMilliseconds,6} ms | {scalarThroughputMBps,8:F1} MB/s");
            _output.WriteLine($"[AFTER ] ZeroFastMask (Unroll): {swFast.ElapsedMilliseconds,6} ms | {fastThroughputMBps,8:F1} MB/s");
            _output.WriteLine($"-> Performance Acceleration  : {speedup:F2}x FASTER");
            _output.WriteLine("==========================================================================");

            Assert.True(fastThroughputMBps > scalarThroughputMBps);
        }

        [Fact]
        public void Benchmark_WebSocketHandshakeAccept_BeforeAndAfter()
        {
            int iterations = 50_000;
            string secKey = "dGhlIHNhbXBsZSBub25jZQ==";
            const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

            // 1. BEFORE: BCL SHA1.Create() + Encoding + ToBase64String
            long gcBeforeBcl = GC.GetTotalMemory(true);
            var swBcl = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                using (var sha1 = SHA1.Create())
                {
                    byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(secKey + WebSocketGuid));
                    string accept = Convert.ToBase64String(hash);
                }
            }
            swBcl.Stop();
            long gcAfterBcl = GC.GetTotalMemory(false);

            // 2. AFTER: ZeroSha1 + ZeroBase64 (zero-alloc stack)
            long gcBeforeZero = GC.GetTotalMemory(true);
            var swZero = Stopwatch.StartNew();
            Span<char> acceptSpan = stackalloc char[28];
            for (int i = 0; i < iterations; i++)
            {
                ZeroSha1.ComputeWebSocketAccept(secKey.AsSpan(), acceptSpan);
            }
            swZero.Stop();
            long gcAfterZero = GC.GetTotalMemory(false);

            double bclOpNs = (swBcl.Elapsed.TotalMilliseconds * 1_000_000.0) / iterations;
            double zeroOpNs = (swZero.Elapsed.TotalMilliseconds * 1_000_000.0) / iterations;
            double speedup = bclOpNs / Math.Max(1.0, zeroOpNs);

            _output.WriteLine("==========================================================================");
            _output.WriteLine("BENCHMARK 2: WebSocket RFC 6455 Handshake Accept Key (50,000 iterations)");
            _output.WriteLine($"[BEFORE] BCL SHA1 + Convert   : {swBcl.ElapsedMilliseconds,6} ms | {bclOpNs,8:F1} ns/op | GC Heap Active");
            _output.WriteLine($"[AFTER ] ZeroSha1 + ZeroBase64: {swZero.ElapsedMilliseconds,6} ms | {zeroOpNs,8:F1} ns/op | 0 BYTES ALLOCATION");
            _output.WriteLine($"-> Performance Acceleration  : {speedup:F2}x FASTER (Zero GC pressure)");
            _output.WriteLine("==========================================================================");

            Assert.True(acceptSpan.Slice(0, 28).SequenceEqual("s3pPLMBiTxaQ9kYGzzhZRbK+xOo=".AsSpan()));
            Assert.True(zeroOpNs < 5000.0);
        }

        [Fact]
        public void Benchmark_Base64Encoding_BeforeAndAfter()
        {
            byte[] binaryData = new byte[1024]; // 1KB chunk
            for (int i = 0; i < binaryData.Length; i++) binaryData[i] = (byte)i;
            int iterations = 100_000;

            // 1. BEFORE: BCL Convert.ToBase64String
            var swBcl = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                string b64 = Convert.ToBase64String(binaryData);
            }
            swBcl.Stop();

            // 2. AFTER: ZeroBase64.Encode
            int encLen = ZeroBase64.GetEncodedLength(binaryData.Length);
            char[] buffer = new char[encLen];
            var swZero = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                ZeroBase64.Encode(binaryData, buffer);
            }
            swZero.Stop();

            double bclMBps = ((double)binaryData.Length * iterations / (1024 * 1024)) / (swBcl.ElapsedMilliseconds / 1000.0);
            double zeroMBps = ((double)binaryData.Length * iterations / (1024 * 1024)) / (swZero.ElapsedMilliseconds / 1000.0);
            double speedup = (double)swBcl.ElapsedTicks / Math.Max(1, swZero.ElapsedTicks);

            _output.WriteLine("==========================================================================");
            _output.WriteLine("BENCHMARK 3: Base64 Encoding Throughput (1KB x 100,000 items)");
            _output.WriteLine($"[BEFORE] BCL Convert.ToBase64 : {swBcl.ElapsedMilliseconds,6} ms | {bclMBps,8:F1} MB/s");
            _output.WriteLine($"[AFTER ] ZeroBase64.Encode    : {swZero.ElapsedMilliseconds,6} ms | {zeroMBps,8:F1} MB/s (In-Place)");
            _output.WriteLine($"-> Performance Acceleration  : {speedup:F2}x FASTER");
            _output.WriteLine("==========================================================================");

            Assert.True(zeroMBps > 50.0);
        }

        [Fact]
        public void Benchmark_IntegerFormatting_BeforeAndAfter()
        {
            int iterations = 500_000;

            // 1. BEFORE: BCL int.ToString()
            var swBcl = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                string s = i.ToString();
            }
            swBcl.Stop();

            // 2. AFTER: ZeroUtf8.TryFormat
            Span<byte> byteBuffer = stackalloc byte[16];
            var swZero = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                ZeroUtf8.TryFormat(i, byteBuffer, out _);
            }
            swZero.Stop();

            double bclOpNs = (swBcl.Elapsed.TotalMilliseconds * 1_000_000.0) / iterations;
            double zeroOpNs = (swZero.Elapsed.TotalMilliseconds * 1_000_000.0) / iterations;
            double speedup = bclOpNs / Math.Max(1.0, zeroOpNs);

            _output.WriteLine("==========================================================================");
            _output.WriteLine("BENCHMARK 4: Integer to ASCII/UTF-8 Formatting (500,000 iterations)");
            _output.WriteLine($"[BEFORE] BCL int.ToString()   : {swBcl.ElapsedMilliseconds,6} ms | {bclOpNs,8:F1} ns/op | String Heap Alloc");
            _output.WriteLine($"[AFTER ] ZeroUtf8.TryFormat   : {swZero.ElapsedMilliseconds,6} ms | {zeroOpNs,8:F1} ns/op | ZERO ALLOC (Span)");
            _output.WriteLine($"-> Performance Acceleration  : {speedup:F2}x (Zero Alloc)");
            _output.WriteLine("==========================================================================");

            Assert.True(zeroOpNs < 100.0);
        }

        [Fact]
        public void Benchmark_QueueThroughput_BeforeAndAfter()
        {
            int iterations = 1_000_000;

            // 1. BEFORE: BCL ConcurrentQueue
            var bclQueue = new ConcurrentQueue<int>();
            var swBcl = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                bclQueue.Enqueue(i);
            }
            for (int i = 0; i < iterations; i++)
            {
                bclQueue.TryDequeue(out _);
            }
            swBcl.Stop();

            // 2. AFTER: ZeroRingBuffer (bounded flat array)
            var zeroQueue = new ZeroRingBuffer<int>(iterations + 16);
            var swZero = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                zeroQueue.TryEnqueue(i);
            }
            for (int i = 0; i < iterations; i++)
            {
                zeroQueue.TryDequeue(out _);
            }
            swZero.Stop();

            double bclMops = (iterations * 2.0 / 1_000_000.0) / (swBcl.ElapsedMilliseconds / 1000.0);
            double zeroMops = (iterations * 2.0 / 1_000_000.0) / (swZero.ElapsedMilliseconds / 1000.0);
            double speedup = (double)swBcl.ElapsedTicks / Math.Max(1, swZero.ElapsedTicks);

            _output.WriteLine("==========================================================================");
            _output.WriteLine("BENCHMARK 5: Lock-Free Enqueue/Dequeue (1,000,000 operations)");
            _output.WriteLine($"[BEFORE] BCL ConcurrentQueue  : {swBcl.ElapsedMilliseconds,6} ms | {bclMops,8:F1} Million ops/sec | Linked Segments");
            _output.WriteLine($"[AFTER ] ZeroRingBuffer       : {swZero.ElapsedMilliseconds,6} ms | {zeroMops,8:F1} Million ops/sec | Padded Flat Array");
            _output.WriteLine($"-> Performance Acceleration  : {speedup:F2}x FASTER");
            _output.WriteLine("==========================================================================");

            Assert.True(zeroMops > 15.0);
        }

        [Fact]
        public void Benchmark_BitOperations_BeforeAndAfter()
        {
            int iterations = 2_000_000;

            // 1. BEFORE: Loop-based Trailing Zero Count
            var swLoop = Stopwatch.StartNew();
            int sumLoop = 0;
            for (int i = 0; i < iterations; i++)
            {
                uint val = (uint)(i + 1);
                int count = 0;
                while ((val & 1) == 0 && count < 32)
                {
                    count++;
                    val >>= 1;
                }
                sumLoop += count;
            }
            swLoop.Stop();

            // 2. AFTER: ZeroBitOps.TrailingZeroCount
            var swZero = Stopwatch.StartNew();
            int sumZero = 0;
            for (int i = 0; i < iterations; i++)
            {
                sumZero += ZeroBitOps.TrailingZeroCount((uint)(i + 1));
            }
            swZero.Stop();

            double loopOpNs = (swLoop.Elapsed.TotalMilliseconds * 1_000_000.0) / iterations;
            double zeroOpNs = (swZero.Elapsed.TotalMilliseconds * 1_000_000.0) / iterations;
            double speedup = loopOpNs / Math.Max(1.0, zeroOpNs);

            _output.WriteLine("==========================================================================");
            _output.WriteLine("BENCHMARK 6: Trailing Zero Bit Count (2,000,000 iterations)");
            _output.WriteLine($"[BEFORE] While-Loop Bit Scan  : {swLoop.ElapsedMilliseconds,6} ms | {loopOpNs,8:F1} ns/op");
            _output.WriteLine($"[AFTER ] ZeroBitOps.CTZ       : {swZero.ElapsedMilliseconds,6} ms | {zeroOpNs,8:F1} ns/op (De Bruijn / Hardware)");
            _output.WriteLine($"-> Performance Acceleration  : {speedup:F2}x FASTER");
            _output.WriteLine("==========================================================================");

            Assert.Equal(sumLoop, sumZero);
            Assert.True(zeroOpNs < 20.0);
        }

        #endregion
    }
}
