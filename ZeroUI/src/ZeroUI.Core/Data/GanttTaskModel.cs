using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Represents a scheduled task item in a high-performance industrial Gantt timeline.
    /// </summary>
    public class GanttTaskItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public float Progress { get; set; } // 0.0f to 1.0f
        public List<int> PredecessorIds { get; } = new List<int>();
        public bool IsMilestone { get; set; }
        public string? AssignedResource { get; set; }
        public uint BarColor { get; set; } = 0xFF2563EB; // Primary Blue
        public int Level { get; set; } = 0;
        public bool IsExpanded { get; set; } = true;

        public TimeSpan Duration => EndDate - StartDate;

        public GanttTaskItem()
        {
        }

        public GanttTaskItem(int id, string name, DateTime start, DateTime end, float progress = 0.0f, bool isMilestone = false, string? resource = null)
        {
            Id = id;
            Name = name;
            StartDate = start;
            EndDate = end;
            Progress = progress;
            IsMilestone = isMilestone;
            AssignedResource = resource;
        }
    }

    /// <summary>
    /// Timeline resolution scale for Gantt charts.
    /// </summary>
    public enum GanttTimeScale
    {
        Hours,
        Days,
        Weeks,
        Months,
        Quarters
    }

    /// <summary>
    /// Type of dependency link between scheduled Gantt tasks.
    /// </summary>
    public enum GanttDependencyType
    {
        FinishToStart,
        StartToStart,
        FinishToFinish,
        StartToFinish
    }

    /// <summary>
    /// High-performance CPM (Critical Path Method) scheduler and slack analyzer.
    /// Computes early/late schedules and resolves zero-slack critical paths.
    /// </summary>
    public static class CriticalPathEngine
    {
        public static HashSet<int> CalculateCriticalPath(IReadOnlyList<GanttTaskItem> tasks)
        {
            var criticalIds = new HashSet<int>();
            if (tasks == null || tasks.Count == 0) return criticalIds;

            var taskMap = new Dictionary<int, GanttTaskItem>(tasks.Count);
            var successors = new Dictionary<int, List<int>>(tasks.Count);
            foreach (var t in tasks)
            {
                taskMap[t.Id] = t;
                if (!successors.ContainsKey(t.Id))
                    successors[t.Id] = new List<int>();
            }

            foreach (var t in tasks)
            {
                foreach (int predId in t.PredecessorIds)
                {
                    if (successors.TryGetValue(predId, out var succList))
                    {
                        succList.Add(t.Id);
                    }
                }
            }

            // Forward Pass
            var earlyStart = new Dictionary<int, DateTime>(tasks.Count);
            var earlyFinish = new Dictionary<int, DateTime>(tasks.Count);

            foreach (var t in tasks)
            {
                ComputeForward(t.Id, taskMap, earlyStart, earlyFinish);
            }

            if (earlyFinish.Count == 0) return criticalIds;

            // Project max finish date
            DateTime maxFinish = DateTime.MinValue;
            foreach (var kvp in earlyFinish)
            {
                if (kvp.Value > maxFinish) maxFinish = kvp.Value;
            }

            // Backward Pass
            var lateFinish = new Dictionary<int, DateTime>(tasks.Count);
            var lateStart = new Dictionary<int, DateTime>(tasks.Count);

            foreach (var t in tasks)
            {
                ComputeBackward(t.Id, taskMap, successors, maxFinish, lateStart, lateFinish);
            }

            // Slack calculation
            foreach (var t in tasks)
            {
                if (earlyStart.TryGetValue(t.Id, out var es) && lateStart.TryGetValue(t.Id, out var ls))
                {
                    var slack = ls - es;
                    if (slack.TotalHours <= 0.01) // Zero float / slack threshold
                    {
                        criticalIds.Add(t.Id);
                    }
                }
            }

            return criticalIds;
        }

        private static void ComputeForward(int taskId, Dictionary<int, GanttTaskItem> taskMap, Dictionary<int, DateTime> esMap, Dictionary<int, DateTime> efMap)
        {
            if (esMap.ContainsKey(taskId)) return;
            if (!taskMap.TryGetValue(taskId, out var task)) return;

            DateTime maxPredFinish = task.StartDate;
            foreach (int predId in task.PredecessorIds)
            {
                ComputeForward(predId, taskMap, esMap, efMap);
                if (efMap.TryGetValue(predId, out var predEf) && predEf > maxPredFinish)
                {
                    maxPredFinish = predEf;
                }
            }

            var duration = task.Duration;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

            esMap[taskId] = maxPredFinish;
            efMap[taskId] = maxPredFinish + duration;
        }

        private static void ComputeBackward(int taskId, Dictionary<int, GanttTaskItem> taskMap, Dictionary<int, List<int>> successors, DateTime projectEnd, Dictionary<int, DateTime> lsMap, Dictionary<int, DateTime> lfMap)
        {
            if (lfMap.ContainsKey(taskId)) return;
            if (!taskMap.TryGetValue(taskId, out var task)) return;

            var succList = successors.TryGetValue(taskId, out var list) ? list : null;
            DateTime minSuccStart = projectEnd;

            if (succList != null && succList.Count > 0)
            {
                foreach (int succId in succList)
                {
                    ComputeBackward(succId, taskMap, successors, projectEnd, lsMap, lfMap);
                    if (lsMap.TryGetValue(succId, out var succLs) && succLs < minSuccStart)
                    {
                        minSuccStart = succLs;
                    }
                }
            }
            else
            {
                minSuccStart = projectEnd;
            }

            var duration = task.Duration;
            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

            lfMap[taskId] = minSuccStart;
            lsMap[taskId] = minSuccStart - duration;
        }
    }
}
