using System;
using System.Collections.Generic;
using System.Drawing;

namespace ZeroPlatform.Charts.DataModels
{
    /// <summary>
    /// Represents an individual Gantt scheduling task or machine operational state interval.
    /// </summary>
    public class GanttTask
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string TrackGroup { get; set; } // E.g., "Line 1", "CNC-02"
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string State { get; set; } // "Running", "Idle", "Fault", "Setup"
        public double Progress { get; set; } // 0.0 to 1.0
        public Color CustomColor { get; set; } = Color.Empty;

        public TimeSpan Duration => EndTime - StartTime;

        public GanttTask(string id, string label, string trackGroup, DateTime startTime, DateTime endTime,
            string state = "Running", double progress = 1.0)
        {
            Id = id ?? Guid.NewGuid().ToString("N");
            Label = label ?? string.Empty;
            TrackGroup = trackGroup ?? "Default";
            StartTime = startTime;
            EndTime = endTime >= startTime ? endTime : startTime;
            State = state;
            Progress = Math.Max(0.0, Math.Min(1.0, progress));
        }

        public Color GetStateColor()
        {
            if (!CustomColor.IsEmpty) return CustomColor;
            switch (State?.ToUpperInvariant())
            {
                case "RUNNING": return Color.FromArgb(0, 230, 118);   // Green
                case "IDLE": return Color.FromArgb(255, 214, 0);       // Amber
                case "FAULT": return Color.FromArgb(255, 23, 68);      // Red
                case "SETUP": return Color.FromArgb(41, 121, 255);     // Blue
                case "CHANGEOVER": return Color.FromArgb(170, 0, 255); // Purple
                default: return Color.FromArgb(120, 144, 156);         // Gray
            }
        }
    }

    /// <summary>
    /// Gantt chart series representing industrial scheduling and OEE machine state timelines.
    /// </summary>
    public class GanttSeries
    {
        private readonly List<GanttTask> _tasks = new List<GanttTask>();

        public string Title { get; set; }
        public IReadOnlyList<GanttTask> Tasks => _tasks;
        public int Count => _tasks.Count;

        public DateTime MinTime { get; private set; } = DateTime.MaxValue;
        public DateTime MaxTime { get; private set; } = DateTime.MinValue;

        public GanttSeries(string title = "Gantt Timeline")
        {
            Title = title;
        }

        public void AddTask(GanttTask task)
        {
            if (task == null) return;
            _tasks.Add(task);
            if (task.StartTime < MinTime) MinTime = task.StartTime;
            if (task.EndTime > MaxTime) MaxTime = task.EndTime;
        }

        public void AddTasks(IEnumerable<GanttTask> tasks)
        {
            if (tasks == null) return;
            foreach (var t in tasks) AddTask(t);
        }

        public List<string> GetDistinctTrackGroups()
        {
            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < _tasks.Count; i++)
            {
                groups.Add(_tasks[i].TrackGroup);
            }
            return new List<string>(groups);
        }

        public void Clear()
        {
            _tasks.Clear();
            MinTime = DateTime.MaxValue;
            MaxTime = DateTime.MinValue;
        }
    }
}
