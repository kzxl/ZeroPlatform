using System;
using Xunit;
using ZeroPrimitives.Extensions;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Tests
{
    public class FastDateParserTests
    {
        [Fact]
        public void TryParse_Iso8601Date_ReturnsExpected()
        {
            Assert.True(FastDateParser.TryParse("2026-09-09".AsSpan(), out DateTime dt));
            Assert.Equal(2026, dt.Year);
            Assert.Equal(9, dt.Month);
            Assert.Equal(9, dt.Day);
            Assert.Equal(0, dt.Hour);
        }

        [Fact]
        public void TryParse_Iso8601DateTime_ReturnsExpected()
        {
            Assert.True(FastDateParser.TryParse("2026-09-09 14:30:45".AsSpan(), out DateTime dt));
            Assert.Equal(2026, dt.Year);
            Assert.Equal(9, dt.Month);
            Assert.Equal(9, dt.Day);
            Assert.Equal(14, dt.Hour);
            Assert.Equal(30, dt.Minute);
            Assert.Equal(45, dt.Second);
        }

        [Fact]
        public void TryParse_VietnameseDateFormat_ReturnsExpected()
        {
            Assert.True(FastDateParser.TryParse("09/09/2026".AsSpan(), out DateTime dt));
            Assert.Equal(2026, dt.Year);
            Assert.Equal(9, dt.Month);
            Assert.Equal(9, dt.Day);

            Assert.True(FastDateParser.TryParse("25/12/2025 08:15:00".AsSpan(), out dt));
            Assert.Equal(2025, dt.Year);
            Assert.Equal(12, dt.Month);
            Assert.Equal(25, dt.Day);
            Assert.Equal(8, dt.Hour);
            Assert.Equal(15, dt.Minute);
        }

        [Fact]
        public void AsSqlDateString_ClampsAndFormats()
        {
            object validDate = new DateTime(2026, 9, 9);
            Assert.Equal("2026-09-09", validDate.AsSqlDateString());

            object earlyDate = new DateTime(1600, 1, 1);
            Assert.Equal("1753-01-01", earlyDate.AsSqlDateString()); // Clamped to SQL Server minimum

            object? nullDate = null;
            Assert.Equal("NULL", nullDate.AsSqlDateString());
        }
    }
}
