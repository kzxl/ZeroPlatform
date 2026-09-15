using System;
using Xunit;
using ZeroRfid.Core.Codecs;

namespace ZeroRfid.Tests;

public class EpcCodecTests
{
    [Theory]
    [InlineData("E28011602000000000000001", true)]
    [InlineData("3034257BF400B7800004D2AC", true)]
    [InlineData("e28011602000", true)]
    [InlineData("GG801160", false)]
    [InlineData("123", false)] // Odd length
    [InlineData("", false)]
    public void IsValidHex_ReturnsExpected(string input, bool expected)
    {
        bool actual = EpcHexCodec.IsValidHex(input.AsSpan());
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Normalize_CleansPunctuationAndConvertsUppercase()
    {
        string raw = "e280-1160:2000 0001";
        string normalized = EpcHexCodec.Normalize(raw);
        Assert.Equal("E280116020000001", normalized);
    }

    [Fact]
    public void ToBytesAndToHexString_RoundTripsAccurately()
    {
        string originalHex = "3034257BF400B7800004D2AC";
        byte[] bytes = EpcHexCodec.ToBytes(originalHex.AsSpan());
        Assert.Equal(12, bytes.Length);

        string roundTripHex = EpcHexCodec.ToHexString(bytes);
        Assert.Equal(originalHex, roundTripHex);
    }

    [Fact]
    public void PadToWordLength_PadsToExpectedWordBoundary()
    {
        string input = "E28011";
        // 6 words = 24 hex characters
        string padded = EpcHexCodec.PadToWordLength(input, 6);
        Assert.Equal(24, padded.Length);
        Assert.StartsWith("E28011", padded);
        Assert.EndsWith("000000000000000000", padded);
    }

    [Fact]
    public void Gs1TdsCodec_IdentifiesSgtin96Scheme()
    {
        string sgtin96Hex = "3034257BF400B7800004D2AC";
        var scheme = Gs1TdsCodec.IdentifyScheme(sgtin96Hex);
        Assert.Equal(Gs1Scheme.Sgtin96, scheme);

        bool parsed = Gs1TdsCodec.TryParseSgtin96(sgtin96Hex, out var info);
        Assert.True(parsed);
        Assert.NotNull(info);
        Assert.Equal(Gs1Scheme.Sgtin96, info!.Scheme);
    }
}
