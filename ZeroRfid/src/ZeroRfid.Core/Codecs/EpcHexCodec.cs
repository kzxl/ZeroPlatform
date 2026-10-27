using System;
using System.Text;

namespace ZeroRfid.Core.Codecs;

/// <summary>
/// Sovereign zero-allocation codec for high-throughput EPC hexadecimal string and byte-span conversions.
/// </summary>
public static class EpcHexCodec
{
    private static readonly char[] HexLookupUpper = "0123456789ABCDEF".ToCharArray();

    /// <summary>
    /// Validates whether a character sequence consists strictly of valid hexadecimal characters (0-9, a-f, A-F).
    /// </summary>
    public static bool IsValidHex(ReadOnlySpan<char> hex)
    {
        if (hex.IsEmpty || (hex.Length % 2 != 0))
            return false;

        for (int i = 0; i < hex.Length; i++)
        {
            char c = hex[i];
            bool isHexChar = (c >= '0' && c <= '9') ||
                             (c >= 'A' && c <= 'F') ||
                             (c >= 'a' && c <= 'f');
            if (!isHexChar)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Normalizes an EPC string by removing white-spaces, colons, dashes, and converting all characters to uppercase.
    /// </summary>
    public static string Normalize(string rawHex)
    {
        if (string.IsNullOrWhiteSpace(rawHex))
            return string.Empty;

        var sb = new StringBuilder(rawHex.Length);
        for (int i = 0; i < rawHex.Length; i++)
        {
            char c = rawHex[i];
            if (c == ' ' || c == '-' || c == ':')
                continue;

            if (c >= 'a' && c <= 'f')
            {
                sb.Append((char)(c - 32)); // to uppercase
            }
            else if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F'))
            {
                sb.Append(c);
            }
            else
            {
                throw new FormatException($"Invalid character '{c}' encountered in EPC hexadecimal sequence.");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Encodes a byte span into an uppercase hexadecimal string.
    /// </summary>
    public static string ToHexString(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
            return string.Empty;

        char[] chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            byte b = bytes[i];
            chars[i * 2] = HexLookupUpper[b >> 4];
            chars[i * 2 + 1] = HexLookupUpper[b & 0x0F];
        }

        return new string(chars);
    }

    /// <summary>
    /// Decodes a hexadecimal span into a byte array.
    /// </summary>
    public static byte[] ToBytes(ReadOnlySpan<char> hex)
    {
        if (hex.IsEmpty)
            return Array.Empty<byte>();

        if (hex.Length % 2 != 0)
            throw new FormatException("Hexadecimal string length must be an even number of characters.");

        byte[] result = new byte[hex.Length / 2];
        for (int i = 0; i < result.Length; i++)
        {
            int high = ParseHexNibble(hex[i * 2]);
            int low = ParseHexNibble(hex[i * 2 + 1]);
            result[i] = (byte)((high << 4) | low);
        }

        return result;
    }

    /// <summary>
    /// Pads or truncates an EPC hex string to a standard word boundary (1 word = 16 bits = 4 hex chars).
    /// </summary>
    public static string PadToWordLength(string hex, int targetWords = 6)
    {
        string norm = Normalize(hex);
        int targetChars = targetWords * 4;
        if (norm.Length == targetChars)
            return norm;

        if (norm.Length < targetChars)
            return norm.PadRight(targetChars, '0');

        return norm.Substring(0, targetChars);
    }

    private static int ParseHexNibble(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        if (c >= 'A' && c <= 'F') return c - 'A' + 10;
        if (c >= 'a' && c <= 'f') return c - 'a' + 10;
        throw new FormatException($"Invalid hex character '{c}'.");
    }
}
