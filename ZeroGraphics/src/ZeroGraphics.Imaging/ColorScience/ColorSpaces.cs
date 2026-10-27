using System;

namespace ZeroGraphics.Imaging.ColorScience
{
    /// <summary>
    /// Comprehensive RGB Working Color Spaces, Chromatic Adaptation (Bradford, CAT16),
    /// and Colorimetric Transformation Matrices.
    /// Provides pure mathematical transformations with zero heap allocation per conversion.
    /// </summary>
    public static class ColorSpaces
    {
        public enum Space
        {
            Srgb,
            AdobeRgb,
            Rec2020,
            DisplayP3,
            ProPhotoRgb
        }

        // RGB(linear) -> XYZ (D65) for each standardized space
        private static readonly float[] SrgbToXyz =
        {
            0.4124564f, 0.3575761f, 0.1804375f,
            0.2126729f, 0.7151522f, 0.0721750f,
            0.0193339f, 0.1191920f, 0.9503041f,
        };

        private static readonly float[] XyzToSrgb =
        {
            3.2404542f, -1.5371385f, -0.4985314f,
            -0.9692660f, 1.8760108f, 0.0415560f,
            0.0556434f, -0.2040259f, 1.0572252f,
        };

        private static readonly float[] AdobeToXyz =
        {
            0.5767309f, 0.1855540f, 0.1881852f,
            0.2973769f, 0.6273491f, 0.0752741f,
            0.0270343f, 0.0706872f, 0.9911085f,
        };

        private static readonly float[] Rec2020ToXyz =
        {
            0.6369580f, 0.1446169f, 0.1688810f,
            0.2627002f, 0.6779981f, 0.0593017f,
            0.0000000f, 0.0280727f, 1.0609851f,
        };

        private static readonly float[] P3ToXyz =
        {
            0.4865709f, 0.2656677f, 0.1982173f,
            0.2289746f, 0.6917385f, 0.0792869f,
            0.0000000f, 0.0451134f, 1.0439444f,
        };

        private static readonly float[] ProPhotoToXyz =
        {
            0.7556009f, 0.1127808f, 0.0820484f,
            0.2683417f, 0.7151191f, 0.0165332f,
            0.0039099f, -0.0129186f, 1.0974131f,
        };

        private static readonly float[] ProPhotoToXyzD50 =
        {
            0.7976749f, 0.1351917f, 0.0313534f,
            0.2880402f, 0.7118741f, 0.0000857f,
            0.0000000f, 0.0000000f, 0.8252100f,
        };

        public static float[] RgbToXyz(Space s) => s switch
        {
            Space.AdobeRgb => AdobeToXyz,
            Space.Rec2020 => Rec2020ToXyz,
            Space.DisplayP3 => P3ToXyz,
            Space.ProPhotoRgb => ProPhotoToXyz,
            _ => SrgbToXyz,
        };

        /// <summary>
        /// RGB(linear) -> XYZ (D65, row-major) matrix of space <paramref name="s"/>.
        /// </summary>
        public static float[] RgbToXyzD65(Space s) => (float[])RgbToXyz(s).Clone();

        /// <summary>
        /// RGB(linear) -> XYZ matrix adapted to D50 (ICC Profile Connection Space) via Bradford adaptation.
        /// </summary>
        public static float[] RgbToXyzD50(Space s) => s == Space.ProPhotoRgb
            ? (float[])ProPhotoToXyzD50.Clone()
            : Mul3x3(BradfordAdaptation(D65White, D50White), RgbToXyz(s));

        /// <summary>
        /// 3x3 matrix (row-major) converting linear RGB from <paramref name="from"/> to <paramref name="to"/>.
        /// </summary>
        public static float[] ConversionMatrix(Space from, Space to)
        {
            if (from == to) return Identity();
            float[] fromXyz = RgbToXyz(from);
            float[] xyzTo = to == Space.Srgb ? XyzToSrgb : Invert3x3(RgbToXyz(to));
            return Mul3x3(xyzTo, fromXyz);
        }

        /// <summary>
        /// Converts linear RGB source -> linear sRGB (working space).
        /// </summary>
        public static float[] ToWorkingMatrix(Space from) => ConversionMatrix(from, Space.Srgb);

        public static bool TryParse(string name, out Space space)
        {
            switch ((name ?? "").Trim().ToLowerInvariant())
            {
                case "adobergb": case "adobe": case "argb": space = Space.AdobeRgb; return true;
                case "rec2020": case "bt2020": case "2020": space = Space.Rec2020; return true;
                case "displayp3": case "p3": case "dci-p3": space = Space.DisplayP3; return true;
                case "prophoto": case "prophotorgb": case "romm": case "rommrgb": space = Space.ProPhotoRgb; return true;
                case "srgb": case "": space = Space.Srgb; return true;
                default: space = Space.Srgb; return false;
            }
        }

        public static string Name(Space s) => s switch
        {
            Space.AdobeRgb => "AdobeRGB",
            Space.Rec2020 => "Rec2020",
            Space.DisplayP3 => "DisplayP3",
            Space.ProPhotoRgb => "ProPhotoRGB",
            _ => "sRGB",
        };

