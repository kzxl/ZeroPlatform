using System;

namespace ZeroUI.Core.Charts
{
    /// <summary>
    /// Platform-neutral data point for ZeroUI charts.
    /// </summary>
    public class ChartPoint
    {
        public string Label { get; set; } = string.Empty;
        public double Value { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>
        /// Optional 32-bit ARGB color override (0xAARRGGBB).
        /// </summary>
        public uint? ColorRgba { get; set; }

        public object? Tag { get; set; }

        public ChartPoint()
        {
        }

        public ChartPoint(string label, double value)
        {
            Label = label ?? string.Empty;
            Value = value;
            Y = value;
        }

        public ChartPoint(string label, double value, uint? colorRgba = null)
        {
            Label = label ?? string.Empty;
            Value = value;
            Y = value;
            ColorRgba = colorRgba;
        }

        public ChartPoint(double x, double y)
        {
            X = x;
            Y = y;
            Value = y;
            Label = x.ToString();
        }

        public ChartPoint(double x, double y, string label)
        {
            X = x;
            Y = y;
            Value = y;
            Label = label ?? string.Empty;
        }
    }

    /// <summary>
    /// Financial or high-density industrial telemetry candlestick data point (OHLCV).
    /// </summary>
    public class CandlePoint
    {
        public DateTime Time { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double Volume { get; set; }

        public bool IsBullish => Close >= Open;

        public CandlePoint() { }

        public CandlePoint(DateTime time, double open, double high, double low, double close, double volume = 0)
        {
            Time = time;
            Open = open;
            High = high;
            Low = low;
            Close = close;
            Volume = volume;
        }

        public override string ToString() => $"{Time:yyyy-MM-dd HH:mm} O:{Open} H:{High} L:{Low} C:{Close}";
    }
}
