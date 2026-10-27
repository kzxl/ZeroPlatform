using System;

namespace ZeroSecurity.Common;

/// <summary>
/// Ultra-fast, zero-allocation hex conversion utilities operating directly on spans.
/// </summary>
public static class FastHex
{
    private static readonly char[] HexLookupLower = "0123456789abcdef".ToCharArray();
    private static readonly char[] HexLookupUpper = "0123456789ABCDEF".ToCharArray();

    /// <summary>
    /// Converts a byte span to a lowercase hex string.
    /// </summary>
    public static string ToHex(ReadOnlySpan<byte> bytes, bool lowerCase = true)
    {
        if (bytes.IsEmpty) return string.Empty;

        var lookup = lowerCase ? HexLookupLower : HexLookupUpper;
#if NET8_0_OR_GREATER
        return string.Create(bytes.Length * 2, (bytes.ToArray(), lookup), (span, state) =>
        {
            var (src, lut) = state;
            for (int i = 0; i < src.Length; i++)
            {
                byte b = src[i];
                span[i * 2] = lut[b >> 4];
                span[i * 2 + 1] = lut[b & 0x0F];
            }
        });
#else
        char[] chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            chars[i * 2] = lookup[b >> 4];
            chars[i * 2 + 1] = lookup[b & 0x0F];
        }
        return new string(chars);
#endif
    }

    /// <summary>
    /// Encodes a byte span into a destination char span.
    /// </summary>
    public static void Encode(ReadOnlySpan<byte> bytes, Span<char> destination, bool lowerCase = true)
    {
        if (destination.Length < bytes.Length * 2)
            throw new ArgumentException("Destination span too small.", nameof(destination));

        var lookup = lowerCase ? HexLookupLower : HexLookupUpper;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            destination[i * 2] = lookup[b >> 4];
            destination[i * 2 + 1] = lookup[b & 0x0F];
        }
    }

    /// <summary>
    /// Parses a hex string into a destination byte span.
    /// </summary>
    public static bool TryDecode(ReadOnlySpan<char> hex, Span<byte> destination, out int bytesWritten)
    {
        bytesWritten = 0;
        if (hex.Length % 2 != 0 || destination.Length < hex.Length / 2)
            return false;

        for (int i = 0; i < hex.Length; i += 2)
        {
            int hi = HexVal(hex[i]);
            int lo = HexVal(hex[i + 1]);
            if (hi == -1 || lo == -1)
                return false;

            destination[bytesWritten++] = (byte)((hi << 4) | lo);
        }

        return true;
    }

    private static int HexVal(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        return -1;
    }
}
