using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Charts
{
    /// <summary>
    /// Represents a discrete axis tick label and coordinate.
    /// </summary>
    public readonly struct AxisTick
    {
        public double Value { get; }
        public string Label { get; }
        public float NormalizedPosition { get; }

        public AxisTick(double value, string label, float normalizedPosition)
        {
            Value = value;
            Label = label;
            NormalizedPosition = normalizedPosition;
        }
    }

    /// <summary>
    /// Pure mathematical axis scaling and nice-ticks generator (100% UI decoupled).
    /// </summary>
    public static class ChartAxisEngine
    {
        /// <summary>
        /// Computes human-friendly round numbers (Wilkinson/Heckbert style) for axis bounds and step increments.
        /// </summary>
        public static void CalculateNiceScale(
            double rawMin,
            double rawMax,
            int maxTicks,
            out double niceMin,
            out double niceMax,
            out double step)
        {
            if (maxTicks <= 1) maxTicks = 2;

            if (Math.Abs(rawMax - rawMin) < 1e-9)
            {
                if (Math.Abs(rawMax) < 1e-9)
                {
                    niceMin = 0.0;
                    niceMax = 10.0;
                    step = 2.0;
                    return;
                }
                rawMin -= Math.Abs(rawMin) * 0.1;
                rawMax += Math.Abs(rawMax) * 0.1;
            }

            double range = NiceNum(rawMax - rawMin, false);
            step = NiceNum(range / (maxTicks - 1), true);
            niceMin = Math.Floor(rawMin / step) * step;
            niceMax = Math.Ceiling(rawMax / step) * step;
        }

        private static double NiceNum(double range, bool round)
        {
            if (range <= 0) return 1.0;

            double exponent = Math.Floor(Math.Log10(range));
            double fraction = range / Math.Pow(10, exponent);
            double niceFraction;

            if (round)
            {
                if (fraction < 1.5) niceFraction = 1.0;
                else if (fraction < 3.0) niceFraction = 2.0;
                else if (fraction < 7.0) niceFraction = 5.0;
                else niceFraction = 10.0;
            }
            else
            {
                if (fraction <= 1.0) niceFraction = 1.0;
                else if (fraction <= 2.0) niceFraction = 2.0;
                else if (fraction <= 5.0) niceFraction = 5.0;
                else niceFraction = 10.0;
            }

            return niceFraction * Math.Pow(10, exponent);
        }

        /// <summary>
        /// Maps a numeric value to a normalized 0.0..1.0 coordinate range.
        /// </summary>
        public static float ValueToNormalized(double value, double min, double max)
        {
            double span = max - min;
            if (Math.Abs(span) < 1e-9) return 0f;
            double norm = (value - min) / span;
            return (float)Math.Max(0.0, Math.Min(1.0, norm));
        }

        /// <summary>
        /// Maps a normalized coordinate (0.0..1.0) to screen pixel coordinate.
        /// </summary>
        public static float NormalizedToScreen(float normalized, float screenStart, float screenLength, bool invert = false)
        {
            if (invert)
            {
                return screenStart + screenLength - (normalized * screenLength);
            }
            return screenStart + (normalized * screenLength);
        }

        /// <summary>
        /// Generates discrete ticks and labels across the axis range.
        /// </summary>
        public static List<AxisTick> GenerateTicks(double niceMin, double niceMax, double step, string? format = null)
        {
            var ticks = new List<AxisTick>();
            if (step <= 0) return ticks;

            string fmt = format ?? (step < 1.0 ? "F2" : (step < 10.0 ? "F1" : "F0"));

            for (double val = niceMin; val <= niceMax + (step * 0.5); val += step)
            {
                float norm = ValueToNormalized(val, niceMin, niceMax);
                string label = val.ToString(fmt);
                ticks.Add(new AxisTick(val, label, norm));
            }

            return ticks;
        }
    }
}
