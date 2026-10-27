using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZeroCharts.DataModels
{
    /// <summary>
    /// Financial and production yield candlestick OHLC bar.
    /// </summary>
    public readonly struct CandlestickItem
    {
        public DateTime Timestamp { get; }
        public double Open { get; }
        public double High { get; }
        public double Low { get; }
        public double Close { get; }
        public double Volume { get; }

        public bool IsBullish => Close >= Open;
        public double BodyTop => Math.Max(Open, Close);
        public double BodyBottom => Math.Min(Open, Close);
        public double BodyHeight => Math.Abs(Close - Open);

        public CandlestickItem(DateTime timestamp, double open, double high, double low, double close, double volume = 0)
        {
            Timestamp = timestamp;
            Open = open;
            High = Math.Max(high, Math.Max(open, close));
            Low = Math.Min(low, Math.Min(open, close));
            Close = close;
            Volume = volume;
        }

        public override string ToString() =>
            $"[{Timestamp:yyyy-MM-dd HH:mm}] O:{Open:F2} H:{High:F2} L:{Low:F2} C:{Close:F2}";
    }

    /// <summary>
    /// Candlestick series for financial analysis and industrial batch yields.
    /// </summary>
    public class CandlestickSeries
    {
        private readonly List<CandlestickItem> _items = new List<CandlestickItem>();

        public string Title { get; set; }
        public Color BullishColor { get; set; } = Color.FromArgb(0, 230, 118); // Emerald Green
        public Color BearishColor { get; set; } = Color.FromArgb(255, 23, 68);  // Crimson Red
        public bool IsVisible { get; set; } = true;

        public IReadOnlyList<CandlestickItem> Items => _items;
        public int Count => _items.Count;

        public double MinPrice { get; private set; } = double.MaxValue;
        public double MaxPrice { get; private set; } = double.MinValue;
        public DateTime MinTime { get; private set; } = DateTime.MaxValue;
        public DateTime MaxTime { get; private set; } = DateTime.MinValue;

        public CandlestickSeries(string title = "OHLC")
        {
            Title = title;
        }

        public void Add(CandlestickItem item)
        {
            _items.Add(item);
            if (item.Low < MinPrice) MinPrice = item.Low;
            if (item.High > MaxPrice) MaxPrice = item.High;
            if (item.Timestamp < MinTime) MinTime = item.Timestamp;
            if (item.Timestamp > MaxTime) MaxTime = item.Timestamp;
        }

        public void AddRange(IEnumerable<CandlestickItem> items)
        {
            if (items == null) return;
            foreach (var it in items) Add(it);
        }

        public void Clear()
        {
            _items.Clear();
            MinPrice = double.MaxValue;
            MaxPrice = double.MinValue;
            MinTime = DateTime.MaxValue;
            MaxTime = DateTime.MinValue;
        }
    }
}
