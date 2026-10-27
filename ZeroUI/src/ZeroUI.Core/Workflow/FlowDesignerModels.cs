namespace ZeroUI.Core.Workflow {
    public class FlowNode {
        public string Id { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; } = "";
        public string Type { get; set; } = "";
    }
    public class FlowEdge {
        public string SourceId { get; set; } = "";
        public string TargetId { get; set; } = "";
    }
}