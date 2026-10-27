using System;
using System.Collections.Generic;
using System.Drawing;
using Xunit;
using ZeroUI.Core.Analytics;
using ZeroUI.WinForms.Charts;

namespace ZeroUI.Desktop.Tests
{
    public class TrendStudioTests
    {
        [Fact]
        public void TrendStatistics_Compute_CalculatesAccurateMetrics()
        {
            var data = new List<TrendDataPoint>
            {
                new TrendDataPoint(DateTime.Now.AddSeconds(0), 10.0),
                new TrendDataPoint(DateTime.Now.AddSeconds(1), 20.0),
                new TrendDataPoint(DateTime.Now.AddSeconds(2), 30.0),
                new TrendDataPoint(DateTime.Now.AddSeconds(3), 40.0),
                new TrendDataPoint(DateTime.Now.AddSeconds(4), 50.0)
            };

            var stats = TrendStatistics.Compute(data);

            Assert.Equal(5, stats.Count);
            Assert.Equal(10.0, stats.Min);
            Assert.Equal(50.0, stats.Max);
            Assert.Equal(30.0, stats.Average);
            Assert.Equal(50.0, stats.Latest);
            Assert.True(stats.StdDev > 0);
        }

        [Fact]
        public void LttbDecimator_Downsample_PreservesPeaksAndValleys()
        {
            // Create a 10,000 point series with a sharp spike in the middle
            int count = 10_000;
            var points = new List<TrendDataPoint>(count);
            var baseTime = new DateTime(2026, 1, 1, 0, 0, 0);

            for (int i = 0; i < count; i++)
            {
                double v = 10.0 + Math.Sin(i * 0.05) * 5.0;
                if (i == 5000)
                {
                    v = 999.0; // Extreme peak
                }
                points.Add(new TrendDataPoint(baseTime.AddSeconds(i), v));
            }

            int targetThreshold = 500;
            var downsampled = LttbDecimator.Downsample(points, targetThreshold);

            Assert.Equal(targetThreshold, downsampled.Length);

            // Assert that the extreme peak (999.0) was strictly preserved by LTTB
            bool peakFound = false;
            for (int i = 0; i < downsampled.Length; i++)
            {
                if (Math.Abs(downsampled[i].Value - 999.0) < 0.001)
                {
                    peakFound = true;
                    break;
                }
            }

            Assert.True(peakFound, "LTTB decimation must strictly preserve peak amplitudes");
        }

        [Fact]
        public void TrendDualCursor_CalculatesDeltaTimeAndValues()
        {
            var pen = new TrendPen("Test Temp", "°C", Color.Red, TrendYAxisPosition.Left1);
            var t0 = new DateTime(2026, 1, 1, 12, 0, 0);

            pen.AddPoint(t0, 100.0);
            pen.AddPoint(t0.AddSeconds(10), 150.0);
            pen.AddPoint(t0.AddSeconds(20), 200.0);

            var dualCursor = new TrendDualCursor
            {
                Enabled = true,
                TimeA = t0,
                TimeB = t0.AddSeconds(20)
            };

            Assert.Equal(20.0, dualCursor.DeltaSeconds);
            Assert.Equal(0.05, dualCursor.FrequencyHz, 4);

            double? deltaVal = dualCursor.GetDeltaValue(pen);
            Assert.NotNull(deltaVal);
            Assert.Equal(100.0, deltaVal.Value); // 200.0 - 100.0
        }

        [Fact]
        public void TrendStudio_Instantiation_ConfiguresPensAndLayout()
        {
            StaTestRunner.Run(() =>
            {
                using var studio = new TrendStudio();
                Assert.NotNull(studio.Plot);
                Assert.NotNull(studio.PenTable);

                var pen1 = studio.AddPen("Sensor 1", "Bar", Color.Cyan, TrendYAxisPosition.Left1);
                var pen2 = studio.AddPen("Sensor 2", "RPM", Color.Purple, TrendYAxisPosition.Right1);

                Assert.Equal(2, studio.Pens.Count);
                Assert.Equal("Sensor 1", pen1.Name);
                Assert.Equal("Sensor 2", pen2.Name);

                studio.AddAnnotation(DateTime.Now, "Trip Alert", "Test description", Color.Red);
                Assert.Single(studio.Annotations);

                // Test DPI Scaling
                studio.ApplyDpiScaling(1.5f);
                Assert.Equal(1.5f, studio.DpiScale);

                // Add points and verify no exception during update
                pen1.AddPoint(12.5);
                pen2.AddPoint(1450.0);
                studio.UpdateLiveStream();
            });
        }
    }
}
