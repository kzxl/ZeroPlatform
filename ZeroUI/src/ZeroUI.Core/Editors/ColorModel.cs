using System;
using System.Globalization;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Headless, cross-platform 32-bit RGBA color representation for ZeroUI.
    /// Provides zero-allocation hex parsing/formatting, perceptual luminance calculation,
    /// and automatic contrasting text color determination.
    /// </summary>
    public readonly struct ZeroColor : IEquatable<ZeroColor>
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }
        public byte A { get; }

        public static readonly ZeroColor Transparent = new ZeroColor(0, 0, 0, 0);
        public static readonly ZeroColor Black = new ZeroColor(0, 0, 0, 255);
        public static readonly ZeroColor White = new ZeroColor(255, 255, 255, 255);

        public ZeroColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public float GetLuminance()
        {
            // Standard Rec. 709 relative luminance
            return (0.2126f * R + 0.7152f * G + 0.0722f * B) / 255f;
        }

        public bool IsDarkColor => GetLuminance() < 0.45f;

        public string GetContrastingHex() => IsDarkColor ? "#FFFFFF" : "#0F172A";

        public string ToHex(bool includeAlpha = false)
        {
            return includeAlpha
                ? string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}", A, R, G, B)
                : string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", R, G, B);
        }

        public override string ToString() => ToHex();

        public bool Equals(ZeroColor other)
        {
            return R == other.R && G == other.G && B == other.B && A == other.A;
        }

        public override bool Equals(object? obj) => obj is ZeroColor other && Equals(other);

        public override int GetHashCode()
        {
            return (A << 24) | (R << 16) | (G << 8) | B;
        }

        public static bool operator ==(ZeroColor left, ZeroColor right) => left.Equals(right);
        public static bool operator !=(ZeroColor left, ZeroColor right) => !left.Equals(right);

        public static ZeroColor FromHex(string hex)
        {
            if (TryParseHex(hex, out var color))
                return color;
            throw new FormatException($"Invalid color hex: '{hex}'");
        }

        public static bool TryParseHex(string? hex, out ZeroColor color)
        {
            color = Black;
            if (string.IsNullOrWhiteSpace(hex)) return false;

            var clean = hex!.Trim();
            if (clean.StartsWith("#", StringComparison.Ordinal))
                clean = clean.Substring(1);

            if (clean.Length == 3) // #RGB -> #RRGGBB
            {
                if (byte.TryParse(new string(clean[0], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                    byte.TryParse(new string(clean[1], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                    byte.TryParse(new string(clean[2], 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    color = new ZeroColor(r, g, b, 255);
                    return true;
                }
            }
            else if (clean.Length == 6) // #RRGGBB
            {
                if (byte.TryParse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                    byte.TryParse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                    byte.TryParse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    color = new ZeroColor(r, g, b, 255);
                    return true;
                }
            }
            else if (clean.Length == 8) // #AARRGGBB
            {
                if (byte.TryParse(clean.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte a) &&
                    byte.TryParse(clean.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                    byte.TryParse(clean.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                    byte.TryParse(clean.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    color = new ZeroColor(r, g, b, a);
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Utility methods for cross-platform color operations.
    /// </summary>
    public static class ZeroColorUtils
    {
        public static string NormalizeHex(string? hex, string defaultHex = "#000000")
        {
            if (ZeroColor.TryParseHex(hex, out var c))
                return c.ToHex();
            return defaultHex;
        }

        public static string GetContrastingTextColor(string? hex)
        {
            if (ZeroColor.TryParseHex(hex, out var c))
                return c.GetContrastingHex();
            return "#FFFFFF";
        }
    }
}
