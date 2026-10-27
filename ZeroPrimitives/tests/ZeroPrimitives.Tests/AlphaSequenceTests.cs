using System;
using Xunit;
using ZeroPrimitives.Text;

namespace ZeroPrimitives.Tests
{
    public class AlphaSequenceTests
    {
        [Theory]
        [InlineData("", "A")]
        [InlineData(null, "A")]
        [InlineData("A", "B")]
        [InlineData("B", "C")]
        [InlineData("Y", "Z")]
        [InlineData("Z", "AA")]
        [InlineData("AA", "AB")]
        [InlineData("AB", "AC")]
        [InlineData("AZ", "BA")]
        [InlineData("BA", "BB")]
        [InlineData("BZ", "CA")]
        [InlineData("ZZ", "AAA")]
        [InlineData("AAA", "AAB")]
        [InlineData("AAZ", "ABA")]
        [InlineData("AZZ", "BAA")]
        [InlineData("ZZZ", "AAAA")]
        public void Increment_StandardSequences_ProducesExpectedResults(string? input, string expected)
        {
            string result = AlphaSequence.Increment(input);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Increment_LowercaseInput_NormalizesToUppercase()
        {
            Assert.Equal("B", AlphaSequence.Increment("a"));
            Assert.Equal("AA", AlphaSequence.Increment("z"));
            Assert.Equal("BA", AlphaSequence.Increment("az"));
            Assert.Equal("AAA", AlphaSequence.Increment("zz"));
        }

        [Fact]
        public void TryIncrement_ZeroAllocationBuffer_Success()
        {
            Span<char> output = stackalloc char[10];

            Assert.True(AlphaSequence.TryIncrement("AZ".AsSpan(), output, out int written));
            Assert.Equal(2, written);
            Assert.Equal("BA", new string(output.Slice(0, written).ToArray()));

            Assert.True(AlphaSequence.TryIncrement("ZZ".AsSpan(), output, out written));
            Assert.Equal(3, written);
            Assert.Equal("AAA", new string(output.Slice(0, written).ToArray()));
        }

        [Fact]
        public void TryIncrement_BufferTooSmall_ReturnsFalse()
        {
            Span<char> output = stackalloc char[1];
            // "Z" needs 2 chars ("AA"), but buffer is length 1
            Assert.False(AlphaSequence.TryIncrement("Z".AsSpan(), output, out int written));
            Assert.Equal(0, written);
        }
    }
}
