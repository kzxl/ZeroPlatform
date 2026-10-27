using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Severity classification for industrial gauge threshold ranges.
    /// </summary>
    public enum GaugeSeverity
    {
        Normal = 0,
        Warning = 1,
        Critical = 2
    }

    /// <summary>
    /// Represents a colored threshold band on an industrial gauge (e.g. 0-60 Normal, 60-80 Warning, 80-100 Danger).
    /// </summary>
    public class GaugeThresholdRange
    {
        public double From { get; set; }
        public double To { get; set; }
        public GaugeSeverity Severity { get; set; }
        public uint ArgbColor { get; set; }
        public string? Label { get; set; }

        public GaugeThresholdRange() { }

        public GaugeThresholdRange(double from, double to, GaugeSeverity severity, uint argbColor, string? label = null)
        {
            From = from;
            To = to;
            Severity = severity;
            ArgbColor = argbColor;
            Label = label;
        }

        public bool Contains(double value) => value >= From && value <= To;
    }

    /// <summary>
    /// Pure mathematical calculation engine for radial and linear gauges.
    /// Provides angle interpolation, inertial needle damping, and range mapping with zero GC allocations.
    /// </summary>
    public static class GaugeMath
    {
        /// <summary>
        /// Converts a scalar value to an angular rotation in degrees.
        /// </summary>
        public static float ValueToAngle(double value, double min, double max, float startAngle, float sweepAngle)
        {
            if (max <= min) return startAngle;
            double clamped = Math.Max(min, Math.Min(max, value));
            double ratio = (clamped - min) / (max - min);
            return (float)(startAngle + ratio * sweepAngle);
        }

        /// <summary>
        /// Converts a scalar value to a linear coordinate position.
        /// </summary>
        public static float ValueToPosition(double value, double min, double max, float startPos, float length)
        {
            if (max <= min) return startPos;
            double clamped = Math.Max(min, Math.Min(max, value));
            double ratio = (clamped - min) / (max - min);
            return (float)(startPos + ratio * length);
        }

        /// <summary>
        /// Applies exponential inertial damping filter to smooth needle motion across frame ticks.
        /// </summary>
        public static double ApplyDamping(double current, double target, double dampingFactor = 0.2)
        {
            if (Math.Abs(target - current) < 1e-4) return target;
            return current + (target - current) * Math.Max(0.01, Math.Min(1.0, dampingFactor));
        }

        /// <summary>
        /// Evaluates the highest or matching severity of a value against defined threshold ranges.
        /// </summary>
        public static GaugeSeverity EvaluateSeverity(double value, IEnumerable<GaugeThresholdRange>? ranges, GaugeSeverity defaultSeverity = GaugeSeverity.Normal)
        {
            if (ranges == null) return defaultSeverity;
            foreach (var r in ranges)
            {
                if (r.Contains(value)) return r.Severity;
            }
            return defaultSeverity;
        }
    }
}
