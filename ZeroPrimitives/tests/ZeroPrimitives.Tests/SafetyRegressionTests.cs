using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Xunit;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Memory;
using ZeroPrimitives.Parsing;
using ZeroPrimitives.Simd;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    [CollectionDefinition("NativeMemoryTrackerSerial", DisableParallelization = true)]
    public class NativeMemoryTrackerSerialCollection
    {
    }

    [Collection("NativeMemoryTrackerSerial")]
    public class SafetyRegressionTests
    {
        private sealed class RecordingArrayPool<T> : ArrayPool<T>
        {
            public bool? LastClearArray { get; private set; }
            public int ReturnCount { get; private set; }

            public override T[] Rent(int minimumLength) => new T[minimumLength];

            public override void Return(T[] array, bool clearArray = false)
            {
                LastClearArray = clearArray;
                ReturnCount++;
            }
        }

        [Fact]
        public void D01_SimdVectorAndOps_UnalignedSlicedSpans_MatchScalarReference()
        {
            const int len = 1024;
            float[] aRaw = new float[len + 3];
            float[] bRaw = new float[len + 3];
            float[] cRaw = new float[len + 3];
            float[] dstRaw = new float[len + 3];

            for (int i = 0; i < aRaw.Length; i++)
            {
                aRaw[i] = (i % 17) - 5.25f;
                bRaw[i] = (i % 13) + 1.5f;
                cRaw[i] = (i % 7) * 0.5f;
            }

            ReadOnlySpan<float> a = aRaw.AsSpan(1, len);
            ReadOnlySpan<float> b = bRaw.AsSpan(1, len);
            ReadOnlySpan<float> c = cRaw.AsSpan(1, len);
            Span<float> dst = dstRaw.AsSpan(1, len);

            SimdVector.Add(dst, a, b);
            for (int i = 0; i < len; i++)
                Assert.Equal(a[i] + b[i], dst[i], 4);

            SimdVector.Subtract(dst, a, b);
            for (int i = 0; i < len; i++)
                Assert.Equal(a[i] - b[i], dst[i], 4);

            SimdVector.Multiply(dst, a, b);
            for (int i = 0; i < len; i++)
                Assert.Equal(a[i] * b[i], dst[i], 4);

            SimdVector.MultiplyAdd(dst, a, b, c);
            for (int i = 0; i < len; i++)
                Assert.Equal((a[i] * b[i]) + c[i], dst[i], 4);

            SimdVector.Scale(dst, a, 2.75f);
            for (int i = 0; i < len; i++)
                Assert.Equal(a[i] * 2.75f, dst[i], 4);

            SimdVector.Clamp(dst, a, -2.0f, 3.0f);
            for (int i = 0; i < len; i++)
            {
                float expected = a[i] < -2.0f ? -2.0f : (a[i] > 3.0f ? 3.0f : a[i]);
                Assert.Equal(expected, dst[i], 4);
            }

            byte[] bytesRaw = new byte[len + 3];
            for (int i = 0; i < bytesRaw.Length; i++)
                bytesRaw[i] = (byte)((i * 37 + 11) % 251);
            bytesRaw[500] = 0;
            bytesRaw[700] = 255;

            SimdOps.MinMax(bytesRaw.AsSpan(1, len), out byte min, out byte max);
            Assert.Equal((byte)0, min);
            Assert.Equal((byte)255, max);

            float expectedDot = 0f;
            for (int i = 0; i < len; i++)
                expectedDot += a[i] * b[i];
            float actualDot = SimdOps.DotProduct(a, b);
            Assert.True(Math.Abs(expectedDot - actualDot) < Math.Abs(expectedDot) * 1e-4f + 1e-2f);
        }

        [Fact]
        public void D02_FastNumberParser_TryParseDouble_SupportsScientificLargeAndNonDecimalDefaults()
        {
            Assert.True(FastNumberParser.TryParseDouble("1e50".AsSpan(), out double d1));
            Assert.Equal(1e50, d1, 1e36);

            Assert.True(FastNumberParser.TryParseDouble("1.5e-35".AsSpan(), out double d2));
            Assert.True(Math.Abs(d2 - 1.5e-35) < 1e-48);

            Assert.True(FastNumberParser.TryParseDouble("-2.5E+10".AsSpan(), out double d3));
            Assert.Equal(-2.5e10, d3, 1e-2);

            Assert.True(FastNumberParser.TryParseDouble("1e300".AsSpan(), out double d4));
            Assert.Equal(1e300, d4);

            Assert.False(FastNumberParser.TryParseDouble("abc".AsSpan(), out double rNan, double.NaN));
            Assert.True(double.IsNaN(rNan));

            Assert.False(FastNumberParser.TryParseDouble("abc".AsSpan(), out double rInf, double.PositiveInfinity));
            Assert.True(double.IsPositiveInfinity(rInf));

            Assert.False(FastNumberParser.TryParseDouble("abc".AsSpan(), out double rHuge, 1e40));
            Assert.Equal(1e40, rHuge);

            // Preserve existing decimal-separator behavior
            Assert.True(FastNumberParser.TryParseDouble("1,234.56".AsSpan(), out double s1));
            Assert.Equal(1234.56, s1, 1e-6);

            Assert.True(FastNumberParser.TryParseDouble("1.234,56".AsSpan(), out double s2));
            Assert.Equal(1234.56, s2, 1e-6);

            Assert.True(FastNumberParser.TryParseDouble("1.500.000".AsSpan(), out double s3));
            Assert.Equal(1500000.0, s3, 1e-6);

            // FastJsonReader scientific double path
            byte[] json = Encoding.UTF8.GetBytes("{\"v\":1.5e-35}");
            var reader = new FastJsonReader(json);
            double parsedFromJson = 0;
            while (reader.Read())
            {
                if (reader.TokenType == FastJsonTokenType.Number)
                {
                    parsedFromJson = reader.GetDouble();
                }
            }
            Assert.True(Math.Abs(parsedFromJson - 1.5e-35) < 1e-48);
        }

        [Fact]
        public void D03_FastJsonWriter_DeepNestingAndSurrogatePairs_ProducesValidRfc8259Json()
        {
            using (var writer = new FastJsonWriter(256))
            {
                for (int i = 0; i < 40; i++)
                {
                    writer.WriteStartArray();
                    writer.WriteRaw("1"u8);
                    writer.WriteRaw("2"u8);
                }
                for (int i = 0; i < 40; i++)
                {
                    writer.WriteEndArray();
                }

                using var doc = JsonDocument.Parse(writer.WrittenSpan.ToArray());
                Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
            }

            // Nesting beyond 63 levels must throw InvalidOperationException
            bool threwDepthOverflow = false;
            try
            {
                using var deepWriter = new FastJsonWriter(128);
                for (int i = 0; i < 64; i++)
                {
                    deepWriter.WriteStartArray();
                }
            }
            catch (InvalidOperationException)
            {
                threwDepthOverflow = true;
            }
            Assert.True(threwDepthOverflow);

            // Supplementary surrogate pair "A😀B" -> valid 4-byte UTF-8 F0 9F 98 80
            using (var emojiWriter = new FastJsonWriter(64))
            {
                emojiWriter.WriteStartObject();
                emojiWriter.WriteString("msg", "A\uD83D\uDE00B");
                emojiWriter.WriteEndObject();

                byte[] bytes = emojiWriter.WrittenSpan.ToArray();
                bool hasUtf8Emoji = false;
                for (int i = 0; i <= bytes.Length - 4; i++)
                {
                    if (bytes[i] == 0xF0 && bytes[i + 1] == 0x9F && bytes[i + 2] == 0x98 && bytes[i + 3] == 0x80)
                    {
                        hasUtf8Emoji = true;
                        break;
                    }
                }
                Assert.True(hasUtf8Emoji);

                using var doc = JsonDocument.Parse(bytes);
                Assert.Equal("A\uD83D\uDE00B", doc.RootElement.GetProperty("msg").GetString());
            }

            // Lone surrogate -> escaped \ud83d
            using (var loneWriter = new FastJsonWriter(64))
            {
                loneWriter.WriteStartObject();
                loneWriter.WriteString("lone", "\uD83D");
                loneWriter.WriteEndObject();

                string raw = loneWriter.ToString();
                Assert.Contains("\\ud83d", raw, StringComparison.OrdinalIgnoreCase);
                using var doc = JsonDocument.Parse(loneWriter.WrittenSpan.ToArray());
                Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
            }
        }

        [Fact]
        public void D04_ArrayPoolClearing_ClearsReferencesOnlyWhenTypeContainsReferences()
        {
            var objPool = new RecordingArrayPool<object>();
            using (var objWriter = new ArrayPoolBufferWriter<object>(8, objPool))
            {
                objWriter.GetSpan(16); // forces resize & return of first buffer
            }
            Assert.True(objPool.ReturnCount >= 2);
            Assert.True(objPool.LastClearArray);

            var bytePool = new RecordingArrayPool<byte>();
            using (var byteWriter = new ArrayPoolBufferWriter<byte>(8, bytePool))
            {
                byteWriter.GetSpan(16);
            }
            Assert.True(bytePool.ReturnCount >= 2);
            Assert.False(bytePool.LastClearArray);

            // Also verify TypeTraits<T> directly via InternalsVisibleTo
            Assert.True(ZeroPrimitives.Internal.TypeTraits<string>.ContainsReferences);
            Assert.True(ZeroPrimitives.Internal.TypeTraits<object>.ContainsReferences);
            Assert.False(ZeroPrimitives.Internal.TypeTraits<byte>.ContainsReferences);
            Assert.False(ZeroPrimitives.Internal.TypeTraits<int>.ContainsReferences);
            Assert.False(ZeroPrimitives.Internal.TypeTraits<Guid>.ContainsReferences);
        }

        [Fact]
        public void D05_NativeMemoryPool_UnpooledOver64MB_AndTracker_BalanceProperly()
        {
            long beforeTrack = NativeMemoryTracker.AllocatedBytes;
            long beforeActive = NativeMemoryTracker.ActiveBlocksCount;
            NativeMemoryTracker.TrackAlloc(4096);
            Assert.Equal(beforeTrack + 4096, NativeMemoryTracker.AllocatedBytes);
            Assert.Equal(beforeActive + 1, NativeMemoryTracker.ActiveBlocksCount);
            NativeMemoryTracker.TrackFree(4096);
            Assert.Equal(beforeTrack, NativeMemoryTracker.AllocatedBytes);
            Assert.Equal(beforeActive, NativeMemoryTracker.ActiveBlocksCount);

            const int over64Mb = (64 * 1024 * 1024) + 4096;
            using var pool = new NativeMemoryPool(maxPerBucket: 4);

            long beforeRent = NativeMemoryTracker.AllocatedBytes;
            using (var block = pool.Rent(over64Mb))
            {
                Assert.True(NativeMemoryTracker.AllocatedBytes >= beforeRent + over64Mb);
            }
            Assert.Equal(beforeRent, NativeMemoryTracker.AllocatedBytes);

            long beforeLease = NativeMemoryTracker.AllocatedBytes;
            using (var lease = pool.RentLease(over64Mb))
            {
                Assert.True(NativeMemoryTracker.AllocatedBytes >= beforeLease + over64Mb);
            }
            Assert.Equal(beforeLease, NativeMemoryTracker.AllocatedBytes);
        }

        [Fact]
        public void D06_VarIntCodec_Rejects5thAnd10thByteOverflowAndRoundTripsMax()
        {
            Assert.False(VarIntCodec.TryReadVarUInt32(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x1F }, out _, out _));
            Assert.True(VarIntCodec.TryReadVarUInt32(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x0F }, out uint u32Max, out int r32));
            Assert.Equal(uint.MaxValue, u32Max);
            Assert.Equal(5, r32);

            Assert.False(VarIntCodec.TryReadVarUInt64(
                new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x02 }, out _, out _));
            Assert.True(VarIntCodec.TryReadVarUInt64(
                new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01 }, out ulong u64Max, out int r64));
            Assert.Equal(ulong.MaxValue, u64Max);
            Assert.Equal(10, r64);

            Span<byte> buf = stackalloc byte[16];
            VarIntCodec.WriteVarUInt32(buf, uint.MaxValue, out int w32);
            Assert.True(VarIntCodec.TryReadVarUInt32(buf.Slice(0, w32), out uint rt32, out _));
            Assert.Equal(uint.MaxValue, rt32);

            VarIntCodec.WriteVarUInt64(buf, ulong.MaxValue, out int w64);
            Assert.True(VarIntCodec.TryReadVarUInt64(buf.Slice(0, w64), out ulong rt64, out _));
            Assert.Equal(ulong.MaxValue, rt64);
        }

        [Fact]
        public void D07_SimdColorConverter_Int32DimensionOverflow_ThrowsArgumentException()
        {
            byte[] tiny = new byte[16];
            Assert.Throws<ArgumentException>(() =>
                SimdColorConverter.Yuv420pToRgb(tiny, tiny, tiny, tiny, 50000, 50000));
            Assert.Throws<ArgumentException>(() =>
                SimdColorConverter.Nv12ToRgb(tiny, tiny, tiny, 50000, 50000));
            Assert.Throws<ArgumentException>(() =>
                SimdColorConverter.Yuv420pToRgb(tiny, tiny, tiny, tiny, 131072, 131072));
            Assert.Throws<ArgumentException>(() =>
                SimdColorConverter.Nv12ToRgb(tiny, tiny, tiny, 131072, 131072));
        }
    }
}
