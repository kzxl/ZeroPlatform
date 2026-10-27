using System;
using System.Collections.Generic;
using System.Linq;
using ZeroUI.Core.Range;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Connects a ChartControl to a RangeControl, mapping category or time domain samples
    /// into background sparkline / distribution data points and synchronizing viewport scrubbing.
    /// </summary>
    public class ChartRangeControlClient : IRangeControlClient
    {
        private readonly ZChart _chart;
        private readonly int _seriesIndex;

        /// <summary>
        /// The bound ChartControl instance.
        /// </summary>
        public ZChart Chart => _chart;

        /// <summary>
        /// The active data domain type (Numeric).
        /// </summary>
        public RangeDataType DataType => RangeDataType.Numeric;

        public ChartRangeControlClient(ZChart chart, int seriesIndex = 0)
        {
            _chart = chart ?? throw new ArgumentNullException(nameof(chart));
            _seriesIndex = Math.Max(0, seriesIndex);
        }

        /// <summary>
        /// Returns the full category index boundaries (0 to N-1).
        /// </summary>
        public (double Start, double End) GetTotalRangeBounds()
        {
            var series = GetTargetSeries();
            if (series == null || series.Points.Count == 0)
            {
                return (0, 10);
            }

            return (0, Math.Max(1, series.Points.Count - 1));
        }

        /// <summary>
        /// Extracts RangeDataPoint items from the primary chart series for background sparkline rendering.
        /// </summary>
        public IEnumerable<RangeDataPoint> GetDataPoints()
        {
            var series = GetTargetSeries();
            if (series == null || series.Points.Count == 0)
            {
                yield break;
            }

            for (int i = 0; i < series.Points.Count; i++)
            {
                var pt = series.Points[i];
                yield return new RangeDataPoint(i, pt.Value, pt.Label);
            }
        }

        /// <summary>
        /// Updates the chart's visible category window when the user drags the range thumbs.
        /// </summary>
        public void OnRangeSelectionChanged(double start, double end)
        {
            int startIdx = (int)Math.Floor(Math.Min(start, end));
            int endIdx = (int)Math.Ceiling(Math.Max(start, end));

            _chart.VisibleStartIndex = startIdx;
            _chart.VisibleEndIndex = endIdx;
            _chart.Invalidate();
        }

        private Model.ChartSeries? GetTargetSeries()
        {
            if (_chart.Series.Count == 0) return null;
            if (_seriesIndex < _chart.Series.Count)
            {
                return _chart.Series[_seriesIndex];
            }
            return _chart.Series[0];
        }
    }

    /// <summary>
    /// Extension methods for binding ChartControl with RangeControl.
    /// </summary>
    public static class ChartRangeExtensions
    {
        /// <summary>
        /// Creates an IRangeControlClient bridge for this chart to bind with a RangeControl.
        /// </summary>
        public static ChartRangeControlClient AsRangeClient(this ZChart chart, int seriesIndex = 0)
        {
            return new ChartRangeControlClient(chart, seriesIndex);
        }
    }
}
