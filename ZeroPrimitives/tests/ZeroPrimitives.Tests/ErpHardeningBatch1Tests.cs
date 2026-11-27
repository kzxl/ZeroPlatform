using System;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using ZeroPrimitives.Parsing;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class ErpHardeningBatch1Tests
    {
        #region FastDateParser

        [Fact]
        public void FastDateParser_Iso8601FractionalAndUtcZ_ParsesExactTicksAndKindUtc()
        {
            Assert.True(FastDateParser.TryParse("2026-11-27T09:30:45.1234567Z".AsSpan(), out DateTime dt));
            Assert.Equal(DateTimeKind.Utc, dt.Kind);
            Assert.Equal(new DateTime(2026, 11, 27, 9, 30, 45, DateTimeKind.Utc).AddTicks(1234567), dt);
        }

        [Fact]
        public void FastDateParser_Iso8601WithOffset_ConvertsToUtcAndPreservesDateTimeOffset()
        {
            Assert.True(FastDateParser.TryParse("2026-11-27T09:30:45.5+07:00".AsSpan(), out DateTime utcDt));
            Assert.Equal(DateTimeKind.Utc, utcDt.Kind);
            Assert.Equal(new DateTime(2026, 11, 27, 2, 30, 45, 500, DateTimeKind.Utc), utcDt);

            Assert.True(FastDateParser.TryParseDateTimeOffset("2026-11-27T09:30:45.5+07:00".AsSpan(), out DateTimeOffset dto));
            Assert.Equal(TimeSpan.FromHours(7), dto.Offset);
            Assert.Equal(new DateTimeOffset(2026, 11, 27, 9, 30, 45, 500, TimeSpan.FromHours(7)), dto);
        }

        [Theory]
        [InlineData("2026-11-27T09:30:45+0700", 2026, 11, 27, 9, 30, 45, 7, 0)]
        [InlineData("2026-11-27T09:30:45-05:30", 2026, 11, 27, 9, 30, 45, -5, -30)]
        [InlineData("2026-11-27T09:30:45+07", 2026, 11, 27, 9, 30, 45, 7, 0)]
        public void FastDateParser_Iso8601OffsetVariants_ParseCorrectly(
            string input, int y, int m, int d, int hh, int mm, int ss, int offH, int offM)
        {
            Assert.True(FastDateParser.TryParseDateTimeOffset(input.AsSpan(), out DateTimeOffset dto));
            var expectedOffset = new TimeSpan(offH, offM, 0);
            Assert.Equal(new DateTimeOffset(y, m, d, hh, mm, ss, expectedOffset), dto);
        }

        [Fact]
        public void FastDateParser_IsoWithoutSeconds_ParsesHourMinute()
        {
            Assert.True(FastDateParser.TryParse("2026-11-27 09:30".AsSpan(), out DateTime dt));
            Assert.Equal(new DateTime(2026, 11, 27, 9, 30, 0), dt);
        }

        [Theory]
        [InlineData("2026-11-27 14:30:45abc")]
        [InlineData("2026-ab-cd")]
        [InlineData("20260230")]
        [InlineData("2026-11-27T09:30:45+15:00")]
        public void FastDateParser_InvalidOrTrailingGarbage_ReturnsFalse(string input)
        {
            Assert.False(FastDateParser.TryParse(input.AsSpan(), out _));
            Assert.False(FastDateParser.TryParseDateTimeOffset(input.AsSpan(), out _));
        }

        [Fact]
        public void FastDateParser_CompactYyyyMmDd_ParsesCorrectly()
        {
            Assert.True(FastDateParser.TryParse("20261127".AsSpan(), out DateTime dt));
            Assert.Equal(new DateTime(2026, 11, 27), dt);
        }

        [Theory]
        [InlineData("5/9/2026", 2026, 9, 5, 0, 0, 0)]
        [InlineData("5/09/2026", 2026, 9, 5, 0, 0, 0)]
        [InlineData("05/9/2026", 2026, 9, 5, 0, 0, 0)]
        [InlineData("5/9/2026 8:05", 2026, 9, 5, 8, 5, 0)]
        [InlineData("5/9/2026 08:05:10", 2026, 9, 5, 8, 5, 10)]
        public void FastDateParser_SingleDigitDayMonth_ParsesDayFirst(
            string input, int y, int m, int d, int hh, int mm, int ss)
        {
            Assert.True(FastDateParser.TryParse(input.AsSpan(), out DateTime dt));
            Assert.Equal(new DateTime(y, m, d, hh, mm, ss), dt);
        }

        [Fact]
        public void FastDateParser_Utf8SpanOverloads_ParseCorrectly()
        {
            ReadOnlySpan<byte> utf8Iso = Encoding.UTF8.GetBytes("2026-11-27T09:30:45.5+07:00");
            Assert.True(FastDateParser.TryParse(utf8Iso, out DateTime utcDt));
            Assert.Equal(new DateTime(2026, 11, 27, 2, 30, 45, 500, DateTimeKind.Utc), utcDt);

            Assert.True(FastDateParser.TryParseDateTimeOffset(utf8Iso, out DateTimeOffset dto));
            Assert.Equal(new DateTimeOffset(2026, 11, 27, 9, 30, 45, 500, TimeSpan.FromHours(7)), dto);

            ReadOnlySpan<byte> utf8Compact = Encoding.UTF8.GetBytes("20261127");
            Assert.True(FastDateParser.TryParse(utf8Compact, out DateTime compactDt));
            Assert.Equal(new DateTime(2026, 11, 27), compactDt);
        }

        #endregion

        #region FixedString32 / FixedString64 / FixedString128

        [Fact]
        public void FixedString_MultiByteVietnameseExceedingCapacity_TruncatesWithoutThrowingOrSplittingRunes()
        {
            string vn = "Kho nguyên vật liệu chính miền Nam đặc biệt";

            var fs32 = new FixedString32(vn.AsSpan());
            Assert.InRange(fs32.Length, 1, FixedString32.MaxByteCapacity);
            string s32 = fs32.ToString();
            Assert.DoesNotContain('\uFFFD', s32);
            Assert.StartsWith(s32, vn, StringComparison.Ordinal);

            var fs64 = new FixedString64(vn.AsSpan());
            Assert.InRange(fs64.Length, 1, FixedString64.MaxByteCapacity);
            string s64 = fs64.ToString();
            Assert.DoesNotContain('\uFFFD', s64);
            Assert.StartsWith(s64, vn, StringComparison.Ordinal);

            var fs128 = new FixedString128(vn.AsSpan());
            Assert.Equal(Encoding.UTF8.GetByteCount(vn), fs128.Length);
            Assert.Equal(vn, fs128.ToString());
        }

        [Fact]
        public void FixedString32_SurrogatePairAtBoundary_DoesNotSplitSurrogate()
        {
            // 29 ASCII bytes + 4-byte emoji = 33 bytes (> 31 byte capacity)
            string input = new string('A', 29) + "\uD83D\uDE00";
            var fs32 = new FixedString32(input.AsSpan());
            Assert.Equal(29, fs32.Length);
            Assert.Equal(new string('A', 29), fs32.ToString());
        }

        [Fact]
        public void FixedString_TryCreate_ReportsExactFitVsTruncation()
        {
            Assert.True(FixedString32.TryCreate("SKU-001".AsSpan(), out FixedString32 ok32));
            Assert.Equal("SKU-001", ok32.ToString());

            string tooLong = "Kho nguyên vật liệu chính miền Nam";
            Assert.False(FixedString32.TryCreate(tooLong.AsSpan(), out FixedString32 trunc32));
            Assert.DoesNotContain('\uFFFD', trunc32.ToString());
            Assert.StartsWith(trunc32.ToString(), tooLong, StringComparison.Ordinal);

            byte[] utf8 = Encoding.UTF8.GetBytes("Kho nguyên vật liệu chính miền Nam");
            Assert.False(FixedString32.TryCreate(utf8.AsSpan(), out FixedString32 utf8Trunc32));
            Assert.DoesNotContain('\uFFFD', utf8Trunc32.ToString());

            Assert.True(FixedString64.TryCreate("NHAP-KHO-2026".AsSpan(), out FixedString64 ok64));
            Assert.Equal("NHAP-KHO-2026", ok64.ToString());
            Assert.False(FixedString64.TryCreate(Encoding.UTF8.GetBytes(new string('đ', 40)).AsSpan(), out FixedString64 utf8Trunc64));
            Assert.Equal(62, utf8Trunc64.Length);
            Assert.DoesNotContain('\uFFFD', utf8Trunc64.ToString());
        }

        [Fact]
        public void FixedString128_LayoutAndValueSemantics_MatchContract()
        {
            Assert.Equal(128, Unsafe.SizeOf<FixedString128>());
            Assert.Equal(127, FixedString128.MaxByteCapacity);

            FixedString128 a = "CHỨNG-TỪ-XUẤT-KHO-SỐ-2026-000123-CHI-NHÁNH-HỒ-CHÍ-MINH";
            FixedString128 b = "CHỨNG-TỪ-XUẤT-KHO-SỐ-2026-000123-CHI-NHÁNH-HỒ-CHÍ-MINH";
            FixedString128 c = "CHỨNG-TỪ-NHẬP-KHO";

            Assert.True(a == b);
            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.False(a == c);
            Assert.NotEqual(0, a.CompareTo(c));
        }

        #endregion

        #region FastNumberParser

        [Fact]
        public void FastNumberParser_Int64ComplexOverflow_ReturnsFalseAndDefault()
        {
            Assert.False(FastNumberParser.TryParseInt64("99,999,999,999,999,999,999".AsSpan(), out long overPos, -1L));
            Assert.Equal(-1L, overPos);

            Assert.False(FastNumberParser.TryParseInt64("-9,223,372,036,854,775,809".AsSpan(), out long overNeg, -2L));
            Assert.Equal(-2L, overNeg);

            Assert.True(FastNumberParser.TryParseInt64("9,223,372,036,854,775,807".AsSpan(), out long maxVal));
            Assert.Equal(long.MaxValue, maxVal);

            Assert.True(FastNumberParser.TryParseInt64("-9,223,372,036,854,775,808".AsSpan(), out long minVal));
            Assert.Equal(long.MinValue, minVal);
        }

        [Fact]
        public void FastNumberParser_VeryLongDecimalWithLeadingZeros_SucceedsWithoutStackOverflow()
        {
            string longZeroPadded = new string('0', 5000) + "123.45";
            Assert.True(FastNumberParser.TryParseDecimal(longZeroPadded.AsSpan(), out decimal d1));
            Assert.Equal(123.45m, d1);

            string longExtended = new string(' ', 300) + "12345678901234567890.12";
            Assert.True(FastNumberParser.TryParseDecimal(longExtended.AsSpan(), out decimal d2));
            Assert.Equal(12345678901234567890.12m, d2);
        }

        [Fact]
        public void FastNumberParser_AccountingParentheses_ParsesAsNegativeAndRejectsDoubleSign()
        {
            Assert.True(FastNumberParser.TryParseDecimal("(1,234.56)".AsSpan(), out decimal d1));
            Assert.Equal(-1234.56m, d1);

            Assert.True(FastNumberParser.TryParseDecimal("($1,234.56)".AsSpan(), out decimal d2));
            Assert.Equal(-1234.56m, d2);

            Assert.True(FastNumberParser.TryParseDecimal("$(1,234.56)".AsSpan(), out decimal d3));
            Assert.Equal(-1234.56m, d3);

            Assert.True(FastNumberParser.TryParseDecimal("(1.500.000 VNĐ)".AsSpan(), out decimal d4));
            Assert.Equal(-1500000m, d4);

            Assert.True(FastNumberParser.TryParseInt32("(1,234)".AsSpan(), out int i1));
            Assert.Equal(-1234, i1);

            Assert.True(FastNumberParser.TryParseInt64("(9,000,000,000)".AsSpan(), out long l1));
            Assert.Equal(-9000000000L, l1);

            Assert.True(FastNumberParser.TryParseDouble("(1,234.56)".AsSpan(), out double dbl1));
            Assert.Equal(-1234.56, dbl1, 6);

            Assert.False(FastNumberParser.TryParseDecimal("(-123)".AsSpan(), out _));
            Assert.False(FastNumberParser.TryParseDecimal("(+123)".AsSpan(), out _));
            Assert.False(FastNumberParser.TryParseInt32("(-123)".AsSpan(), out _));
        }

        [Fact]
        public void FastNumberParser_Utf8BytesWithMultiByteCurrency_ParsesCorrectly()
        {
            byte[] vndBytes = Encoding.UTF8.GetBytes("1.500.000 ₫");
            Assert.True(FastNumberParser.TryParseDecimal(vndBytes, out decimal vndDec));
            Assert.Equal(1500000m, vndDec);
            Assert.True(FastNumberParser.TryParseDouble(vndBytes, out double vndDbl));
            Assert.Equal(1500000.0, vndDbl, 6);

            byte[] eurBytes = Encoding.UTF8.GetBytes("€ 999.99");
            Assert.True(FastNumberParser.TryParseDecimal(eurBytes, out decimal eurDec));
            Assert.Equal(999.99m, eurDec);
            Assert.True(FastNumberParser.TryParseDouble(eurBytes, out double eurDbl));
            Assert.Equal(999.99, eurDbl, 6);
        }

        #endregion

        #region FastJsonReader & FastJsonWriter

        [Fact]
        public void FastJsonReader_EscapedStringsAndUnicodeSurrogates_UnescapeCorrectly()
        {
            string json = "{\"name\":\"C\\u00f4ng ty \\\"ABC\\\"\\\\Kho\\n\\u0111\\u1ed3ng\",\"emoji\":\"\\uD83D\\uDE00\"}";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var reader = new FastJsonReader(bytes);

            Assert.True(reader.Read()); // {
            Assert.True(reader.Read()); // "name"
            Assert.True(reader.ValueEquals("name"));
            Assert.True(reader.ValueEquals(Encoding.UTF8.GetBytes("name")));

            Assert.True(reader.Read()); // value
            Assert.Equal("Công ty \"ABC\"\\Kho\nđồng", reader.GetString());
            Assert.True(reader.ValueEquals("Công ty \"ABC\"\\Kho\nđồng"));

            Span<char> small = stackalloc char[4];
            Assert.False(reader.TryGetUnescapedString(small, out _));

            Span<char> exact = stackalloc char[32];
            Assert.True(reader.TryGetUnescapedString(exact, out int written));
            Assert.Equal("Công ty \"ABC\"\\Kho\nđồng", exact.Slice(0, written).ToString());

            Assert.True(reader.Read()); // "emoji"
            Assert.True(reader.Read()); // emoji value
            Assert.Equal("\uD83D\uDE00", reader.GetString());
        }

        [Fact]
        public void FastJsonReader_Skip_SkipsNestedStructuresCleanly()
        {
            string json = "{\"meta\":{\"tags\":[1,2,{\"a\":\"b\"}],\"x\":true},\"total\":42}";
            var reader = new FastJsonReader(Encoding.UTF8.GetBytes(json));

            Assert.True(reader.Read()); // {
            Assert.True(reader.Read()); // "meta"
            Assert.True(reader.ValueEquals("meta"));
            Assert.True(reader.Skip()); // skips entire meta object

            Assert.True(reader.Read()); // "total"
            Assert.Equal(FastJsonTokenType.PropertyName, reader.TokenType);
            Assert.True(reader.ValueEquals("total"));
            Assert.True(reader.Read()); // 42
            Assert.Equal(42, reader.GetInt32());
        }

        [Fact]
        public void FastJsonWriter_NonFiniteDoubles_ThrowArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Span<byte> buf = stackalloc byte[64];
                var writer = new FastJsonWriter(buf);
                writer.WriteStartObject();
                writer.WriteNumber("v", double.NaN);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                Span<byte> buf = stackalloc byte[64];
                var writer = new FastJsonWriter(buf);
                writer.WriteStartArray();
                writer.WriteNumber(double.PositiveInfinity);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                Span<byte> buf = stackalloc byte[64];
                var writer = new FastJsonWriter(buf);
                writer.WriteStartArray();
                writer.WriteNumber(double.NegativeInfinity);
            });
        }

        [Fact]
        public void FastJsonWriter_ArrayValueOverloads_WriteValidJson()
        {
            Span<byte> buf = stackalloc byte[256];
            using var writer = new FastJsonWriter(buf);

            writer.WriteStartArray();
            writer.WriteNumber(10);
            writer.WriteNumber(9000000000L);
            writer.WriteNumber(12.5m);
            writer.WriteNumber(3.25);
            writer.WriteBoolean(true);
            writer.WriteBoolean(false);
            writer.WriteNull();
            writer.WriteEndArray();

            Assert.Equal("[10,9000000000,12.5,3.25,true,false,null]", writer.ToString());
        }

        #endregion

        #region FastConvert & FastCsvParser

        [Fact]
        public void FastConvert_AsNullableBool_RespectsDefaultOnInvalidAndSupportsLongShort()
        {
            Assert.Null(FastConvert.AsNullableBool("invalid"));
            Assert.True(FastConvert.AsNullableBool("invalid", true));
            Assert.True(FastConvert.AsNullableBool("YES"));
            Assert.False(FastConvert.AsNullableBool("no"));
            Assert.True(FastConvert.AsNullableBool(1L));
            Assert.False(FastConvert.AsNullableBool(0L));
            Assert.True(FastConvert.AsNullableBool((short)5));
            Assert.False(FastConvert.AsNullableBool((short)0));
        }

        [Fact]
        public void FastCsvParser_TryUnquoteAndUnquote_CheckDestinationBounds()
        {
            ReadOnlySpan<char> quoted = "\"He said: \"\"Hello\"\"\"".AsSpan();

            Span<char> tiny = stackalloc char[4];
            Assert.False(FastCsvParser.TryUnquote(quoted, tiny, out _));

            Assert.Throws<ArgumentException>(() =>
            {
                Span<char> small = stackalloc char[4];
                FastCsvParser.Unquote("\"He said: \"\"Hello\"\"\"".AsSpan(), small, out _);
            });

            Span<char> ok = stackalloc char[32];
            Assert.True(FastCsvParser.TryUnquote(quoted, ok, out ReadOnlySpan<char> unquoted));
            Assert.Equal("He said: \"Hello\"", unquoted.ToString());
        }

        #endregion
    }
}
