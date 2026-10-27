using System;

namespace ZeroUI.Core.Charts
{
    public class GanttTask
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double Progress { get; set; }
        public string Dependencies { get; set; }
    }
}
