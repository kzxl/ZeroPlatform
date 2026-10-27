namespace ZeroUI.Core.Scheduler {
    using System;
    public enum WorkOrderViewMode { Calendar, List }
    public class WorkOrder {
        public string Id { get; set; } = "";
        public string Asset { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime ScheduledDate { get; set; }
        public string Priority { get; set; } = "";
        public string Status { get; set; } = "";
    }
}