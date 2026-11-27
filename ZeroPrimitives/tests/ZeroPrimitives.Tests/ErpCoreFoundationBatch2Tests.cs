using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using ZeroPrimitives.Core.Identifiers;
using ZeroPrimitives.Extensions;
using ZeroPrimitives.Numerics;
using ZeroPrimitives.Parsing;
using ZeroPrimitives.Text;
using ZeroPrimitives.Time;

namespace ZeroPrimitives.Tests
{
    public class ErpCoreFoundationBatch2Tests
    {
        #region DecimalMath

        [Theory]
        [InlineData("1.235", 2, RoundingMode.HalfToEven, "1.24")]
        [InlineData("1.245", 2, RoundingMode.HalfToEven, "1.24")]
        [InlineData("1.245", 2, RoundingMode.HalfAwayFromZero, "1.25")]
        [InlineData("-1.245", 2, RoundingMode.HalfAwayFromZero, "-1.25")]
        [InlineData("1.999", 2, RoundingMode.TowardZero, "1.99")]
        [InlineData("-1.999", 2, RoundingMode.TowardZero, "-1.99")]
        [InlineData("1.991", 2, RoundingMode.AwayFromZero, "2.00")]
        [InlineData("-1.991", 2, RoundingMode.AwayFromZero, "-2.00")]
        [InlineData("1.001", 2, RoundingMode.TowardPositiveInfinity, "1.01")]
        [InlineData("-1.009", 2, RoundingMode.TowardPositiveInfinity, "-1.00")]
        [InlineData("1.009", 2, RoundingMode.TowardNegativeInfinity, "1.00")]
        [InlineData("-1.001", 2, RoundingMode.TowardNegativeInfinity, "-1.01")]
        [InlineData("1.9", 0, RoundingMode.TowardPositiveInfinity, "2")]
        [InlineData("-1.1", 0, RoundingMode.TowardNegativeInfinity, "-2")]
        [InlineData("1.99", 1, RoundingMode.AwayFromZero, "2.0")]
        public void DecimalMath_Round_AllSixModes_ProduceExactResults(
            string input, int decimals, RoundingMode mode, string expected)
        {
            decimal val = decimal.Parse(input, CultureInfo.InvariantCulture);
            decimal exp = decimal.Parse(expected, CultureInfo.InvariantCulture);
            decimal actual = DecimalMath.Round(val, decimals, mode);
            Assert.Equal(exp, actual);
        }

        [Fact]
        public void DecimalMath_RoundToStep_Normalize_AndArithmeticHelpers()
        {
            Assert.Equal(12500m, DecimalMath.RoundToStep(12340m, 500m, RoundingMode.HalfAwayFromZero));
            Assert.Equal(1.25m, DecimalMath.RoundToStep(1.23m, 0.05m, RoundingMode.HalfAwayFromZero));
            Assert.Equal(1.20m, DecimalMath.RoundToStep(1.23m, 0.05m, RoundingMode.TowardZero));
            Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.RoundToStep(10m, 0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => DecimalMath.RoundToStep(10m, -5m));

            Assert.Equal("100.5", DecimalMath.Normalize(100.5000m).ToString(CultureInfo.InvariantCulture));
            Assert.Equal("0", DecimalMath.Normalize(0.000m).ToString(CultureInfo.InvariantCulture));
            Assert.Equal("0", DecimalMath.Normalize(-0.00m).ToString(CultureInfo.InvariantCulture));

            Assert.Equal(4, DecimalMath.GetScale(12.3400m));
            Assert.Equal(0, DecimalMath.GetScale(120m));

            Assert.Equal(99m, DecimalMath.SafeDivide(10m, 0m, 99m));
            Assert.Equal(3.33m, DecimalMath.SafeDivide(10m, 3m, 2));
            Assert.Equal(25.00m, DecimalMath.Percentage(1m, 4m, 2));

            ReadOnlySpan<decimal> vals = stackalloc decimal[] { 100m, 200m };
            ReadOnlySpan<decimal> wts = stackalloc decimal[] { 1m, 3m };
            Assert.Equal(175m, DecimalMath.WeightedAverage(vals, wts));

            ReadOnlySpan<decimal> zeroWts = stackalloc decimal[] { 0m, 0m };
            Assert.Equal(-1m, DecimalMath.WeightedAverage(vals, zeroWts, -1m));
        }

