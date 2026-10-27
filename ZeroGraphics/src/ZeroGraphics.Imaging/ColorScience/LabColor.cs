using System;

namespace ZeroGraphics.Imaging.ColorScience
{
    /// <summary>
    /// Represents a color in the CIE-XYZ color space under D65 standard illuminant.
    /// Reference white: Xn = 95.0489, Yn = 100.0000, Zn = 108.8840.
    /// </summary>
    public readonly struct XyzColor
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public XyzColor(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString() => $"XYZ({X:F3}, {Y:F3}, {Z:F3})";
    }

    /// <summary>
    /// Represents a color in the device-independent CIE L*a*b* (CIELAB) color space.
    /// Standardized by the International Commission on Illumination (CIE) for uniform human color perception.
    /// L*: Lightness [0..100]
    /// a*: Green (-) to Red (+) [-128..128]
    /// b*: Blue (-) to Yellow (+) [-128..128]
    /// </summary>
    public readonly struct LabColor
    {
        // D65 standard illuminant reference white points (normalized to Y = 100)
        public const double Xn = 95.0489;
        public const double Yn = 100.0000;
        public const double Zn = 108.8840;

        public double L { get; }
        public double A { get; }
        public double B { get; }

        public LabColor(double l, double a, double b)
        {
            L = l;
            A = a;
            B = b;
        }

        /// <summary>
        /// Creates a LabColor from standard sRGB [0..255] channels using ITU-R BT.709 D65 white point.
        /// </summary>
        public static LabColor FromRgb(byte r, byte g, byte b)
        {
            var xyz = RgbToXyz(r, g, b);
            return XyzToLab(xyz);
        }

        /// <summary>
        /// Converts this LabColor back to standard sRGB [0..255] channels.
        /// Clamps out-of-gamut values to [0..255].
        /// </summary>
        public void ToRgb(out byte r, out byte g, out byte b)
        {
            var xyz = ToXyz();
            XyzToRgb(xyz, out r, out g, out b);
        }

        /// <summary>
        /// Converts this LabColor to CIE-XYZ color space.
        /// </summary>
        public XyzColor ToXyz()
        {
            double fy = (L + 16.0) / 116.0;
            double fx = A / 500.0 + fy;
            double fz = fy - B / 200.0;

            const double delta = 6.0 / 29.0;

            double x = fx > delta ? fx * fx * fx : 3.0 * delta * delta * (fx - 4.0 / 29.0);
            double y = fy > delta ? fy * fy * fy : 3.0 * delta * delta * (fy - 4.0 / 29.0);
            double z = fz > delta ? fz * fz * fz : 3.0 * delta * delta * (fz - 4.0 / 29.0);

            return new XyzColor(x * Xn, y * Yn, z * Zn);
        }

        /// <summary>
        /// Converts CIE-XYZ color to CIE L*a*b*.
        /// </summary>
        public static LabColor XyzToLab(XyzColor xyz)
        {
            double xr = xyz.X / Xn;
            double yr = xyz.Y / Yn;
            double zr = xyz.Z / Zn;

            const double delta = 6.0 / 29.0;
            const double delta3 = delta * delta * delta; // ~0.008856
            const double factor = 1.0 / (3.0 * delta * delta);

            double fx = xr > delta3 ? Math.Pow(xr, 1.0 / 3.0) : factor * xr + 4.0 / 29.0;
            double fy = yr > delta3 ? Math.Pow(yr, 1.0 / 3.0) : factor * yr + 4.0 / 29.0;
            double fz = zr > delta3 ? Math.Pow(zr, 1.0 / 3.0) : factor * zr + 4.0 / 29.0;

            double l = 116.0 * fy - 16.0;
            double a = 500.0 * (fx - fy);
            double b = 200.0 * (fy - fz);

            return new LabColor(l, a, b);
        }

        /// <summary>
        /// Converts sRGB bytes to CIE-XYZ with gamma linearization (IEC 61966-2-1).
        /// </summary>
        public static XyzColor RgbToXyz(byte r, byte g, byte b)
        {
            // Linearize gamma
            double rLin = LinearizeRgbChannel(r / 255.0);
            double gLin = LinearizeRgbChannel(g / 255.0);
            double bLin = LinearizeRgbChannel(b / 255.0);

            // sRGB D65 transformation matrix
            double x = (rLin * 0.4124564 + gLin * 0.3575761 + bLin * 0.1804375) * 100.0;
            double y = (rLin * 0.2126729 + gLin * 0.7151522 + bLin * 0.0721750) * 100.0;
            double z = (rLin * 0.0193339 + gLin * 0.1191920 + bLin * 0.9503041) * 100.0;

            return new XyzColor(x, y, z);
        }

        /// <summary>
        /// Converts CIE-XYZ to sRGB bytes with gamma compression.
        /// </summary>
        public static void XyzToRgb(XyzColor xyz, out byte r, out byte g, out byte b)
        {
            double x = xyz.X / 100.0;
            double y = xyz.Y / 100.0;
            double z = xyz.Z / 100.0;

            // Inverse sRGB D65 transformation matrix
            double rLin = x * 3.2404542 + y * -1.5371385 + z * -0.4985314;
            double gLin = x * -0.9692660 + y * 1.8760108 + z * 0.0415560;
            double bLin = x * 0.0556434 + y * -0.2040259 + z * 1.0572252;

            r = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(DelinearizeRgbChannel(rLin) * 255.0)));
            g = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(DelinearizeRgbChannel(gLin) * 255.0)));
            b = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(DelinearizeRgbChannel(bLin) * 255.0)));
        }

        private static double LinearizeRgbChannel(double v)
        {
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static double DelinearizeRgbChannel(double v)
        {
            v = Math.Max(0.0, v);
            return v <= 0.0031308 ? v * 12.92 : 1.055 * Math.Pow(v, 1.0 / 2.4) - 0.055;
        }

        public override string ToString() => $"Lab(L={L:F2}, a={A:F2}, b={B:F2})";
    }
}
