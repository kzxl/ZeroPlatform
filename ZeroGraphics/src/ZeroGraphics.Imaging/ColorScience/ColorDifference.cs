using System;

namespace ZeroGraphics.Imaging.ColorScience
{
    /// <summary>
    /// Quality classification grades for industrial color matching based on Delta E tolerances.
    /// </summary>
    public enum ColorToleranceGrade
    {
        /// <summary>
        /// Delta E &lt; 1.0: Color difference is invisible to the average human eye.
        /// </summary>
        Imperceptible,

        /// <summary>
        /// 1.0 &lt;= Delta E &lt; 2.0: Perceptible through close observation, standard commercial tolerance pass.
        /// </summary>
        Perceptible,

        /// <summary>
        /// 2.0 &lt;= Delta E &lt; 3.5: Noticeable difference, acceptable in low-precision industrial batches.
        /// </summary>
        AcceptableIndustrial,

        /// <summary>
        /// Delta E &gt;= 3.5: Significant visual disparity, rejected defect (Fail).
        /// </summary>
        Unacceptable
    }

    /// <summary>
    /// Detailed comparative inspection results for color metrology.
    /// </summary>
    public readonly struct ColorComparisonResult
    {
        public double DeltaE00 { get; }
        public double DeltaE76 { get; }
        public ColorToleranceGrade Grade { get; }
        public bool IsPassed { get; }

        public ColorComparisonResult(double deltaE00, double deltaE76, ColorToleranceGrade grade, bool isPassed)
        {
            DeltaE00 = deltaE00;
            DeltaE76 = deltaE76;
            Grade = grade;
            IsPassed = isPassed;
        }

        public override string ToString()
            => $"ΔE00={DeltaE00:F3}, ΔE76={DeltaE76:F3}, Grade={Grade}, Passed={IsPassed}";
    }

    /// <summary>
    /// Industrial Color Difference and Metrology Engine.
    /// Implements Euclidean Delta E 1976 (CIE76) and ISO/CIE CIEDE2000 (ISO 11664-6:2014).
    /// </summary>
    public static class ColorDifference
    {
        /// <summary>
        /// Computes Euclidean CIE 1976 color difference: sqrt((dL)^2 + (da)^2 + (db)^2).
        /// Fast approximation suitable for high-throughput coarse filtering.
        /// </summary>
        public static double DeltaE76(LabColor c1, LabColor c2)
        {
            double dl = c1.L - c2.L;
            double da = c1.A - c2.A;
            double db = c1.B - c2.B;
            return Math.Sqrt(dl * dl + da * da + db * db);
        }

