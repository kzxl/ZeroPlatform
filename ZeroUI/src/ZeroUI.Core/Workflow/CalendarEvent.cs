namespace ZeroUI.Core.Workflow {
    using System;
    public class CalendarEvent {
        public DateTime Date { get; set; }
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public object Color { get; set; } = null!;
    }
}