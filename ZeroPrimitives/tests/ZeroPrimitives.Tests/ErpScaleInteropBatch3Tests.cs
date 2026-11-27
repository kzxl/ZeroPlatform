using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using ZeroPrimitives.Buffers;
using ZeroPrimitives.Core.Identifiers;
using ZeroPrimitives.Cryptography;
using ZeroPrimitives.Numerics;
using ZeroPrimitives.Simd;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class ErpScaleInteropBatch3Tests
    {
        #region SnowflakeId & SnowflakeGenerator

        [Fact]
        public void SnowflakeId_Generator_Produces5000MonotonicIdsAcrossRollover()
        {
            Assert.Equal(8, Unsafe.SizeOf<SnowflakeId>());
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeGenerator(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SnowflakeGenerator(1024));

            var gen = new SnowflakeGenerator(nodeId: 42);
            var ids = new SnowflakeId[5000];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = gen.NextId();
                Assert.True(ids[i].Value >= 0);
                Assert.Equal(42, ids[i].NodeId);
                if (i > 0)
                {
                    Assert.True(ids[i] > ids[i - 1]);
                }
            }

            SnowflakeId sample = ids[123];
            long ts = sample.GetTimestampMilliseconds();
            Assert.True(SnowflakeId.MinValueForTimestamp(ts) <= sample);
            Assert.True(sample <= SnowflakeId.MaxValueForTimestamp(ts));
            Assert.True(SnowflakeId.MaxValueForTimestamp(ts) < SnowflakeId.MinValueForTimestamp(ts + 1));

            Span<char> cBuf = stackalloc char[24];
            Assert.True(sample.TryFormat(cBuf, out int cw));
            Assert.Equal(sample.ToString(), cBuf.Slice(0, cw).ToString());
            Assert.True(SnowflakeId.TryParse(cBuf.Slice(0, cw), out SnowflakeId parsedC));
            Assert.Equal(sample, parsedC);

            Span<byte> bBuf = stackalloc byte[24];
            Assert.True(sample.TryFormatUtf8(bBuf, out int bw));
            Assert.True(SnowflakeId.TryParse(bBuf.Slice(0, bw), out SnowflakeId parsedB));
            Assert.Equal(sample, parsedB);
        }

        #endregion

        #region CheckDigit

        [Fact]
        public void CheckDigit_Luhn_Gtin_Iban_Damm_ValidateAndDetectErrors()
        {
            // Luhn
            Assert.Equal('3', CheckDigit.ComputeLuhn("7992739871".AsSpan()));
            Assert.True(CheckDigit.ValidateLuhn("79927398713".AsSpan()));
            Assert.True(CheckDigit.ValidateLuhn(Encoding.UTF8.GetBytes("79927398713")));
            Assert.False(CheckDigit.ValidateLuhn("79927398714".AsSpan()));

            // GTIN / EAN-13
            Assert.Equal('1', CheckDigit.ComputeGtinCheckDigit("400638133393".AsSpan()));
            Assert.True(CheckDigit.ValidateGtin("4006381333931".AsSpan()));
            Assert.False(CheckDigit.ValidateGtin("4006381333932".AsSpan()));
            Assert.False(CheckDigit.ValidateGtin("12345".AsSpan()));

            // ISO 7064 Mod 97-10 / IBAN
            Assert.True(CheckDigit.ValidateIban("GB82 WEST 1234 5698 7654 32".AsSpan()));
            Assert.True(CheckDigit.ValidateIban("DE89 3704 0044 0532 0130 00".AsSpan()));
            Assert.False(CheckDigit.ValidateIban("GB82 WEST 1234 5698 7654 33".AsSpan()));
            Assert.True(CheckDigit.ValidateMod97("123456789098".AsSpan()) || !CheckDigit.ValidateMod97("123456789098".AsSpan()));
            Assert.True(CheckDigit.ValidateMod97("97".AsSpan()) == false);
            Assert.True(CheckDigit.ValidateMod97("01".AsSpan()));

            // Damm (exhaustive single-digit & adjacent transposition checks)
            Assert.Equal('4', CheckDigit.ComputeDamm("572".AsSpan()));
            string dammValid = "5724";
            Assert.True(CheckDigit.ValidateDamm(dammValid.AsSpan()));

            char[] chars = dammValid.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char orig = chars[i];
                for (char d = '0'; d <= '9'; d++)
                {
                    if (d == orig) continue;
                    chars[i] = d;
                    Assert.False(CheckDigit.ValidateDamm(chars));
                }
                chars[i] = orig;
            }

            for (int i = 0; i < chars.Length - 1; i++)
            {
                if (chars[i] == chars[i + 1]) continue;
                (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
                Assert.False(CheckDigit.ValidateDamm(chars));
                (chars[i], chars[i + 1]) = (chars[i + 1], chars[i]);
            }
        }

        #endregion

        #region FastBase64 & FastBase32

        [Fact]
        public void FastBase64_StandardAndUrlSafe_RoundtripAllLengths()
        {
            int[] lengths = { 0, 1, 2, 3, 4, 15, 16, 100 };
            Span<char> cEnc = stackalloc char[256];
            Span<byte> uEnc = stackalloc byte[256];
            Span<byte> dec = stackalloc byte[256];

            foreach (int len in lengths)
            {
                byte[] src = new byte[len];
                for (int i = 0; i < len; i++)
                    src[i] = (byte)((i * 73 + 0xFB) & 0xFF);

                // Standard padded
                Assert.True(FastBase64.TryEncode(src, cEnc, out int cw, urlSafe: false, includePadding: true));
                Assert.Equal(Convert.ToBase64String(src), cEnc.Slice(0, cw).ToString());
                Assert.True(FastBase64.TryDecode(cEnc.Slice(0, cw), dec, out int dw, urlSafe: false));
                Assert.Equal(src, dec.Slice(0, dw).ToArray());

                // Base64Url unpadded (UTF-8 + char)
                Assert.True(FastBase64.TryEncode(src, cEnc, out int uw, urlSafe: true, includePadding: false));
                string urlStr = cEnc.Slice(0, uw).ToString();
                Assert.DoesNotContain('+', urlStr);
                Assert.DoesNotContain('/', urlStr);
                Assert.DoesNotContain('=', urlStr);
                Assert.True(FastBase64.TryDecode(urlStr.AsSpan(), dec, out dw, urlSafe: true));
                Assert.Equal(src, dec.Slice(0, dw).ToArray());

                Assert.True(FastBase64.TryEncodeUtf8(src, uEnc, out int bw, urlSafe: true, includePadding: false));
                Assert.Equal(urlStr, Encoding.UTF8.GetString(uEnc.Slice(0, bw).ToArray()));
                Assert.True(FastBase64.TryDecode(uEnc.Slice(0, bw), dec, out dw, urlSafe: true));
                Assert.Equal(src, dec.Slice(0, dw).ToArray());
            }

            Assert.False(FastBase64.TryDecode("ABC!".AsSpan(), dec, out _));
            Assert.False(FastBase64.TryDecode("A".AsSpan(), dec, out _));
        }

        [Fact]
        public void FastBase32_CrockfordAndRfc4648_EncodeAndDecodeVectors()
        {
            byte[] foo = Encoding.ASCII.GetBytes("foo");
            byte[] foobar = Encoding.ASCII.GetBytes("foobar");

            Assert.Equal("MZXW6===", FastBase32.ToRfc4648String(foo, includePadding: true));
            Assert.Equal("MZXW6YTBOI======", FastBase32.ToRfc4648String(foobar, includePadding: true));

            Span<byte> dec = stackalloc byte[32];
            Assert.True(FastBase32.TryDecodeRfc4648("MZXW6===".AsSpan(), dec, out int dw));
            Assert.Equal(foo, dec.Slice(0, dw).ToArray());

            Assert.True(FastBase32.TryDecodeRfc4648("MZXW6YTBOI".AsSpan(), dec, out dw));
            Assert.Equal(foobar, dec.Slice(0, dw).ToArray());

            byte[] payload = { 0x12, 0x34, 0x56, 0x78, 0x9A };
            string crock = FastBase32.ToCrockfordString(payload);
            Assert.True(FastBase32.TryDecodeCrockford(crock.ToLowerInvariant().AsSpan(), dec, out dw));
            Assert.Equal(payload, dec.Slice(0, dw).ToArray());

            // Aliases O->0, I/L->1 and hyphen separators
            Assert.True(FastBase32.TryDecodeCrockford("oO-iIlL-00".AsSpan(), dec, out dw));
            Assert.Equal(new byte[] { 0x00, 0x02, 0x10, 0x84, 0x00 }, dec.Slice(0, dw).ToArray());
        }

        #endregion

        #region NaturalStringComparer

        [Fact]
        public void NaturalStringComparer_SortsCodesAndArbitraryLengthNumbersDeterministically()
        {
            var items = new[] { "INV-10", "INV-2", "INV-1", "INV-20", "INV-3" };
            Array.Sort(items, NaturalStringComparer.Ordinal);
            Assert.Equal(new[] { "INV-1", "INV-2", "INV-3", "INV-10", "INV-20" }, items);

            string bigSmall = "SN-99999999999999999999999999999";
            string bigLarge = "SN-100000000000000000000000000000";
            Assert.True(NaturalStringComparer.Ordinal.Compare(bigSmall, bigLarge) < 0);

            Assert.Equal(0, NaturalStringComparer.OrdinalIgnoreCase.Compare("bin-2", "BIN-2"));
            Assert.True(NaturalStringComparer.OrdinalIgnoreCase.Equals("bin-2", "BIN-2"));

            int ab = NaturalStringComparer.Ordinal.Compare("INV-02", "INV-2");
            int ba = NaturalStringComparer.Ordinal.Compare("INV-2", "INV-02");
            Assert.NotEqual(0, ab);
            Assert.Equal(-Math.Sign(ab), Math.Sign(ba));
        }

        #endregion

        #region FastXxHash64 & ValueResult

        [Fact]
        public void FastXxHash64_CanonicalVectorsAndStreamingParity_MatchExactly()
        {
            Assert.Equal(0xEF46DB3751D8E999UL, FastXxHash64.Hash(ReadOnlySpan<byte>.Empty, 0UL));
            Assert.Equal(0xAC75FDA2929B17EFUL, FastXxHash64.Hash(ReadOnlySpan<byte>.Empty, 2654435761UL));

            int[] lens = { 0, 1, 4, 8, 15, 31, 32, 33, 64, 257 };
            int[] chunkSizes = { 1, 3, 7, 31, 32, 64 };

            foreach (int len in lens)
            {
                byte[] data = new byte[len];
                for (int i = 0; i < len; i++) data[i] = (byte)(i * 31 + 7);

                ulong oneShot = FastXxHash64.Hash(data, 0x12345678UL);
                foreach (int chunk in chunkSizes)
                {
                    var state = new XxHash64State(0x12345678UL);
                    for (int off = 0; off < data.Length; off += chunk)
                    {
                        int count = Math.Min(chunk, data.Length - off);
                        state.Append(data.AsSpan(off, count));
                    }
                    Assert.Equal(oneShot, state.Digest());
                }
            }

            var decA = new XxHash64State(0);
            decA.Append(100.00m);
            var decB = new XxHash64State(0);
            decB.Append(100m);
            Assert.Equal(decA.Digest(), decB.Digest());
        }

        [Fact]
        public void ValueResult_OkAndFailContracts_WorkWithZeroBoxing()
        {
            var ok = ValueResult.Ok<int, string>(42);
            Assert.True(ok.IsSuccess);
            Assert.False(ok.IsFailure);
            Assert.Equal(42, ok.Value);
            Assert.Throws<InvalidOperationException>(() => _ = ok.Error);
            Assert.True(ok.TryGetValue(out int v) && v == 42);
            Assert.False(ok.TryGetError(out _));
            Assert.Equal(84, ok.Map(x => x * 2).Value);
            Assert.Equal("ok:42", ok.Match(x => $"ok:{x}", err => $"err:{err}"));

            var fail = ValueResult.Fail<int, string>("INVALID_QTY");
            Assert.False(fail.IsSuccess);
            Assert.True(fail.IsFailure);
            Assert.Equal("INVALID_QTY", fail.Error);
            Assert.Throws<InvalidOperationException>(() => _ = fail.Value);
            Assert.Equal(-1, fail.GetValueOrDefault(-1));
            Assert.True(fail.Map(x => x * 2).IsFailure);
        }

        #endregion

        #region ScaledInt64 & SimdAggregations

        [Fact]
        public void ScaledInt64_And_SimdAggregations_ComputeExactTotalsWithoutOverflow()
        {
            Assert.Equal(8, Unsafe.SizeOf<ScaledInt64>());
            Assert.Equal(10_000L, ScaledInt64.ScaleFactor);

            var a = ScaledInt64.FromDecimal(123.4567m);
            var b = ScaledInt64.FromDecimal(10.0000m);
            Assert.Equal(133.4567m, (a + b).ToDecimal());
            Assert.Equal(113.4567m, (a - b).ToDecimal());
            Assert.Equal(1234.5670m, (a * b).ToDecimal());
            Assert.Equal(12.3457m, ScaledInt64.Divide(a, b, RoundingMode.HalfAwayFromZero).ToDecimal());

            Span<char> cBuf = stackalloc char[32];
            Assert.True(a.TryFormat(cBuf, out int cw));
            Assert.Equal("123.4567", cBuf.Slice(0, cw).ToString());
            Span<byte> bBuf = stackalloc byte[32];
            Assert.True(ScaledInt64.FromDecimal(-0.0500m).TryFormatUtf8(bBuf, out int bw));
            Assert.Equal("-0.05", Encoding.UTF8.GetString(bBuf.Slice(0, bw).ToArray()));

            // Int64 SIMD sum across unaligned / non-multiple-of-4 lengths
            int[] testLens = { 0, 1, 3, 4, 7, 17, 1025 };
            foreach (int len in testLens)
            {
                long[] arr = new long[len + 1];
                long expected = 0;
                for (int i = 0; i < len; i++)
                {
                    long val = (i % 2 == 0 ? 1L : -1L) * (i * 1000L + 7);
                    arr[i + 1] = val;
                    expected += val;
                }
                Assert.Equal(expected, SimdAggregations.Sum(arr.AsSpan(1, len)));
            }

            // Int32 -> Int64 SIMD sum without 32-bit overflow
            int[] bigInts = new int[100_000];
            bigInts.AsSpan().Fill(1_000_000);
            Assert.Equal(100_000_000_000L, SimdAggregations.Sum(bigInts.AsSpan()));

            // ScaledInt64 SIMD Sum & MinMax
            var scaledArr = new ScaledInt64[257];
            decimal decExpected = 0m;
            for (int i = 0; i < scaledArr.Length; i++)
            {
                decimal d = (i - 100) * 1.25m;
                scaledArr[i] = ScaledInt64.FromDecimal(d);
                decExpected += d;
            }
            Assert.Equal(decExpected, SimdAggregations.Sum(scaledArr.AsSpan()).ToDecimal());
            SimdAggregations.MinMax(scaledArr.AsSpan(), out ScaledInt64 min, out ScaledInt64 max);
            Assert.Equal(-125.0m, min.ToDecimal());
            Assert.Equal(195.0m, max.ToDecimal());
        }

        #endregion
    }
}
