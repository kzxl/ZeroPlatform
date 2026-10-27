using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ZeroUI.Core.Process;

namespace ZeroUI.Core.Tests.Process
{
    public class ProcessMapAdvancedTests
    {
        [Fact]
        public void CreateLaneFromSelection_CalculatesBoundingBoxAndAssignsLaneId()
        {
            var def = new ProcessFlowDefinition();
            var node1 = new ProcessFlowNode("n1", "Step 1", "Desc 1", 100, 100, 200, 80);
            var node2 = new ProcessFlowNode("n2", "Step 2", "Desc 2", 400, 150, 200, 80);
            def.Nodes.Add(node1);
            def.Nodes.Add(node2);

            var lane = def.CreateLaneFromSelection(new[] { node1, node2 }, "1. KINH DOANH & THIẾT KẾ", "#0EA5E9");

            Assert.NotNull(lane);
            Assert.Contains(lane, def.Lanes);
            Assert.Equal("1. KINH DOANH & THIẾT KẾ", lane.Title);
            Assert.Equal(lane.Id, node1.LaneId);
            Assert.Equal(lane.Id, node2.LaneId);

            // Bounding box: X from min(100) - 36 = 64
            // MaxX: 400 + 200 = 600, + 36 = 636 => Width = 636 - 64 = 572
            Assert.True(lane.X <= node1.X);
            Assert.True(lane.Y <= node1.Y);
            Assert.True(lane.X + lane.Width >= node2.X + node2.Width);
            Assert.True(lane.Y + lane.Height >= node2.Y + node2.Height);
            Assert.True(lane.Contains(node1));
            Assert.True(lane.Contains(node2));
        }

        [Fact]
        public void FitLanesToNodes_ExpandsLaneToFitContainedNodes()
        {
            var def = new ProcessFlowDefinition();
            var lane = new ProcessFlowLane("lane_1", "Production", 50, 50, 200, 150);
            def.Lanes.Add(lane);

            // Node is far to the right (x=450, w=200 => right=650)
            var node = new ProcessFlowNode("n1", "Assembly", "Assemble parts", 450, 80, 200, 80)
            {
                LaneId = "lane_1"
            };
            def.Nodes.Add(node);

            def.FitLanesToNodes(padding: 30);

            Assert.True(lane.X <= node.X - 30);
            Assert.True(lane.X + lane.Width >= node.X + node.Width + 30);
            Assert.True(lane.Y + lane.Height >= node.Y + node.Height + 30);
            Assert.True(lane.Contains(node));
        }

        [Fact]
        public void AutoArrangeLayout_HorizontalDAG_ArrangesNodesInSequentialLayers()
        {
            var def = new ProcessFlowDefinition();
            var n1 = new ProcessFlowNode("step1", "Start", "", 500, 300, 180, 70);
            var n2 = new ProcessFlowNode("step2", "Review", "", 100, 200, 180, 70);
            var n3 = new ProcessFlowNode("step3", "Approve", "", 50, 50, 180, 70);
            def.Nodes.Add(n1);
            def.Nodes.Add(n2);
            def.Nodes.Add(n3);

            // Flow: step1 -> step2 -> step3
            def.Connections.Add(new ProcessFlowConnection("step1", "step2"));
            def.Connections.Add(new ProcessFlowConnection("step2", "step3"));

            def.AutoArrangeLayout(horizontal: true, nodeSpacingX: 80, nodeSpacingY: 50);

            // step1 should have smaller X than step2, which has smaller X than step3
            Assert.True(n1.X < n2.X, $"Expected n1.X ({n1.X}) < n2.X ({n2.X})");
            Assert.True(n2.X < n3.X, $"Expected n2.X ({n2.X}) < n3.X ({n3.X})");
        }

        [Fact]
        public void AlignNodes_AlignLeftAndAlignTop_AlignsCoordinatesCorrectly()
        {
            var def = new ProcessFlowDefinition();
            var n1 = new ProcessFlowNode("n1", "Step 1", "", 100, 50, 200, 70);
            var n2 = new ProcessFlowNode("n2", "Step 2", "", 180, 160, 200, 70);
            var n3 = new ProcessFlowNode("n3", "Step 3", "", 250, 280, 200, 70);
            def.Nodes.Add(n1);
            def.Nodes.Add(n2);
            def.Nodes.Add(n3);

            // Align Left
            ProcessFlowDefinition.AlignNodes(new[] { n1, n2, n3 }, ProcessNodeAlignment.Left);
            Assert.Equal(100, n1.X);
            Assert.Equal(100, n2.X);
            Assert.Equal(100, n3.X);

            // Align Top
            ProcessFlowDefinition.AlignNodes(new[] { n1, n2, n3 }, ProcessNodeAlignment.Top);
            Assert.Equal(50, n1.Y);
            Assert.Equal(50, n2.Y);
            Assert.Equal(50, n3.Y);
        }

