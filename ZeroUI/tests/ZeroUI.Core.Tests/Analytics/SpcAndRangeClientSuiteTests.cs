using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZeroUI.Core.Analytics;
using ZeroUI.Core.Range;

namespace ZeroUI.Core.Tests.Analytics
{
    public class SpcAndRangeClientSuiteTests
    {
        #region SpcStatisticsCalculator Tests

        [Fact]
        public void Spc_EmptyDataset_ReturnsZeroLimits()
        {
            var limits = SpcStatisticsCalculator.Calculate(ReadOnlySpan<double>.Empty);

            Assert.Equal(0, limits.Mean);
            Assert.Equal(0, limits.StandardDeviation);
            Assert.Equal(0, limits.Ucl);
            Assert.Equal(0, limits.Lcl);
            Assert.Equal(0, limits.TotalSamples);
            Assert.Equal(0, limits.OutOfControlCount);
            Assert.False(limits.HasSpecLimits);
        }

        [Fact]
        public void Spc_SingleValue_ReturnsConsistentLimits()
        {
            double[] data = new[] { 42.0 };
            var limits = SpcStatisticsCalculator.Calculate(data);

            Assert.Equal(42.0, limits.Mean);
            Assert.Equal(0, limits.StandardDeviation);
            Assert.Equal(42.0, limits.Ucl);
            Assert.Equal(42.0, limits.Lcl);
            Assert.Equal(42.0, limits.Target);
            Assert.Equal(1, limits.TotalSamples);
            Assert.Equal(0, limits.OutOfControlCount);
        }

        [Fact]
        public void Spc_KnownDataset_CalculatesAccurateMeanAndStdDev()
        {
            // Sample dataset: 10, 12, 23, 23, 16, 23, 21, 16
            // Sum = 144, Mean = 18.0
            // Deviations: -8, -6, 5, 5, -2, 5, 3, -2
            // Squared: 64, 36, 25, 25, 4, 25, 9, 4 => Sum = 192
            // Sample Variance = 192 / 7 = 27.42857... => Stdev = ~5.237229
            double[] data = new[] { 10.0, 12.0, 23.0, 23.0, 16.0, 23.0, 21.0, 16.0 };

            var limits = SpcStatisticsCalculator.Calculate(data);

            Assert.Equal(18.0, limits.Mean, 4);
            Assert.Equal(Math.Sqrt(192.0 / 7.0), limits.StandardDeviation, 4);
            Assert.Equal(18.0 + 3.0 * limits.StandardDeviation, limits.Ucl, 4);
            Assert.Equal(18.0 - 3.0 * limits.StandardDeviation, limits.Lcl, 4);
            Assert.Equal(8, limits.TotalSamples);
            Assert.Equal(0, limits.OutOfControlCount);
        }

        [Fact]
        public void Spc_OutlierPoints_DetectedAccurately()
        {
            // Stable baseline of 20 points centered around 10.0 with low variance,
            // plus outliers at 25.0 (> UCL) and -5.0 (< LCL)
            var list = new List<double>();
            for (int i = 0; i < 20; i++)
            {
                list.Add(10.0 + (i % 2 == 0 ? 0.1 : -0.1));
            }
            list.Add(25.0); // Outlier above UCL (~23.9)
            list.Add(-5.0); // Outlier below LCL (~ -3.9)
            double[] data = list.ToArray();

            var limits = SpcStatisticsCalculator.Calculate(data);

            Assert.Equal(22, limits.TotalSamples);
            Assert.Equal(2, limits.OutOfControlCount);
        }

        [Fact]
        public void Spc_ProcessCapability_CalculatesCpAndCpkCorrectly()
        {
            // Mean = 10.0, Stdev = 1.0
            // USL = 16.0, LSL = 4.0
            // Cp = (16 - 4) / 6 = 2.0 (Six Sigma capable)
            // Cpk = min((16 - 10) / 3, (10 - 4) / 3) = 2.0
            double[] data = new[] { 9.0, 11.0, 9.0, 11.0, 10.0 };
            // Mean = 10.0, Sample variance = (1 + 1 + 1 + 1 + 0) / 4 = 1.0 => Stdev = 1.0

            var limits = SpcStatisticsCalculator.Calculate(data, usl: 16.0, lsl: 4.0);

            Assert.Equal(10.0, limits.Mean, 4);
            Assert.Equal(1.0, limits.StandardDeviation, 4);
            Assert.Equal(2.0, limits.Cp, 4);
            Assert.Equal(2.0, limits.Cpk, 4);
            Assert.True(limits.HasSpecLimits);
        }

        [Fact]
        public void Spc_OffCenterProcess_CpkReflectsAsymmetry()
        {
            // Mean = 12.0, Stdev = 1.0
            // USL = 15.0, LSL = 5.0
            // CPU = (15 - 12) / 3 = 1.0
            // CPL = (12 - 5) / 3 = 2.333
            // Cpk = min(1.0, 2.333) = 1.0
            double[] data = new[] { 11.0, 13.0, 11.0, 13.0, 12.0 };

            var limits = SpcStatisticsCalculator.Calculate(data, usl: 15.0, lsl: 5.0);

            Assert.Equal(12.0, limits.Mean, 4);
            Assert.Equal(1.0, limits.StandardDeviation, 4);
            Assert.Equal(1.0, limits.Cpk, 4);
            Assert.Equal((15.0 - 5.0) / 6.0, limits.Cp, 4);
        }

        #endregion

        #region RangeControl Client Model Tests

        [Fact]
        public void RangeControlModel_AttachMockClient_SynchronizesRangeAndData()
        {
            var model = new RangeControlModel();
            var mockClient = new MockRangeClient(0, 100, new[]
            {
                new RangeDataPoint(0, 10),
                new RangeDataPoint(50, 25),
                new RangeDataPoint(100, 50)
            });

            // Simulate attaching client
            model.DataType = mockClient.DataType;
            var bounds = mockClient.GetTotalRangeBounds();
            model.SetTotalRange(bounds.Start, bounds.End);
            model.SetVisibleRange(bounds.Start, bounds.End);
            model.SelectAll();
            model.DataPoints.AddRange(mockClient.GetDataPoints());

            Assert.Equal(0, model.TotalRangeStart);
            Assert.Equal(100, model.TotalRangeEnd);
            Assert.Equal(3, model.DataPoints.Count);
            Assert.Equal(0, model.SelectedRangeStart);
            Assert.Equal(100, model.SelectedRangeEnd);

            // Change range selection
            model.SetSelectedRange(20, 80);
            mockClient.OnRangeSelectionChanged(model.SelectedRangeStart, model.SelectedRangeEnd);

            Assert.Equal(20, mockClient.LastStart);
            Assert.Equal(80, mockClient.LastEnd);
        }

        private sealed class MockRangeClient : IRangeControlClient
        {
            private readonly double _start;
            private readonly double _end;
            private readonly List<RangeDataPoint> _points;

            public double LastStart { get; private set; }
            public double LastEnd { get; private set; }

            public RangeDataType DataType => RangeDataType.Numeric;

            public MockRangeClient(double start, double end, IEnumerable<RangeDataPoint> points)
            {
                _start = start;
                _end = end;
                _points = points.ToList();
            }

            public (double Start, double End) GetTotalRangeBounds() => (_start, _end);

            public IEnumerable<RangeDataPoint> GetDataPoints() => _points;

            public void OnRangeSelectionChanged(double start, double end)
            {
                LastStart = start;
                LastEnd = end;
            }
        }

        #endregion
    }
}
