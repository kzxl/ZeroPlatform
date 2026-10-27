using System;
using Xunit;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class ValueStringBuilderTests
    {
        [Fact]
        public void StackBuffer_AppendCharactersAndStrings_WorksCorrectly()
        {
            Span<char> initialBuffer = stackalloc char[32];
            using var sb = new ValueStringBuilder(initialBuffer);

            sb.Append("Hello");
            sb.Append(' ');
            sb.Append("World!");

            Assert.Equal("Hello World!", sb.ToString());
            Assert.Equal(12, sb.Length);
        }

        [Fact]
        public void SpilloverToArrayPool_ExpandsWithoutDataLoss()
        {
            Span<char> initialBuffer = stackalloc char[8];
            using var sb = new ValueStringBuilder(initialBuffer);

            sb.Append("12345678"); // Fills initial buffer
            sb.Append("90ABCDEF"); // Triggers ArrayPool expansion

            Assert.Equal("1234567890ABCDEF", sb.ToString());
            Assert.Equal(16, sb.Length);
            Assert.True(sb.Capacity >= 16);
        }

        [Fact]
        public void NumericAppends_FormatsAccurately()
        {
            Span<char> initialBuffer = stackalloc char[64];
            using var sb = new ValueStringBuilder(initialBuffer);

            sb.Append("Integer: ");
            sb.Append(12345);
            sb.Append(", Long: ");
            sb.Append(9876543210L);
            sb.Append(", Decimal: ");
            sb.Append(1234.56m);
            sb.Append(", Double: ");
            sb.Append(45.67);
            sb.Append(", Bool: ");
            sb.Append(true);

            string result = sb.ToString();
            Assert.Contains("Integer: 12345", result);
            Assert.Contains("Long: 9876543210", result);
            Assert.Contains("Decimal: 1234.56", result);
            Assert.Contains("Double: 45.67", result);
            Assert.Contains("Bool: True", result);
        }

        [Fact]
        public void ClearAndIndex_WorksAsExpected()
        {
            Span<char> initialBuffer = stackalloc char[16];
            using var sb = new ValueStringBuilder(initialBuffer);

            sb.Append("Test");
            Assert.Equal('T', sb[0]);
            Assert.Equal('s', sb[2]);

            sb.Clear();
            Assert.Equal(0, sb.Length);

            sb.Append("Reused");
            Assert.Equal("Reused", sb.ToString());
        }
    }
}
