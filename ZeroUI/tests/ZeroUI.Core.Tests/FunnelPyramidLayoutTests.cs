using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class FunnelPyramidLayoutTests
    {
        [Fact]
        public void CalculateSegmentWidths_FunnelMode_TapersDownward()
        {
            float maxW = 300f;
            float minW = 100f;
            int total = 4;

            // Stage 0: Top-most
            FunnelMath.CalculateSegmentWidths(0, total, maxW, minW, FunnelChartMode.Funnel, out float wTop0, out float wBot0);
            Assert.Equal(300f, wTop0);
            Assert.Equal(250f, wBot0);

            // Stage 3: Bottom-most
            FunnelMath.CalculateSegmentWidths(3, total, maxW, minW, FunnelChartMode.Funnel, out float wTop3, out float wBot3);
            Assert.Equal(150f, wTop3);
            Assert.Equal(100f, wBot3);
        }

        [Fact]
        public void CalculateSegmentWidths_PyramidMode_ExpandsDownward()
        {
            float maxW = 300f;
            float minW = 100f;
            int total = 4;

            // Stage 0: Top-most (Apex)
            FunnelMath.CalculateSegmentWidths(0, total, maxW, minW, FunnelChartMode.Pyramid, out float wTop0, out float wBot0);
            Assert.Equal(100f, wTop0);
            Assert.Equal(150f, wBot0);

            // Stage 3: Bottom-most (Base)
            FunnelMath.CalculateSegmentWidths(3, total, maxW, minW, FunnelChartMode.Pyramid, out float wTop3, out float wBot3);
            Assert.Equal(250f, wTop3);
            Assert.Equal(300f, wBot3);
        }

        [Fact]
        public void ConversionAndDropOffRates_CalculateAccurately()
        {
            double stage1 = 10000;
            double stage2 = 9500;

            double conversion = FunnelMath.CalculateConversionRate(stage2, stage1);
            double dropOff = FunnelMath.CalculateDropOffRate(stage2, stage1);
            double yieldTotal = FunnelMath.CalculateOverallYield(stage2, stage1);

            Assert.Equal(95.0, conversion, 1);
            Assert.Equal(5.0, dropOff, 1);
            Assert.Equal(95.0, yieldTotal, 1);
        }

        [Fact]
        public void SafeAgainstZeroDivision()
        {
            double conv = FunnelMath.CalculateConversionRate(500, 0);
            Assert.Equal(100.0, conv);

            double yieldVal = FunnelMath.CalculateOverallYield(500, 0);
            Assert.Equal(100.0, yieldVal);
        }
    }
}