        [Fact]
        public void DistributeNodes_Horizontal_EquallySpacesNodes()
        {
            var def = new ProcessFlowDefinition();
            var n1 = new ProcessFlowNode("n1", "1", "", 100, 100, 100, 60);
            var n2 = new ProcessFlowNode("n2", "2", "", 180, 100, 100, 60);
            var n3 = new ProcessFlowNode("n3", "3", "", 700, 100, 100, 60);
            def.Nodes.Add(n1);
            def.Nodes.Add(n2);
            def.Nodes.Add(n3);

            ProcessFlowDefinition.DistributeNodes(new[] { n1, n2, n3 }, horizontally: true);

            double gap1 = n2.X - (n1.X + n1.Width);
            double gap2 = n3.X - (n2.X + n2.Width);
            Assert.True(Math.Abs(gap1 - gap2) < 0.001, $"Expected equal gaps: {gap1} vs {gap2}");
        }

        [Fact]
        public void ProcessFlowLane_ContainsAndIntersects_CalculatesAccurately()
        {
            var lane = new ProcessFlowLane("lane1", "Sales", 100, 100, 500, 300);
            var insideNode = new ProcessFlowNode("n1", "", "", 150, 150, 180, 70);
            var outsideNode = new ProcessFlowNode("n2", "", "", 700, 150, 180, 70);
            var intersectingNode = new ProcessFlowNode("n3", "", "", 50, 50, 100, 100);

            Assert.True(lane.Contains(200, 200));
            Assert.False(lane.Contains(50, 50));
            Assert.True(lane.Contains(insideNode));
            Assert.False(lane.Contains(outsideNode));
            Assert.True(lane.Intersects(intersectingNode));
        }

        [Fact]
        public void AlignNodes_CenterRightMiddleBottom_CalculatesCorrectPositions()
        {
            var n1 = new ProcessFlowNode("n1", "1", "", 100, 50, 100, 40);
            var n2 = new ProcessFlowNode("n2", "2", "", 200, 120, 200, 60);

            // Align Center (average center: (150 + 300) / 2 = 225)
            ProcessFlowDefinition.AlignNodes(new[] { n1, n2 }, ProcessNodeAlignment.Center);
            Assert.Equal(225 - n1.Width / 2, n1.X);
            Assert.Equal(225 - n2.Width / 2, n2.X);

            // Align Right (maxRight: 125 + 200 = 325)
            ProcessFlowDefinition.AlignNodes(new[] { n1, n2 }, ProcessNodeAlignment.Right);
            double maxRight = Math.Max(n1.X + n1.Width, n2.X + n2.Width);
            Assert.Equal(maxRight - n1.Width, n1.X);
            Assert.Equal(maxRight - n2.Width, n2.X);

            // Align Middle (average center: (70 + 150) / 2 = 110)
            ProcessFlowDefinition.AlignNodes(new[] { n1, n2 }, ProcessNodeAlignment.Middle);
            Assert.Equal(110 - n1.Height / 2, n1.Y);
            Assert.Equal(110 - n2.Height / 2, n2.Y);

            // Align Bottom (maxBottom: 80 + 60 = 140)
            ProcessFlowDefinition.AlignNodes(new[] { n1, n2 }, ProcessNodeAlignment.Bottom);
            double maxBottom = Math.Max(n1.Y + n1.Height, n2.Y + n2.Height);
            Assert.Equal(maxBottom - n1.Height, n1.Y);
            Assert.Equal(maxBottom - n2.Height, n2.Y);
        }

        [Fact]
        public void DistributeNodes_Vertical_EquallySpacesNodes()
        {
            var n1 = new ProcessFlowNode("n1", "1", "", 50, 100, 100, 50);
            var n2 = new ProcessFlowNode("n2", "2", "", 50, 220, 100, 50);
            var n3 = new ProcessFlowNode("n3", "3", "", 50, 500, 100, 50);

            ProcessFlowDefinition.DistributeNodes(new[] { n1, n2, n3 }, horizontally: false);

            double gap1 = n2.Y - (n1.Y + n1.Height);
            double gap2 = n3.Y - (n2.Y + n2.Height);
            Assert.True(Math.Abs(gap1 - gap2) < 0.001, $"Expected equal vertical gaps: {gap1} vs {gap2}");
        }

