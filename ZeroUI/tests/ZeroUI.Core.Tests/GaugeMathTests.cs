using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    public class GaugeMathTests
    {
        [Fact]
        public void ValueToAngle_InterpolatesCorrectlyWithinRange()
        {
            // Range 0 to 100, StartAngle = -135, SweepAngle = 270
            float angleMin = GaugeMath.ValueToAngle(0, 0, 100, -135f, 270f);
            float angleMid = GaugeMath.ValueToAngle(50, 0, 100, -135f, 270f);
            float angleMax = GaugeMath.ValueToAngle(100, 0, 100, -135f, 270f);

            Assert.Equal(-135f, angleMin, 2);
            Assert.Equal(0f, angleMid, 2);
            Assert.Equal(135f, angleMax, 2);
        }

        [Fact]
        public void ValueToAngle_ClampsOutsideValues()
        {
            float under = GaugeMath.ValueToAngle(-50, 0, 100, 0f, 180f);
            float over = GaugeMath.ValueToAngle(150, 0, 100, 0f, 180f);

            Assert.Equal(0f, under, 2);
            Assert.Equal(180f, over, 2);
        }

        [Fact]
        public void ValueToPosition_InterpolatesCorrectly()
        {
            float pos = GaugeMath.ValueToPosition(25, 0, 100, 10f, 200f);
            Assert.Equal(60f, pos, 2); // 10 + 0.25 * 200 = 60
        }

        [Fact]
        public void ApplyDamping_SmoothlyApproachesTarget()
        {
            double current = 0;
            double target = 100;

            current = GaugeMath.ApplyDamping(current, target, 0.5);
            Assert.Equal(50.0, current, 1);

            current = GaugeMath.ApplyDamping(current, target, 0.5);
            Assert.Equal(75.0, current, 1);
        }

        [Fact]
        public void EvaluateSeverity_ReturnsMatchingRangeSeverity()
        {
            var ranges = new List<GaugeThresholdRange>
            {
                new GaugeThresholdRange(0, 70, GaugeSeverity.Normal, 0xFF10B981u),
                new GaugeThresholdRange(70, 90, GaugeSeverity.Warning, 0xFFF59E0Bu),
                new GaugeThresholdRange(90, 120, GaugeSeverity.Critical, 0xFFEF4444u)
            };

            Assert.Equal(GaugeSeverity.Normal, GaugeMath.EvaluateSeverity(50, ranges));
            Assert.Equal(GaugeSeverity.Warning, GaugeMath.EvaluateSeverity(75, ranges));
            Assert.Equal(GaugeSeverity.Critical, GaugeMath.EvaluateSeverity(95, ranges));
            Assert.Equal(GaugeSeverity.Normal, GaugeMath.EvaluateSeverity(-10, ranges, GaugeSeverity.Normal));
        }
    }
}
