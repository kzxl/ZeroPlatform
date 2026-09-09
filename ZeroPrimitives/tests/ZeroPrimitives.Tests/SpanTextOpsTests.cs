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
    }
}
