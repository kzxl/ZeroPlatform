namespace ZeroUI.Core.Compliance {
    using System;
    public class AuditEntry {
        public DateTime Timestamp { get; set; }
        public string User { get; set; } = "";
        public string Action { get; set; } = "";
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string OldValue { get; set; } = "";
        public string NewValue { get; set; } = "";
    }
}