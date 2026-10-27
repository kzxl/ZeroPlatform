using System;
using Xunit;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Tests
{
    public class FastNumberFormatterTests
    {
        [Theory]
        [InlineData(0, "0")]
        [InlineData(42, "42")]
        [InlineData(1024, "1024")]
        [InlineData(2048, "2048")]
        public void GetCachedIntString_ReturnsExpectedString(int val, string expected)
        {
            string s = FastNumberFormatter.GetCachedIntString(val);
            Assert.Equal(expected, s);
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        [InlineData(-1, "-1")]
        [InlineData(123456789, "123456789")]
        [InlineData(-987654321, "-987654321")]
        [InlineData(int.MaxValue, "2147483647")]
        [InlineData(int.MinValue, "-2147483648")]
        public void TryFormatInt32_FormatsCorrectly(int val, string expected)
        {
            Span<char> buf = stackalloc char[32];
            bool success = FastNumberFormatter.TryFormatInt32(val, buf, out int len);

            Assert.True(success);
            Assert.Equal(expected, buf.Slice(0, len).ToString());
        }

        [Theory]
        [InlineData(0L, "0")]
        [InlineData(1000000000000L, "1000000000000")]
        [InlineData(-5000000000000L, "-5000000000000")]
        [InlineData(long.MaxValue, "9223372036854775807")]
        [InlineData(long.MinValue, "-9223372036854775808")]
        public void TryFormatInt64_FormatsCorrectly(long val, string expected)
        {
            Span<char> buf = stackalloc char[32];
            bool success = FastNumberFormatter.TryFormatInt64(val, buf, out int len);

            Assert.True(success);
            Assert.Equal(expected, buf.Slice(0, len).ToString());
        }

        [Fact]
        public void TryFormatDouble_FormatsWithPrecision()
        {
            Span<char> buf = stackalloc char[32];
            bool success = FastNumberFormatter.TryFormatDouble(123.456, buf, out int len, precision: 2);

            Assert.True(success);
            Assert.Equal("123.46", buf.Slice(0, len).ToString());

            success = FastNumberFormatter.TryFormatDouble(-45.1, buf, out len, precision: 1);
            Assert.True(success);
            Assert.Equal("-45.1", buf.Slice(0, len).ToString());
        }

        [Fact]
        public void Formatting_HotLoop_ZeroAllocations()
        {
            Span<char> buf = stackalloc char[32];

            // Warmup
            for (int i = 0; i < 100; i++)
            {
                FastNumberFormatter.TryFormatInt32(i, buf, out _);
                FastNumberFormatter.TryFormatDouble(i * 1.25, buf, out _, 2);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1000; i++)
            {
                FastNumberFormatter.TryFormatInt32(i, buf, out _);
                FastNumberFormatter.TryFormatDouble(i * 1.25, buf, out _, 2);
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.Equal(0, after - before);
        }
    }
}