        /// <summary>
        /// Computes ISO/CIE standard CIEDE2000 color difference (Delta E 00).
        /// Incorporates lightness, chroma, and hue weighting functions with rotation factor RT.
        /// </summary>
        /// <param name="c1">First color (e.g. Reference / Golden sample).</param>
        /// <param name="c2">Second color (e.g. Measured sample).</param>
        /// <param name="kL">Lightness weighting factor (default 1.0).</param>
        /// <param name="kC">Chroma weighting factor (default 1.0).</param>
        /// <param name="kH">Hue weighting factor (default 1.0).</param>
        public static double DeltaE2000(LabColor c1, LabColor c2, double kL = 1.0, double kC = 1.0, double kH = 1.0)
        {
            // 1. Calculate C* and mean C*
            double c1Star = Math.Sqrt(c1.A * c1.A + c1.B * c1.B);
            double c2Star = Math.Sqrt(c2.A * c2.A + c2.B * c2.B);
            double cBarStar = (c1Star + c2Star) * 0.5;

            // 2. G factor for chroma adjustment
            double cBarStar7 = Math.Pow(cBarStar, 7.0);
            const double pow25_7 = 6103515625.0; // 25^7
            double g = 0.5 * (1.0 - Math.Sqrt(cBarStar7 / (cBarStar7 + pow25_7)));

            // 3. Adjusted a' and C'
            double a1Prime = (1.0 + g) * c1.A;
            double a2Prime = (1.0 + g) * c2.A;

            double c1Prime = Math.Sqrt(a1Prime * a1Prime + c1.B * c1.B);
            double c2Prime = Math.Sqrt(a2Prime * a2Prime + c2.B * c2.B);

            // 4. Hue angles h' in degrees [0..360)
            double h1Prime = ComputeHueAngleDeg(a1Prime, c1.B);
            double h2Prime = ComputeHueAngleDeg(a2Prime, c2.B);

            // 5. Delta L', Delta C', Delta h'
            double deltaLPrime = c2.L - c1.L;
            double deltaCPrime = c2Prime - c1Prime;

            double deltahPrime;
            if (c1Prime * c2Prime < 1e-9)
            {
                deltahPrime = 0.0;
            }
            else
            {
                double diff = h2Prime - h1Prime;
                if (Math.Abs(diff) <= 180.0)
                {
                    deltahPrime = diff;
                }
                else if (diff > 180.0)
                {
                    deltahPrime = diff - 360.0;
                }
                else
                {
                    deltahPrime = diff + 360.0;
                }
            }

            double deltaHPrime = 2.0 * Math.Sqrt(c1Prime * c2Prime) * Math.Sin((deltahPrime * 0.5) * (Math.PI / 180.0));

            // 6. Mean values: LBarPrime, CBarPrime, HBarPrime
            double lBarPrime = (c1.L + c2.L) * 0.5;
            double cBarPrime = (c1Prime + c2Prime) * 0.5;

            double hBarPrime;
            if (c1Prime * c2Prime < 1e-9)
            {
                hBarPrime = h1Prime + h2Prime;
            }
            else
            {
                double sum = h1Prime + h2Prime;
                double diff = Math.Abs(h1Prime - h2Prime);
                if (diff <= 180.0)
                {
                    hBarPrime = sum * 0.5;
                }
                else if (sum < 360.0)
                {
                    hBarPrime = (sum + 360.0) * 0.5;
                }
                else
                {
                    hBarPrime = (sum - 360.0) * 0.5;
                }
            }

            // 7. Weighting functions: SL, SC, SH, T
            double lBarMinus50Sq = (lBarPrime - 50.0) * (lBarPrime - 50.0);
            double sL = 1.0 + (0.015 * lBarMinus50Sq) / Math.Sqrt(20.0 + lBarMinus50Sq);
            double sC = 1.0 + 0.045 * cBarPrime;

            double hBarRad = hBarPrime * (Math.PI / 180.0);
            double t = 1.0 - 0.17 * Math.Cos(hBarRad - 30.0 * (Math.PI / 180.0))
                           + 0.24 * Math.Cos(2.0 * hBarRad)
                           + 0.32 * Math.Cos(3.0 * hBarRad + 6.0 * (Math.PI / 180.0))
                           - 0.20 * Math.Cos(4.0 * hBarRad - 63.0 * (Math.PI / 180.0));

            double sH = 1.0 + 0.015 * cBarPrime * t;

            // 8. Rotation function RT
            double cBarPrime7 = Math.Pow(cBarPrime, 7.0);
            double rC = 2.0 * Math.Sqrt(cBarPrime7 / (cBarPrime7 + pow25_7));

            double deltaThetaDeg = 30.0 * Math.Exp(-Math.Pow((hBarPrime - 275.0) / 25.0, 2.0));
            double deltaThetaRad = deltaThetaDeg * (Math.PI / 180.0);
            double rT = -Math.Sin(2.0 * deltaThetaRad) * rC;

            // 9. Total Delta E 2000
            double termL = deltaLPrime / (kL * sL);
            double termC = deltaCPrime / (kC * sC);
            double termH = deltaHPrime / (kH * sH);

            double de00Sq = termL * termL + termC * termC + termH * termH + rT * termC * termH;
            return Math.Sqrt(Math.Max(0.0, de00Sq));
        }

        private static double ComputeHueAngleDeg(double a, double b)
        {
            if (Math.Abs(a) < 1e-9 && Math.Abs(b) < 1e-9) return 0.0;
            double rad = Math.Atan2(b, a);
            double deg = rad * (180.0 / Math.PI);
            if (deg < 0.0) deg += 360.0;
            return deg;
        }

        /// <summary>
        /// Evaluates color difference between sample and reference with pass/fail tolerance classification.
        /// </summary>
        public static ColorComparisonResult EvaluateColorMatch(LabColor sample, LabColor golden, double tolerance = 2.0)
        {
            double de00 = DeltaE2000(golden, sample);
            double de76 = DeltaE76(golden, sample);

            ColorToleranceGrade grade;
            if (de00 < 1.0) grade = ColorToleranceGrade.Imperceptible;
            else if (de00 < 2.0) grade = ColorToleranceGrade.Perceptible;
            else if (de00 < 3.5) grade = ColorToleranceGrade.AcceptableIndustrial;
            else grade = ColorToleranceGrade.Unacceptable;

            bool isPassed = de00 <= tolerance;
            return new ColorComparisonResult(de00, de76, grade, isPassed);
        }

        /// <summary>
        /// Evaluates raw sRGB pixel color match against a golden master sRGB value.
        /// </summary>
        public static ColorComparisonResult EvaluateColorMatch(
            byte sampleR, byte sampleG, byte sampleB,
            byte goldenR, byte goldenG, byte goldenB,
            double tolerance = 2.0)
        {
            var labSample = LabColor.FromRgb(sampleR, sampleG, sampleB);
            var labGolden = LabColor.FromRgb(goldenR, goldenG, goldenB);
            return EvaluateColorMatch(labSample, labGolden, tolerance);
        }
    }
}
