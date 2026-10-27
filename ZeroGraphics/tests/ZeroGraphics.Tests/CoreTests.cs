using System;
using Xunit;
using ZeroGraphics.Core.Data;
using ZeroGraphics.Core.Math;
using ZeroGraphics.Core.Telemetry;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace ZeroGraphics.Tests
{
    public class CoreTests
    {
        [Fact]
        public void SdfMath_EvaluateBoxSdf_CalculatesAccurateDistance()
        {
            // Point at center (0, 0) of 100x100 box with corner radius 10
            float d = SdfMath.EvaluateBoxSdf(0f, 0f, 50f, 50f, 10f);
            Assert.True(d < 0f, "Center must have negative distance (inside boundary).");

            // Point outside box at (60, 0) -> distance should be roughly 10
            float dOut = SdfMath.EvaluateBoxSdf(60f, 0f, 50f, 50f, 10f);
            Assert.True(dOut > 0f, "Outside point must have positive distance.");
        }

        [Fact]
        public void SdfMath_ComputeGaussianKernel_WeightsSumToOne()
        {
            Span<float> weights = stackalloc float[15];
            SdfMath.ComputeGaussianKernel(7, weights);

            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                sum += weights[i];
            }

            Assert.True(Math.Abs(sum - 1.0f) < 0.001f, "Gaussian weights must normalize to 1.0.");
        }

        [Fact]
        public void LttbDecimation_DownsamplesMassivePointSeriesAccurately()
        {
            var raw = new TimePoint[10000];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = new TimePoint(i, Math.Sin(i * 0.1));
            }

            var dest = new TimePoint[100];
            int written = LttbDecimation.Downsample(raw, dest, 100);

            Assert.Equal(100, written);
            Assert.Equal(raw[0], dest[0]); // Preserves first point
            Assert.Equal(raw[^1], dest[^1]); // Preserves last point
        }

        [Fact]
        public void MinMaxDecimation_PreservesExtremePeaksAndValleys()
        {
            var raw = new TimePoint[10000];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = new TimePoint(i, Math.Sin(i * 0.05));
            }

            // Inject narrow extreme spike at index 543 and valley at index 544
            raw[543] = new TimePoint(543, 999.0);
            raw[544] = new TimePoint(544, -999.0);

            var dest = new TimePoint[100];
            int written = MinMaxDecimation.Downsample(raw, dest, 100);

            Assert.True(written >= 2 && written <= 100);
            Assert.Equal(raw[0], dest[0]);
            Assert.Equal(raw[^1], dest[written - 1]);

            // Ensure the extreme spike was not lost in downsampling
            bool foundSpike = false;
            bool foundValley = false;
            for (int i = 0; i < written; i++)
            {
                if (dest[i].Y == 999.0) foundSpike = true;
                if (dest[i].Y == -999.0) foundValley = true;
            }

            Assert.True(foundSpike, "MinMax decimation must preserve the maximum outlier spike.");
            Assert.True(foundValley, "MinMax decimation must preserve the minimum outlier valley.");
        }

        [Fact]
        public void MinMaxDecimation_HandlesSmallArraysAndBoundaryCases()
        {
            var small = new TimePoint[] { new TimePoint(0, 1), new TimePoint(1, 5) };
            var dest = new TimePoint[10];

            int written = MinMaxDecimation.Downsample(small, dest, 10);
            Assert.Equal(2, written);
            Assert.Equal(small[0], dest[0]);
            Assert.Equal(small[1], dest[1]);
        }

        [Fact]
        public void GpuCapabilities_ConfiguresAndResetsCorrectly()
        {
            GpuCapabilities.Configure("NVIDIA GeForce RTX Test", HardwareGpuTier.Tier2_Discrete, 16384.0, 32768.0, 0x10DE);

            Assert.Equal("NVIDIA GeForce RTX Test", GpuCapabilities.AdapterName);
            Assert.Equal(HardwareGpuTier.Tier2_Discrete, GpuCapabilities.CurrentTier);
            Assert.Equal(16384.0, GpuCapabilities.DedicatedVramMb);
            Assert.True(GpuCapabilities.IsHardwareAccelerated);

            GpuCapabilities.Reset();
        }
    }
}
