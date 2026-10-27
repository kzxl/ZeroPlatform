namespace ZeroUI.Core.Scheduler {
    using System;
    public class Employee {
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
    }
    public class Shift {
        public string EmployeeId { get; set; } = "";
        public DateTime Date { get; set; }
        public string ShiftType { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
    }
}