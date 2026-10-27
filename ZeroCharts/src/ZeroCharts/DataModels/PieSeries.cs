using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroCharts.Axis;

namespace ZeroCharts.DataModels
{
    /// <summary>
    /// Represents an individual wedge / slice of a Pie or Doughnut chart.
    /// </summary>
    public class PieSlice
    {
        public string Label { get; set; }
        public double Value { get; set; }
        public Color Color { get; set; }
        public bool IsExploded { get; set; }

        public float StartAngle { get; internal set; }
        public float SweepAngle { get; internal set; }
        public double Percentage { get; internal set; }

        public PieSlice(string label, double value, Color color, bool isExploded = false)
        {
            Label = label ?? string.Empty;
            Value = Math.Max(0.0, value);
            Color = color;
            IsExploded = isExploded;
        }
    }

    /// <summary>
    /// Series for rendering proportional Pie and Doughnut charts.
    /// </summary>
    public class PieSeries
    {
        public string Name { get; set; }
        public float StartAngle { get; set; } = -90.0f; // 12 o'clock
        public float HoleRadiusRatio { get; set; } = 0.0f; // 0 = Pie, 0.5 = Doughnut
        public float ExplodeDistance { get; set; } = 12.0f;
        public bool ShowPercentages { get; set; } = true;
        public bool ShowLabels { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        private readonly List<PieSlice> _slices = new List<PieSlice>();
        public IReadOnlyList<PieSlice> Slices => _slices;
        public int Count => _slices.Count;

        public double TotalValue { get; private set; } = 0.0;

        public PieSeries(string name = "Pie Series")
        {
            Name = name;
        }

        public void AddSlice(string label, double value, Color? color = null, bool isExploded = false)
        {
            Color c = color ?? ChartPalette.SeriesCycle[_slices.Count % ChartPalette.SeriesCycle.Length];
            var slice = new PieSlice(label, value, c, isExploded);
            _slices.Add(slice);
            RecalculateAngles();
        }

        public void Clear()
        {
            _slices.Clear();
            TotalValue = 0.0;
        }

        public void RecalculateAngles()
        {
            TotalValue = 0.0;
            for (int i = 0; i < _slices.Count; i++)
            {
                TotalValue += _slices[i].Value;
            }

            float currentAngle = StartAngle;
            for (int i = 0; i < _slices.Count; i++)
            {
                var slice = _slices[i];
                if (TotalValue > 0)
                {
                    slice.Percentage = (slice.Value / TotalValue) * 100.0;
                    slice.SweepAngle = (float)((slice.Value / TotalValue) * 360.0);
                }
                else
                {
                    slice.Percentage = 0.0;
                    slice.SweepAngle = 0.0f;
                }

                slice.StartAngle = currentAngle;
                currentAngle += slice.SweepAngle;
            }
        }

        /// <summary>
        /// Identifies which slice (if any) contains the given test point relative to the chart center and radius.
        /// </summary>
        public PieSlice? HitTest(float px, float py, float cx, float cy, float radius)
        {
            if (_slices.Count == 0 || radius <= 0) return null;

            float dx = px - cx;
            float dy = py - cy;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            float innerRadius = radius * HoleRadiusRatio;
            if (dist < innerRadius || dist > radius + ExplodeDistance) return null;

            // Angle in degrees from -180 to 180
            double angleRad = Math.Atan2(dy, dx);
            double angleDeg = angleRad * (180.0 / Math.PI);

            // Normalize angle to [0, 360)
            while (angleDeg < 0) angleDeg += 360.0;

            for (int i = 0; i < _slices.Count; i++)
            {
                var slice = _slices[i];
                float s = slice.StartAngle;
                while (s < 0) s += 360.0f;
                while (s >= 360.0f) s -= 360.0f;

                float e = s + slice.SweepAngle;
                if (e >= 360.0f)
                {
                    if (angleDeg >= s || angleDeg < (e - 360.0f))
                        return slice;
                }
                else
                {
                    if (angleDeg >= s && angleDeg < e)
                        return slice;
                }
            }

            return null;
        }
    }
}
