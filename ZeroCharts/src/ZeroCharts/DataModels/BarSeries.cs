using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroCharts.Axis;

namespace ZeroCharts.DataModels
{
    /// <summary>
    /// Individual data item within a BarSeries.
    /// </summary>
    public class BarItem
    {
        public string Category { get; set; }
        public double Value { get; set; }
        public Color? CustomColor { get; set; }
        public string? CustomLabel { get; set; }

        public BarItem(string category, double value, Color? customColor = null, string? customLabel = null)
        {
            Category = category ?? string.Empty;
            Value = value;
            CustomColor = customColor;
            CustomLabel = customLabel;
        }
    }

    /// <summary>
    /// Series for rendering vertical or clustered bar charts for telemetry and categorical KPIs.
    /// </summary>
    public class BarSeries
    {
        public string Name { get; set; }
        public Color FillColor { get; set; }
        public Color BorderColor { get; set; }
        public float BorderWidth { get; set; } = 1.0f;
        public float BarWidthRatio { get; set; } = 0.7f;
        public float CornerRadius { get; set; } = 0.0f;
        public bool ShowDataLabels { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        private readonly List<BarItem> _items = new List<BarItem>();
        public IReadOnlyList<BarItem> Items => _items;
        public int Count => _items.Count;

        public double MinValue { get; private set; } = 0.0;
        public double MaxValue { get; private set; } = 0.0;

        public BarSeries(string name = "Bar Series", Color? fillColor = null)
        {
            Name = name;
            FillColor = fillColor ?? ChartPalette.Cyan;
            BorderColor = ChartPalette.BackgroundDark;
        }

        public void Add(string category, double value, Color? customColor = null, string? customLabel = null)
        {
            var item = new BarItem(category, value, customColor, customLabel);
            _items.Add(item);

            if (_items.Count == 1)
            {
                MinValue = Math.Min(0.0, value);
                MaxValue = Math.Max(0.0, value);
            }
            else
            {
                if (value < MinValue) MinValue = value;
                if (value > MaxValue) MaxValue = value;
            }
        }

        public void Add(double value, Color? customColor = null)
        {
            string autoCategory = (_items.Count + 1).ToString();
            Add(autoCategory, value, customColor);
        }

        public void Clear()
        {
            _items.Clear();
            MinValue = 0.0;
            MaxValue = 0.0;
        }

        public BarItem? FindByCategory(string category)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Category, category, StringComparison.OrdinalIgnoreCase))
                    return _items[i];
            }
            return null;
        }
    }
}
