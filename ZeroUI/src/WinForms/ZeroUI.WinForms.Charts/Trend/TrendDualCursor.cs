using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZeroUI.WinForms.Charts
{
    /// <summary>
    /// Manages dual measurement cursors (Cursor A and Cursor B) for calculating
    /// exact time delta and channel amplitude changes across the visual waveform.
    /// </summary>
    public class TrendDualCursor
    {
        public bool Enabled { get; set; } = false;

        public DateTime? TimeA { get; set; }
        public DateTime? TimeB { get; set; }

        public Color ColorA { get; set; } = Color.FromArgb(234, 179, 8); // Gold / Amber
        public Color ColorB { get; set; } = Color.FromArgb(6, 182, 212);  // Cyan

        public TimeSpan DeltaTime
        {
            get
            {
                if (!TimeA.HasValue || !TimeB.HasValue) return TimeSpan.Zero;
                return (TimeB.Value - TimeA.Value).Duration();
            }
        }

        public double DeltaSeconds => DeltaTime.TotalSeconds;

        public double FrequencyHz
        {
            get
            {
                double s = DeltaSeconds;
                return s > 0.0001 ? 1.0 / s : 0.0;
            }
        }

        /// <summary>
        /// Interpolates or finds the closest pen value at the specified timestamp.
        /// </summary>
        public static double? GetValueAt(TrendPen pen, DateTime time)
        {
            if (pen == null) return null;
            var points = pen.Points;
            if (points.Count == 0) return null;

            // Binary search or linear search for closest point
            int low = 0;
            int high = points.Count - 1;

            if (time <= points[0].Timestamp) return points[0].Value;
            if (time >= points[high].Timestamp) return points[high].Value;

            while (low <= high)
            {
                int mid = (low + high) / 2;
                if (points[mid].Timestamp == time) return points[mid].Value;

                if (points[mid].Timestamp < time)
                    low = mid + 1;
                else
                    high = mid - 1;
            }

            // low is the element immediately after time, high is immediately before
            if (high >= 0 && low < points.Count)
            {
                var ptBefore = points[high];
                var ptAfter = points[low];

                double ratio = (double)(time.Ticks - ptBefore.Timestamp.Ticks) /
                               (ptAfter.Timestamp.Ticks - ptBefore.Timestamp.Ticks);
                return ptBefore.Value + ratio * (ptAfter.Value - ptBefore.Value);
            }

            return points[Math.Max(0, Math.Min(points.Count - 1, low))].Value;
        }

        public double? GetDeltaValue(TrendPen pen)
        {
            if (!TimeA.HasValue || !TimeB.HasValue) return null;
            double? valA = GetValueAt(pen, TimeA.Value);
            double? valB = GetValueAt(pen, TimeB.Value);
            if (valA.HasValue && valB.HasValue)
            {
                return valB.Value - valA.Value;
            }
            return null;
        }
    }
}
