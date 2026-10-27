using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Containers
{
    public class DiagramNode
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class DiagramLink
    {
        public string SourceId { get; set; }
        public string TargetId { get; set; }
    }
}
