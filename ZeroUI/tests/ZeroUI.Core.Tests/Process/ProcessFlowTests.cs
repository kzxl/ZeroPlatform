using System;
using Xunit;
using ZeroUI.Core.Process;

namespace ZeroUI.Core.Tests.Process
{
    public class ProcessFlowTests
    {
        [Fact]
        public void ProcessActionRegistry_RegisterAndExecute_InvokesHandlerSuccessfully()
        {
            string actionKey = "test.action.open_uc_" + Guid.NewGuid().ToString("N");
            bool actionExecuted = false;
            string receivedParam = "";

            ProcessActionRegistry.Register(actionKey, "Open Sample UC", "Test", ctx =>
            {
                actionExecuted = true;
                receivedParam = ctx.Parameter?.ToString() ?? "";
            });

            var node = new ProcessFlowNode("node1", "Test Node", "Subtitle", 10, 20)
            {
                ActionKey = actionKey
            };

            var context = new ProcessActionContext(node, null, "sample_param");
            bool result = ProcessActionRegistry.Execute(actionKey, context);

            Assert.True(result);
            Assert.True(actionExecuted);
            Assert.Equal("sample_param", receivedParam);

            // Clean up
            Assert.True(ProcessActionRegistry.Unregister(actionKey));
            Assert.False(ProcessActionRegistry.Execute(actionKey, context));
        }

        [Fact]
        public void ProcessFlowSerializer_RoundTrip_PreservesAllProperties()
        {
            var def = new ProcessFlowDefinition
            {
                Code = "TEST_WORKFLOW",
                Title = "Test Business Workflow",
                Description = "Unit test process description",
                Version = 2
            };

            def.Lanes.Add(new ProcessFlowLane("lane_1", "Sales & Orders", 10, 20, 800, 300)
            {
                HeaderColorHex = "#64748B",
                BackgroundColorHex = "#F1F5F9"
            });

            def.Nodes.Add(new ProcessFlowNode("node_1", "Order Entry", "Enter customer order", 100, 50, 200, 70)
            {
                Shape = ProcessNodeShape.TaskCard,
                Status = ProcessNodeStatus.Completed,
                ActionKey = "sales.order.entry",
                IconGlyph = "📦",
                HeaderColorHex = "#3B82F6",
                BorderColorHex = "#93C5FD",
                LaneId = "lane_1"
            });

            def.Nodes.Add(new ProcessFlowNode("node_check", "Credit Check", "Verify payment terms", 100, 160, 160, 80)
            {
                Shape = ProcessNodeShape.DecisionDiamond,
                Status = ProcessNodeStatus.InProgress,
                ActionKey = "sales.credit.check",
                IconGlyph = "🔍",
                HeaderColorHex = "#0284C7",
                BorderColorHex = "#38BDF8",
                LaneId = "lane_1"
            });

            def.Connections.Add(new ProcessFlowConnection("node_1", "node_check", "SUBMITTED", "#10B981")
            {
                SourcePort = ProcessPortPosition.Bottom,
                TargetPort = ProcessPortPosition.Top,
                StrokeThickness = 2.5,
                RoutingMode = ProcessRoutingMode.Orthogonal
            });

            // 1. Serialize
            string json = ProcessFlowSerializer.ToJson(def, indented: true);
            Assert.False(string.IsNullOrWhiteSpace(json));
            Assert.Contains("\"code\": \"TEST_WORKFLOW\"", json);
            Assert.Contains("\"shape\": \"DecisionDiamond\"", json);

            // 2. Deserialize
            var restored = ProcessFlowSerializer.FromJson(json);
            Assert.NotNull(restored);
            Assert.Equal("TEST_WORKFLOW", restored.Code);
            Assert.Equal("Test Business Workflow", restored.Title);
            Assert.Equal(2, restored.Version);

            Assert.Single(restored.Lanes);
            Assert.Equal("lane_1", restored.Lanes[0].Id);
            Assert.Equal("Sales & Orders", restored.Lanes[0].Title);

            Assert.Equal(2, restored.Nodes.Count);
            Assert.Equal("Order Entry", restored.Nodes[0].Title);
            Assert.Equal(ProcessNodeShape.TaskCard, restored.Nodes[0].Shape);
            Assert.Equal(ProcessNodeShape.DecisionDiamond, restored.Nodes[1].Shape);
            Assert.Equal("sales.order.entry", restored.Nodes[0].ActionKey);

            Assert.Single(restored.Connections);
            Assert.Equal("node_1", restored.Connections[0].SourceNodeId);
            Assert.Equal("node_check", restored.Connections[0].TargetNodeId);
            Assert.Equal("SUBMITTED", restored.Connections[0].Label);
            Assert.Equal(ProcessPortPosition.Bottom, restored.Connections[0].SourcePort);
            Assert.Equal(ProcessPortPosition.Top, restored.Connections[0].TargetPort);
        }
    }
}
