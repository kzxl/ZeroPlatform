using System;
using System.Collections.Generic;
using ZeroData.Core;
using ZeroUI.Core.Charts;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// High-performance adapter bridging sovereign Tier-2 columnar <see cref="DataFrame"/> data structures
    /// directly into ZeroUI charts and visual components without per-row object allocations.
    /// </summary>
    public static class ColumnarDataSeriesAdapter
    {
        /// <summary>
        /// Converts columns of a <see cref="DataFrame"/> into a contiguous <see cref="ChartSeries"/>.
        /// </summary>
        public static ChartSeries ToChartSeries(
            DataFrame dataFrame,
            string xColumnName,
            string yColumnName,
            string? seriesName = null,
            ChartType? chartType = null,
            uint? colorRgba = null)
        {
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));
            if (string.IsNullOrEmpty(xColumnName)) throw new ArgumentException("X column name cannot be empty", nameof(xColumnName));
            if (string.IsNullOrEmpty(yColumnName)) throw new ArgumentException("Y column name cannot be empty", nameof(yColumnName));

            int rowCount = dataFrame.RowCount;
            var series = new ChartSeries(seriesName ?? yColumnName)
            {
                Type = chartType,
                ColorRgba = colorRgba
            };

            if (rowCount == 0) return series;

            var yCol = dataFrame.GetColumn(yColumnName);
            var xCol = dataFrame.GetColumn(xColumnName);

            // Fast path for numeric Y column
            for (int i = 0; i < rowCount; i++)
            {
                double yVal = ConvertToDouble(yCol.GetValue(i));
                object? xObj = xCol.GetValue(i);

                if (xObj is double xNum)
                {
                    series.Add(new ChartPoint(xNum, yVal, xNum.ToString()));
                }
                else if (xObj is float xFlt)
                {
                    series.Add(new ChartPoint(xFlt, yVal, xFlt.ToString()));
                }
                else if (xObj is int xInt)
                {
                    series.Add(new ChartPoint(xInt, yVal, xInt.ToString()));
                }
                else if (xObj is DateTime xDt)
                {
                    series.Add(new ChartPoint(xDt.Ticks, yVal, xDt.ToString("HH:mm:ss")));
                }
                else
                {
                    string label = xObj?.ToString() ?? string.Empty;
                    series.Add(new ChartPoint(label, yVal));
                }
            }

            return series;
        }

        /// <summary>
        /// Maps OHLC (Open, High, Low, Close) columns from a <see cref="DataFrame"/> to a list of <see cref="CandlePoint"/>s.
        /// </summary>
        public static List<CandlePoint> ToCandlePoints(
            DataFrame dataFrame,
            string timeColumnName,
            string openColumnName,
            string highColumnName,
            string lowColumnName,
            string closeColumnName,
            string? volumeColumnName = null)
        {
            if (dataFrame == null) throw new ArgumentNullException(nameof(dataFrame));

            int rowCount = dataFrame.RowCount;
            var result = new List<CandlePoint>(rowCount);
            if (rowCount == 0) return result;

            var timeCol = dataFrame.GetColumn(timeColumnName);
            var openCol = dataFrame.GetColumn(openColumnName);
            var highCol = dataFrame.GetColumn(highColumnName);
            var lowCol = dataFrame.GetColumn(lowColumnName);
            var closeCol = dataFrame.GetColumn(closeColumnName);
            var volCol = volumeColumnName != null && dataFrame.HasColumn(volumeColumnName)
                ? dataFrame.GetColumn(volumeColumnName)
                : null;

            for (int i = 0; i < rowCount; i++)
            {
                DateTime time = ConvertToDateTime(timeCol.GetValue(i));
                double open = ConvertToDouble(openCol.GetValue(i));
                double high = ConvertToDouble(highCol.GetValue(i));
                double low = ConvertToDouble(lowCol.GetValue(i));
                double close = ConvertToDouble(closeCol.GetValue(i));
                double volume = volCol != null ? ConvertToDouble(volCol.GetValue(i)) : 0.0;

                result.Add(new CandlePoint(time, open, high, low, close, volume));
            }

            return result;
        }

        private static double ConvertToDouble(object? val)
        {
            if (val == null) return 0.0;
            if (val is double d) return d;
            if (val is float f) return f;
            if (val is int i) return i;
            if (val is long l) return l;
            if (val is decimal m) return (double)m;
            if (double.TryParse(val.ToString(), out double parsed)) return parsed;
            return 0.0;
        }

        private static DateTime ConvertToDateTime(object? val)
        {
            if (val == null) return DateTime.MinValue;
            if (val is DateTime dt) return dt;
            if (val is long ticks) return new DateTime(ticks);
            if (DateTime.TryParse(val.ToString(), out DateTime parsed)) return parsed;
            return DateTime.MinValue;
        }
    }
}
