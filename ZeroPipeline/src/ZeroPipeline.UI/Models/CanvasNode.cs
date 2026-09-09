using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.UI.Models
{
    /// <summary>
    /// Visual representation of a pipeline node on the canvas.
    /// Handles pin layout geometry, selection state, execution badge metrics, and hit testing.
    /// </summary>
    public sealed class CanvasNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NodeType { get; set; }
        public string Category { get; set; }
        public IPipelineNode? UnderlyingNode { get; set; }

        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; } = 210f;
        public float Height { get; private set; } = 80f;

        public const float HeaderHeight = 32f;
        public const float RowHeight = 24f;
        public const float CornerRadius = 8f;

        public List<CanvasPortPin> Inputs { get; } = new List<CanvasPortPin>();
        public List<CanvasPortPin> Outputs { get; } = new List<CanvasPortPin>();
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public bool IsSelected { get; set; }
        public bool IsHovered { get; set; }
        public bool IsDragging { get; set; }

        public NodeState State => UnderlyingNode?.State ?? _manualState;
        private NodeState _manualState = NodeState.Idle;

        public double LastExecutionDurationMs => UnderlyingNode?.LastExecutionDurationMs ?? _manualDurationMs;
        private double _manualDurationMs;

        public long ExecutionCount => UnderlyingNode?.ExecutionCount ?? _manualCount;
        private long _manualCount;

        public CanvasNode(string id, string name, string nodeType, string category = "General", float x = 100f, float y = 100f)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Name = name ?? id;
            NodeType = nodeType ?? "Node";
            Category = category ?? "General";
            X = x;
            Y = y;
        }

        public void SetManualExecutionState(NodeState state, double durationMs = 0, long count = 0)
        {
            _manualState = state;
            _manualDurationMs = durationMs;
            _manualCount = count;
        }

        public CanvasPortPin AddInputPin(string name, Type dataType)
        {
            var pin = new CanvasPortPin(this, name, dataType, PortDirection.Input, PointF.Empty);
            Inputs.Add(pin);
            RecalculateLayout();
            return pin;
        }

        public CanvasPortPin AddOutputPin(string name, Type dataType)
        {
            var pin = new CanvasPortPin(this, name, dataType, PortDirection.Output, PointF.Empty);
            Outputs.Add(pin);
            RecalculateLayout();
            return pin;
        }

        public void RecalculateLayout()
        {
            int maxPins = Math.Max(Inputs.Count, Outputs.Count);
            Height = Math.Max(76f, HeaderHeight + 16f + (maxPins * RowHeight));

            for (int i = 0; i < Inputs.Count; i++)
            {
                float pinY = HeaderHeight + 16f + (i * RowHeight);
                Inputs[i].LocalOffset = new PointF(0f, pinY);
            }

            for (int j = 0; j < Outputs.Count; j++)
            {
                float pinY = HeaderHeight + 16f + (j * RowHeight);
                Outputs[j].LocalOffset = new PointF(Width, pinY);
            }
        }

        public RectangleF GetBounds() => new RectangleF(X, Y, Width, Height);

        public RectangleF GetHeaderBounds() => new RectangleF(X, Y, Width, HeaderHeight);

        public bool ContainsPoint(PointF worldPoint) => GetBounds().Contains(worldPoint);

        public bool ContainsInHeader(PointF worldPoint) => GetHeaderBounds().Contains(worldPoint);

        public CanvasPortPin? FindPinAt(PointF worldPoint)
        {
            foreach (var pin in Inputs)
            {
                if (pin.GetWorldHitBox().Contains(worldPoint))
                    return pin;
            }

            foreach (var pin in Outputs)
            {
                if (pin.GetWorldHitBox().Contains(worldPoint))
                    return pin;
            }

            return null;
        }

        public static CanvasNode FromPipelineNode(IPipelineNode node, float x = 100f, float y = 100f, string category = "Pipeline")
        {
            if (node == null) throw new ArgumentNullException(nameof(node));

            var cNode = new CanvasNode(node.Id, node.Name, node.GetType().Name, category, x, y)
            {
                UnderlyingNode = node
            };

            foreach (var port in node.InputPorts)
            {
                cNode.Inputs.Add(new CanvasPortPin(cNode, port.Name, port.DataType, PortDirection.Input, PointF.Empty));
            }

            foreach (var port in node.OutputPorts)
            {
                cNode.Outputs.Add(new CanvasPortPin(cNode, port.Name, port.DataType, PortDirection.Output, PointF.Empty));
            }

            cNode.RecalculateLayout();
            return cNode;
        }

        public override string ToString() => $"CanvasNode '{Id}' ({NodeType}) @ ({X:F0}, {Y:F0})";
    }
}
