using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Represents an event in the scheduler.
    /// </summary>
    public class SchedulerEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Subject { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool AllDay { get; set; }
        public string? Category { get; set; }
        public string? Location { get; set; }
        public object? ColorTag { get; set; } // Color stored as object for cross-platform
        public object? Tag { get; set; }
    }

    /// <summary>
    /// Represents the view mode of the scheduler.
    /// </summary>
    public enum SchedulerView { Day, Week, WorkWeek, Month, Agenda }

    /// <summary>
    /// Event arguments for scheduler events.
    /// </summary>
    public class SchedulerEventArgs : EventArgs
    {
        public SchedulerEvent Event { get; }
        public SchedulerEventArgs(SchedulerEvent evt) { Event = evt; }
    }

    /// <summary>
    /// Event arguments for date changes.
    /// </summary>
    public class DateChangedEventArgs : EventArgs
    {
        public DateTime OldDate { get; }
        public DateTime NewDate { get; }
        public DateChangedEventArgs(DateTime oldDate, DateTime newDate) { OldDate = oldDate; NewDate = newDate; }
    }

    /// <summary>
    /// Helper methods for scheduling layouts.
    /// </summary>
    public static class SchedulerLayoutHelper
    {
        public static List<SchedulerEvent> GetEventsForDate(IEnumerable<SchedulerEvent> events, DateTime date)
        {
            if (events == null) return new List<SchedulerEvent>();
            return events.Where(e => e.Start.Date <= date.Date && e.End.Date >= date.Date).ToList();
        }

        public static List<SchedulerEvent> GetEventsInRange(IEnumerable<SchedulerEvent> events, DateTime start, DateTime end)
        {
            if (events == null) return new List<SchedulerEvent>();
            return events.Where(e => e.Start < end && e.End > start).ToList();
        }

        public static List<(SchedulerEvent Event, int Column, int TotalColumns)> LayoutOverlapping(List<SchedulerEvent> events)
        {
            var result = new List<(SchedulerEvent, int, int)>();
            if (events == null || !events.Any()) return result;

            var sortedEvents = events.OrderBy(e => e.Start).ThenByDescending(e => e.End).ToList();
            var columns = new List<List<SchedulerEvent>>();

            foreach (var ev in sortedEvents)
            {
                bool placed = false;
                for (int i = 0; i < columns.Count; i++)
                {
                    if (columns[i].Last().End <= ev.Start)
                    {
                        columns[i].Add(ev);
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    columns.Add(new List<SchedulerEvent> { ev });
                }
            }

            foreach (var ev in sortedEvents)
            {
                int colIndex = columns.FindIndex(c => c.Contains(ev));
                result.Add((ev, colIndex, columns.Count));
            }

            return result;
        }
    }
}
