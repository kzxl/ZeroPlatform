namespace ZeroUI.Core.Workflow {
    using System;
    using System.Collections.Generic;
    public class Allocation {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Label { get; set; } = "";
        public object Color { get; set; } = null!;
    }
    public class Resource {
        public string Name { get; set; } = "";
        public List<Allocation> Allocations { get; set; } = new List<Allocation>();
    }
}