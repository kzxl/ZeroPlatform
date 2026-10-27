namespace ZeroUI.Core.Scheduler {
    using System.Collections.Generic;
    public class ResourceRow {
        public string Name { get; set; } = "";
        public List<object> Slots { get; set; } = new List<object>();
    }
}