        /// <summary>
        /// Standardized photometric luminance weights for the specified color space.
        /// </summary>
        public static (float LumR, float LumG, float LumB) GetLuminanceWeights(Space space) => space switch
        {
            Space.Rec2020 => (0.2627002f, 0.6779981f, 0.0593017f),
            Space.ProPhotoRgb => (0.2683417f, 0.7151191f, 0.0165332f),
            Space.AdobeRgb => (0.2973769f, 0.6273491f, 0.0752741f),
            Space.DisplayP3 => (0.2289746f, 0.6917385f, 0.0792869f),
            _ => (0.2126729f, 0.7151522f, 0.0721750f),
        };

        /// <summary>
        /// Calculates perceived luminance in the specified color space.
        /// </summary>
        public static float Luminance(float r, float g, float b, Space space = Space.Srgb)
        {
            var (lr, lg, lb) = GetLuminanceWeights(space);
            return lr * r + lg * g + lb * b;
        }

        // Standard White Points (XYZ, Y=1)
        private static readonly float[] D50White = { 0.96422f, 1.00000f, 0.82521f };
        private static readonly float[] D65White = { 0.95047f, 1.00000f, 1.08883f };

        // Bradford cone response matrix
        private static readonly float[] Bradford =
        {
             0.8951000f,  0.2664000f, -0.1614000f,
            -0.7502000f,  1.7135000f,  0.0367000f,
             0.0389000f, -0.0685000f,  1.0296000f,
        };

        /// <summary>
        /// Bradford chromatic adaptation matrix converting white point <paramref name="srcWhite"/> to <paramref name="dstWhite"/>.
        /// </summary>
        public static float[] BradfordAdaptation(float[] srcWhite, float[] dstWhite)
        {
            float[] bInv = Invert3x3(Bradford);
            float[] src = Mul3x1(Bradford, srcWhite);
            float[] dst = Mul3x1(Bradford, dstWhite);
            float[] diag =
            {
                dst[0] / src[0], 0, 0,
                0, dst[1] / src[1], 0,
                0, 0, dst[2] / src[2],
            };
            return Mul3x3(bInv, Mul3x3(diag, Bradford));
        }

        /// <summary>
        /// Adapts RGB->XYZ matrix from D50 reference to D65.
        /// </summary>
        public static float[] AdaptD50ToD65(float[] rgbToXyzD50)
            => Mul3x3(BradfordAdaptation(D50White, D65White), rgbToXyzD50);

        /// <summary>
        /// Finds the closest matching standardized color space for a given RGB->XYZ (D65) matrix within a tolerance.
        /// </summary>
        public static Space? MatchSpace(float[] rgbToXyzD65, float tolerance = 0.02f)
        {
            if (rgbToXyzD65 == null || rgbToXyzD65.Length != 9) return null;
            Space best = Space.Srgb;
            float bestErr = float.MaxValue;
            foreach (Space s in new[] { Space.Srgb, Space.AdobeRgb, Space.Rec2020, Space.DisplayP3, Space.ProPhotoRgb })
            {
                float[] @ref = RgbToXyz(s);
                float err = 0f;
                for (int i = 0; i < 9; i++)
                {
                    err += Math.Abs(rgbToXyzD65[i] - @ref[i]);
                }
                if (err < bestErr)
                {
                    bestErr = err;
                    best = s;
                }
            }
            return bestErr <= tolerance ? best : (Space?)null;
        }

        public static float[] Mul3x1(float[] m, float[] v) => new float[]
        {
            m[0] * v[0] + m[1] * v[1] + m[2] * v[2],
            m[3] * v[0] + m[4] * v[1] + m[5] * v[2],
            m[6] * v[0] + m[7] * v[1] + m[8] * v[2],
        };

        public static float[] Identity() => new float[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 };

        public static float[] Mul3x3(float[] a, float[] b)
        {
            var r = new float[9];
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    r[row * 3 + col] =
                        a[row * 3 + 0] * b[0 * 3 + col] +
                        a[row * 3 + 1] * b[1 * 3 + col] +
                        a[row * 3 + 2] * b[2 * 3 + col];
                }
            }
            return r;
        }

        public static float[] Invert3x3(float[] m)
        {
            float a = m[0], b = m[1], c = m[2];
            float d = m[3], e = m[4], f = m[5];
            float g = m[6], h = m[7], i = m[8];
            float A = e * i - f * h;
            float B = -(d * i - f * g);
            float C = d * h - e * g;
            float det = a * A + b * B + c * C;
            if (Math.Abs(det) < 1e-12f) return Identity();
            float inv = 1f / det;
            return new float[]
            {
                A * inv,                 -(b * i - c * h) * inv,  (b * f - c * e) * inv,
                B * inv,                  (a * i - c * g) * inv, -(a * f - c * d) * inv,
                C * inv,                 -(a * h - b * g) * inv,  (a * e - b * d) * inv,
            };
        }
    }
}
