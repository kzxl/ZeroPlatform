using System;
using Xunit;
using ZeroGraphics.Imaging.ColorScience;
using ZeroGraphics.Imaging.Filters;

namespace ZeroGraphics.Tests
{
    public class ColorMetrologyTests
    {
        [Fact]
        public void LabColor_RgbRoundtrip_PreservesColorValues()
        {
            byte[][] testColors = new[]
            {
                new byte[] { 255, 0, 0 },     // Pure Red
                new byte[] { 0, 255, 0 },     // Pure Green
                new byte[] { 0, 0, 255 },     // Pure Blue
                new byte[] { 255, 255, 255 }, // White
                new byte[] { 0, 0, 0 },       // Black
                new byte[] { 128, 128, 128 }, // Mid Gray
                new byte[] { 240, 180, 50 }   // Industrial Orange
            };

            foreach (var rgb in testColors)
            {
                var lab = LabColor.FromRgb(rgb[0], rgb[1], rgb[2]);
                lab.ToRgb(out byte rOut, out byte gOut, out byte bOut);

                Assert.InRange(rOut, rgb[0] - 1, rgb[0] + 1);
                Assert.InRange(gOut, rgb[1] - 1, rgb[1] + 1);
                Assert.InRange(bOut, rgb[2] - 1, rgb[2] + 1);
            }
        }

        [Fact]
        public void DeltaE2000_IdenticalColors_ReturnsZero()
        {
            var c1 = new LabColor(50.0, 10.0, -20.0);
            var c2 = new LabColor(50.0, 10.0, -20.0);

            double de00 = ColorDifference.DeltaE2000(c1, c2);
            double de76 = ColorDifference.DeltaE76(c1, c2);

            Assert.Equal(0.0, de00, 4);
            Assert.Equal(0.0, de76, 4);
        }

        [Fact]
        public void DeltaE2000_EvaluatesStandardCieTestPair()
        {
            // Standard CIE test pair (Bruce Lindbloom CIE standard reference)
            // Color 1: L=50, a=2.6772, b=-79.7751
            // Color 2: L=50, a=0.0000, b=-82.7485
            // Expected CIEDE2000 delta E ~= 2.0425
            var c1 = new LabColor(50.0, 2.6772, -79.7751);
            var c2 = new LabColor(50.0, 0.0000, -82.7485);

            double de00 = ColorDifference.DeltaE2000(c1, c2);
            Assert.InRange(de00, 2.040, 2.045);
        }

        [Fact]
        public void EvaluateColorMatch_ClassifiesIndustrialToleranceCorrectly()
        {
            // Reference golden master: sRGB(200, 100, 50)
            byte refR = 200, refG = 100, refB = 50;

            // 1. Imperceptible sample (almost identical: 200, 100, 51)
            var resultImperceptible = ColorDifference.EvaluateColorMatch(200, 100, 51, refR, refG, refB);
            Assert.True(resultImperceptible.IsPassed);
            Assert.Equal(ColorToleranceGrade.Imperceptible, resultImperceptible.Grade);
            Assert.True(resultImperceptible.DeltaE00 < 1.0);

            // 2. Perceptible sample (slight shift: 205, 102, 50)
            var resultPerceptible = ColorDifference.EvaluateColorMatch(205, 102, 50, refR, refG, refB);
            Assert.True(resultPerceptible.IsPassed);
            Assert.True(resultPerceptible.DeltaE00 >= 1.0 && resultPerceptible.DeltaE00 < 2.0);

            // 3. Unacceptable defect (major shift: 150, 50, 10)
            var resultDefect = ColorDifference.EvaluateColorMatch(150, 50, 10, refR, refG, refB, tolerance: 2.0);
            Assert.False(resultDefect.IsPassed);
            Assert.Equal(ColorToleranceGrade.Unacceptable, resultDefect.Grade);
            Assert.True(resultDefect.DeltaE00 > 3.5);
        }
    }
}
