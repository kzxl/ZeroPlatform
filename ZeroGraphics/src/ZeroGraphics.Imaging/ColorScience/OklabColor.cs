using System;
using System.Runtime.CompilerServices;

namespace ZeroGraphics.Imaging.ColorScience
{
    /// <summary>
    /// High-precision, perceptual OKLab and OKLCh color model with constant-hue gamut compression.
    /// Based on Björn Ottosson's 2020 OKLab formulation, providing superior hue linearity and uniform lightness.
    /// </summary>
    public static class OklabColor
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Cbrt(float v)
        {
#if NETCOREAPP || NET8_0_OR_GREATER
            return MathF.Cbrt(v);
#else
            return v >= 0f ? (float)Math.Pow(v, 1.0 / 3.0) : -(float)Math.Pow(-v, 1.0 / 3.0);
#endif
        }

        /// <summary>
        /// Converts linear sRGB [0..1] to OKLab (L in [0..1], a, b in [-0.5..0.5]).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LinearRgbToOklab(float r, float g, float b, out float L, out float a, out float bOut)
        {
            float cr = r < 0f ? 0f : r;
            float cg = g < 0f ? 0f : g;
            float cb = b < 0f ? 0f : b;

            float l = 0.4122214708f * cr + 0.5363325363f * cg + 0.0514459929f * cb;
            float m = 0.2119034982f * cr + 0.6806995451f * cg + 0.1073969566f * cb;
            float s = 0.0883024619f * cr + 0.2817188376f * cg + 0.6299787005f * cb;

            float l_ = Cbrt(l);
            float m_ = Cbrt(m);
            float s_ = Cbrt(s);

            L = 0.2104542553f * l_ + 0.7936177850f * m_ - 0.0040720468f * s_;
            a = 1.9779984951f * l_ - 2.4285922050f * m_ + 0.4505937099f * s_;
            bOut = 0.0259040371f * l_ + 0.7827717662f * m_ - 0.8086757660f * s_;
        }

        /// <summary>
        /// Converts OKLab (L, a, b) to linear sRGB.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OklabToLinearRgb(float L, float a, float bIn, out float r, out float g, out float b)
        {
            float l_ = L + 0.3963377774f * a + 0.2158037573f * bIn;
            float m_ = L - 0.1055613458f * a - 0.0638541728f * bIn;
            float s_ = L - 0.0894841775f * a - 1.2914855480f * bIn;

            float l = l_ * l_ * l_;
            float m = m_ * m_ * m_;
            float s = s_ * s_ * s_;

            r = +4.0767439362f * l - 3.3077115913f * m + 0.2309699291f * s;
            g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
            b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
        }

        /// <summary>
        /// Checks if a linear sRGB triple is strictly within [0..1] range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsInGamut(float r, float g, float b, float tol = 1e-4f)
        {
            return r >= -tol && r <= 1f + tol &&
                   g >= -tol && g <= 1f + tol &&
                   b >= -tol && b <= 1f + tol;
        }

        /// <summary>
        /// Compresses chroma along constant perceptual hue (a, b) towards neutral white/black
        /// until the color fits strictly within linear sRGB gamut [0..1].
        /// Prevents highlight hue shifting and channel clipping blowout.
        /// </summary>
        public static void CompressToGamut(ref float L, ref float a, ref float bIn)
        {
            if (L < 0f) L = 0f;
            else if (L > 1f) L = 1f;

            if (L <= 1e-5f || L >= 1f - 1e-5f)
            {
                a = 0f;
                bIn = 0f;
                return;
            }

            OklabToLinearRgb(L, a, bIn, out float r, out float g, out float b);
            if (IsInGamut(r, g, b)) return;

            // Binary search for maximum permissible chroma scale factor t in [0..1]
            float low = 0f;
            float high = 1f;
            float bestT = 0f;

            for (int iter = 0; iter < 8; iter++)
            {
                float mid = 0.5f * (low + high);
                OklabToLinearRgb(L, a * mid, bIn * mid, out float tr, out float tg, out float tb);
                if (IsInGamut(tr, tg, tb))
                {
                    bestT = mid;
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            a *= bestT;
            bIn *= bestT;
        }
    }
}
