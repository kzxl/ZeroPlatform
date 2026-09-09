using System;
using Xunit;
using ZeroPrimitives.Extensions;

namespace ZeroPrimitives.Tests
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void MonthAndYearBoundaries_CalculatedCorrectly()
        {
            var date = new DateTime(2026, 9, 15, 10, 20, 30);

            var firstDayMonth = date.FirstDayOfMonth();
            Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0), firstDayMonth);

            var lastDayMonth = date.LastDayOfMonth();
            Assert.Equal(new DateTime(2026, 9, 30, 23, 59, 59, 999), lastDayMonth);

            var firstDayYear = date.FirstDayOfYear();
            Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0), firstDayYear);

            var lastDayYear = date.LastDayOfYear();
            Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 59, 999), lastDayYear);

            var startDay = date.StartOfDay();
            Assert.Equal(new DateTime(2026, 9, 15, 0, 0, 0), startDay);

            var endDay = date.EndOfDay();
            Assert.Equal(new DateTime(2026, 9, 15, 23, 59, 59, 999), endDay);
        }

        [Fact]
        public void QuarterBoundaries_CalculatedCorrectly()
        {
            var q3Date = new DateTime(2026, 8, 20);
            Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0), q3Date.FirstDayOfQuarter());
            Assert.Equal(new DateTime(2026, 9, 30, 23, 59, 59, 999), q3Date.LastDayOfQuarter());
        }

        [Fact]
        public void UnixTimestamps_RoundtripSuccessfully()
        {
            var utcNow = new DateTime(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
            long seconds = utcNow.ToUnixTimestampSeconds();
            long millis = utcNow.ToUnixTimestampMilliseconds();

            Assert.Equal(utcNow, DateTimeExtensions.FromUnixSeconds(seconds));
            Assert.Equal(utcNow, DateTimeExtensions.FromUnixMilliseconds(millis));
        }

        [Fact]
        public void OrdinalDateString_FormatsProperly()
        {
            var dt1 = new DateTime(2026, 1, 1);
            Assert.Equal("1st Jan, 2026", dt1.ToOrdinalDateString(false));

            var dt2 = new DateTime(2026, 1, 2);
            Assert.Equal("2nd Jan, 2026", dt2.ToOrdinalDateString(false));

            var dt3 = new DateTime(2026, 1, 3);
            Assert.Equal("3rd Jan, 2026", dt3.ToOrdinalDateString(false));

            var dt4 = new DateTime(2026, 1, 4);
            Assert.Equal("4th Jan, 2026", dt4.ToOrdinalDateString(false));
            Assert.Equal("4<sup>th</sup> Jan, 2026", dt4.ToOrdinalDateString(true));
        }

        [Fact]
        public void StandardizedBoundaries_SymmetricAndPrecise()
        {
            var date = new DateTime(2026, 9, 15, 10, 20, 30);

            Assert.Equal(new DateTime(2026, 9, 1, 0, 0, 0), date.StartOfMonth());
            Assert.Equal(new DateTime(2026, 9, 30, 23, 59, 59, 999), date.EndOfMonth());
            Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0), date.StartOfYear());
            Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 59, 999), date.EndOfYear());
            Assert.Equal(new DateTime(2026, 7, 1, 0, 0, 0), date.StartOfQuarter());
            Assert.Equal(new DateTime(2026, 9, 30, 23, 59, 59, 999), date.EndOfQuarter());
            Assert.Equal(new DateTime(2026, 9, 15, 0, 0, 0), date.StartOfDay());
            Assert.Equal(new DateTime(2026, 9, 15, 23, 59, 59, 999), date.EndOfDay());

            // Nullable tests
            DateTime? nullDate = null;
            Assert.Null(nullDate.StartOfMonth());
            Assert.Null(nullDate.EndOfMonth());
            Assert.Null(nullDate.StartOfYear());
            Assert.Null(nullDate.EndOfYear());
            Assert.Null(nullDate.StartOfQuarter());
            Assert.Null(nullDate.EndOfQuarter());
            Assert.Null(nullDate.StartOfDay());
            Assert.Null(nullDate.EndOfDay());
        }

        [Fact]
        public void VietnameseFormatters_ReturnExpectedStrings()
        {
            var dt = new DateTime(2026, 9, 9, 14, 30, 45);
            Assert.Equal("09/09/2026", dt.ToVnDateString());
            Assert.Equal("09/09/2026 14:30:45", dt.ToVnDateTimeString());
            Assert.Equal("2026-09-09", dt.ToIsoDateString());
            Assert.Equal("2026-09-09 14:30:45", dt.ToIsoDateTimeString());
            Assert.Equal("09-09-2026", dt.ToDateString("dd-MM-yyyy"));

            // Backward compatibility aliases
            Assert.Equal("09/09/2026", dt.AsDateString_ddMMyyyy());
            Assert.Equal("09/09/2026 14:30:45", dt.AsDateString_ddMMyyyyHHmmss());

            DateTime? nullDt = null;
            Assert.Equal(string.Empty, nullDt.ToVnDateString());
            Assert.Equal(string.Empty, nullDt.ToVnDateTimeString());
            Assert.Equal(string.Empty, nullDt.ToIsoDateString());
            Assert.Equal(string.Empty, nullDt.ToIsoDateTimeString());
            Assert.Equal(string.Empty, nullDt.AsDateString_ddMMyyyy());
            Assert.Equal(string.Empty, nullDt.AsDateString_ddMMyyyyHHmmss());
        }
    }
}
