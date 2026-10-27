using System;
using Xunit;
using ZeroText.Matching;

namespace ZeroText.Tests
{
    public class FastFuzzyMatchTests
    {
        [Theory]
        [InlineData("", "", 0)]
        [InlineData("a", "", 1)]
        [InlineData("", "abc", 3)]
        [InlineData("kitten", "sitting", 3)]
        [InlineData("flaw", "lawn", 2)]
        [InlineData("gumbo", "gambol", 2)]
        [InlineData("book", "back", 2)]
        public void LevenshteinDistance_KnownPairs_ComputesCorrectDistance(string s1, string s2, int expected)
        {
            int distance = FastFuzzyMatch.LevenshteinDistance(s1.AsSpan(), s2.AsSpan());
            Assert.Equal(expected, distance);

            // Extension method check
            Assert.Equal(expected, s1.LevenshteinDistance(s2));
        }

        [Fact]
        public void LevenshteinDistance_CaseSensitivity_Respected()
        {
            Assert.Equal(1, FastFuzzyMatch.LevenshteinDistance("abc".AsSpan(), "Abc".AsSpan(), ignoreCase: false));
            Assert.Equal(0, FastFuzzyMatch.LevenshteinDistance("abc".AsSpan(), "Abc".AsSpan(), ignoreCase: true));
        }

        [Fact]
        public void LevenshteinSimilarity_CalculatesNormalizedRatio()
        {
            Assert.Equal(1.0, FastFuzzyMatch.LevenshteinSimilarity("".AsSpan(), "".AsSpan()));
            Assert.Equal(1.0, FastFuzzyMatch.LevenshteinSimilarity("apple".AsSpan(), "apple".AsSpan()));

            // "kitten" vs "sitting" (maxLen 7, dist 3 -> similarity = 1 - 3/7 = 4/7 ~ 0.5714)
            double sim = FastFuzzyMatch.LevenshteinSimilarity("kitten".AsSpan(), "sitting".AsSpan());
            Assert.InRange(sim, 0.571, 0.572);
        }

        [Fact]
        public void LevenshteinDistance_LargeStrings_UsesArrayPoolCorrectly()
        {
            string long1 = new string('A', 300) + "XYZ";
            string long2 = new string('A', 300) + "ABC";

            int distance = FastFuzzyMatch.LevenshteinDistance(long1.AsSpan(), long2.AsSpan());
            Assert.Equal(3, distance);
        }

        [Theory]
        [InlineData("MARTHA", "MARHTA", 0.944, 0.961)]
        [InlineData("DWAYNE", "DUANE", 0.822, 0.840)]
        [InlineData("DIXON", "DICKSONX", 0.767, 0.813)]
        public void JaroAndJaroWinkler_StandardBenchmarks_MatchExpectedRanges(
            string s1, string s2, double expectedJaro, double expectedJaroWinkler)
        {
            double jaro = FastFuzzyMatch.JaroSimilarity(s1.AsSpan(), s2.AsSpan());
            double jaroWinkler = FastFuzzyMatch.JaroWinklerSimilarity(s1.AsSpan(), s2.AsSpan());

            Assert.InRange(jaro, expectedJaro - 0.01, expectedJaro + 0.01);
            Assert.InRange(jaroWinkler, expectedJaroWinkler - 0.01, expectedJaroWinkler + 0.01);

            // Extension method check
            double extJaroWinkler = s1.JaroWinklerSimilarity(s2);
            Assert.InRange(extJaroWinkler, expectedJaroWinkler - 0.01, expectedJaroWinkler + 0.01);
        }

        [Fact]
        public void JaroWinkler_CompletelyDissimilar_ReturnsZero()
        {
            Assert.Equal(0.0, FastFuzzyMatch.JaroWinklerSimilarity("ABCD".AsSpan(), "WXYZ".AsSpan()));
        }

        [Fact]
        public void LevenshteinDistance_MyersBitParallel_Boundary64AndUnicode_MatchesExactDistance()
        {
            // Reduced pattern M = 64 boundary
            string p64 = new string('a', 31) + "X" + new string('b', 31) + "Y"; // 64 chars
            string t64 = "Z" + new string('a', 31) + "W" + new string('b', 31) + "Q"; // 65 chars
            int dist64 = FastFuzzyMatch.LevenshteinDistance(t64.AsSpan(), p64.AsSpan());
            Assert.Equal(3, dist64);

            // Reduced pattern M = 65 (exercises > 64 DP fallback)
            string p65 = "M" + p64;
            string t65 = "N" + t64;
            int dist65 = FastFuzzyMatch.LevenshteinDistance(t65.AsSpan(), p65.AsSpan());
            Assert.Equal(4, dist65);

            // High Unicode characters (Vietnamese) with ignoreCase
            int vnDistCaseSensitive = FastFuzzyMatch.LevenshteinDistance("Nguyễn Văn Ánh".AsSpan(), "nguyễn văn ánh".AsSpan(), ignoreCase: false);
            int vnDistIgnoreCase = FastFuzzyMatch.LevenshteinDistance("Nguyễn Văn Ánh".AsSpan(), "nguyễn văn ánh".AsSpan(), ignoreCase: true);
            Assert.Equal(3, vnDistCaseSensitive);
            Assert.Equal(0, vnDistIgnoreCase);
        }
    }
}
