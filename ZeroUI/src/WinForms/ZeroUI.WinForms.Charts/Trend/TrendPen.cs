using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroUI.Core.Analytics;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Position of the independent Y-axis for a trend pen channel.
    /// </summary>
    public enum TrendYAxisPosition
    {
        Left1,
        Left2,
        Right1,
        Right2
    }

    /// <summary>
    /// Represents an independent signal channel (Pen) on a TrendPlot.
    /// Each pen can have its own Y-axis, scaling parameters, alarm limits, and points.
    /// </summary>
    public class TrendPen
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public Color Color { get; set; }
        public TrendYAxisPosition AxisPosition { get; set; }
        public float LineWidth { get; set; } = 2.0f;
        public bool IsVisible { get; set; } = true;
        public bool AutoScale { get; set; } = true;
        public double ManualMin { get; set; } = 0.0;
        public double ManualMax { get; set; } = 100.0;
        public double? HighAlarmLimit { get; set; }
        public double? LowAlarmLimit { get; set; }

        private readonly List<TrendDataPoint> _points = new List<TrendDataPoint>(1024);
        private readonly object _syncLock = new object();

        public IReadOnlyList<TrendDataPoint> Points
        {
            get
            {
                lock (_syncLock)
                {
                    return _points.ToArray();
                }
            }
        }

        public int PointCount
        {
            get
            {
                lock (_syncLock) return _points.Count;
            }
        }

        public double LatestValue
        {
            get
            {
                lock (_syncLock)
                {
                    return _points.Count > 0 ? _points[_points.Count - 1].Value : 0.0;
                }
            }
        }

        public TrendPen(string name, string unit, Color color, TrendYAxisPosition axisPosition = TrendYAxisPosition.Left1)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name;
            Unit = unit;
            Color = color;
            AxisPosition = axisPosition;
        }

        public void AddPoint(DateTime timestamp, double value)
        {
            lock (_syncLock)
            {
                _points.Add(new TrendDataPoint(timestamp, value));
            }
        }

        public void AddPoint(double value) => AddPoint(DateTime.Now, value);

        public void AddPoints(IEnumerable<TrendDataPoint> points)
        {
            if (points == null) return;
            lock (_syncLock)
            {
                _points.AddRange(points);
            }
        }

        public void Clear()
        {
            lock (_syncLock)
            {
                _points.Clear();
            }
        }

        public (double Min, double Max) GetEffectiveRange(DateTime startTime, DateTime endTime)
        {
            if (!AutoScale)
            {
                return (ManualMin, ManualMax > ManualMin ? ManualMax : ManualMin + 1.0);
            }

            lock (_syncLock)
            {
                if (_points.Count == 0) return (ManualMin, ManualMax);

                double min = double.MaxValue;
                double max = double.MinValue;
                bool found = false;

                for (int i = 0; i < _points.Count; i++)
                {
                    var pt = _points[i];
                    if (pt.Timestamp >= startTime && pt.Timestamp <= endTime)
                    {
                        if (pt.Value < min) min = pt.Value;
                        if (pt.Value > max) max = pt.Value;
                        found = true;
                    }
                }

                if (!found)
                {
                    // Fallback to recent points
                    min = _points[_points.Count - 1].Value - 5.0;
                    max = _points[_points.Count - 1].Value + 5.0;
                }

                if (Math.Abs(max - min) < 0.0001)
                {
                    min -= 1.0;
                    max += 1.0;
                }
                else
                {
                    // Add 5% padding
                    double pad = (max - min) * 0.05;
                    min -= pad;
                    max += pad;
                }

                return (min, max);
            }
        }

        public TrendStatistics GetVisibleStatistics(DateTime startTime, DateTime endTime)
        {
            lock (_syncLock)
            {
                var slice = new List<TrendDataPoint>();
                for (int i = 0; i < _points.Count; i++)
                {
                    var pt = _points[i];
                    if (pt.Timestamp >= startTime && pt.Timestamp <= endTime)
                    {
                        slice.Add(pt);
                    }
                }
                return TrendStatistics.Compute(slice);
            }
        }

        public List<TrendDataPoint> GetPointsInRange(DateTime startTime, DateTime endTime)
        {
            lock (_syncLock)
            {
                var slice = new List<TrendDataPoint>();
                for (int i = 0; i < _points.Count; i++)
                {
                    var pt = _points[i];
                    if (pt.Timestamp >= startTime && pt.Timestamp <= endTime)
                    {
                        slice.Add(pt);
                    }
                }
                return slice;
            }
        }
    }
}
