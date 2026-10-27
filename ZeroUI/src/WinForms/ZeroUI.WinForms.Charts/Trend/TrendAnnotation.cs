using System;
using System.Drawing;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Represents an event flag, trip milestone, or operator annotation attached to a specific timestamp.
    /// </summary>
    public class TrendAnnotation
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string Label { get; set; }
        public string Description { get; set; }
        public Color Color { get; set; }
        public string Icon { get; set; }

        public TrendAnnotation(DateTime timestamp, string label, string description = "", Color? color = null, string icon = "🚩")
        {
            Id = Guid.NewGuid().ToString("N");
            Timestamp = timestamp;
            Label = label;
            Description = description;
            Color = color ?? Color.FromArgb(239, 68, 68);
            Icon = icon;
        }
    }
}
