using System;
using Xunit;
using ZeroUI.Core.Editors;

namespace ZeroUI.Core.Tests
{
    public class MathExpressionParserTests
    {
        [Theory]
        [InlineData("25 * 4", 100)]
        [InlineData("100 / 2 + 10", 60)]
        [InlineData("(15 + 5) * 2", 40)]
        [InlineData("12.5 * 2", 25)]
        [InlineData("10 + 2 * 6", 22)]
        [InlineData("(10 + 2) * 6", 72)]
        [InlineData("-5 + 15", 10)]
        [InlineData("2 ^ 3", 8)]
        [InlineData("10 % 3", 1)]
        [InlineData("  50   +   50  ", 100)]
        public void TryEvaluate_ValidExpressions_ReturnCorrectResult(string expr, decimal expected)
        {
            bool success = MathExpressionParser.TryEvaluate(expr, out decimal result);

            Assert.True(success);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("10 / 0")]
        [InlineData("abc * 2")]
        [InlineData("(10 + 2")]
        [InlineData("++")]
        [InlineData("")]
        [InlineData("   ")]
        public void TryEvaluate_InvalidExpressions_ReturnsFalse(string expr)
        {
            bool success = MathExpressionParser.TryEvaluate(expr, out decimal result);

            Assert.False(success);
            Assert.Equal(0m, result);
        }
    }
}
