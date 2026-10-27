using System;
using Xunit;
using ZeroData.Core;
using ZeroUI.Core.Charts;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Tests
{
    public class ColumnarDataSeriesAdapterTests
    {
        [Fact]
        public void ToChartSeries_ConvertsDataFrameColumnsToChartSeries()
        {
            var df = new DataFrame();
            df.AddColumn(new DataColumn<double>("Timestamp", new[] { 1.0, 2.0, 3.0, 4.0, 5.0 }));
            df.AddColumn(new DataColumn<double>("Temperature", new[] { 22.5, 23.1, 24.0, 23.8, 25.2 }));

            var series = ColumnarDataSeriesAdapter.ToChartSeries(
                df,
                xColumnName: "Timestamp",
                yColumnName: "Temperature",
                seriesName: "Reactor 1 Temp",
                chartType: ChartType.Line);

            Assert.Equal("Reactor 1 Temp", series.Name);
            Assert.Equal(ChartType.Line, series.Type);
            Assert.Equal(5, series.Points.Count);
            Assert.Equal(22.5, series.Points[0].Value);
            Assert.Equal(25.2, series.Points[4].Value);
        }

        [Fact]
        public void ToCandlePoints_ExtractsOhlcPoints()
        {
            var df = new DataFrame();
            var now = DateTime.UtcNow;
            df.AddColumn(new DataColumn<DateTime>("Time", new[] { now, now.AddMinutes(1), now.AddMinutes(2) }));
            df.AddColumn(new DataColumn<double>("Open", new[] { 100.0, 102.0, 101.0 }));
            df.AddColumn(new DataColumn<double>("High", new[] { 105.0, 104.0, 106.0 }));
            df.AddColumn(new DataColumn<double>("Low", new[] { 98.0, 100.0, 99.0 }));
            df.AddColumn(new DataColumn<double>("Close", new[] { 102.0, 101.0, 105.5 }));

            var candles = ColumnarDataSeriesAdapter.ToCandlePoints(
                df,
                timeColumnName: "Time",
                openColumnName: "Open",
                highColumnName: "High",
                lowColumnName: "Low",
                closeColumnName: "Close");

            Assert.Equal(3, candles.Count);
            Assert.True(candles[0].IsBullish); // 102 >= 100
            Assert.False(candles[1].IsBullish); // 101 < 102
            Assert.True(candles[2].IsBullish); // 105.5 >= 101
            Assert.Equal(106.0, candles[2].High);
        }
    }
}
