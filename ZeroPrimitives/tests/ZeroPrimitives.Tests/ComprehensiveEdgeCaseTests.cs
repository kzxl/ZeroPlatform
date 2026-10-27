using System;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Concurrency;
using ZeroPrimitives.Diagnostics;
using ZeroPrimitives.Mapping;
using ZeroPrimitives.Simd;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class ComprehensiveEdgeCaseTests
    {
        #region FastHex Tests

        [Fact]
        public void FastHex_EncodeAndDecode_RoundTripsAccurately()
        {
            byte[] original = new byte[] { 0x00, 0x12, 0xAB, 0xCD, 0xEF, 0xFF, 0x42 };
            Span<char> hexChars = stackalloc char[original.Length * 2];

            int encodedCount = FastHex.Encode(original, hexChars);
            Assert.Equal(original.Length * 2, encodedCount);
            Assert.Equal("0012ABCDEFFF42", hexChars.ToString());

            Span<byte> decoded = stackalloc byte[original.Length];
            int decodedCount = FastHex.Decode(hexChars, decoded);
            Assert.Equal(original.Length, decodedCount);
            Assert.True(original.AsSpan().SequenceEqual(decoded));
        }

        [Fact]
        public void FastHex_Encode_LowercaseOption_Works()
        {
            byte[] data = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            string hex = FastHex.ToString(data, lowercase: true);
            Assert.Equal("deadbeef", hex);
        }

        [Fact]
        public void FastHex_TryDecode_InvalidScenarios_FailsSafely()
        {
            Span<byte> dest = stackalloc byte[16];

            // 1. Odd length
            Assert.False(FastHex.TryDecode("123".AsSpan(), dest, out _));

            // 2. Non-hex characters
            Assert.False(FastHex.TryDecode("12G4".AsSpan(), dest, out _));
            Assert.False(FastHex.TryDecode("12 4".AsSpan(), dest, out _));
            Assert.False(FastHex.TryDecode("12-4".AsSpan(), dest, out _));

            // 3. Destination too small
            Assert.False(FastHex.TryDecode("12345678".AsSpan(), dest.Slice(0, 2), out _));

            // 4. Empty hex returns true with 0 bytes written
            Assert.True(FastHex.TryDecode(ReadOnlySpan<char>.Empty, dest, out int written));
            Assert.Equal(0, written);
        }

        [Fact]
        public void FastHex_IsValid_ChecksFormatCorrectly()
        {
            Assert.True(FastHex.IsValid("A1B2C3D4".AsSpan()));
            Assert.True(FastHex.IsValid("a1b2c3d4".AsSpan()));
            Assert.True(FastHex.IsValid("".AsSpan()));

            Assert.False(FastHex.IsValid("A1B".AsSpan())); // Odd length
            Assert.False(FastHex.IsValid("A1G2".AsSpan())); // Invalid char 'G'
            Assert.False(FastHex.IsValid("A1 2".AsSpan())); // Space
        }

        [Fact]
        public void FastHex_DecodeAsciiBytes_WorksDirectly()
        {
            byte[] asciiHex = Encoding.ASCII.GetBytes("01020A0B");
            Span<byte> dest = stackalloc byte[4];

            Assert.True(FastHex.TryDecode(asciiHex.AsSpan(), dest, out int written));
            Assert.Equal(4, written);
            Assert.Equal(new byte[] { 1, 2, 10, 11 }, dest.ToArray());
        }

        #endregion

        #region SpanSplitter Tests

        [Fact]
        public void SpanSplitter_SingleChar_EnumeratesTokensAccurately()
        {
            string input = "apple;banana;cherry;durian";
            string[] expected = new[] { "apple", "banana", "cherry", "durian" };

            int index = 0;
            foreach (var token in input.AsSpan().SplitFast(';'))
            {
                Assert.Equal(expected[index++], token.ToString());
            }
            Assert.Equal(4, index);
        }

        [Fact]
        public void SpanSplitter_WithEmptyEntries_HandlesRemoveEmptyFlag()
        {
            string input = ";apple;;banana;";

            // Without removeEmpty: ["", "apple", "", "banana", ""] -> 5 items
            int count = 0;
            foreach (var _ in input.AsSpan().SplitFast(';', removeEmpty: false))
            {
                count++;
            }
            Assert.Equal(5, count);

            // With removeEmpty: ["apple", "banana"] -> 2 items
            int countFiltered = 0;
            foreach (var _ in input.AsSpan().SplitFast(';', removeEmpty: true))
            {
                countFiltered++;
            }
            Assert.Equal(2, countFiltered);
        }

        [Fact]
        public void SpanSplitter_StringDelimiter_SplitsCorrectly()
        {
            string input = "step1::step2::step3";
            string[] expected = new[] { "step1", "step2", "step3" };

            int index = 0;
            foreach (var token in input.AsSpan().SplitFast("::".AsSpan()))
            {
                Assert.Equal(expected[index++], token.ToString());
            }
            Assert.Equal(3, index);
        }

        [Fact]
        public void SpanSplitter_ByteDelimiter_SplitsNetworkPackets()
        {
            byte[] stream = new byte[] { 0xAA, 0x00, 0xBB, 0xCC, 0x00, 0xDD };
            int chunkCount = 0;

            foreach (var chunk in stream.AsSpan().SplitFast((byte)0x00))
            {
                chunkCount++;
            }
            Assert.Equal(3, chunkCount);
        }

        #endregion

        #region BitOps Tests

        [Fact]
        public void BitOps_PopCount_ReturnsCorrectBitCount()
        {
            Assert.Equal(0, BitOps.PopCount(0U));
            Assert.Equal(1, BitOps.PopCount(1U));
            Assert.Equal(32, BitOps.PopCount(0xFFFFFFFFU));
            Assert.Equal(16, BitOps.PopCount(0x55555555U));

            Assert.Equal(0, BitOps.PopCount(0UL));
            Assert.Equal(64, BitOps.PopCount(0xFFFFFFFFFFFFFFFFUL));
        }

        [Fact]
        public void BitOps_LeadingAndTrailingZeros_WorkAccurately()
        {
            Assert.Equal(32, BitOps.LeadingZeroCount(0U));
            Assert.Equal(31, BitOps.LeadingZeroCount(1U));
            Assert.Equal(0, BitOps.LeadingZeroCount(0x80000000U));

            Assert.Equal(32, BitOps.TrailingZeroCount(0U));
            Assert.Equal(0, BitOps.TrailingZeroCount(1U));
            Assert.Equal(3, BitOps.TrailingZeroCount(8U));
            Assert.Equal(31, BitOps.TrailingZeroCount(0x80000000U));

            Assert.Equal(64, BitOps.LeadingZeroCount(0UL));
            Assert.Equal(63, BitOps.LeadingZeroCount(1UL));
            Assert.Equal(0, BitOps.LeadingZeroCount(0x8000000000000000UL));

            Assert.Equal(64, BitOps.TrailingZeroCount(0UL));
            Assert.Equal(0, BitOps.TrailingZeroCount(1UL));
            Assert.Equal(40, BitOps.TrailingZeroCount(1UL << 40));
        }

        [Fact]
        public void BitOps_RotateAndPowerOfTwo_FunctionsProperly()
        {
            uint val = 0x80000001U;
            Assert.Equal(0x00000003U, BitOps.RotateLeft(val, 1));
            Assert.Equal(val, BitOps.RotateRight(BitOps.RotateLeft(val, 5), 5));

            Assert.False(BitOps.IsPowerOfTwo(0U));
            Assert.True(BitOps.IsPowerOfTwo(1U));
            Assert.True(BitOps.IsPowerOfTwo(1024U));
            Assert.False(BitOps.IsPowerOfTwo(1025U));

            Assert.Equal(0U, BitOps.RoundUpToPowerOfTwo(0U));
            Assert.Equal(1U, BitOps.RoundUpToPowerOfTwo(1U));
            Assert.Equal(1024U, BitOps.RoundUpToPowerOfTwo(1000U));
            Assert.Equal(1024U, BitOps.RoundUpToPowerOfTwo(1024U));
        }

        #endregion

        #region ArrayPoolBufferWriter Tests

        [Fact]
        public void ArrayPoolBufferWriter_ExpandsAndWrites_Correctly()
        {
            using (var writer = new ArrayPoolBufferWriter<byte>(initialCapacity: 16))
            {
                for (int i = 0; i < 500; i++)
                {
                    var span = writer.GetSpan(1);
                    span[0] = (byte)(i % 256);
                    writer.Advance(1);
                }

                Assert.Equal(500, writer.WrittenCount);
                Assert.True(writer.Capacity >= 500);

                var written = writer.WrittenSpan;
                for (int i = 0; i < 500; i++)
                {
                    Assert.Equal((byte)(i % 256), written[i]);
                }

                writer.Reset();
                Assert.Equal(0, writer.WrittenCount);
            }
        }

        [Fact]
        public void ArrayPoolBufferWriter_DisposedState_ThrowsObjectDisposedException()
        {
            var writer = new ArrayPoolBufferWriter<byte>(32);
            writer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => writer.Advance(1));
            Assert.Throws<ObjectDisposedException>(() => writer.GetSpan(1));
        }

        #endregion

        #region ByteRingBuffer Tests

        [Fact]
        public void ByteRingBuffer_WrapAround_PreservesDataIntegrity()
        {
            using (var ring = new ByteRingBuffer(100))
            {
                // Write 80 bytes
                byte[] block1 = new byte[80];
                for (int i = 0; i < 80; i++) block1[i] = (byte)i;
                Assert.Equal(80, ring.Write(block1));

                // Read 50 bytes
                byte[] read1 = new byte[50];
                Assert.Equal(50, ring.Read(read1));
                for (int i = 0; i < 50; i++) Assert.Equal(i, read1[i]);

                // Write 50 bytes (crosses boundary back to index 0)
                byte[] block2 = new byte[50];
                for (int i = 0; i < 50; i++) block2[i] = (byte)(100 + i);
                Assert.Equal(50, ring.Write(block2));

                // Available read is 30 (remaining from block1) + 50 (from block2) = 80
                Assert.Equal(80, ring.AvailableRead);

                // Read all remaining
                byte[] readAll = new byte[80];
                Assert.Equal(80, ring.Read(readAll));

                // Verify block1 tail
                for (int i = 0; i < 30; i++) Assert.Equal(50 + i, readAll[i]);
                // Verify block2
                for (int i = 0; i < 50; i++) Assert.Equal(100 + i, readAll[30 + i]);

                Assert.True(ring.IsEmpty);
            }
        }

        [Fact]
        public void ByteRingBuffer_PeekAndAdvance_DoesNotPrematurelyDropData()
        {
            using (var ring = new ByteRingBuffer(32))
            {
                byte[] data = new byte[] { 1, 2, 3, 4, 5 };
                ring.Write(data);

                Span<byte> peekBuffer = stackalloc byte[5];
                int peeked = ring.Peek(peekBuffer);
                Assert.Equal(5, peeked);
                Assert.Equal(5, ring.AvailableRead); // Not consumed yet

                ring.Advance(3);
                Assert.Equal(2, ring.AvailableRead); // 2 bytes left: { 4, 5 }

                Span<byte> remaining = stackalloc byte[2];
                ring.Read(remaining);
                Assert.Equal(4, remaining[0]);
                Assert.Equal(5, remaining[1]);
            }
        }

        #endregion

        #region ValueStopwatch Tests

        [Fact]
        public void ValueStopwatch_MeasuresElapsedTimes_WithoutHeapAllocation()
        {
            var sw = ValueStopwatch.StartNew();
            Assert.True(sw.IsActive);

            // Busy wait a tiny bit
            int dummy = 0;
            for (int i = 0; i < 10000; i++) dummy += i;

            var elapsed = sw.GetElapsedTime();
            Assert.True(elapsed.TotalMilliseconds >= 0);

            var defaultSw = default(ValueStopwatch);
            Assert.False(defaultSw.IsActive);
            Assert.Throws<InvalidOperationException>(() => defaultSw.GetElapsedTime());
        }

        #endregion

        #region High-Concurrency Stress Tests

        [Fact]
        public async Task SpscQueue_HighThroughputMultiThreaded_NoLostItems()
        {
            const int totalItems = 200_000;
            var queue = new SpscQueue<int>(1024);

            long sumReceived = 0;
            int countReceived = 0;

            var consumer = Task.Run(() =>
            {
                while (countReceived < totalItems)
                {
                    if (queue.TryDequeue(out int val))
                    {
                        sumReceived += val;
                        countReceived++;
                    }
                    else
                    {
                        // Spin or yield
                        System.Threading.Thread.SpinWait(10);
                    }
                }
            });

            var producer = Task.Run(() =>
            {
                for (int i = 1; i <= totalItems; i++)
                {
                    while (!queue.TryEnqueue(i))
                    {
                        System.Threading.Thread.SpinWait(10);
                    }
                }
            });

            await Task.WhenAll(producer, consumer);

            Assert.Equal(totalItems, countReceived);
            long expectedSum = (long)totalItems * (totalItems + 1) / 2;
            Assert.Equal(expectedSum, sumReceived);
        }

        [Fact]
        public async Task FastSpinLock_MultiThreadedIncrement_ReachesExactTotal()
        {
            const int threadCount = 8;
            const int incrementsPerThread = 10_000;
            int counter = 0;
            var spinLock = new FastSpinLock();

            Task[] tasks = new Task[threadCount];
            for (int t = 0; t < threadCount; t++)
            {
                tasks[t] = Task.Run(() =>
                {
                    for (int i = 0; i < incrementsPerThread; i++)
                    {
                        spinLock.Enter();
                        try
                        {
                            counter++;
                        }
                        finally
                        {
                            spinLock.Exit();
                        }
                    }
                });
            }

            await Task.WhenAll(tasks);
            Assert.Equal(threadCount * incrementsPerThread, counter);
        }

        [Fact]
        public void SpanWriter_EmptyString_ReturnsTrueAndZeroBytes()
        {
            Span<byte> buffer = stackalloc byte[32];
            var writer = new SpanWriter(buffer);
            bool success = writer.TryWriteStringUtf8(string.Empty.AsSpan(), out int bytesWritten);

            Assert.True(success);
            Assert.Equal(0, bytesWritten);
            Assert.Equal(0, writer.Position);
        }

        [Fact]
        public void SimdColorConverter_TooSmallBuffers_ThrowsArgumentException()
        {
            byte[] y = new byte[10];
            byte[] u = new byte[5];
            byte[] v = new byte[5];
            byte[] dst = new byte[20];

            Assert.Throws<ArgumentException>(() =>
                SimdColorConverter.Yuv420pToRgb(y, u, v, dst, 100, 100));

            Assert.Throws<ArgumentException>(() =>
                SimdColorConverter.Nv12ToRgb(y, u, dst, 100, 100));
        }

        [Fact]
        public void FastSpinLock_EnterScope_SynchronizesCorrectly()
        {
            var spinLock = new FastSpinLock();
            int val = 0;

            using (spinLock.EnterScope())
            {
                Assert.True(spinLock.IsHeld);
                val = 42;
            }

            Assert.False(spinLock.IsHeld);
            Assert.Equal(42, val);
        }

        [Fact]
        public void FastTableBinary_NonZdtPayload_ReturnsEmptyDataTableSafely()
        {
            byte[] invalidPayload = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var table = FastTableBinary.Deserialize(invalidPayload);

            Assert.NotNull(table);
            Assert.Equal(0, table.Rows.Count);
        }

        #endregion
    }
}

