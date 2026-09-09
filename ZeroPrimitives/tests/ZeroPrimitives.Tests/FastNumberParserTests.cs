using System;
using Xunit;
using ZeroPrimitives.Parsing;

namespace ZeroPrimitives.Tests
{
    public class FastNumberParserTests
    {
        [Theory]
        [InlineData("0", 0)]
        [InlineData("1", 1)]
        [InlineData("-1", -1)]
        [InlineData("2147483647", int.MaxValue)]
        [InlineData("-2147483648", int.MinValue)]
        [InlineData(" 123 ", 123)]
        [InlineData("+456", 456)]
        [InlineData("1,000,000", 1000000)]
        [InlineData("1.000.000", 1000000)]
        public void TryParseInt32_ValidInputs_MatchesExpected(string input, int expected)
        {
            bool ok = FastNumberParser.TryParseInt32(input.AsSpan(), out int res);
            Assert.True(ok);
            Assert.Equal(expected, res);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("abc")]
        [InlineData("++12")]
        public void TryParseInt32_InvalidInputs_ReturnsFalse(string input)
        {
            bool ok = FastNumberParser.TryParseInt32(input.AsSpan(), out int res, -999);
            Assert.False(ok);
            Assert.Equal(-999, res);
        }

        [Fact]
        public void TryParseInt64_LargeNumbers_ParsesCorrectly()
        {
            long big = 9_000_000_000_000L;
            Assert.True(FastNumberParser.TryParseInt64(big.ToString().AsSpan(), out long res));
            Assert.Equal(big, res);

            long negativeBig = -9_000_000_000_000L;
            Assert.True(FastNumberParser.TryParseInt64(negativeBig.ToString().AsSpan(), out res));
            Assert.Equal(negativeBig, res);
        }

        [Fact]
        public void TryParseDecimal_CurrencySymbolsAndFormatting()
        {
            Assert.True(FastNumberParser.TryParseDecimal(" 1,500,000.50 VNĐ ".AsSpan(), out decimal res));
            Assert.Equal(1500000.50m, res);

            Assert.True(FastNumberParser.TryParseDecimal(" $1,234.56 ".AsSpan(), out res));
            Assert.Equal(1234.56m, res);

            Assert.True(FastNumberParser.TryParseDecimal("€ 999.99".AsSpan(), out res));
            Assert.Equal(999.99m, res);
        }
    }
}
