using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Analytics
{
    /// <summary>
    /// Platform-agnostic qualitative threshold band in a Stephen Few Bullet graph.
    /// </summary>
    public class BulletRange
    {
        public string Name { get; set; } = string.Empty;
        public double EndValue { get; set; }
        public uint? ColorRgba { get; set; }

        public BulletRange() { }

        public BulletRange(string name, double endValue, uint? colorRgba = null)
        {
            Name = name ?? string.Empty;
            EndValue = endValue;
            ColorRgba = colorRgba;
        }
    }

    /// <summary>
    /// Platform-agnostic KPI benchmarking entry with qualitative ranges, target marker, and actual progress.
    /// </summary>
    public class BulletItem
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public double Minimum { get; set; }
        public double Maximum { get; set; } = 100;
        public double ActualValue { get; set; }
        public double TargetValue { get; set; }
        public double? ComparativeValue { get; set; }
        public List<BulletRange> Ranges { get; } = new List<BulletRange>();
        public uint? BarColorRgba { get; set; }
        public uint? TargetColorRgba { get; set; }
        public object? Tag { get; set; }

        public BulletItem() { }

        public BulletItem(string title, string subtitle, double actual, double target, double max = 100, double min = 0)
        {
            Title = title ?? string.Empty;
            Subtitle = subtitle ?? string.Empty;
            ActualValue = actual;
            TargetValue = target;
            Maximum = max;
            Minimum = min;
        }
    }

    /// <summary>
    /// Normalized qualitative band representation mapped into [0..1] unit space.
    /// </summary>
    public class BulletNormalizedRange
    {
        public string Name { get; set; } = string.Empty;
        public double NormalizedStart { get; set; }
        public double NormalizedEnd { get; set; }
        public uint? ColorRgba { get; set; }
    }

    /// <summary>
    /// Evaluated spatial benchmarks normalized into [0..1] coordinate system ready for UI rendering.
    /// </summary>
    public class BulletNormalizedMetrics
    {
        public double NormalizedActual { get; set; }
        public double NormalizedTarget { get; set; }
        public double? NormalizedComparative { get; set; }
        public IReadOnlyList<BulletNormalizedRange> Ranges { get; set; } = Array.Empty<BulletNormalizedRange>();
        public double PercentOfTarget { get; set; }
    }

    /// <summary>
    /// Core calculation engine for Stephen Few's Bullet Graphs.
    /// Normalizes performance metrics and qualitative thresholds into mathematical [0..1] scales.
    /// </summary>
    public static class BulletBenchmarkEngine
    {
        public static BulletNormalizedMetrics Compute(BulletItem item)
        {
            if (item == null)
            {
                return new BulletNormalizedMetrics();
            }

            double span = Math.Max(0.0001, item.Maximum - item.Minimum);
            double clampedActual = Math.Max(item.Minimum, Math.Min(item.Maximum, item.ActualValue));
            double normActual = (clampedActual - item.Minimum) / span;

            double normTarget = Math.Max(0.0, Math.Min(1.0, (item.TargetValue - item.Minimum) / span));

            double? normComp = null;
            if (item.ComparativeValue.HasValue)
            {
                normComp = Math.Max(0.0, Math.Min(1.0, (item.ComparativeValue.Value - item.Minimum) / span));
            }

            var rawRanges = (item.Ranges != null && item.Ranges.Count > 0)
                ? item.Ranges
                : GetDefaultRanges(item.Minimum, item.Maximum);

            var normalizedRanges = new List<BulletNormalizedRange>(rawRanges.Count);
            double prevEnd = item.Minimum;

            foreach (var r in rawRanges)
            {
                double startVal = Math.Max(item.Minimum, prevEnd);
                double endVal = Math.Min(item.Maximum, r.EndValue);
                if (endVal > startVal)
                {
                    normalizedRanges.Add(new BulletNormalizedRange
                    {
                        Name = r.Name,
                        NormalizedStart = (startVal - item.Minimum) / span,
                        NormalizedEnd = (endVal - item.Minimum) / span,
                        ColorRgba = r.ColorRgba
                    });
                }
                prevEnd = endVal;
            }

            double pctTarget = item.TargetValue > 0 ? (item.ActualValue / item.TargetValue) * 100.0 : 0.0;

            return new BulletNormalizedMetrics
            {
                NormalizedActual = normActual,
                NormalizedTarget = normTarget,
                NormalizedComparative = normComp,
                Ranges = normalizedRanges,
                PercentOfTarget = pctTarget
            };
        }

        public static List<BulletRange> GetDefaultRanges(double min, double max)
        {
            double span = max - min;
            return new List<BulletRange>
            {
                new BulletRange("Poor", min + span * 0.60),
                new BulletRange("Satisfactory", min + span * 0.85),
                new BulletRange("Good", max)
            };
        }
    }
}
