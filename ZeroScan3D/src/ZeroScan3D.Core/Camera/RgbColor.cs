using System;

namespace ZeroScan3D.Core.Camera
{
    /// <summary>
    /// Represents an 8-bit per channel RGB color value.
    /// </summary>
    public readonly struct RgbColor : IEquatable<RgbColor>
    {
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public static readonly RgbColor Black = new RgbColor(0, 0, 0);
        public static readonly RgbColor White = new RgbColor(255, 255, 255);
        public static readonly RgbColor Gray = new RgbColor(128, 128, 128);

        public RgbColor(byte r, byte g, byte b)
        {
            R = r;
            G = g;
            B = b;
        }

        public bool Equals(RgbColor other) => R == other.R && G == other.G && B == other.B;
        public override bool Equals(object? obj) => obj is RgbColor other && Equals(other);
        public override int GetHashCode() => unchecked((R << 16) | (G << 8) | B);
        public override string ToString() => $"RGB({R}, {G}, {B})";

        public static bool operator ==(RgbColor left, RgbColor right) => left.Equals(right);
        public static bool operator !=(RgbColor left, RgbColor right) => !left.Equals(right);
    }
}
