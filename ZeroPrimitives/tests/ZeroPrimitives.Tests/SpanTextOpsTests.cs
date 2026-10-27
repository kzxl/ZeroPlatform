using System;
using Xunit;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class SpanTextOpsTests
    {
        [Fact]
        public void CleanCurrency_StripsVariousSymbolsAndWhitespace()
        {
            Assert.Equal("1500000", SpanTextOps.CleanCurrency(" 1500000 VNĐ ".AsSpan()).ToString());
            Assert.Equal("1500000", SpanTextOps.CleanCurrency(" 1500000 VND ".AsSpan()).ToString());
            Assert.Equal("1500000", SpanTextOps.CleanCurrency(" 1500000 đ ".AsSpan()).ToString());
            Assert.Equal("1500000", SpanTextOps.CleanCurrency(" 1500000 ₫ ".AsSpan()).ToString());
            Assert.Equal("250", SpanTextOps.CleanCurrency(" $ 250 ".AsSpan()).ToString());
            Assert.Equal("350", SpanTextOps.CleanCurrency(" €350 ".AsSpan()).ToString());
            Assert.Equal("450", SpanTextOps.CleanCurrency(" ¥450 ".AsSpan()).ToString());
            Assert.Equal("550", SpanTextOps.CleanCurrency(" £550 ".AsSpan()).ToString());
            Assert.Equal("100", SpanTextOps.CleanCurrency(" 100 USD ".AsSpan()).ToString());
        }

        [Fact]
        public void BytesToHex_And_TryHexToBytes_Roundtrip()
        {
            byte[] original = new byte[] { 0x00, 0x1A, 0x2B, 0xFF, 0xDE, 0xAD, 0xBE, 0xEF };
            Span<char> hexChars = stackalloc char[original.Length * 2];

            int written = SpanTextOps.BytesToHex(original.AsSpan(), hexChars);
            Assert.Equal(16, written);
            Assert.Equal("001A2BFFDEADBEEF", hexChars.ToString());

            Span<byte> decoded = stackalloc byte[original.Length];
            Assert.True(SpanTextOps.TryHexToBytes(hexChars, decoded, out int bytesWritten));
            Assert.Equal(original.Length, bytesWritten);
            Assert.True(original.AsSpan().SequenceEqual(decoded));
        }

        [Fact]
        public void TryHexToBytes_InvalidLengthOrChars_FailsSafely()
        {
            Span<byte> buf = stackalloc byte[10];
            Assert.False(SpanTextOps.TryHexToBytes("123".AsSpan(), buf, out _)); // Odd length
            Assert.False(SpanTextOps.TryHexToBytes("12ZZ".AsSpan(), buf, out _)); // Non-hex chars
        }

        [Fact]
        public void IsDigitsOnly_ValidatesCorrectly()
        {
            Assert.True(SpanTextOps.IsDigitsOnly("123456"));
            Assert.True(SpanTextOps.IsDigitsOnly("0"));
            Assert.False(SpanTextOps.IsDigitsOnly("123a"));
            Assert.False(SpanTextOps.IsDigitsOnly(""));
            Assert.False(SpanTextOps.IsDigitsOnly((string?)null));
            Assert.False(SpanTextOps.IsDigitsOnly("-123"));
            Assert.False(SpanTextOps.IsDigitsOnly(" 123 "));
        }

        [Fact]
        public void IsInteger_ValidatesCorrectly()
        {
            Assert.True(SpanTextOps.IsInteger("123456"));
            Assert.True(SpanTextOps.IsInteger("-123", allowNegative: true));
            Assert.False(SpanTextOps.IsInteger("-123", allowNegative: false));
            Assert.False(SpanTextOps.IsInteger("12.34"));
            Assert.False(SpanTextOps.IsInteger("abc"));
            Assert.False(SpanTextOps.IsInteger(""));
        }

        [Fact]
        public void FastConvert_Round_WorksWithMultipleTypes()
        {
            Assert.Equal(12.35m, FastConvert.Round(12.3456m, 2));
            Assert.Equal(12.35m, FastConvert.Round(12.3456d, 2));
            Assert.Equal(12.00m, FastConvert.Round("12.004", 2));
            Assert.Equal(0m, FastConvert.Round(null, 2));
            Assert.Equal(0m, FastConvert.Round(DBNull.Value, 2));
        }
    }
}
