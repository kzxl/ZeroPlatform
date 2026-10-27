using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.WinForms.Data;

namespace ZeroUI.Desktop.Tests
{
    public class WinFormsTreeListSummaryRollupTests
    {
        [Fact]
        public void WinForms_TreeList_FooterSummaries_CalculatesAccurately()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var colName = tree.AddColumn("Item", "Item", 120);
                var colPrice = tree.AddColumn("Price", "Price", 100);
                var colQty = tree.AddColumn("Qty", "Qty", 80);

                colPrice.SummaryType = TreeListSummaryType.Sum;
                colQty.SummaryType = TreeListSummaryType.Average;

                var root = new ZeroTreeNode("Assembly");
                root["Price"] = 0m;
                root["Qty"] = 10;

                var part1 = root.AddChild("Part 1");
                part1["Price"] = 150.50m;
                part1["Qty"] = 20;

                var part2 = root.AddChild("Part 2");
                part2["Price"] = 49.50m;
                part2["Qty"] = 30;

                tree.AddNode(root);
                tree.ShowFooter = true;

                // Sum of prices = 0 + 150.50 + 49.50 = 200.00
                Assert.True(tree.ColumnSummaries.ContainsKey("Price"));
                Assert.Equal(200.00m, (decimal)tree.ColumnSummaries["Price"]!);

                // Average of Qty = (10 + 20 + 30) / 3 = 20
                Assert.True(tree.ColumnSummaries.ContainsKey("Qty"));
                Assert.Equal(20.0m, (decimal)tree.ColumnSummaries["Qty"]!);
            });
        }

        [Fact]
        public void WinForms_TreeList_HierarchicalRollup_ComputesChildSumsBottomUp()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var colName = tree.AddColumn("Task", "Task", 140);
                var colCost = tree.AddColumn("Cost", "Cost", 100);
                colCost.RollupMode = TreeListRollupMode.ChildSum;

                // 3-level tree
                // Root
                //   Branch 1
                //     Leaf 1A: 50
                //     Leaf 1B: 70
                //   Branch 2
                //     Leaf 2A: 100
                var root = new ZeroTreeNode("Project");
                var branch1 = root.AddChild("Sub-phase 1");
                var leaf1A = branch1.AddChild("Task 1A");
                leaf1A["Cost"] = 50m;
                var leaf1B = branch1.AddChild("Task 1B");
                leaf1B["Cost"] = 70m;

                var branch2 = root.AddChild("Sub-phase 2");
                var leaf2A = branch2.AddChild("Task 2A");
                leaf2A["Cost"] = 100m;

                tree.AddNode(root);
                tree.RecalculateRollups();

                // Branch 1 = 50 + 70 = 120
                Assert.Equal(120m, (decimal)branch1["Cost"]!);

                // Branch 2 = 100
                Assert.Equal(100m, (decimal)branch2["Cost"]!);

                // Root = 120 + 100 = 220
                Assert.Equal(220m, (decimal)root["Cost"]!);
            });
        }

        [Fact]
        public void WinForms_TreeList_InPlaceEditing_UpdatesValueAndRecalculatesRollup()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var colName = tree.AddColumn("Task", "Task", 140);
                var colCost = tree.AddColumn("Cost", "Cost", 100);
                colCost.RollupMode = TreeListRollupMode.ChildSum;
                colCost.SummaryType = TreeListSummaryType.Sum;

                tree.AllowInPlaceEditing = true;
                tree.ShowFooter = true;

                var parent = new ZeroTreeNode("Parent Task");
                var child1 = parent.AddChild("Child 1");
                child1["Cost"] = 100m;
                var child2 = parent.AddChild("Child 2");
                child2["Cost"] = 200m;

                tree.AddNode(parent);
                tree.RecalculateRollups();
                tree.RecalculateSummaries();

                Assert.Equal(300m, (decimal)parent["Cost"]!);

                // Begin editing child1 cost
                bool started = tree.ShowEditor(child1, colCost);
                Assert.True(started);
                Assert.True(tree.IsEditing);

                // Modify editor text and commit edit
                bool changedEventFired = false;
                tree.CellValueChanged += (s, e) =>
                {
                    changedEventFired = true;
                    Assert.Equal(colCost, e.Column);
                    Assert.Equal(child1, e.Node);
                    Assert.Equal("500", e.NewValue?.ToString());
                };

                // Find active editor TextBox
                var editor = tree.ActiveEditor as System.Windows.Forms.TextBox;
                Assert.NotNull(editor);
                editor.Text = "500";

                tree.CloseEditor(saveChanges: true);
                Assert.False(tree.IsEditing);
                Assert.True(changedEventFired);

                // Verify child1 updated
                Assert.Equal("500", child1["Cost"]?.ToString());

                // Verify parent cost rolled up: 500 + 200 = 700
                Assert.Equal(700m, (decimal)parent["Cost"]!);
            });
        }

        [Fact]
        public void WinForms_TreeList_DragDrop_ReParentsNodeAndUpdatesHierarchy()
        {
            StaTestRunner.Run(() =>
            {
                using var tree = new TreeList();
                var colName = tree.AddColumn("Name", "Name", 120);
                tree.AllowDragDrop = true;

                var root1 = new ZeroTreeNode("Root 1");
                var child1 = root1.AddChild("Child 1");
                var root2 = new ZeroTreeNode("Root 2");

                tree.AddNode(root1);
                tree.AddNode(root2);

                Assert.Equal(2, tree.Nodes.Count);
                Assert.Single(root1.Children);
                Assert.Empty(root2.Children);

                bool dropFired = false;
                tree.AfterDropNode += (s, e) =>
                {
                    dropFired = true;
                    Assert.Equal(child1, e.DragNode);
                    Assert.Equal(root2, e.TargetNode);
                    Assert.Equal(TreeListDropPosition.AsChild, e.Position);
                };

                // Reflection call or internal ExecuteDrop
                var method = typeof(ZTreeList).GetMethod("ExecuteDrop",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Assert.NotNull(method);

                method.Invoke(tree, new object[] { child1, root2, TreeListDropPosition.AsChild });

                Assert.True(dropFired);
                Assert.Empty(root1.Children);
                Assert.Single(root2.Children);
                Assert.Equal(child1, root2.Children[0]);
                Assert.Equal(root2, child1.Parent);
                Assert.Equal(1, child1.Level);
            });
        }
    }
}
