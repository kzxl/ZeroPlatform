using System;
using Xunit;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class TimeSeriesPyramidTests
    {
        [Fact]
        public void LttbDecimation_SpanOverload_PreservesWaveformAndPeak()
        {
            const int count = 10_000;
            var src = new TimePoint[count];
            for (int i = 0; i < count; i++)
            {
                src[i] = new TimePoint(i, Math.Sin(i * 0.05) * 50.0);
            }

            // Inject an extreme spike at index 5000
            src[5000] = new TimePoint(5000, 999.0);

            var dest = new TimePoint[200];
            int written = LttbDecimation.Downsample(src.AsSpan(), dest.AsSpan(), 200);

            Assert.Equal(200, written);

            // First and last points preserved
            Assert.Equal(src[0].X, dest[0].X);
            Assert.Equal(src[count - 1].X, dest[written - 1].X);

            // The extreme spike (999.0) MUST be preserved in the decimated points
            bool spikePreserved = false;
            for (int i = 0; i < written; i++)
            {
                if (Math.Abs(dest[i].Y - 999.0) < 0.01)
                {
                    spikePreserved = true;
                    break;
                }
            }
            Assert.True(spikePreserved, "LTTB decimation failed to preserve critical telemetry spike.");
        }

        [Fact]
        public void TimeSeriesPyramid_IncrementalAppend_BuildsHierarchicalLevels()
        {
            var pyramid = new TimeSeriesPyramid(10_000);

            // Append 1,024 points
            for (int i = 0; i < 1024; i++)
            {
                pyramid.Append(i, Math.Sin(i * 0.1) * 100.0);
            }

            Assert.Equal(1024, pyramid.Count);

            // Query full range down to 64 points
            var dest = new TimePoint[64];
            int written = pyramid.QueryRange(0, 1023, dest.AsSpan(), 64);

            Assert.Equal(64, written);
            Assert.Equal(0, dest[0].X);
            Assert.True(dest[written - 1].X >= 1000);
        }

        [Fact]
        public void TimeSeriesPyramid_ZoomRangeQuery_SelectsOptimalResolution()
        {
            var pyramid = new TimeSeriesPyramid(50_000);

            for (int i = 0; i < 20_000; i++)
            {
                pyramid.Append(i, i % 100);
            }

            // Zoom into narrow range [5,000 to 5,500] (500 raw points)
            var dest = new TimePoint[100];
            int written = pyramid.QueryRange(5000, 5500, dest.AsSpan(), 100);

            Assert.True(written >= 50 && written <= 100);
            Assert.True(dest[0].X >= 5000);
            Assert.True(dest[written - 1].X <= 5500);
        }
    }
}
