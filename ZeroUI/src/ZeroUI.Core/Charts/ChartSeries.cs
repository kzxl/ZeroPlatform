using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Charts
{
    /// <summary>
    /// Platform-neutral series collection for ZeroUI charts.
    /// </summary>
    public class ChartSeries
    {
        public string Name { get; set; } = string.Empty;
        public List<ChartPoint> Points { get; set; } = new List<ChartPoint>();
        public ChartType? SeriesType { get; set; }

        /// <summary>
        /// 32-bit ARGB primary stroke/fill color (0xAARRGGBB).
        /// </summary>
        public uint? ColorRgba { get; set; }

        /// <summary>
        /// 32-bit ARGB secondary fill color for gradient areas (0xAARRGGBB).
        /// </summary>
        public uint? FillColorRgba { get; set; }

        public float BorderWidth { get; set; } = 2.0f;

        /// <summary>
        /// Alias for SeriesType to harmonize with desktop chart controls.
        /// </summary>
        public ChartType? Type
        {
            get => SeriesType;
            set => SeriesType = value;
        }

        /// <summary>
        /// Alias for BorderWidth to harmonize with desktop chart controls.
        /// </summary>
        public float LineWidth
        {
            get => BorderWidth;
            set => BorderWidth = value;
        }

        public bool ShowMarkers { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public bool ShowDataLabels { get; set; } = false;
        public string? ValuePrefix { get; set; }
        public string? ValueSuffix { get; set; }
        public object? Tag { get; set; }

        public ChartSeries()
        {
        }

        public ChartSeries(string name)
        {
            Name = name ?? string.Empty;
        }

        public ChartSeries(string name, uint colorRgba)
        {
            Name = name ?? string.Empty;
            ColorRgba = colorRgba;
        }

        public void Add(ChartPoint point)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));
            Points.Add(point);
        }

        public void Add(string label, double value)
        {
            Points.Add(new ChartPoint(label, value));
        }

        public void Add(double x, double y)
        {
            Points.Add(new ChartPoint(x, y));
        }

        public void Add(string label, double value, uint? colorRgba = null)
        {
            Points.Add(new ChartPoint(label, value, colorRgba));
        }
    }
}
