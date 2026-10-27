using System;

namespace ZeroUI.Core.Range
{
    /// <summary>
    /// Represents a discrete distribution data point or histogram bucket
    /// rendered in the background of the range control.
    /// </summary>
    public sealed class RangeDataPoint
    {
        /// <summary>
        /// The horizontal coordinate argument (real number or OADate).
        /// </summary>
        public double Argument { get; set; }

        /// <summary>
        /// The vertical measure value (e.g. frequency, volume, metric magnitude).
        /// </summary>
        public double Value { get; set; }

        /// <summary>
        /// Optional associated timestamp when operating in temporal mode.
        /// </summary>
        public DateTime? Timestamp { get; set; }

        /// <summary>
        /// Optional descriptive tooltip or category tag.
        /// </summary>
        public string? Label { get; set; }

        public RangeDataPoint()
        {
        }

        public RangeDataPoint(double argument, double value, string? label = null)
        {
            Argument = argument;
            Value = value;
            Label = label;
        }

        public RangeDataPoint(DateTime timestamp, double value, string? label = null)
        {
            Timestamp = timestamp;
            Argument = timestamp.ToOADate();
            Value = value;
            Label = label;
        }
    }
}
