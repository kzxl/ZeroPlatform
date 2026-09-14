using System;
using System.Collections.Generic;

namespace ZeroCharts.Axis
{
    /// <summary>
    /// Linear and Time-Series axis coordinate transformer.
    /// </summary>
    public class ChartAxis
    {
        public string Title { get; set; } = string.Empty;
        public double Min { get; set; } = 0.0;
        public double Max { get; set; } = 100.0;
        public bool IsAutoRange { get; set; } = true;

        public double Range => Max > Min ? Max - Min : 1.0;

        public ChartAxis(string title = "")
        {
            Title = title;
        }

        public void SetRange(double min, double max)
        {
            if (max <= min) max = min + 1.0;
            Min = min;
            Max = max;
        }

        /// <summary>
        /// Projects a data coordinate into pixel screen space.
        /// </summary>
        public float ToScreen(double val, float screenStart, float screenLength, bool invert = false)
        {
            double norm = (val - Min) / Range;
            if (invert)
            {
                return screenStart + screenLength - (float)(norm * screenLength);
            }
            return screenStart + (float)(norm * screenLength);
        }

        /// <summary>
        /// Inverse projects a pixel screen coordinate back to data space.
        /// </summary>
        public double FromScreen(float screenPos, float screenStart, float screenLength, bool invert = false)
        {
            float rel = screenPos - screenStart;
            double norm = invert ? (screenLength - rel) / screenLength : rel / screenLength;
            return Min + (norm * Range);
        }

        /// <summary>
        /// Computes aesthetically pleasing human-readable major tick steps.
        /// </summary>
        public List<double> GenerateMajorTicks(int maxTicks = 6)
        {
            var ticks = new List<double>();
            if (maxTicks <= 1 || Range <= 0) return ticks;

            double roughStep = Range / (maxTicks - 1);
            double exponent = Math.Floor(Math.Log10(roughStep));
            double fraction = roughStep / Math.Pow(10, exponent);

            double niceFraction;
            if (fraction < 1.5) niceFraction = 1.0;
            else if (fraction < 3.0) niceFraction = 2.0;
            else if (fraction < 7.0) niceFraction = 5.0;
            else niceFraction = 10.0;

            double step = niceFraction * Math.Pow(10, exponent);
            if (step <= 0) step = 1.0;

            double start = Math.Ceiling(Min / step) * step;
            for (double t = start; t <= Max; t += step)
            {
                ticks.Add(t);
            }

            return ticks;
        }
    }
}