        [Fact]
        public void DecimalMath_WeightedAverage_MismatchedLengths_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ReadOnlySpan<decimal> vals = stackalloc decimal[] { 1m, 2m };
                ReadOnlySpan<decimal> wts = stackalloc decimal[] { 1m };
                DecimalMath.WeightedAverage(vals, wts);
            });
        }

        #endregion

        #region ProportionalAllocator

        [Fact]
        public void ProportionalAllocator_Decimal_ConservesExactSumAcrossPositiveNegativeAndLargeFanOut()
        {
            ReadOnlySpan<decimal> w3 = stackalloc decimal[] { 1m, 1m, 1m };
            Span<decimal> d3 = stackalloc decimal[3];
            ProportionalAllocator.Allocate(100m, w3, d3, decimals: 2);
            Assert.Equal(33.34m, d3[0]);
            Assert.Equal(33.33m, d3[1]);
            Assert.Equal(33.33m, d3[2]);
            Assert.Equal(100m, d3[0] + d3[1] + d3[2]);

            // Credit note (negative total)
            ProportionalAllocator.Allocate(-100m, w3, d3, decimals: 2);
            Assert.Equal(-33.34m, d3[0]);
            Assert.Equal(-33.33m, d3[1]);
            Assert.Equal(-33.33m, d3[2]);
            Assert.Equal(-100m, d3[0] + d3[1] + d3[2]);

            // Integer VND rounding (decimals = 0)
            ReadOnlySpan<decimal> w123 = stackalloc decimal[] { 1m, 2m, 3m };
            ProportionalAllocator.Allocate(1000m, w123, d3, decimals: 0);
            Assert.Equal(167m, d3[0]);
            Assert.Equal(333m, d3[1]);
            Assert.Equal(500m, d3[2]);
            Assert.Equal(1000m, d3[0] + d3[1] + d3[2]);

            // Large fan-out (> 64 triggers ArrayPool spillover)
            decimal[] bigWeights = new decimal[200];
            decimal[] bigOut = new decimal[200];
            for (int i = 0; i < 200; i++) bigWeights[i] = (i % 7) + 1m;
            decimal bigTotal = 123456.78m;
            ProportionalAllocator.Allocate(bigTotal, bigWeights, bigOut, decimals: 2);
            decimal sum = 0m;
            for (int i = 0; i < 200; i++) sum += bigOut[i];
            Assert.Equal(bigTotal, sum);
        }

        [Fact]
        public void ProportionalAllocator_Decimal_ValidationGuards_WorkProperly()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ReadOnlySpan<decimal> w = stackalloc decimal[] { 0m, 0m };
                Span<decimal> dst = stackalloc decimal[2];
                ProportionalAllocator.Allocate(10m, w, dst, decimals: 2);
            });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                ReadOnlySpan<decimal> w = stackalloc decimal[] { 1m, -1m };
                Span<decimal> dst = stackalloc decimal[2];
                ProportionalAllocator.Allocate(10m, w, dst, decimals: 2);
            });

            Assert.Throws<ArgumentException>(() =>
            {
                ReadOnlySpan<decimal> w = stackalloc decimal[] { 1m, 1m };
                Span<decimal> dst = stackalloc decimal[2];
                ProportionalAllocator.Allocate(100.005m, w, dst, decimals: 2);
            });

            Span<decimal> eq = stackalloc decimal[3];
            ProportionalAllocator.AllocateEqual(10m, eq, decimals: 2);
            Assert.Equal(3.34m, eq[0]);
            Assert.Equal(3.33m, eq[1]);
            Assert.Equal(3.33m, eq[2]);
        }

        [Fact]
        public void ProportionalAllocator_Int64_ConservesExactSumEvenAtLargeValues()
        {
            ReadOnlySpan<long> weights = stackalloc long[] { 1, 2, 3 };
            Span<long> dst = stackalloc long[3];
            long total = 1_000_000_000_000L;
            ProportionalAllocator.Allocate(total, weights, dst);
            Assert.Equal(total, dst[0] + dst[1] + dst[2]);
            Assert.Equal(166_666_666_667L, dst[0]);
            Assert.Equal(333_333_333_333L, dst[1]);
            Assert.Equal(500_000_000_000L, dst[2]);

            ProportionalAllocator.Allocate(-100L, weights, dst);
            Assert.Equal(-100L, dst[0] + dst[1] + dst[2]);

            Span<long> eq = stackalloc long[4];
            ProportionalAllocator.AllocateEqual(10L, eq);
            Assert.Equal(3L, eq[0]);
            Assert.Equal(3L, eq[1]);
            Assert.Equal(2L, eq[2]);
            Assert.Equal(2L, eq[3]);
        }

        #endregion

        #region CompactDate & DateRange

        [Fact]
        public void CompactDate_SizeLayoutCalendarMathAndFormatting_WorkAccurately()
        {
            Assert.Equal(4, Unsafe.SizeOf<CompactDate>());

            var d = new CompactDate(2026, 11, 27);
            Assert.Equal(2026, d.Year);
            Assert.Equal(11, d.Month);
            Assert.Equal(27, d.Day);
            Assert.Equal(4, d.Quarter);
            Assert.Equal(DayOfWeek.Friday, d.DayOfWeek);
            Assert.Equal(20261127, d.ToYyyyMmDd());
            Assert.Equal(d, CompactDate.FromYyyyMmDd(20261127));
            Assert.Equal(d, CompactDate.FromDayNumber(d.DayNumber));
            Assert.Equal(d, CompactDate.FromDateTime(new DateTime(2026, 11, 27, 15, 30, 0)));

            Assert.Equal(new CompactDate(2026, 11, 1), d.StartOfMonth());
            Assert.Equal(new CompactDate(2026, 11, 30), d.EndOfMonth());
            Assert.Equal(new CompactDate(2026, 10, 1), d.StartOfQuarter());
            Assert.Equal(new CompactDate(2026, 12, 31), d.EndOfQuarter());
            Assert.Equal(new CompactDate(2026, 1, 1), d.StartOfYear());
            Assert.Equal(new CompactDate(2026, 12, 31), d.EndOfYear());

            Assert.Equal(new CompactDate(2024, 2, 29), new CompactDate(2024, 1, 31).AddMonths(1));
            Assert.Equal(new CompactDate(2025, 2, 28), new CompactDate(2024, 2, 29).AddYears(1));
            Assert.Equal(10, new CompactDate(2026, 12, 7) - d);

            Assert.Equal("2026-11-27", d.ToString());
            Span<char> buf = stackalloc char[16];
            Assert.True(d.TryFormat(buf, out int cw, "dd/MM/yyyy".AsSpan()));
            Assert.Equal("27/11/2026", buf.Slice(0, cw).ToString());

            Span<byte> u8 = stackalloc byte[16];
            Assert.True(d.TryFormatUtf8(u8, out int bw, "yyyyMMdd".AsSpan()));
            Assert.Equal("20261127", Encoding.UTF8.GetString(u8.Slice(0, bw).ToArray()));

            Assert.True(CompactDate.TryParse("27/11/2026".AsSpan(), out CompactDate p1));
            Assert.Equal(d, p1);
            Assert.True(CompactDate.TryParse(Encoding.UTF8.GetBytes("2026-11-27"), out CompactDate p2));
            Assert.Equal(d, p2);

#if NET8_0_OR_GREATER
            DateOnly dateOnly = d.ToDateOnly();
            Assert.Equal(new DateOnly(2026, 11, 27), dateOnly);
            Assert.Equal(d, CompactDate.FromDateOnly(dateOnly));
#endif
        }

        [Fact]
        public void DateRange_HalfOpenSetOperationsAndSplitByMonth_WorkAccurately()
        {
            var r1 = DateRange.Inclusive(new CompactDate(2026, 1, 1), new CompactDate(2026, 1, 31));
            Assert.Equal(31, r1.TotalDays);
            Assert.Equal(new CompactDate(2026, 2, 1), r1.EndExclusive);
            Assert.Equal(new CompactDate(2026, 1, 31), r1.EndInclusive);
            Assert.True(r1.Contains(new CompactDate(2026, 1, 15)));
            Assert.False(r1.Contains(new CompactDate(2026, 2, 1)));

            var r2 = DateRange.ForMonth(2026, 2);
            Assert.True(r1.IsAdjacentTo(r2));
            Assert.False(r1.Overlaps(r2));
            Assert.True(r1.TryUnion(r2, out DateRange union));
            Assert.Equal(59, union.TotalDays);

            var rOverlap = new DateRange(new CompactDate(2026, 1, 20), new CompactDate(2026, 2, 10));
            Assert.True(r1.Overlaps(rOverlap));
            Assert.True(r1.TryIntersect(rOverlap, out DateRange inter));
            Assert.Equal(new CompactDate(2026, 1, 20), inter.Start);
            Assert.Equal(new CompactDate(2026, 2, 1), inter.EndExclusive);

            // Subtract middle hole -> 2 segments
            var full = DateRange.ForQuarter(2026, 1); // Jan 1 .. Apr 1 (90 days)
            var hole = DateRange.ForMonth(2026, 2);   // Feb 1 .. Mar 1 (28 days)
            Span<DateRange> rem = stackalloc DateRange[2];
            int count = full.Subtract(hole, rem);
            Assert.Equal(2, count);
            Assert.Equal(DateRange.ForMonth(2026, 1), rem[0]);
            Assert.Equal(DateRange.ForMonth(2026, 3), rem[1]);

            // Split multi-month range into monthly buckets
            var span = DateRange.Inclusive(new CompactDate(2026, 1, 15), new CompactDate(2026, 4, 10));
            Span<DateRange> months = stackalloc DateRange[4];
            int mCount = span.SplitByMonth(months);
            Assert.Equal(4, mCount);
            Assert.Equal(new CompactDate(2026, 1, 15), months[0].Start);
            Assert.Equal(new CompactDate(2026, 2, 1), months[0].EndExclusive);
            Assert.Equal(DateRange.ForMonth(2026, 2), months[1]);
            Assert.Equal(DateRange.ForMonth(2026, 3), months[2]);
            Assert.Equal(new CompactDate(2026, 4, 1), months[3].Start);
            Assert.Equal(new CompactDate(2026, 4, 11), months[3].EndExclusive);
            Assert.Equal(span.TotalDays, months[0].TotalDays + months[1].TotalDays + months[2].TotalDays + months[3].TotalDays);
        }

        #endregion

        #region StringInternPool

        [Fact]
        public void StringInternPool_DeduplicatesSpansAndRespectsBounds()
        {
            var pool = new StringInternPool(maxEntries: 2, maxStringLength: 16);

            string a1 = pool.GetOrAdd("WH-HCM-01".AsSpan());
            string a2 = pool.GetOrAdd("WH-HCM-01".AsSpan());
            string a3 = pool.GetOrAddUtf8(Encoding.UTF8.GetBytes("WH-HCM-01"));

            Assert.Same(a1, a2);
            Assert.Same(a1, a3);
            Assert.Equal(1, pool.Count);
            Assert.Equal(2, pool.Hits);
            Assert.Equal(1, pool.Misses);

            pool.GetOrAdd("WH-HAN-02".AsSpan());
            Assert.Equal(2, pool.Count);

            // Exceeding maxEntries still returns valid string without growing beyond MaxEntries
            string overflow = pool.GetOrAdd("WH-DN-03".AsSpan());
            Assert.Equal("WH-DN-03", overflow);
            Assert.Equal(2, pool.Count);

            // Exceeding maxStringLength skips pool
            string tooLong = pool.GetOrAdd("THIS-STRING-IS-LONGER-THAN-16".AsSpan());
            Assert.Equal("THIS-STRING-IS-LONGER-THAN-16", tooLong);
            Assert.Equal(2, pool.Count);

            pool.Clear();
            Assert.Equal(0, pool.Count);
        }

        #endregion

        #region Uuid7 & FastUlid SQL Server Ordering

        [Fact]
        public void Uuid7_And_FastUlid_ToSqlServerGuid_MatchSqlGuidSortOrderAndRoundtrip()
        {
            var uList = new List<Uuid7>(500);
            var ulidList = new List<FastUlid>(500);
            for (int i = 0; i < 500; i++)
            {
                uList.Add(Uuid7.NewUuid());
                ulidList.Add(FastUlid.NewUlid());
            }

            for (int i = 0; i < 500; i++)
            {
                Guid sqlU = uList[i].ToSqlServerGuid();
                Assert.Equal(uList[i], Uuid7.FromSqlServerGuid(sqlU));

                Guid sqlUlid = ulidList[i].ToSqlServerGuid();
                Assert.Equal(ulidList[i], FastUlid.FromSqlServerGuid(sqlUlid));

                if (i > 0)
                {
                    var prevU = new SqlGuid(uList[i - 1].ToSqlServerGuid());
                    var currU = new SqlGuid(sqlU);
                    Assert.True(prevU.CompareTo(currU) < 0, $"SqlGuid mismatch for Uuid7 at index {i}");

                    var prevUlid = new SqlGuid(ulidList[i - 1].ToSqlServerGuid());
                    var currUlid = new SqlGuid(sqlUlid);
                    Assert.True(prevUlid.CompareTo(currUlid) < 0, $"SqlGuid mismatch for FastUlid at index {i}");
                }
            }
        }

        [Fact]
        public void Uuid7_TimestampBounds_And_Utf8FormatParse_WorkAccurately()
        {
            var u = Uuid7.NewUuid();
            long ts = u.GetUnixTimeMilliseconds();
            Assert.True(Uuid7.MinValueForTimestamp(ts) <= u);
            Assert.True(u <= Uuid7.MaxValueForTimestamp(ts));
            Assert.True(Uuid7.MaxValueForTimestamp(ts) < Uuid7.MinValueForTimestamp(ts + 1));
            Assert.True(Uuid7.MinValueForTimestamp(1700000000123L) < Uuid7.MaxValueForTimestamp(1700000000123L));

            Span<byte> utf8 = stackalloc byte[38];
            foreach (char fmt in new[] { 'D', 'N', 'B', 'P' })
            {
                Assert.True(u.TryFormatUtf8(utf8, out int written, new ReadOnlySpan<char>(new[] { fmt })));
                Assert.True(Uuid7.TryParse(utf8.Slice(0, written), out Uuid7 parsed));
                Assert.Equal(u, parsed);
            }
        }

        #endregion

        #region FastFormat

        [Fact]
        public void FastFormat_Decimal_MatchesInvariantCultureAcrossEdgeValues()
        {
            decimal[] cases =
            {
                0m,
                -0.00m,
                1m,
                -1m,
                12.50m,
                -123456.789m,
                0.0000000000000000000000000001m,
                79228162514264337593543950335m,
                -79228162514264337593543950335m
            };

            Span<char> cBuf = stackalloc char[64];
            Span<byte> bBuf = stackalloc byte[64];

            foreach (decimal v in cases)
            {
                string expected = v.ToString(CultureInfo.InvariantCulture);

                Assert.True(FastFormat.TryFormatDecimal(v, cBuf, out int cw));
                Assert.Equal(expected, cBuf.Slice(0, cw).ToString());

                Assert.True(FastFormat.TryFormatDecimalUtf8(v, bBuf, out int bw));
                Assert.Equal(expected, Encoding.UTF8.GetString(bBuf.Slice(0, bw).ToArray()));
            }
        }

        [Fact]
        public void FastFormat_DecimalGrouped_Date_And_Guid_FormatAccurately()
        {
            Span<char> cBuf = stackalloc char[64];
            Span<byte> bBuf = stackalloc byte[64];

            Assert.True(FastFormat.TryFormatDecimalGrouped(1234567.89m, cBuf, out int cw));
            Assert.Equal("1,234,567.89", cBuf.Slice(0, cw).ToString());

            Assert.True(FastFormat.TryFormatDecimalGrouped(-1234567.89m, cBuf, out cw, decimals: 2, thousandSeparator: '.', decimalSeparator: ','));
            Assert.Equal("-1.234.567,89", cBuf.Slice(0, cw).ToString());

            Assert.True(FastFormat.TryFormatDecimalGrouped(1234.50m, cBuf, out cw, decimals: 2, trimTrailingZeros: true));
            Assert.Equal("1,234.5", cBuf.Slice(0, cw).ToString());

            Assert.True(FastFormat.TryFormatDecimalGrouped(1234.00m, cBuf, out cw, decimals: 2, trimTrailingZeros: true));
            Assert.Equal("1,234", cBuf.Slice(0, cw).ToString());

            var dt = new DateTime(2026, 11, 27, 9, 30, 45, DateTimeKind.Utc).AddTicks(1230000);
            Assert.True(FastFormat.TryFormatIsoDate(dt, cBuf, out cw));
            Assert.Equal("2026-11-27", cBuf.Slice(0, cw).ToString());
            Assert.True(FastFormat.TryFormatIsoDateUtf8(dt, bBuf, out int bw));
            Assert.Equal("2026-11-27", Encoding.UTF8.GetString(bBuf.Slice(0, bw).ToArray()));

            Assert.True(FastFormat.TryFormatIsoDateTime(dt, cBuf, out cw, includeMilliseconds: true, useT: true));
            Assert.Equal("2026-11-27T09:30:45.123Z", cBuf.Slice(0, cw).ToString());

            Assert.True(FastFormat.TryFormatDate(dt, cBuf, out cw));
            Assert.Equal("27/11/2026", cBuf.Slice(0, cw).ToString());

            var g = Guid.Parse("12345678-9abc-def0-1234-56789abcdef0");
            Assert.True(FastFormat.TryFormatGuid(g, cBuf, out cw, 'D'));
            Assert.Equal("12345678-9abc-def0-1234-56789abcdef0", cBuf.Slice(0, cw).ToString());
            Assert.True(FastFormat.TryFormatGuidUtf8(g, bBuf, out bw, 'N'));
            Assert.Equal("123456789abcdef0123456789abcdef0", Encoding.UTF8.GetString(bBuf.Slice(0, bw).ToArray()));

            Assert.True(FastFormat.TryFormatInt32Utf8(-42, bBuf, out bw));
            Assert.Equal("-42", Encoding.UTF8.GetString(bBuf.Slice(0, bw).ToArray()));

            Assert.True(FastFormat.TryFormatUInt64(ulong.MaxValue, cBuf, out cw));
            Assert.Equal(ulong.MaxValue.ToString(CultureInfo.InvariantCulture), cBuf.Slice(0, cw).ToString());
            Assert.True(FastFormat.TryFormatUInt64Utf8(ulong.MaxValue, bBuf, out bw));
            Assert.Equal(ulong.MaxValue.ToString(CultureInfo.InvariantCulture), Encoding.UTF8.GetString(bBuf.Slice(0, bw).ToArray()));
        }

        #endregion

        #region FastCsvWriter & DateTimeExtensions

        [Fact]
        public void FastCsvWriter_Rfc4180AndFormulaNeutralization_RoundtripCleanly()
        {
            Span<byte> buf = stackalloc byte[512];
            using var writer = new FastCsvWriter(buf, delimiter: ',', neutralizeFormulaInjection: true);

            writer.WriteUtf8Bom();
            writer.WriteField("Code");
            writer.WriteField("Note");
            writer.WriteField("Amount");
            writer.WriteField("Date");
            writer.NextRow();

            writer.WriteField("=CMD|'/C calc'!A0");
            writer.WriteField("Công ty \"ABC\", Q1");
            writer.WriteField(-123.45m);
            writer.WriteField(new CompactDate(2026, 11, 27));
            writer.NextRow();

            byte[] bytes = writer.WrittenSpan.ToArray();
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);

            string text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            var rows = new List<List<string>>();
            Span<char> unquoteBuf = stackalloc char[128];
            foreach (var row in FastCsvParser.EnumerateRows(text.AsSpan()))
            {
                if (row.IsEmpty) continue;
                var cells = new List<string>();
                foreach (var cell in FastCsvParser.EnumerateCells(row))
                {
                    Assert.True(FastCsvParser.TryUnquote(cell, unquoteBuf, out var clean));
                    cells.Add(clean.ToString());
                }
                rows.Add(cells);
            }

            Assert.Equal(2, rows.Count);
            Assert.Equal("'=CMD|'/C calc'!A0", rows[1][0]);
            Assert.Equal("Công ty \"ABC\", Q1", rows[1][1]);
            Assert.Equal("-123.45", rows[1][2]);
            Assert.Equal("2026-11-27", rows[1][3]);
        }

        [Fact]
        public void DateTimeExtensions_HalfOpenBoundariesAndEndOfDaySql_WorkAccurately()
        {
            var dt = new DateTime(2026, 11, 27, 14, 20, 10, DateTimeKind.Utc);
            Assert.Equal(new DateTime(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc), dt.StartOfNextDay());
            Assert.Equal(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), dt.StartOfNextMonth());
            Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), dt.StartOfNextQuarter());
            Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc), dt.StartOfNextYear());
            Assert.Equal(new DateTime(2026, 11, 27, 23, 59, 59, 997, DateTimeKind.Utc), dt.EndOfDaySql());

            DateTime? nullDt = null;
            Assert.Null(nullDt.StartOfNextDay());
            Assert.Null(nullDt.StartOfNextMonth());
            Assert.Null(nullDt.StartOfNextQuarter());
            Assert.Null(nullDt.StartOfNextYear());
            Assert.Null(nullDt.EndOfDaySql());
        }

        #endregion
    }
}
