using System;
using System.Collections.Generic;
using Xunit;
using ZeroGraphics.Core.Analytics;

namespace ZeroGraphics.Tests
{
    public class SpcAnalyticsTests
    {
        [Fact]
        public void SpcAnalysis_CalculatesAccurateControlLimitsAndCpk()
        {
            // Sample measurements with known properties: mean = 10.0
            float[] samples = new float[] { 9.8f, 10.2f, 10.1f, 9.9f, 10.0f, 10.0f, 9.7f, 10.3f, 10.1f, 9.9f };

            float usl = 11.0f;
            float lsl = 9.0f;

            var summary = SpcAnalysis.Calculate(samples, usl, lsl);

            Assert.Equal(10, summary.Count);
            Assert.InRange(summary.Mean, 9.95f, 10.05f);
            Assert.True(summary.StdDev > 0);
            Assert.True(summary.UCL > summary.Mean);
            Assert.True(summary.LCL < summary.Mean);

            // Process is well centered within [9.0, 11.0], so Cpk should be positive and >= 1.0
            Assert.NotNull(summary.Cp);
            Assert.NotNull(summary.Cpk);
            Assert.True(summary.Cpk.Value > 1.0f);
        }

        [Fact]
        public void SpcRuleEngine_DetectsRule1_Beyond3Sigma()
        {
            var baseline = new SpcSummary { Mean = 10.0f, StdDev = 1.0f, Count = 100 };
            float[] samples = new float[] { 10f, 10.1f, 9.9f, 10f, 10.2f, 9.8f, 10f, 10.1f, 9.9f, 15.0f }; // 15.0 exceeds UCL (13.0)

            var alarms = SpcRuleEngine.EvaluateRules(samples, baseline);

            Assert.Contains(alarms, a => a.Rule == SpcRuleViolation.Rule1_Beyond3Sigma && a.SampleIndex == 9);
        }

        [Fact]
        public void SpcRuleEngine_DetectsRule2_RunOf9PointsOnOneSide()
        {
            // Baseline centered around 10.0, but samples has 9 consecutive points > 10.0
            var baseline = new SpcSummary { Mean = 10.0f, StdDev = 1.0f, Count = 100 };

            float[] samples = new float[] { 10.5f, 10.6f, 10.4f, 10.7f, 10.8f, 10.5f, 10.6f, 10.7f, 10.9f }; // 9 points > 10

            var alarms = SpcRuleEngine.EvaluateRules(samples, baseline);

            Assert.Contains(alarms, a => a.Rule == SpcRuleViolation.Rule2_RunOf9);
        }

        [Fact]
        public void SpcRuleEngine_DetectsRule3_TrendOf6PointsContinuallyIncreasing()
        {
            var baseline = new SpcSummary { Mean = 10.0f, StdDev = 2.0f, Count = 100 };

            float[] samples = new float[] { 8.0f, 8.5f, 9.0f, 9.5f, 10.0f, 10.5f }; // 6 points increasing

            var alarms = SpcRuleEngine.EvaluateRules(samples, baseline);

            Assert.Contains(alarms, a => a.Rule == SpcRuleViolation.Rule3_TrendOf6);
        }

        [Fact]
        public void GaussianDistribution_GeneratesBellCurvePointsSymmetrically()
        {
            float mean = 50.0f;
            float stdDev = 5.0f;
            int pointCount = 101; // odd number so center is exactly at mean

            var curve = SpcAnalysis.GenerateGaussianCurve(mean, stdDev, pointCount);

            Assert.Equal(pointCount, curve.Length);

            // Center point should be close to mean and have maximum density
            int mid = pointCount / 2;
            Assert.InRange(curve[mid].X, 49.9f, 50.1f);

            // Verify density at center is greater than edges
            Assert.True(curve[mid].Density > curve[0].Density);
            Assert.True(curve[mid].Density > curve[pointCount - 1].Density);

            // Verify symmetry
            Assert.InRange(Math.Abs(curve[0].Density - curve[pointCount - 1].Density), 0.0f, 0.001f);
        }
    }
}