        [Fact]
        public void ProcessFlowSerializer_RoundTrips_ComplexDefinitionsWithAllShapes()
        {
            var def = new ProcessFlowDefinition
            {
                Title = "Enterprise Production Workflow",
                Description = "End-to-end MES and Quality Control Flow"
            };

            var lane1 = new ProcessFlowLane("lane_sales", "Sales Order", 10, 10, 400, 200);
            def.Lanes.Add(lane1);

            var startNode = new ProcessFlowNode("start", "Order Received", "ERP Webhook", 30, 40, 140, 50)
            {
                Shape = ProcessNodeShape.StartTerminal,
                LaneId = lane1.Id
            };
            var decisionNode = new ProcessFlowNode("check_stock", "Stock Available?", "WMS Check", 200, 40, 140, 70)
            {
                Shape = ProcessNodeShape.DecisionDiamond,
                LaneId = lane1.Id
            };
            var taskNode = new ProcessFlowNode("produce", "Work Order", "Shop Floor", 400, 40, 160, 60)
            {
                Shape = ProcessNodeShape.TaskCard,
                ActionKey = "WpfMdsModule"
            };
            var endNode = new ProcessFlowNode("done", "Completed", "Archived", 600, 40, 120, 50)
            {
                Shape = ProcessNodeShape.EndTerminal
            };

            def.Nodes.Add(startNode);
            def.Nodes.Add(decisionNode);
            def.Nodes.Add(taskNode);
            def.Nodes.Add(endNode);

            def.Connections.Add(new ProcessFlowConnection("start", "check_stock") { SourcePort = ProcessPortPosition.Right, TargetPort = ProcessPortPosition.Left });
            def.Connections.Add(new ProcessFlowConnection("check_stock", "produce", "In Stock", "#10B981") { SourcePort = ProcessPortPosition.Right, TargetPort = ProcessPortPosition.Left });
            def.Connections.Add(new ProcessFlowConnection("produce", "done", "Finished") { SourcePort = ProcessPortPosition.Right, TargetPort = ProcessPortPosition.Left });

            string json = ProcessFlowSerializer.ToJson(def, indented: true);
            Assert.NotNull(json);

            var restored = ProcessFlowSerializer.FromJson(json);
            Assert.NotNull(restored);
            Assert.Equal(def.Title, restored.Title);
            Assert.Equal(4, restored.Nodes.Count);
            Assert.Single(restored.Lanes);
            Assert.Equal(3, restored.Connections.Count);

            var rDecision = restored.Nodes.First(n => n.Id == "check_stock");
            Assert.Equal(ProcessNodeShape.DecisionDiamond, rDecision.Shape);
            Assert.Equal("Stock Available?", rDecision.Title);

            var rTask = restored.Nodes.First(n => n.Id == "produce");
            Assert.Equal("WpfMdsModule", rTask.ActionKey);
            Assert.Equal(ProcessNodeShape.TaskCard, rTask.Shape);
        }

        [Fact]
        public void ProcessFlowLane_ColorPresetsAndCustomColors_PreservedInSerialization()
        {
            Assert.NotEmpty(ProcessFlowLane.DefaultPresets);
            var preset = ProcessFlowLane.DefaultPresets.First(p => p.Name.Contains("Emerald"));

            var lane = new ProcessFlowLane("lane_qc", "Quality Inspection", 100, 100, 600, 300)
            {
                HeaderColorHex = preset.HeaderHex,
                BackgroundColorHex = preset.BgHex
            };

            var def = new ProcessFlowDefinition();
            def.Lanes.Add(lane);

            string json = ProcessFlowSerializer.ToJson(def);
            var restored = ProcessFlowSerializer.FromJson(json);

            Assert.Single(restored.Lanes);
            var restoredLane = restored.Lanes[0];
            Assert.Equal("Quality Inspection", restoredLane.Title);
            Assert.Equal(preset.HeaderHex, restoredLane.HeaderColorHex);
            Assert.Equal(preset.BgHex, restoredLane.BackgroundColorHex);
        }
    }
}
