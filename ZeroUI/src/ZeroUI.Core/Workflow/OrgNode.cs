namespace ZeroUI.Core.Workflow {
    using System.Collections.Generic;
    public enum OrgChartOrientation { TopDown, LeftRight }
    public class OrgNode {
        public string Name { get; set; } = "";
        public string Title { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public List<OrgNode> Children { get; set; } = new List<OrgNode>();
    }
}